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

window.blazorCulture = {
    get: () => {
        const name = '.AspNetCore.Culture=';
        const decodedCookie = decodeURIComponent(document.cookie);
        const ca = decodedCookie.split(';');
        for (let i = 0; i < ca.length; i++) {
            let c = ca[i];
            while (c.charAt(0) === ' ') {
                c = c.substring(1);
            }
            if (c.indexOf(name) === 0) {
                const value = c.substring(name.length, c.length);
                const parts = value.split('|');
                for (let part of parts) {
                    if (part.startsWith('c=')) {
                        return part.substring(2);
                    }
                }
            }
        }
        return null;
    }
};

window.blazorCookies = {
    set: (name, value, days) => {
        let expires = "";
        if (days) {
            const date = new Date();
            date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
            expires = "; expires=" + date.toUTCString();
        }
        document.cookie = name + "=" + (value || "") + expires + "; path=/; SameSite=Lax";
    },
    get: (name) => {
        const nameEQ = name + "=";
        const ca = document.cookie.split(';');
        for (let i = 0; i < ca.length; i++) {
            let c = ca[i];
            while (c.charAt(0) === ' ') c = c.substring(1, c.length);
            if (c.indexOf(nameEQ) === 0) return c.substring(nameEQ.length, c.length);
        }
        return null;
    }
};

window.librarianSpeech = {
    recognition: null,
    voices: [],

    initVoices: function() {
        if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
            this.voices = window.speechSynthesis.getVoices();
            window.speechSynthesis.onvoiceschanged = () => {
                this.voices = window.speechSynthesis.getVoices();
            };
        }
    },

    lastTranscript: '',

    isRecognitionSupported: function() {
        return !!(typeof window !== 'undefined' && (window.SpeechRecognition || window.webkitSpeechRecognition));
    },

    isSynthesisSupported: function() {
        return typeof window !== 'undefined' && ('speechSynthesis' in window);
    },

    resolveSpeechLang: function(lang) {
        let resolved = (lang || '').trim();
        if (!resolved && typeof window !== 'undefined' && window.blazorCulture) {
            resolved = window.blazorCulture.get() || '';
        }
        if (!resolved && typeof navigator !== 'undefined') {
            resolved = navigator.language || navigator.userLanguage || '';
        }
        if (!resolved) {
            resolved = 'en-US';
        }
        const lower = resolved.toLowerCase();
        if (lower === 'it' || lower.startsWith('it-')) return 'it-IT';
        if (lower === 'en' || lower.startsWith('en-')) return 'en-US';
        if (lower === 'es' || lower.startsWith('es-')) return 'es-ES';
        if (lower === 'fr' || lower.startsWith('fr-')) return 'fr-FR';
        if (lower === 'de' || lower.startsWith('de-')) return 'de-DE';
        return resolved;
    },

    mediaStream: null,
    audioCtx: null,
    analyser: null,
    hasDetectedAudioSignal: false,
    silenceTimer: null,

    cleanupAudio: function() {
        if (this.silenceTimer) {
            clearTimeout(this.silenceTimer);
            this.silenceTimer = null;
        }
        if (this.mediaStream) {
            try {
                this.mediaStream.getTracks().forEach(t => t.stop());
            } catch(e) {}
            this.mediaStream = null;
        }
        if (this.audioCtx) {
            try {
                if (this.audioCtx.state !== 'closed') this.audioCtx.close();
            } catch(e) {}
            this.audioCtx = null;
            this.analyser = null;
        }
    },

    startListening: async function(dotNetHelper, lang) {
        const SpeechRec = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRec) {
            return false;
        }

        if (this.recognition) {
            try { this.recognition.abort(); } catch(e) {}
            this.recognition = null;
        }

        this.cleanupAudio();

        if ('speechSynthesis' in window) {
            window.speechSynthesis.cancel();
        }

        this.lastTranscript = '';
        this.hasDetectedAudioSignal = false;
        const resolvedLang = this.resolveSpeechLang(lang);
        console.log('[LibrarianSpeech] Starting SpeechRecognition with lang:', resolvedLang);

        // Pre-warm microphone and inspect real audio signal
        try {
            if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
                const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                this.mediaStream = stream;

                const AudioCtx = window.AudioContext || window.webkitAudioContext;
                if (AudioCtx) {
                    this.audioCtx = new AudioCtx();
                    if (this.audioCtx.state === 'suspended') {
                        await this.audioCtx.resume();
                    }
                    const source = this.audioCtx.createMediaStreamSource(stream);
                    this.analyser = this.audioCtx.createAnalyser();
                    this.analyser.fftSize = 256;
                    source.connect(this.analyser);

                    const buffer = new Uint8Array(this.analyser.frequencyBinCount);
                    const checkSound = () => {
                        if (!this.analyser || !this.recognition) return;
                        this.analyser.getByteFrequencyData(buffer);
                        let sum = 0;
                        for (let i = 0; i < buffer.length; i++) sum += buffer[i];
                        if (sum > 60) {
                            this.hasDetectedAudioSignal = true;
                        }
                        requestAnimationFrame(checkSound);
                    };
                    requestAnimationFrame(checkSound);
                }
            }
        } catch (micErr) {
            console.warn('[LibrarianSpeech] getUserMedia issue:', micErr);
            if (micErr.name === 'NotAllowedError' || micErr.name === 'PermissionDeniedError') {
                dotNetHelper.invokeMethodAsync('OnSpeechError', 'Microphone permission was denied. Please allow microphone access in your browser or macOS System Settings.');
                return false;
            }
        }

        try {
            const rec = new SpeechRec();
            rec.lang = resolvedLang;
            // Use continuous = true so the browser does NOT prematurely timeout while speaking
            rec.continuous = true;
            rec.interimResults = true;
            rec.maxAlternatives = 1;

            const resetSilenceTimer = () => {
                if (this.silenceTimer) clearTimeout(this.silenceTimer);
                this.silenceTimer = setTimeout(() => {
                    if (this.lastTranscript && this.recognition) {
                        console.log('[LibrarianSpeech] Silence after speech detected. Finalizing...');
                        this.stopListening();
                    }
                }, 2200); // 2.2 seconds of silence after words were spoken
            };

            rec.onstart = () => {
                console.log('[LibrarianSpeech] Recognition session active.');
            };

            rec.onresult = (event) => {
                let fullTranscript = '';
                let interimTranscript = '';

                for (let i = 0; i < event.results.length; i++) {
                    const res = event.results[i];
                    if (res && res[0]) {
                        if (res.isFinal) {
                            fullTranscript += res[0].transcript + ' ';
                        } else {
                            interimTranscript += res[0].transcript;
                        }
                    }
                }

                const currentTranscript = (fullTranscript + interimTranscript).trim();
                console.log('[LibrarianSpeech] onresult:', { fullTranscript, interimTranscript, currentTranscript });

                if (currentTranscript) {
                    this.lastTranscript = currentTranscript;
                    resetSilenceTimer();

                    const inputEl = document.getElementById('librarianChatInput');
                    if (inputEl) {
                        inputEl.value = currentTranscript;
                        inputEl.dispatchEvent(new Event('input', { bubbles: true }));
                    }

                    const isFinal = event.results[event.results.length - 1].isFinal;
                    dotNetHelper.invokeMethodAsync('OnSpeechRecognized', currentTranscript, isFinal);
                }
            };

            rec.onerror = (event) => {
                console.warn('[LibrarianSpeech] Recognition error:', event.error, event);
                
                if (event.error === 'no-speech') {
                    if (this.lastTranscript && this.lastTranscript.trim().length > 0) {
                        // User spoke words and then paused, ignore error and complete
                        console.log('[LibrarianSpeech] Pause treated as speech conclusion.');
                        return;
                    }

                    let message = 'No speech was detected.';
                    if (this.hasDetectedAudioSignal) {
                        message += ` Sound was received from your microphone, but words could not be transcribed. Please ensure the voice language (${resolvedLang}) matches what you are speaking.`;
                    } else {
                        message += ' No audio signal was detected from your microphone. Please check that your microphone is unmuted in system settings.';
                    }

                    const isSafari = /^((?!chrome|android).)*safari/i.test(navigator.userAgent);
                    if (isSafari) {
                        message += ' (On macOS Safari, ensure "Dictation" is enabled in System Settings > Keyboard > Dictation).';
                    }

                    dotNetHelper.invokeMethodAsync('OnSpeechError', message);
                    return;
                }

                let message = '';
                if (event.error === 'audio-capture') {
                    message = 'No microphone was found or microphone access is disabled.';
                } else if (event.error === 'not-allowed') {
                    message = 'Microphone permission was denied. Please allow microphone access in your browser settings.';
                } else if (event.error === 'network') {
                    message = 'Speech recognition network error. Please verify your connection to speech services.';
                } else {
                    message = 'Speech error: ' + (event.error || 'unknown');
                }
                dotNetHelper.invokeMethodAsync('OnSpeechError', message);
            };

            rec.onend = () => {
                console.log('[LibrarianSpeech] Recognition ended. Final text was:', this.lastTranscript);
                this.cleanupAudio();
                const finalTx = (this.lastTranscript || '').trim();
                dotNetHelper.invokeMethodAsync('OnSpeechCompleted', finalTx);
                this.recognition = null;
            };

            rec.start();
            this.recognition = rec;
            return true;
        } catch (err) {
            console.error('[LibrarianSpeech] Start failed:', err);
            this.cleanupAudio();
            dotNetHelper.invokeMethodAsync('OnSpeechError', err.message || 'Speech recognition initialization failed');
            return false;
        }
    },

    stopListening: function() {
        if (this.silenceTimer) {
            clearTimeout(this.silenceTimer);
            this.silenceTimer = null;
        }
        if (this.recognition) {
            try {
                this.recognition.stop();
            } catch(e) {}
        }
    },

    cleanMarkdownForSpeech: function(rawMarkdown) {
        if (!rawMarkdown) return '';
        let text = rawMarkdown;
        // Strip markdown links: [Title](url) -> Title
        text = text.replace(/\[([^\]]+)\]\([^\)]+\)/g, '$1');
        // Strip images: ![Alt](url) -> ''
        text = text.replace(/!\[([^\]]*)\]\([^\)]+\)/g, '');
        // Strip bold/italics
        text = text.replace(/(\*\*|__)(.*?)\1/g, '$2');
        text = text.replace(/(\*|_)(.*?)\1/g, '$2');
        // Strip headers
        text = text.replace(/^#{1,6}\s+/gm, '');
        // Strip bullet markers
        text = text.replace(/^[\*\-\+]\s+/gm, '');
        text = text.replace(/^\d+\.\s+/gm, '');
        // Strip blockquotes
        text = text.replace(/^>\s+/gm, '');
        // Strip code fences and inline code
        text = text.replace(/```[\s\S]*?```/g, '');
        text = text.replace(/`([^`]+)`/g, '$1');
        // Strip html tags
        text = text.replace(/<[^>]*>/g, '');
        // Normalize whitespace and punctuation pauses
        text = text.replace(/\n+/g, '. ').replace(/\s+/g, ' ').trim();
        return text;
    },

    speak: function(dotNetHelper, messageIndex, rawMarkdown, lang) {
        if (!('speechSynthesis' in window)) return false;

        window.speechSynthesis.cancel();
        const text = this.cleanMarkdownForSpeech(rawMarkdown);
        if (!text) return false;

        const utterance = new SpeechSynthesisUtterance(text);
        const resolvedLang = lang || 'en-US';
        utterance.lang = resolvedLang;
        utterance.rate = 1.0;
        utterance.pitch = 1.0;

        if (!this.voices || this.voices.length === 0) {
            this.voices = window.speechSynthesis.getVoices();
        }

        if (this.voices && this.voices.length > 0) {
            const lowerLang = resolvedLang.toLowerCase();
            const prefix = lowerLang.split('-')[0];
            const matchedVoice = this.voices.find(v => v.lang.toLowerCase() === lowerLang) ||
                                 this.voices.find(v => v.lang.toLowerCase().startsWith(prefix));
            if (matchedVoice) {
                utterance.voice = matchedVoice;
            }
        }

        utterance.onstart = () => {
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('OnSpeechSynthesisStatus', messageIndex, true);
            }
        };

        utterance.onend = () => {
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('OnSpeechSynthesisStatus', messageIndex, false);
            }
        };

        utterance.onerror = (e) => {
            console.warn('SpeechSynthesis error:', e);
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('OnSpeechSynthesisStatus', messageIndex, false);
            }
        };

        window.speechSynthesis.speak(utterance);
        return true;
    },

    stopSpeaking: function(dotNetHelper) {
        if ('speechSynthesis' in window) {
            window.speechSynthesis.cancel();
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('OnSpeechSynthesisStatus', -1, false);
            }
        }
    }
};

if (typeof window !== 'undefined') {
    window.librarianSpeech.initVoices();
}
