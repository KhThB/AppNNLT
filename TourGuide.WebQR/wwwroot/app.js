window.tts = {
    play: function (text, lang) {
        if (!('speechSynthesis' in window)) {
            alert("Trình duyệt của bạn không hỗ trợ đọc văn bản.");
            return;
        }

        window.speechSynthesis.cancel(); // Hủy giọng đọc cũ
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = lang;
        utterance.rate = 0.9;

        // Ép tìm đúng giọng đọc bản địa (giống logic cũ của bạn)
        const voices = window.speechSynthesis.getVoices();
        const voicePrefix = lang.split('-')[0];
        const selectedVoice = voices.find(voice => voice.lang.startsWith(voicePrefix));
        if (selectedVoice) {
            utterance.voice = selectedVoice;
        }

        window.speechSynthesis.speak(utterance);
    },

    pauseResume: function () {
        if (window.speechSynthesis.speaking) {
            if (window.speechSynthesis.paused) {
                window.speechSynthesis.resume();
            } else {
                window.speechSynthesis.pause();
            }
        }
    },

    stop: function () {
        if ('speechSynthesis' in window) {
            window.speechSynthesis.cancel();
        }
    }
};

// Đảm bảo load đủ danh sách giọng nói trên một số trình duyệt
if ('speechSynthesis' in window) {
    window.speechSynthesis.onvoiceschanged = () => window.speechSynthesis.getVoices();
}