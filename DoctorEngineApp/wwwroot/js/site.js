
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

    // Eyni anda yalnız bir video oynasın: biri başlayanda digərləri dayanır.
    // "play" hadisəsi yuxarı qalxmır, ona görə capture ilə sənəd səviyyəsində tutulur —
    // sonradan əlavə olunan videolar (tablar, çat) da əhatə olunur.
    document.addEventListener('play', (e) => {
        const current = e.target;
        if (!(current instanceof HTMLVideoElement)) return;
        document.querySelectorAll('video').forEach(v => {
            if (v !== current && !v.paused) v.pause();
        });
        activeVideo = current;
    }, true);

    document.querySelectorAll('video').forEach(video => {
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

    // Tab dəyişəndə: videoları dayandır, mobil ekranda məzmuna sürüşdür, ünvana #cat-N yaz
    document.addEventListener('shown.bs.tab', (e) => {
        document.querySelectorAll('.tab-pane video').forEach(v => {
            if (!v.paused) v.pause();
        });

        const target = e.target && e.target.dataset ? e.target.dataset.bsTarget : null;
        const pane = target ? document.querySelector(target) : null;
        if (!pane || !pane.closest('[data-de-services]')) return;

        // Mobildə tablar məzmunun üstündədir — seçim edəndə dəyişiklik görünsün
        if (window.matchMedia('(max-width: 991.98px)').matches) {
            const top = pane.getBoundingClientRect().top;
            if (top > window.innerHeight * 0.6 || top < 0) pane.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }

        if (history.replaceState) history.replaceState(null, '', target);
    });

    // Birbaşa link: /Service#cat-2 həmin kateqoriyanı açır
    if (location.hash && /^#cat-\d+$/.test(location.hash) && window.bootstrap) {
        const tabBtn = document.querySelector('[data-de-services] [data-bs-target="' + location.hash + '"]');
        if (tabBtn) bootstrap.Tab.getOrCreateInstance(tabBtn).show();
    }

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

    // WhatsApp = Phone (işarəli olduqca telefon dəyişdikcə WhatsApp da yenilənir)
    const sameAsPhone = document.getElementById("sameAsPhone");
    const phoneField = document.getElementById("phoneInput");
    const whatsappField = document.getElementById("whatsappInput");
    if (sameAsPhone && phoneField && whatsappField) {
        const sync = () => {
            if (sameAsPhone.checked) whatsappField.value = phoneField.value;
            whatsappField.readOnly = sameAsPhone.checked;
        };
        sameAsPhone.addEventListener("change", () => {
            if (!sameAsPhone.checked) whatsappField.value = "";
            sync();
        });
        phoneField.addEventListener("input", sync);
    }

    // Reservation form AJAX submit
    const reservationForm = document.getElementById("reservationForm");
    if (reservationForm) {
        const messageDiv = document.getElementById("reservationMessage");
        const submitBtn = reservationForm.querySelector("button[type=submit]");
        const originalLabel = submitBtn ? submitBtn.innerHTML : "";

        function showMessage(success, text) {
            if (!messageDiv) return;
            const alertDiv = document.createElement("div");
            alertDiv.className = "alert d-flex align-items-center gap-2 " + (success ? "alert-success" : "alert-danger");
            const icon = document.createElement("i");
            icon.className = success ? "fa-solid fa-circle-check" : "fa-solid fa-circle-exclamation";
            const span = document.createElement("span");
            span.textContent = text;
            alertDiv.append(icon, span);
            messageDiv.replaceChildren(alertDiv);
        }

        reservationForm.addEventListener("submit", async function (e) {
            e.preventDefault();
            if (submitBtn && submitBtn.disabled) return; // təkrar göndərmənin qarşısı

            // Brauzerin öz yoxlaması (boş sahələr və s.)
            if (!this.checkValidity()) {
                this.reportValidity();
                return;
            }

            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Göndərilir…';
            }

            try {
                const response = await fetch(this.action, { method: "POST", body: new FormData(this) });
                const data = await response.json();
                showMessage(!!data.success, data.message || "Xəta baş verdi. Yenidən cəhd edin.");
                if (data.success) {
                    this.reset();
                    if (whatsappField) whatsappField.readOnly = false;
                }
            } catch (err) {
                console.error(err);
                showMessage(false, "Bağlantı xətası. İnternetinizi yoxlayıb yenidən cəhd edin.");
            } finally {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalLabel;
                }
            }
        });
    }
})();
