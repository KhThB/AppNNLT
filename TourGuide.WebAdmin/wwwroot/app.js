// 1. BẢN ĐỒ HEATMAP (Cho Dashboard Admin)
window.renderHeatmap = (mapId, heatmapPoints) => {
    try {
        const el = document.getElementById(mapId);
        if (!el) return;

        if (el._leaflet_id) {
            el._leaflet_id = null;
            el.innerHTML = "";
        }
        if (window.myMap) { window.myMap.remove(); }

        window.myMap = L.map(mapId).setView([10.7725, 106.6980], 14);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(window.myMap);

        if (heatmapPoints && heatmapPoints.length > 0) {
            L.heatLayer(heatmapPoints, { radius: 25, blur: 15 }).addTo(window.myMap);
        }

        // VŨ KHÍ TỐI THƯỢNG: Ép vẽ lại mỗi 100ms trong 1 giây đầu tiên
        var count = 0;
        var interval = setInterval(function () {
            window.myMap.invalidateSize();
            count++;
            if (count > 10) clearInterval(interval);
        }, 100);

    } catch (e) {
        console.log("Heatmap chưa sẵn sàng.", e);
    }
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