
(function () {

    let activeVideo = null;

    function stopVideo(video) {
        if (!video) return;
        video.pause();
        video.currentTime = 0;
        video.load();
    }

    function stopAllExcept(current) {
        document.querySelectorAll('video').forEach(v => {
            if (v !== current) stopVideo(v);
        });
    }

    // Video play/pause/ended
    document.querySelectorAll('video').forEach(video => {
        video.addEventListener('play', () => {
            stopAllExcept(video);
            activeVideo = video;
        });

        video.addEventListener('ended', () => {
            stopVideo(video);
            if (video.id === "aboutVideo") {
                const btn = document.getElementById('playButton');
                if (btn) btn.style.display = 'block';
            }
        });
    });

    // About play button
    const aboutVideo = document.getElementById('aboutVideo');
    const playButton = document.getElementById('playButton');

    if (aboutVideo && playButton) {
        playButton.addEventListener('click', () => {
            stopAllExcept(aboutVideo);
            aboutVideo.play();
            playButton.style.display = 'none';
        });
    }

    // Tab change video stop
    document.addEventListener('shown.bs.tab', () => {
        document.querySelectorAll('.tab-pane video').forEach(v => stopVideo(v));
    });

    // Phone mask function
    function maskAZPhone(input) {
        if (!input) return;
        input.addEventListener("input", function () {
            let digits = this.value.replace(/\D/g, "");
            if (!digits.startsWith("994")) digits = "994" + digits;
            digits = digits.substring(0, 12);
            let f = "+";
            if (digits.length > 0) f += digits.substring(0, 3);
            if (digits.length > 3) f += " " + digits.substring(3, 5);
            if (digits.length > 5) f += " " + digits.substring(5, 8);
            if (digits.length > 8) f += " " + digits.substring(8, 10);
            if (digits.length > 10) f += " " + digits.substring(10, 12);
            this.value = f;
        });
        input.addEventListener("keypress", function (e) {
            if (!/[0-9]/.test(e.key)) e.preventDefault();
        });
    }

    maskAZPhone(document.getElementById("phoneInput"));
    maskAZPhone(document.getElementById("whatsappInput"));

    // WhatsApp = Phone
    const sameAsPhone = document.getElementById("sameAsPhone");
    if (sameAsPhone) {
        sameAsPhone.addEventListener("change", function () {
            const whatsappInput = document.getElementById("whatsappInput");
            const phoneInput = document.getElementById("phoneInput");
            if (whatsappInput && phoneInput) {
                whatsappInput.value = this.checked ? phoneInput.value : "";
            }
        });
    }

    // Reservation form AJAX submit
    const reservationForm = document.getElementById("reservationForm");
    if (reservationForm) {
        reservationForm.addEventListener("submit", function (e) {
            e.preventDefault();
            var formData = new FormData(this);
            fetch(this.action, {
                method: 'POST',
                body: formData
            })
                .then(response => response.json())
                .then(data => {
                    const messageDiv = document.getElementById("reservationMessage");
                    if (!messageDiv) return;
                    messageDiv.innerHTML = data.success
                        ? `<div class="alert alert-success">${data.message}</div>`
                        : `<div class="alert alert-danger">${data.message}</div>`;
                    if (data.success) this.reset();
                })
                .catch(console.error);
        });
    }

})();
