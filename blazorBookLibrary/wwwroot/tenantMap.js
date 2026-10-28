// Geolocation and map helper using Leaflet & OpenStreetMap for Tenants
let mapInstance = null;
let markerInstance = null;
let circleInstance = null;
let guestCircleInstance = null;

const hoverMapInstances = {};

if (typeof window !== 'undefined') {
    window.addEventListener('focus', function () {
        for (const key in hoverMapInstances) {
            if (hoverMapInstances[key] && hoverMapInstances[key].map) {
                try {
                    hoverMapInstances[key].map.invalidateSize();
                } catch (e) { }
            }
        }
    });
}

window.tenantMap = {
    initialize: function (elementId, dotNetHelper, initialLat, initialLng, initialRadius, initialGuestRadius) {
        const container = document.getElementById(elementId);
        if (!container) {
            setTimeout(function () {
                window.tenantMap.initialize(elementId, dotNetHelper, initialLat, initialLng, initialRadius, initialGuestRadius);
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
            guestCircleInstance = null;
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

        // Create circle representing the discovery radius area
        circleInstance = L.circle([defaultLat, defaultLng], {
            radius: radius,
            color: '#3d619b',
            fillColor: '#3d619b',
            fillOpacity: 0.15
        }).addTo(mapInstance);

        // Create circle representing the guest loan proximity area if enabled
        if (typeof initialGuestRadius === 'number' && initialGuestRadius > 0) {
            guestCircleInstance = L.circle([defaultLat, defaultLng], {
                radius: initialGuestRadius,
                color: '#198754',
                fillColor: '#198754',
                fillOpacity: 0.25,
                dashArray: '4, 4'
            }).addTo(mapInstance);
        }

        // Event listener for dragging the marker
        markerInstance.on('dragend', function () {
            const position = markerInstance.getLatLng();
            if (circleInstance) {
                circleInstance.setLatLng(position);
            }
            if (guestCircleInstance) {
                guestCircleInstance.setLatLng(position);
            }
            dotNetHelper.invokeMethodAsync('UpdateCoordinatesFromMap', position.lat, position.lng);
        });

        // Event listener during drag to keep circles centered
        markerInstance.on('drag', function () {
            const position = markerInstance.getLatLng();
            if (circleInstance) {
                circleInstance.setLatLng(position);
            }
            if (guestCircleInstance) {
                guestCircleInstance.setLatLng(position);
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
            if (guestCircleInstance) {
                guestCircleInstance.setLatLng([lat, lng]);
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

    updateMarker: function (lat, lng, radius, guestRadius) {
        if (markerInstance && mapInstance) {
            markerInstance.setLatLng([lat, lng]);
            if (circleInstance) {
                circleInstance.setLatLng([lat, lng]);
                if (typeof radius === 'number' && radius > 0) {
                    circleInstance.setRadius(radius);
                }
            }
            if (guestCircleInstance) {
                guestCircleInstance.setLatLng([lat, lng]);
                if (typeof guestRadius === 'number' && guestRadius > 0) {
                    guestCircleInstance.setRadius(guestRadius);
                }
            }
            if (circleInstance && typeof radius === 'number' && radius > 1000) {
                mapInstance.fitBounds(circleInstance.getBounds(), { padding: [20, 20] });
            } else {
                mapInstance.setView([lat, lng], 16);
            }
        }
    },

    updateGuestRadius: function (guestRadius) {
        if (mapInstance && markerInstance) {
            const pos = markerInstance.getLatLng();
            if (typeof guestRadius === 'number' && guestRadius > 0) {
                if (guestCircleInstance) {
                    guestCircleInstance.setLatLng(pos);
                    guestCircleInstance.setRadius(guestRadius);
                } else {
                    guestCircleInstance = L.circle(pos, {
                        radius: guestRadius,
                        color: '#198754',
                        fillColor: '#198754',
                        fillOpacity: 0.25,
                        dashArray: '4, 4'
                    }).addTo(mapInstance);
                }
            } else if (guestCircleInstance) {
                mapInstance.removeLayer(guestCircleInstance);
                guestCircleInstance = null;
            }
        }
    },

    clearMarker: function () {
        if (circleInstance && mapInstance) {
            mapInstance.removeLayer(circleInstance);
            circleInstance = null;
        }
        if (guestCircleInstance && mapInstance) {
            mapInstance.removeLayer(guestCircleInstance);
            guestCircleInstance = null;
        }
    },

    updateRadius: function (radius) {
        if (circleInstance && typeof radius === 'number' && radius > 0) {
            circleInstance.setRadius(radius);
            if (mapInstance && radius > 1000) {
                mapInstance.fitBounds(circleInstance.getBounds(), { padding: [20, 20] });
            }
        }
    },

    fitBoundsToRadius: function () {
        if (circleInstance && mapInstance) {
            mapInstance.fitBounds(circleInstance.getBounds(), { padding: [20, 20] });
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
    },

    showHoverMiniMap: function (containerId, userLat, userLng, tenantLat, tenantLng, tenantName, userLabel, tenantLabel) {
        const container = document.getElementById(containerId);
        if (!container) {
            return;
        }

        let item = hoverMapInstances[containerId];

        // If instance exists but container in DOM is different, detached, or corrupted, clean it up
        if (item && item.map) {
            const mapContainer = item.map.getContainer ? item.map.getContainer() : null;
            if (!mapContainer || mapContainer !== container || !document.body.contains(mapContainer)) {
                try {
                    item.map.remove();
                } catch (e) {
                    console.warn("Error removing detached hover map instance:", e);
                }
                delete hoverMapInstances[containerId];
                item = null;
            }
        }

        if (!item || !item.map) {
            if (container._leaflet_id) {
                delete container._leaflet_id;
                container.innerHTML = '';
            }

            const map = L.map(containerId, {
                zoomControl: false,
                attributionControl: false,
                scrollWheelZoom: false,
                dragging: false,
                touchZoom: false,
                doubleClickZoom: false,
                boxZoom: false
            });

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19
            }).addTo(map);

            item = {
                map: map,
                userMarker: null,
                tenantMarker: null,
                polyline: null
            };
            hoverMapInstances[containerId] = item;
        }

        if (item.userMarker) {
            item.map.removeLayer(item.userMarker);
            item.userMarker = null;
        }
        if (item.tenantMarker) {
            item.map.removeLayer(item.tenantMarker);
            item.tenantMarker = null;
        }
        if (item.polyline) {
            item.map.removeLayer(item.polyline);
            item.polyline = null;
        }

        const hasTenant = typeof tenantLat === 'number' && typeof tenantLng === 'number' && !isNaN(tenantLat) && !isNaN(tenantLng);
        const hasUser = typeof userLat === 'number' && typeof userLng === 'number' && !isNaN(userLat) && !isNaN(userLng);

        if (!hasTenant) {
            return;
        }

        const tenantIcon = L.divIcon({
            className: 'tenant-map-marker-pin',
            html: '<div style="background-color: #dc3545; color: white; width: 28px; height: 28px; border-radius: 50%; display: flex; align-items: center; justify-content: center; box-shadow: 0 2px 6px rgba(0,0,0,0.3); border: 2px solid white;"><i class="bi bi-buildings-fill" style="font-size: 13px;"></i></div>',
            iconSize: [28, 28],
            iconAnchor: [14, 14]
        });

        item.tenantMarker = L.marker([tenantLat, tenantLng], { icon: tenantIcon })
            .bindTooltip(tenantName || tenantLabel || "Library", { permanent: false, direction: 'top', offset: [0, -14] })
            .addTo(item.map);

        if (hasUser) {
            const userIcon = L.divIcon({
                className: 'user-map-marker-pin',
                html: '<div style="background-color: #0d6efd; color: white; width: 26px; height: 26px; border-radius: 50%; display: flex; align-items: center; justify-content: center; box-shadow: 0 2px 6px rgba(0,0,0,0.3); border: 2px solid white;"><i class="bi bi-person-fill" style="font-size: 14px;"></i></div>',
                iconSize: [26, 26],
                iconAnchor: [13, 13]
            });

            item.userMarker = L.marker([userLat, userLng], { icon: userIcon })
                .bindTooltip(userLabel || "Your Location", { permanent: false, direction: 'top', offset: [0, -14] })
                .addTo(item.map);

            item.polyline = L.polyline([
                [userLat, userLng],
                [tenantLat, tenantLng]
            ], {
                color: '#0d6efd',
                weight: 3,
                dashArray: '6, 8',
                opacity: 0.8
            }).addTo(item.map);

            const bounds = L.latLngBounds([
                [userLat, userLng],
                [tenantLat, tenantLng]
            ]);
            item.map.fitBounds(bounds, { padding: [35, 35], maxZoom: 15 });
        } else {
            item.map.setView([tenantLat, tenantLng], 15);
        }

        setTimeout(function () {
            if (item && item.map) {
                item.map.invalidateSize();
            }
        }, 60);
        setTimeout(function () {
            if (item && item.map) {
                item.map.invalidateSize();
            }
        }, 220);
    },

    destroyHoverMiniMap: function (containerId) {
        if (containerId && hoverMapInstances[containerId]) {
            try {
                hoverMapInstances[containerId].map.remove();
            } catch (e) {
                console.warn("Error destroying hover map instance:", e);
            }
            delete hoverMapInstances[containerId];
        }
    },

    positionHoverPopover: function (trigger, popover) {
        const triggerEl = (typeof trigger === 'string') ? document.getElementById(trigger) : trigger;
        const popoverEl = (typeof popover === 'string') ? document.getElementById(popover) : popover;
        if (!triggerEl || !popoverEl) return;
        const rect = triggerEl.getBoundingClientRect();
        const popoverWidth = 320;
        const popoverHeight = 310;

        let top = rect.bottom + 8;
        let left = rect.left;

        if (left + popoverWidth > window.innerWidth - 12) {
            left = Math.max(12, window.innerWidth - popoverWidth - 12);
        }

        if (top + popoverHeight > window.innerHeight - 12) {
            top = Math.max(12, rect.top - popoverHeight - 8);
        }

        popoverEl.style.top = top + 'px';
        popoverEl.style.left = left + 'px';
    }
};

window.restaurantMap = window.tenantMap;
