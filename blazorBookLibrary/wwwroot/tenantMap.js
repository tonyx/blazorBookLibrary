// Geolocation and map helper using Leaflet & OpenStreetMap for Tenants
let mapInstance = null;
let markerInstance = null;
let circleInstance = null;

window.tenantMap = {
    initialize: function (elementId, dotNetHelper, initialLat, initialLng, initialRadius) {
        const container = document.getElementById(elementId);
        if (!container) {
            setTimeout(function () {
                window.tenantMap.initialize(elementId, dotNetHelper, initialLat, initialLng, initialRadius);
            }, 100);
            return;
        }

        // If there's an existing map instance, remove it to prevent duplicates on hot reload or navigation
        if (mapInstance) {
            try {
                mapInstance.remove();
            } catch (e) {
                console.warn("Error removing previous map instance:", e);
            }
            mapInstance = null;
            markerInstance = null;
            circleInstance = null;
        }

        // Ensure default marker icons work reliably with CDN
        if (typeof L !== 'undefined' && L.Icon && L.Icon.Default) {
            delete L.Icon.Default.prototype._getIconUrl;
            L.Icon.Default.mergeOptions({
                iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
                iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
                shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
            });
        }

        const hasInitialCoords = (typeof initialLat === 'number' && typeof initialLng === 'number' && !isNaN(initialLat) && !isNaN(initialLng));
        const defaultLat = hasInitialCoords ? initialLat : 41.9028; // Default to Rome latitude
        const defaultLng = hasInitialCoords ? initialLng : 12.4964; // Default to Rome longitude
        const radius = (typeof initialRadius === 'number' && initialRadius > 0) ? initialRadius : 50;
        const zoomLevel = hasInitialCoords ? 16 : 6;

        mapInstance = L.map(elementId).setView([defaultLat, defaultLng], zoomLevel);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '© OpenStreetMap contributors'
        }).addTo(mapInstance);

        // Create draggable marker
        markerInstance = L.marker([defaultLat, defaultLng], {
            draggable: true
        }).addTo(mapInstance);

        // Create circle representing the proximity radius area
        circleInstance = L.circle([defaultLat, defaultLng], {
            radius: radius,
            color: '#3d619b',
            fillColor: '#3d619b',
            fillOpacity: 0.2
        }).addTo(mapInstance);

        // Event listener for dragging the marker
        markerInstance.on('dragend', function () {
            const position = markerInstance.getLatLng();
            if (circleInstance) {
                circleInstance.setLatLng(position);
            }
            dotNetHelper.invokeMethodAsync('UpdateCoordinatesFromMap', position.lat, position.lng);
        });

        // Event listener during drag to keep circle centered
        markerInstance.on('drag', function () {
            const position = markerInstance.getLatLng();
            if (circleInstance) {
                circleInstance.setLatLng(position);
            }
        });

        // Event listener for clicking on the map
        mapInstance.on('click', function (event) {
            const lat = event.latlng.lat;
            const lng = event.latlng.lng;
            markerInstance.setLatLng([lat, lng]);
            if (circleInstance) {
                circleInstance.setLatLng([lat, lng]);
            }
            dotNetHelper.invokeMethodAsync('UpdateCoordinatesFromMap', lat, lng);
        });

        // Force a size recalculation after the DOM settles
        setTimeout(function () {
            if (mapInstance) {
                mapInstance.invalidateSize();
            }
        }, 250);
    },

    updateMarker: function (lat, lng, radius) {
        if (markerInstance && mapInstance) {
            markerInstance.setLatLng([lat, lng]);
            if (circleInstance) {
                circleInstance.setLatLng([lat, lng]);
                if (typeof radius === 'number' && radius > 0) {
                    circleInstance.setRadius(radius);
                }
            }
            mapInstance.setView([lat, lng], 16);
        }
    },

    updateRadius: function (radius) {
        if (circleInstance && typeof radius === 'number' && radius > 0) {
            circleInstance.setRadius(radius);
        }
    },

    getUserLocation: function (dotNetHelper) {
        if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(
                function (position) {
                    const lat = position.coords.latitude;
                    const lng = position.coords.longitude;
                    dotNetHelper.invokeMethodAsync('OnLocationSuccess', lat, lng);
                },
                function (error) {
                    let errMsg = "Unable to retrieve your location.";
                    switch (error.code) {
                        case error.PERMISSION_DENIED:
                            errMsg = "User denied the request for Geolocation.";
                            break;
                        case error.POSITION_UNAVAILABLE:
                            errMsg = "Location information is unavailable.";
                            break;
                        case error.TIMEOUT:
                            errMsg = "The request to get user location timed out.";
                            break;
                    }
                    dotNetHelper.invokeMethodAsync('OnLocationError', errMsg);
                },
                { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }
            );
        } else {
            dotNetHelper.invokeMethodAsync('OnLocationError', "Geolocation is not supported by this browser.");
        }
    },

    getDeviceCoordinates: function () {
        return new Promise(function (resolve) {
            if (!navigator.geolocation) {
                resolve({
                    success: false,
                    errorCode: -1,
                    errorMessage: "Geolocation is not supported by this browser."
                });
                return;
            }
            navigator.geolocation.getCurrentPosition(
                function (position) {
                    resolve({
                        success: true,
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude,
                        accuracy: position.coords.accuracy,
                        errorCode: 0,
                        errorMessage: null
                    });
                },
                function (error) {
                    let errMsg = "Unable to retrieve your location.";
                    switch (error.code) {
                        case error.PERMISSION_DENIED:
                            errMsg = "User denied the request for Geolocation.";
                            break;
                        case error.POSITION_UNAVAILABLE:
                            errMsg = "Location information is unavailable.";
                            break;
                        case error.TIMEOUT:
                            errMsg = "The request to get user location timed out.";
                            break;
                    }
                    resolve({
                        success: false,
                        latitude: 0,
                        longitude: 0,
                        accuracy: 0,
                        errorCode: error.code,
                        errorMessage: errMsg
                    });
                },
                { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }
            );
        });
    }
};

window.restaurantMap = window.tenantMap;
