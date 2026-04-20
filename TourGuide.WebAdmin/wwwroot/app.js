window.renderHeatmap = (mapId, heatmapPoints) => {
    try {
        const el = document.getElementById(mapId);
        // Nếu không thấy thẻ map hoặc thẻ map chưa có kích thước, thoát ngay lập tức
        if (!el || el.offsetWidth === 0) return;

        if (window.myMap) { window.myMap.remove(); }

        window.myMap = L.map(mapId).setView([10.7725, 106.6980], 14);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(window.myMap);
        L.heatLayer(heatmapPoints, { radius: 25, blur: 15 }).addTo(window.myMap);
    } catch (e) {
        // Chỉ hiện cảnh báo nhẹ, không làm treo cả trang web
        console.log("Map chưa sẵn sàng, sẽ thử lại sau.");
    }
};
window.initMapPicker = function (mapId, dotNetHelper) {
    var container = document.getElementById(mapId);
    if (!container) return;

    // Tránh lỗi khởi tạo lại của Blazor
    if (container._leaflet_id) {
        container._leaflet_id = null;
        container.innerHTML = "";
    }

    // Tọa độ mặc định: Khu vực phố Vinh Khánh, Quận 4
    var centerLat = 10.7628;
    var centerLng = 106.7005;

    var map = L.map(mapId).setView([centerLat, centerLng], 17);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);

    // Tạo điểm cắm (Marker) có thể kéo thả
    var marker = L.marker([centerLat, centerLng], { draggable: true }).addTo(map);

    // Bắt sự kiện khi kéo thả marker xong
    marker.on('dragend', function (e) {
        var pos = marker.getLatLng();
        dotNetHelper.invokeMethodAsync('UpdateCoordinates', pos.lat, pos.lng);
    });

    // Bắt sự kiện khi click trực tiếp lên bản đồ
    map.on('click', function (e) {
        marker.setLatLng(e.latlng);
        dotNetHelper.invokeMethodAsync('UpdateCoordinates', e.latlng.lat, e.latlng.lng);
    });
};