/* =====================================================
   DoctorEngine.Az — sayt üzrə kiçik UX təkmilləşdirmələri
   ===================================================== */
(function () {
    "use strict";

    // ---------- Header: sürüşdürəndə kölgə və kiçilmə ----------
    const navbar = document.querySelector(".s-header .navbar");
    if (navbar) {
        let ticking = false;
        const update = () => {
            navbar.classList.toggle("is-scrolled", window.scrollY > 24);
            ticking = false;
        };
        window.addEventListener("scroll", () => {
            if (!ticking) {
                requestAnimationFrame(update);
                ticking = true;
            }
        }, { passive: true });
        update();
    }

    // ---------- Üzən düymələr (WhatsApp + Yuxarı qayıt) footer-in alt sətrini örtməsin ----------
    // "Developed by" sətri ekranda görünəndə düymələr onun hündürlüyü qədər yuxarı qalxır.
    const copyright = document.querySelector(".footer .copyright");
    if (copyright && "IntersectionObserver" in window) {
        const setLift = (visible) => {
            const lift = visible ? Math.ceil(copyright.getBoundingClientRect().height) + 12 : 0;
            document.body.style.setProperty("--de-float-lift", lift + "px");
        };
        let isVisible = false;
        new IntersectionObserver((entries) => {
            isVisible = entries[0].isIntersecting;
            setLift(isVisible);
        }).observe(copyright);
        // Ekran ölçüsü dəyişəndə (mobildə sətir iki xəttə düşə bilər) yenidən hesabla
        window.addEventListener("resize", () => { if (isVisible) setLift(true); }, { passive: true });
    }

    // ---------- Yuxarı qayıt düyməsi (oxunma halqası ilə) ----------
    const toTop = document.querySelector(".de-totop");
    if (toTop) {
        const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
        let queued = false;

        const updateToTop = () => {
            queued = false;
            const max = document.documentElement.scrollHeight - window.innerHeight;
            const progress = max > 0 ? Math.min(100, Math.max(0, (window.scrollY / max) * 100)) : 0;
            toTop.style.setProperty("--de-progress", progress.toFixed(1));
            toTop.classList.toggle("is-visible", window.scrollY > 400);
        };

        window.addEventListener("scroll", () => {
            if (!queued) {
                queued = true;
                requestAnimationFrame(updateToTop);
            }
        }, { passive: true });
        window.addEventListener("resize", updateToTop, { passive: true });

        toTop.addEventListener("click", () => {
            window.scrollTo({ top: 0, behavior: reduceMotion.matches ? "auto" : "smooth" });
            // Klaviatura istifadəçisi üçün fokus səhifənin əvvəlinə
            const brand = document.querySelector(".navbar-brand");
            if (brand) brand.focus({ preventScroll: true });
        });

        updateToTop();
    }

    // ---------- Slayder: hərəkət azaldılıbsa avtomatik dəyişməsin ----------
    const hero = document.getElementById("header-carousel");
    if (hero && window.bootstrap && window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        bootstrap.Carousel.getOrCreateInstance(hero, { interval: false, ride: false }).pause();
        hero.classList.add("is-paused");
    }

    // ---------- Mobil menyu: keçid seçiləndə bağlansın ----------
    const collapseEl = document.getElementById("navbarCollapse");
    if (collapseEl && window.bootstrap) {
        collapseEl.querySelectorAll("a.nav-link:not(.dropdown-toggle), .dropdown-item").forEach((link) => {
            link.addEventListener("click", () => {
                if (window.matchMedia("(max-width: 991.98px)").matches && collapseEl.classList.contains("show")) {
                    bootstrap.Collapse.getOrCreateInstance(collapseEl).hide();
                }
            });
        });
    }

    // ---------- Formalar: göndərilərkən düymə "yüklənir" vəziyyətinə keçsin ----------
    document.querySelectorAll("form[data-de-loading]").forEach((form) => {
        form.addEventListener("submit", () => {
            // jQuery validation formu rədd edibsə yükləmə göstərmə
            if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;
            const btn = form.querySelector("button[type=submit]");
            if (!btn || btn.disabled) return;
            btn.dataset.label = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>' + (form.dataset.deLoading || "Göndərilir…");
        });
    });

    // Geri düyməsi ilə qayıdanda (bfcache) düymələr bərpa olunsun
    window.addEventListener("pageshow", () => {
        document.querySelectorAll("form[data-de-loading] button[type=submit][data-label]").forEach((btn) => {
            btn.innerHTML = btn.dataset.label;
            btn.disabled = false;
        });
    });
})();
