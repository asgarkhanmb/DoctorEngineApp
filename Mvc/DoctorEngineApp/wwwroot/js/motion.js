/* =====================================================
   DoctorEngine.Az — "canlı" sayt
   Hərəkət azaldılıbsa (prefers-reduced-motion) animasiyalar işə düşmür,
   amma məlumat (iş statusu) yenə göstərilir.
   ===================================================== */
(function () {
    "use strict";

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)").matches;

    // =====================================================
    // 1. CANLI İŞ STATUSU (Bakı vaxtı ilə, hər dəqiqə)
    // =====================================================

    const hoursEl = document.querySelector("[data-de-hours]");
    const statusEl = document.querySelector("[data-de-status]");

    const I18N_AZ = {
        openSoon: "İndi açığıq · {0} dəq sonra bağlanırıq",
        openUntil: "İndi açığıq · {0}-dək",
        opensToday: "Bağlıyıq · bu gün {0}-da açılırıq",
        opensTomorrow: "Bağlıyıq · sabah {0}-da açılırıq",
        opensNext: "Bağlıyıq · yaxın gün {0}-da açılırıq",
        closed: "Bağlıyıq"
    };
    let i18n = {};
    try { i18n = JSON.parse(hoursEl?.dataset.i18n || "{}"); } catch (e) { /* standart: az */ }

    /** "10:00 - 20:00" → { open: 600, close: 1200 } (dəqiqə ilə); tanınmırsa null */
    function parseRange(text) {
        const m = (text || "").match(/(\d{1,2})[:.](\d{2})\s*[-–—]\s*(\d{1,2})[:.](\d{2})/);
        if (!m) return null;
        const open = +m[1] * 60 + +m[2];
        const close = +m[3] * 60 + +m[4];
        return close > open ? { open, close } : null;
    }

    const fmt = (min) => String(Math.floor(min / 60)).padStart(2, "0") + ":" + String(min % 60).padStart(2, "0");

    /** Bakı vaxtı: həftənin günü (0=Bazar ... 6=Şənbə) və gün ərzindəki dəqiqə */
    function bakuNow() {
        const parts = new Intl.DateTimeFormat("en-GB", {
            timeZone: "Asia/Baku", weekday: "short", hour: "2-digit", minute: "2-digit", hourCycle: "h23"
        }).formatToParts(new Date());
        const get = (t) => parts.find((p) => p.type === t)?.value;
        const day = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].indexOf(get("weekday"));
        return { day, minutes: +get("hour") * 60 + +get("minute") };
    }

    function updateStatus() {
        if (!hoursEl || !statusEl) return;
        const weekdays = parseRange(hoursEl.dataset.weekdays);
        const weekend = parseRange(hoursEl.dataset.weekend);
        if (!weekdays && !weekend) { statusEl.hidden = true; return; }

        const rangeFor = (day) => (day === 0 || day === 6) ? weekend : weekdays;
        const now = bakuNow();
        const today = rangeFor(now.day);
        let text, open = false;

        // Seçilmiş dildə mətnlər (Header: data-i18n), "{0}" — vaxt / dəqiqə
        const t = (key, arg) => (i18n[key] || I18N_AZ[key]).replace("{0}", arg);

        if (today && now.minutes >= today.open && now.minutes < today.close) {
            open = true;
            const left = today.close - now.minutes;
            text = left <= 60 ? t("openSoon", left) : t("openUntil", fmt(today.close));
        } else if (today && now.minutes < today.open) {
            text = t("opensToday", fmt(today.open));
        } else {
            // Növbəti iş günü
            let next = null, offset = 1;
            for (; offset <= 7; offset++) {
                next = rangeFor((now.day + offset) % 7);
                if (next) break;
            }
            text = next ? t(offset === 1 ? "opensTomorrow" : "opensNext", fmt(next.open)) : t("closed", "");
        }

        statusEl.classList.toggle("is-open", open);
        statusEl.classList.toggle("is-closed", !open);
        statusEl.querySelector(".de-status__text").textContent = text;
        statusEl.hidden = false;

        // Footer: bu günün sətrini vurğula
        const group = (now.day === 0 || now.day === 6) ? "weekend" : "weekdays";
        document.querySelectorAll("[data-de-days]").forEach((row) =>
            row.classList.toggle("is-today", row.dataset.deDays === group));
    }

    updateStatus();
    setInterval(updateStatus, 60 * 1000);

    // Header sürüşdürüləndə zolaq gizlənsin
    const header = document.querySelector(".s-header");
    const progress = document.createElement("div");
    progress.className = "de-read-progress";
    progress.setAttribute("aria-hidden", "true");
    document.body.appendChild(progress);

    // =====================================================
    // 2. OXUNMA GÖSTƏRİCİSİ + HEADER VƏZİYYƏTİ
    // =====================================================

    let ticking = false;
    function onScroll() {
        ticking = false;
        const max = document.documentElement.scrollHeight - window.innerHeight;
        progress.style.setProperty("--de-read", max > 0 ? Math.min(1, window.scrollY / max).toFixed(4) : 0);
        if (header) header.classList.toggle("is-scrolled", window.scrollY > 24);
    }
    window.addEventListener("scroll", () => { if (!ticking) { ticking = true; requestAnimationFrame(onScroll); } }, { passive: true });
    window.addEventListener("resize", onScroll, { passive: true });
    onScroll();

    if (reduceMotion) return;   // aşağıdakılar yalnız hərəkət effektləridir

    // =====================================================
    // 3. SÜRÜŞDÜRDÜKCƏ GÖRÜNMƏ (ardıcıl)
    // =====================================================

    const revealSelectors = [
        ".de-feature", ".de-service", ".de-contact-card", ".team-item", ".de-form-card",
        ".de-call", ".ai-card", ".de-checklist li", ".footer .row > div", ".de-empty",
        ".booking .bg-primary", ".de-perks li"
    ].join(",");

    const revealObserver = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting) return;
            const el = entry.target;
            el.classList.add("is-in");
            revealObserver.unobserve(el);
            // Animasiya bitəndə köməkçi sinifləri sil — kartın öz hover/keçid effektləri qayıtsın
            const delay = parseFloat(getComputedStyle(el).getPropertyValue("--de-delay")) || 0;
            setTimeout(() => {
                el.classList.remove("de-reveal", "is-in");
                el.style.removeProperty("--de-delay");
            }, 750 + delay * 1000);
        });
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.08 });

    document.querySelectorAll(revealSelectors).forEach((el) => {
        // wow.js ilə artıq animasiya olunan bölmələrə toxunma (ikiqat effekt olmasın)
        if (el.closest(".wow") || el.closest(".de-hero")) return;
        // Ekranda artıq görünən elementlər dərhal göstərilsin (yanıb-sönmə olmasın)
        if (el.getBoundingClientRect().top < window.innerHeight * 0.9) return;

        const siblings = Array.from(el.parentElement ? el.parentElement.children : []).filter((s) => s.matches(revealSelectors));
        const index = Math.max(0, siblings.indexOf(el));
        el.style.setProperty("--de-delay", Math.min(index * 0.08, 0.4) + "s");
        el.classList.add("de-reveal");
        revealObserver.observe(el);
    });

    // =====================================================
    // 4. KARTLARDA SİÇANI İZLƏYƏN İŞIQ (yalnız siçanla)
    // =====================================================

    if (finePointer) {
        document.querySelectorAll(".de-feature, .de-contact-card, .team-item, .de-service, .de-form-card, .ai-card, .de-call").forEach((card) => {
            card.classList.add("de-spot");
            if (card.matches(".de-call, .ai-card--accent")) card.classList.add("is-dark");
            card.addEventListener("pointermove", (e) => {
                const r = card.getBoundingClientRect();
                card.style.setProperty("--mx", (e.clientX - r.left) + "px");
                card.style.setProperty("--my", (e.clientY - r.top) + "px");
            });
        });
    }

    // =====================================================
    // 5. DÜYMƏLƏRDƏ KLİK DALĞASI
    // =====================================================

    document.addEventListener("pointerdown", (e) => {
        const btn = e.target.closest(".btn, .de-login-btn, .de-call");
        if (!btn || btn.disabled || btn.closest(".dc-chat, .ai")) return;
        const r = btn.getBoundingClientRect();
        const dot = document.createElement("span");
        dot.className = "de-ripple";
        dot.style.left = (e.clientX - r.left) + "px";
        dot.style.top = (e.clientY - r.top) + "px";
        btn.appendChild(dot);
        dot.addEventListener("animationend", () => dot.remove(), { once: true });
    });

    // =====================================================
    // 6. SLAYDERDƏ PARALLAKS (maşın şəkli siçanla yüngülcə hərəkət edir)
    // =====================================================

    const hero = document.querySelector(".de-hero");
    if (hero && finePointer) {
        let raf = 0;
        hero.addEventListener("pointermove", (e) => {
            if (raf) return;
            raf = requestAnimationFrame(() => {
                raf = 0;
                const r = hero.getBoundingClientRect();
                const x = (e.clientX - r.left) / r.width - 0.5;
                const y = (e.clientY - r.top) / r.height - 0.5;
                hero.style.setProperty("--px", (x * -22).toFixed(1) + "px");
                hero.style.setProperty("--py", (y * -14).toFixed(1) + "px");
            });
        });
        hero.addEventListener("pointerleave", () => {
            hero.style.setProperty("--px", "0px");
            hero.style.setProperty("--py", "0px");
        });
    }

    // =====================================================
    // 7. ŞƏKİLLƏR YUMŞAQ GÖRÜNSÜN (hələ yüklənməyənlər)
    // =====================================================

    document.querySelectorAll("main img, .team-item img, .de-feature img, .de-marquee img").forEach((img) => {
        if (img.complete) return;   // artıq yüklənib — toxunma
        img.classList.add("de-fade");
        const done = () => img.classList.add("is-loaded");
        img.addEventListener("load", done, { once: true });
        img.addEventListener("error", done, { once: true });
    });
})();

// =====================================================
// 8. AÇILIŞ ANİMASİYASI — ekran bitəndə silinir, kliklə keçmək olur
// =====================================================
(function () {
    "use strict";

    const root = document.documentElement;
    const splash = document.querySelector(".de-splash");
    if (!splash) return;
    if (!root.classList.contains("de-intro")) { splash.remove(); return; }

    let finished = false;
    const finish = () => {
        if (finished) return;
        finished = true;
        splash.remove();
        // Gecikmələr yalnız ilk açılış üçündür — slayder sonrakı slaydlarda normal sürətlə işləsin
        setTimeout(() => root.classList.remove("de-intro"), 1800);
    };

    splash.addEventListener("animationend", (e) => {
        if (e.animationName === "de-splash-out" || e.animationName === "de-splash-skip") finish();
    });

    splash.addEventListener("click", () => {
        if (splash.classList.contains("is-skip")) return;
        splash.classList.add("is-skip");
    });

    // Ehtiyat: hər hansı səbəbdən animasiya bitməsə
    setTimeout(finish, 3500);
})();
