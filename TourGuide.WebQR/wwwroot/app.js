window.tourGuideQr = {
    ensureVisitorId() {
        const cookieKey = "tourguide_visitor_id";
        const existing = document.cookie.split("; ").find(x => x.startsWith(cookieKey + "="));
        if (existing) {
            return existing.split("=")[1];
        }

        const value = (window.crypto?.randomUUID?.() || ("visitor-" + Math.random().toString(36).slice(2)));
        document.cookie = `${cookieKey}=${value}; path=/; max-age=${60 * 60 * 24 * 365}`;
        return value;
    },

    ensureSessionId() {
        const key = "tourguide_qr_session_id";
        let value = sessionStorage.getItem(key);
        if (!value) {
            value = window.crypto?.randomUUID?.() || ("session-" + Math.random().toString(36).slice(2));
            sessionStorage.setItem(key, value);
        }

        return value;
    },

    stopPlayback(state) {
        if (state.audio) {
            state.audio.pause();
            state.audio.currentTime = 0;
            state.audio = null;
        }

        if (window.speechSynthesis?.speaking) {
            window.speechSynthesis.cancel();
        }
    },

    playContent(state, content, lang, onEnded, onError) {
        this.stopPlayback(state);

        if (content.audioUrl && content.audioUrl.trim() !== "") {
            const audio = new Audio(content.audioUrl);
            state.audio = audio;
            audio.onended = onEnded;
            audio.onerror = onError;
            audio.play().catch(onError);
            return;
        }

        if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) {
            onError();
            return;
        }

        const utterance = new SpeechSynthesisUtterance(content.description || "No content");
        utterance.lang = {
            VI: "vi-VN",
            EN: "en-US",
            KO: "ko-KR",
            JA: "ja-JP",
            ZH: "zh-CN"
        }[lang] || "vi-VN";
        utterance.onend = onEnded;
        utterance.onerror = onError;
        window.speechSynthesis.speak(utterance);
    },

    mountMap(lat, lng, title) {
        const el = document.getElementById("mini-map");
        if (!window.L || !el) {
            return;
        }

        if (el._leaflet_id) {
            el._leaflet_id = null;
            el.innerHTML = "";
        }
        if (window.qrMiniMap) {
            window.qrMiniMap.remove();
        }

        const map = L.map("mini-map", {
            zoomControl: false,
            dragging: false,
            scrollWheelZoom: false,
            doubleClickZoom: false
        }).setView([lat, lng], 16);
        window.qrMiniMap = map;

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
        L.marker([lat, lng]).addTo(map).bindPopup(title).openPopup();
        
        setTimeout(() => map.invalidateSize(), 200);
    }
};

window.tourGuideQrPage = {
    async init(config) {
        const state = { audio: null, logId: null, startedAt: null };
        const visitorId = window.tourGuideQr.ensureVisitorId();
        const sessionId = window.tourGuideQr.ensureSessionId();
        const payload = config.payload;
        const languages = Object.keys(payload.contents || {});
        let currentLang = languages.includes("VI") ? "VI" : languages[0];

        const langSelector = document.getElementById("lang-selector");
        const contentStatus = document.getElementById("content-status");
        const descriptionEl = document.getElementById("desc-text");
        const playBtn = document.getElementById("play-audio-btn");
        const stopBtn = document.getElementById("stop-audio-btn");
        const playStatus = document.getElementById("play-status");
        const banner = document.getElementById("qr-banner");
        const directionsBtn = document.getElementById("directions-btn");

        const showPlayStatus = (message, isError) => {
            if (!playStatus) {
                return;
            }

            if (!message) {
                playStatus.style.display = "none";
                playStatus.innerText = "";
                playStatus.classList.remove("error");
                return;
            }

            playStatus.style.display = "block";
            playStatus.innerText = message;
            playStatus.classList.toggle("error", !!isError);
        };

        const renderDescription = () => {
            const content = payload.contents[currentLang];
            if (!content) {
                descriptionEl.innerText = "Đang cập nhật nội dung...";
                contentStatus.style.display = "block";
                contentStatus.innerText = "Nội dung ngôn ngữ này chưa sẵn sàng.";
                return;
            }

            descriptionEl.innerText = content.description || "Đang cập nhật nội dung...";
            if (content.status && content.status !== "Ready") {
                contentStatus.style.display = "block";
                contentStatus.innerText = "Bản dịch đang chờ xử lý thủ công. Nội dung hiện tại có thể là bản nguồn.";
            } else {
                contentStatus.style.display = "none";
                contentStatus.innerText = "";
            }
        };

        languages.forEach(lang => {
            const btn = document.createElement("button");
            btn.className = "lang-btn" + (lang === currentLang ? " active" : "");
            btn.textContent = lang;
            btn.onclick = () => {
                currentLang = lang;
                langSelector.querySelectorAll(".lang-btn").forEach(x => x.classList.remove("active"));
                btn.classList.add("active");
                window.tourGuideQr.stopPlayback(state);
                showPlayStatus("");
                playBtn.style.display = "inline-flex";
                stopBtn.style.display = "none";
                renderDescription();
            };
            langSelector.appendChild(btn);
        });

        renderDescription();

        const scanResponse = await fetch(`${config.apiBase}api/qr/scan`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                poiId: config.poiId,
                visitorId,
                sessionId,
                triggerSource: "WebQR"
            })
        }).then(r => r.json()).catch(() => null);

        if (scanResponse && scanResponse.inCooldown) {
            banner.style.display = "block";
            banner.innerText = scanResponse.message;
        }
        if (!scanResponse) {
            banner.style.display = "block";
            banner.innerText = "Không ghi nhận được lượt quét lúc này, bạn vẫn có thể nghe thuyết minh.";
        }

        const finishLog = async (status, errorCode) => {
            if (!state.logId) {
                return;
            }

            const dwell = state.startedAt ? Math.max(0, Math.round((Date.now() - state.startedAt) / 1000)) : 0;
            await fetch(`${config.apiBase}api/narration/finish`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    logId: state.logId,
                    status,
                    dwellTimeSeconds: dwell,
                    errorCode: errorCode || ""
                })
            }).catch(() => null);

            state.logId = null;
            state.startedAt = null;
        };

        playBtn.onclick = async () => {
            showPlayStatus("");
            const playResponse = await fetch(`${config.apiBase}api/narration/play`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    poiId: config.poiId,
                    visitorId,
                    sessionId,
                    language: currentLang,
                    triggerSource: "WebQR"
                })
            }).then(r => r.json()).catch(() => null);

            if (!playResponse) {
                showPlayStatus("Không ghi nhận được lượt nghe. Vui lòng thử lại.", true);
                return;
            }

            if (playResponse.rateLimited) {
                showPlayStatus("Bạn đang nghe lại trong khung giới hạn, hệ thống không cộng thêm lượt nghe.", false);
            }

            state.logId = playResponse.logId;
            state.startedAt = Date.now();
            playBtn.style.display = "none";
            stopBtn.style.display = "inline-flex";

            window.tourGuideQr.playContent(
                state,
                payload.contents[currentLang],
                currentLang,
                async () => {
                    playBtn.style.display = "inline-flex";
                    stopBtn.style.display = "none";
                    showPlayStatus("");
                    await finishLog("Completed");
                },
                async () => {
                    playBtn.style.display = "inline-flex";
                    stopBtn.style.display = "none";
                    showPlayStatus("Không thể phát audio hoặc giọng đọc trên trình duyệt này.", true);
                    await finishLog("Error", "PLAYBACK_FAILED");
                });
        };

        stopBtn.onclick = async () => {
            window.tourGuideQr.stopPlayback(state);
            playBtn.style.display = "inline-flex";
            stopBtn.style.display = "none";
            showPlayStatus("Đã dừng phát thuyết minh.", false);
            await finishLog("Stopped");
        };

        directionsBtn.onclick = () => {
            window.open(`https://www.google.com/maps/dir/?api=1&destination=${payload.latitude},${payload.longitude}`, "_blank");
        };

        window.tourGuideQr.mountMap(payload.latitude, payload.longitude, payload.name);
    }
};
