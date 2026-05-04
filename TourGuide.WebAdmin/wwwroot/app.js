window.renderClusterMap = (mapId, points) => {
    try {
        const el = document.getElementById(mapId);
        if (!el || typeof L === 'undefined') return;

        if (el._leaflet_id) {
            el._leaflet_id = null;
            el.innerHTML = "";
        }
        if (window.myMap) window.myMap.remove();

        window.myMap = L.map(mapId).setView([10.7628, 106.7005], 14);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(window.myMap);

        if (points && points.length > 0 && typeof L.markerClusterGroup === 'function') {
            const markers = L.markerClusterGroup({ maxClusterRadius: 50 });
            points.forEach(p => {
                const marker = L.marker([p[0], p[1]]);
                if (p[2]) marker.bindPopup("<b>" + p[2] + "</b>");
                markers.addLayer(marker);
            });
            window.myMap.addLayer(markers);
        }

        let count = 0;
        const interval = setInterval(function () {
            window.myMap.invalidateSize();
            count++;
            if (count > 10) clearInterval(interval);
        }, 100);
    } catch (e) {
        console.log("Cluster map chưa sẵn sàng.", e);
    }
};

window.heatmapMaps = window.heatmapMaps || {};
window.heatmapOnlineLayers = window.heatmapOnlineLayers || {};

window.updateOnlineDevicesOnMap = (mapId, devices) => {
    const map = window.heatmapMaps?.[mapId] || window.heatmapMap;
    if (!map || typeof L === 'undefined') return;

    if (window.heatmapOnlineLayers[mapId]) {
        map.removeLayer(window.heatmapOnlineLayers[mapId]);
    }

    const layer = L.layerGroup();
    (devices || []).forEach(device => {
        const lat = device.latitude ?? device.Latitude;
        const lng = device.longitude ?? device.Longitude;
        if (!lat || !lng) return;

        const name = device.deviceName ?? device.DeviceName ?? 'Thiết bị';
        const platform = device.platform ?? device.Platform ?? '';
        const lastSeen = device.lastSeenAt ?? device.LastSeenAt ?? '';
        L.circleMarker([lat, lng], {
            radius: 7,
            fillColor: '#16a34a',
            color: '#166534',
            weight: 2,
            fillOpacity: 0.9
        }).bindTooltip(`${name}<br/>${platform}<br/>${lastSeen}`).addTo(layer);
    });

    layer.addTo(map);
    window.heatmapOnlineLayers[mapId] = layer;
};

window.renderHeatmapMap = (mapId, points, pois, onlineDevices) => {
    const el = document.getElementById(mapId);
    if (!el || typeof L === 'undefined') return;

    if (el._leaflet_id) {
        el._leaflet_id = null;
        el.innerHTML = "";
    }

    if (window.heatmapMaps[mapId]) {
        window.heatmapMaps[mapId].remove();
        if (window.heatmapMap === window.heatmapMaps[mapId]) {
            window.heatmapMap = null;
        }
    } else if (window.heatmapMap) {
        window.heatmapMap.remove();
    }

    window.heatmapMap = L.map(mapId).setView([10.7628, 106.7005], 14);
    window.heatmapMaps[mapId] = window.heatmapMap;
    delete window.heatmapOnlineLayers[mapId];
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(window.heatmapMap);

    const poiPoints = pois || [];
    if (poiPoints.length > 0 && typeof L.markerClusterGroup === 'function') {
        const poiMarkers = L.markerClusterGroup({ maxClusterRadius: 45 });
        poiPoints.forEach(poi => {
            const lat = poi.latitude ?? poi.Latitude;
            const lng = poi.longitude ?? poi.Longitude;
            if (!lat || !lng) return;

            const name = poi.name ?? poi.Name ?? 'POI';
            const status = poi.status ?? poi.Status ?? '';
            const packageName = poi.subscriptionPackage ?? poi.SubscriptionPackage ?? '';
            const marker = L.marker([lat, lng]);
            marker.bindPopup(`<strong>${name}</strong><br/>${status} - ${packageName}`);
            poiMarkers.addLayer(marker);
        });
        window.heatmapMap.addLayer(poiMarkers);
    }

    window.updateOnlineDevicesOnMap(mapId, onlineDevices || []);

    if (!points || points.length === 0) {
        const emptyControl = L.control({ position: 'topright' });
        emptyControl.onAdd = function () {
            const div = L.DomUtil.create('div', 'heatmap-empty-state');
            div.innerText = poiPoints.length > 0
                ? `Đang hiển thị ${poiPoints.length} POI. Chưa có tracking trong khung thời gian này.`
                : 'Chưa có dữ liệu tracking trong khung thời gian này.';
            return div;
        };
        emptyControl.addTo(window.heatmapMap);
        setTimeout(() => window.heatmapMap.invalidateSize(), 150);
        return;
    }

    const maxIntensity = Math.max(1, ...(points || []).map(p => p.intensity || p.Intensity || 1));
    (points || []).forEach(point => {
        const lat = point.latitude ?? point.Latitude;
        const lng = point.longitude ?? point.Longitude;
        const intensity = point.intensity ?? point.Intensity ?? 1;
        const radius = 8 + ((intensity / maxIntensity) * 22);
        const opacity = 0.2 + ((intensity / maxIntensity) * 0.6);

        L.circleMarker([lat, lng], {
            radius,
            fillColor: '#ef4444',
            color: '#b91c1c',
            weight: 1,
            fillOpacity: opacity
        }).bindTooltip(`Users: ${intensity}`).addTo(window.heatmapMap);
    });

    setTimeout(() => window.heatmapMap.invalidateSize(), 150);
};

window.initStaticMap = function (mapId, lat, lng, title) {
    const container = document.getElementById(mapId);
    if (!container || typeof L === 'undefined') return;
    if (container._leaflet_id) {
        container._leaflet_id = null;
        container.innerHTML = "";
    }

    const map = L.map(mapId).setView([lat, lng], 17);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
    L.marker([lat, lng]).addTo(map).bindPopup(title).openPopup();
    setTimeout(() => map.invalidateSize(), 200);
};

window.initMapPicker = function (mapId, dotNetHelper) {
    const container = document.getElementById(mapId);
    if (!container || typeof L === 'undefined') return;

    if (container._leaflet_id) {
        container._leaflet_id = null;
        container.innerHTML = "";
    }

    const centerLat = 10.7628;
    const centerLng = 106.7005;
    const map = L.map(mapId).setView([centerLat, centerLng], 17);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap'
    }).addTo(map);

    const marker = L.marker([centerLat, centerLng], { draggable: true }).addTo(map);
    const sendCoords = (lat, lng) => dotNetHelper.invokeMethodAsync('UpdateCoordinates', lat, lng);

    marker.on('dragend', function () {
        const pos = marker.getLatLng();
        sendCoords(pos.lat, pos.lng);
    });

    map.on('click', function (e) {
        marker.setLatLng(e.latlng);
        sendCoords(e.latlng.lat, e.latlng.lng);
    });

    let count = 0;
    const interval = setInterval(function () {
        map.invalidateSize();
        count++;
        if (count >= 10) clearInterval(interval);
    }, 100);
};

window.poiLocationEditors = window.poiLocationEditors || {};

window.initPoiLocationEditor = function (mapId, lat, lng, radius, dotNetHelper) {
    const container = document.getElementById(mapId);
    if (!container || typeof L === 'undefined') return;

    const centerLat = Number(lat) || 10.7628;
    const centerLng = Number(lng) || 106.7005;
    const currentRadius = Number(radius) || 50;

    if (window.poiLocationEditors[mapId]) {
        window.poiLocationEditors[mapId].map.remove();
        delete window.poiLocationEditors[mapId];
    }

    if (container._leaflet_id) {
        container._leaflet_id = null;
        container.innerHTML = "";
    }

    const map = L.map(mapId).setView([centerLat, centerLng], 17);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap'
    }).addTo(map);

    const marker = L.marker([centerLat, centerLng], { draggable: true }).addTo(map);
    const circle = L.circle([centerLat, centerLng], {
        radius: currentRadius,
        color: '#f97316',
        fillColor: '#fdba74',
        fillOpacity: 0.22,
        weight: 2
    }).addTo(map);

    const updatePosition = (latValue, lngValue, notify) => {
        marker.setLatLng([latValue, lngValue]);
        circle.setLatLng([latValue, lngValue]);
        if (notify && dotNetHelper) {
            dotNetHelper.invokeMethodAsync('UpdateLocationFromMap', latValue, lngValue);
        }
    };

    marker.on('dragend', function () {
        const pos = marker.getLatLng();
        updatePosition(pos.lat, pos.lng, true);
    });

    map.on('click', function (e) {
        updatePosition(e.latlng.lat, e.latlng.lng, true);
    });

    window.poiLocationEditors[mapId] = { map, marker, circle };
    setTimeout(() => map.invalidateSize(), 150);
};

window.updatePoiLocationRadius = function (mapId, radius) {
    const editor = window.poiLocationEditors?.[mapId];
    if (!editor) return;
    editor.circle.setRadius(Number(radius) || 50);
    setTimeout(() => editor.map.invalidateSize(), 50);
};

window.updatePoiLocationPosition = function (mapId, lat, lng) {
    const editor = window.poiLocationEditors?.[mapId];
    if (!editor) return;
    const latitude = Number(lat);
    const longitude = Number(lng);
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return;
    editor.marker.setLatLng([latitude, longitude]);
    editor.circle.setLatLng([latitude, longitude]);
    editor.map.setView([latitude, longitude], editor.map.getZoom());
    setTimeout(() => editor.map.invalidateSize(), 50);
};
