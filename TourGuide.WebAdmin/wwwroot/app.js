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