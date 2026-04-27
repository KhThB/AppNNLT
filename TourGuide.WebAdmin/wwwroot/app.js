// 1. BẢN ĐỒ CLUSTER MAP (Cho Dashboard Admin)
window.renderClusterMap = (mapId, points) => {
    try {
        const el = document.getElementById(mapId);
        if (!el) return;

        if (el._leaflet_id) {
            el._leaflet_id = null;
            el.innerHTML = "";
        }
        if (window.myMap) { window.myMap.remove(); }

        window.myMap = L.map(mapId).setView([10.7628, 106.7005], 14); // Tọa độ trung tâm Vĩnh Khánh
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(window.myMap);

        if (points && points.length > 0) {
            var markers = L.markerClusterGroup({
                maxClusterRadius: 50 // Bán kính gộp nhóm
            });

            points.forEach(p => {
                // p[0] là lat, p[1] là lng
                var marker = L.marker([p[0], p[1]]);
                // Nếu có title thì add popup
                if(p[2]) marker.bindPopup("<b>" + p[2] + "</b>");
                markers.addLayer(marker);
            });

            window.myMap.addLayer(markers);
        }

        var count = 0;
        var interval = setInterval(function () {
            window.myMap.invalidateSize();
            count++;
            if (count > 10) clearInterval(interval);
        }, 100);

    } catch (e) {
        console.log("ClusterMap chưa sẵn sàng.", e);
    }
};
window.renderHeatmapMap = (mapId, points) => {
    const el = document.getElementById(mapId);
    if (!el || typeof L === 'undefined') {
        return;
    }

    if (el._leaflet_id) {
        el._leaflet_id = null;
        el.innerHTML = "";
    }

    if (window.heatmapMap) {
        window.heatmapMap.remove();
    }

    window.heatmapMap = L.map(mapId).setView([10.7628, 106.7005], 14);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(window.heatmapMap);

    if (!points || points.length === 0) {
        const emptyControl = L.control({ position: 'topright' });
        emptyControl.onAdd = function () {
            const div = L.DomUtil.create('div', 'heatmap-empty-state');
            div.innerText = 'Chưa có dữ liệu tracking trong khung thời gian này';
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
    var container = document.getElementById(mapId);
    if (!container) return;
    if (container._leaflet_id) { container._leaflet_id = null; container.innerHTML = ""; }

    var map = L.map(mapId).setView([lat, lng], 17);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
    L.marker([lat, lng]).addTo(map).bindPopup(title).openPopup();

    // Tự động sửa kích thước
    setTimeout(() => { map.invalidateSize(); }, 200);
};
// 2. BẢN ĐỒ CHỌN VỊ TRÍ (Cho Chủ Quán)
window.initMapPicker = function (mapId, dotNetHelper) {
    var container = document.getElementById(mapId);
    if (!container) return;

    if (container._leaflet_id) {
        container._leaflet_id = null;
        container.innerHTML = "";
    }

    var centerLat = 10.7628;
    var centerLng = 106.7005;

    var map = L.map(mapId).setView([centerLat, centerLng], 17);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap'
    }).addTo(map);

    var marker = L.marker([centerLat, centerLng], { draggable: true }).addTo(map);

    function sendCoords(lat, lng) {
        dotNetHelper.invokeMethodAsync('UpdateCoordinates', lat, lng);
    }

    marker.on('dragend', function (e) {
        var pos = marker.getLatLng();
        sendCoords(pos.lat, pos.lng);
    });

    map.on('click', function (e) {
        marker.setLatLng(e.latlng);
        sendCoords(e.latlng.lat, e.latlng.lng);
    });

    // VŨ KHÍ TỐI THƯỢNG: Ép vẽ lại mỗi 100ms trong 1 giây đầu tiên
    var count = 0;
    var interval = setInterval(function () {
        map.invalidateSize();
        count++;
        if (count >= 10) clearInterval(interval);
    }, 100);
};
