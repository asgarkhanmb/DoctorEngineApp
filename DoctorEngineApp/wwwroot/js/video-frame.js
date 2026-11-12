/* =====================================================
   Vahid video çərçivəsi: şaquli videoların yanlarını
   videonun öz bulanıq kadrı ilə doldurur.
   Kiçik kətan (64×36) istifadə olunur — yük çox azdır.
   ===================================================== */
(function () {
    "use strict";

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    function setup(frame) {
        if (frame.dataset.vfReady) return;
        frame.dataset.vfReady = "1";

        const video = frame.querySelector(".de-video__player");
        const canvas = frame.querySelector(".de-video__backdrop");
        if (!video || !canvas) return;

        const ctx = canvas.getContext("2d", { alpha: false });
        if (!ctx) return;
        canvas.width = 64;
        canvas.height = 36;

        let raf = 0;
        let last = 0;

        const draw = () => {
            if (!video.videoWidth) return;
            try {
                ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
                frame.classList.add("has-backdrop");
            } catch {
                /* kadr hələ hazır deyil */
            }
        };

        // Oynayarkən fonu ~8 kadr/san yenilə (hərəkətli fon)
        const loop = (t) => {
            if (video.paused || video.ended) { raf = 0; return; }
            if (t - last > 125) { draw(); last = t; }
            raf = requestAnimationFrame(loop);
        };

        // Çərçivə videonun öz nisbətini alır — video tam görünür, kəsilmir.
        // Kiçik önizləmələr (is-thumb) siyahıda səliqəli dursun deyə 16:9 qalır.
        const updateOrientation = () => {
            if (!video.videoWidth || !video.videoHeight) return;
            const ratio = video.videoWidth / video.videoHeight;
            frame.classList.toggle("is-wide", ratio >= 1.7);
            if (frame.classList.contains("is-thumb")) return;
            frame.style.setProperty("--vf-ratio", ratio.toFixed(4));
            frame.classList.toggle("is-tall", ratio < 1);
        };

        video.addEventListener("loadedmetadata", updateOrientation);
        video.addEventListener("loadeddata", draw);
        video.addEventListener("seeked", draw);
        video.addEventListener("play", () => {
            if (!reduceMotion && !raf) raf = requestAnimationFrame(loop);
        });
        video.addEventListener("pause", draw);

        if (video.readyState >= 1) updateOrientation();
        if (video.readyState >= 2) draw();
    }

    function init(root) {
        (root || document).querySelectorAll(".de-video").forEach(setup);
    }

    window.deVideoFrame = { init };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", () => init());
    } else {
        init();
    }
})();
