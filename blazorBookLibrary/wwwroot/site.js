window.generateRecaptchaToken = (siteKey, actionName) => {
    return new Promise((resolve, reject) => {
        grecaptcha.ready(() => {
            grecaptcha.execute(siteKey, { action: actionName })
                .then(token => resolve(token))
                .catch(error => reject(error));
        });
    });
};

window.playScanSound = () => {
    try {
        const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        if (audioCtx.state === 'suspended') {
            audioCtx.resume();
        }
        const oscillator = audioCtx.createOscillator();
        const gainNode = audioCtx.createGain();

        oscillator.connect(gainNode);
        gainNode.connect(audioCtx.destination);

        oscillator.type = 'sine';
        oscillator.frequency.setValueAtTime(880, audioCtx.currentTime); 
        gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);

        oscillator.start();
        oscillator.stop(audioCtx.currentTime + 0.1);
    } catch (e) {
        console.error("Audio failed", e);
    }
};

window.showToast = (message, type) => {
    let container = document.getElementById('toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container';
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = `toast-notification toast-${type}`;
    
    const icons = {
        'success': 'bi-check-circle-fill',
        'error': 'bi-exclamation-triangle-fill',
        'warning': 'bi-exclamation-circle-fill',
        'info': 'bi-info-circle-fill'
    };
    
    const icon = document.createElement('i');
    icon.className = `bi ${icons[type] || icons['info']} me-2`;
    
    const text = document.createElement('span');
    text.innerText = message;
    
    toast.appendChild(icon);
    toast.appendChild(text);
    container.appendChild(toast);

    setTimeout(() => {
        toast.classList.add('toast-fade-out');
        setTimeout(() => toast.remove(), 450);
    }, 4000);
};

window.downloadFile = (fileName, content) => {
    const blob = new Blob([content], { type: 'text/plain' });
    const url = window.URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
    window.URL.revokeObjectURL(url);
}

window.captureVideoFrame = (videoSelector) => {
    return new Promise((resolve) => {
        let attempts = 0;
        const maxAttempts = 20; // Up to 4 seconds total
        
        const capture = () => {
            try {
                const video = document.querySelector(videoSelector);
                if (!video) {
                    if (attempts < maxAttempts) {
                        attempts++;
                        setTimeout(capture, 200);
                        return;
                    }
                    console.warn("captureVideoFrame: Video element not found:", videoSelector);
                    resolve(null);
                    return;
                }
                
                // Check if video is playing and has dimensions
                if (video.readyState < 2 || video.videoWidth === 0) {
                    if (attempts < maxAttempts) {
                        attempts++;
                        setTimeout(capture, 200);
                        return;
                    }
                    console.warn("captureVideoFrame: Video not ready after max attempts.");
                    resolve(null);
                    return;
                }

                const canvas = document.createElement('canvas');
                canvas.width = video.videoWidth;
                canvas.height = video.videoHeight;
                const ctx = canvas.getContext('2d');
                ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
                const dataUrl = canvas.toDataURL('image/jpeg', 0.8);
                resolve(dataUrl);
            } catch (e) {
                console.error("captureVideoFrame failed:", e);
                resolve(null);
            }
        };
        
        capture();
    });
};
