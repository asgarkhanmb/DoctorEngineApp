/* =====================================================
   Xidmətlər səhifəsi: kateqoriya filtri, kart önizləməsi, video pəncərəsi
   ===================================================== */
(function () {
    "use strict";

    // Səhifədə bir neçə .sv bloku ola bilər (Ana səhifə: Haqqımızda, Xidmətlər, Komanda) —
    // ona görə bütün sənəd üzrə işləyirik; xidmət kartları yoxdursa heç nə etmirik
    const root = document;
    if (!root.querySelector(".sv-grid")) return;

    // ---------- Filtr ----------
    const chips = root.querySelectorAll(".sv-chip");
    const cards = root.querySelectorAll(".sv-card");

    function applyFilter(filter, updateHash) {
        let found = false;
        chips.forEach((c) => {
            const on = c.dataset.filter === filter;
            if (on) found = true;
            c.classList.toggle("is-active", on);
            c.setAttribute("aria-pressed", on ? "true" : "false");
        });
        if (!found) return applyFilter("all", false);

        cards.forEach((card) => {
            const show = filter === "all" || card.dataset.cat === filter;
            card.hidden = !show;
            if (show) {
                card.classList.remove("is-in");
                void card.offsetWidth;            // animasiyanı yenidən başlat
                card.classList.add("is-in");
            }
        });

        if (updateHash && history.replaceState) {
            history.replaceState(null, "", filter === "all" ? location.pathname : "#" + filter);
        }
    }

    chips.forEach((chip) => chip.addEventListener("click", () => applyFilter(chip.dataset.filter, true)));

    // Birbaşa link: /Service#cat-2
    if (/^#cat-\d+$/.test(location.hash)) applyFilter(location.hash.slice(1), false);

    // ---------- Kart videosu: müddət + üzərinə gələndə səssiz önizləmə ----------
    const canHover = window.matchMedia("(hover: hover) and (pointer: fine)").matches;
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const fmt = (s) => {
        s = Math.round(s);
        return Math.floor(s / 60) + ":" + String(s % 60).padStart(2, "0");
    };

    root.querySelectorAll(".sv-card__media").forEach((media) => {
        const video = media.querySelector("video");
        const label = media.querySelector("[data-duration]");
        if (!video) return;

        const setDuration = () => {
            if (label && isFinite(video.duration) && video.duration > 0) label.textContent = fmt(video.duration);
        };
        video.addEventListener("loadedmetadata", setDuration);
        if (video.readyState >= 1) setDuration();

        if (canHover && !reduceMotion) {
            media.addEventListener("mouseenter", () => {
                const p = video.play();
                if (p && p.catch) p.catch(() => { });
                media.classList.add("is-previewing");
            });
            media.addEventListener("mouseleave", () => {
                video.pause();
                media.classList.remove("is-previewing");
            });
        }
    });

    // ---------- Video pəncərəsi ----------
    const modalEl = document.getElementById("svVideoModal");
    if (!modalEl || !window.bootstrap) return;

    // Pəncərə body-də olsun — animasiyalı (transform) bölmələr onu kəsməsin
    if (modalEl.parentElement !== document.body) document.body.appendChild(modalEl);

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const player = modalEl.querySelector(".sv-modal__video");
    const title = modalEl.querySelector(".sv-modal__title");

    root.addEventListener("click", (e) => {
        const media = e.target.closest(".sv-card__media");
        if (!media) return;

        const preview = media.querySelector("video");
        if (preview) preview.pause();

        title.textContent = media.dataset.title || "";
        player.src = media.dataset.video;
        modal.show();
    });

    player.addEventListener("loadedmetadata", () => {
        modalEl.classList.toggle("is-wide", player.videoWidth > player.videoHeight);
    });

    modalEl.addEventListener("shown.bs.modal", () => {
        const p = player.play();
        if (p && p.catch) p.catch(() => { });
    });

    // Bağlananda dayandır və yükləməni kəs
    modalEl.addEventListener("hidden.bs.modal", () => {
        player.pause();
        player.removeAttribute("src");
        player.load();
        modalEl.classList.remove("is-wide");
    });
})();
