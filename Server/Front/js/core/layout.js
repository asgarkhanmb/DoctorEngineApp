/* =====================================================
   DoctorEngine — səhifə çərçivəsi (header, footer, çat düyməsi) və açılış ardıcıllığı

   Hər səhifə:
     <div id="de-header"></div>  <main>...</main>  <div id="de-footer"></div>
     <script> DE.page({ nav: "Home", render: async (home) => { ... } }) </script>

   DE.page:
     1) giriş tələb olunursa yoxlayır (auth: "user" | "admin")
     2) tərcümələri və saytın ümumi məlumatını (api/v1/content/home) gözləyir
     3) header/footer + səhifənin öz məzmununu çəkir
     4) şablonun skriptlərini (jQuery, Bootstrap, animasiyalar, ...) ardıcıl yükləyir
   ===================================================== */
(function () {
    "use strict";
    const DE = window.DE;

    const esc = DE.esc = (s) => String(s == null ? "" : s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c]));
    const t = (s, ...a) => esc(DE.t(s, ...a));
    DE.qs = (name) => new URLSearchParams(location.search).get(name);

    // ---------- Skript yükləyici ----------
    const loaded = new Set();
    DE.loadScript = (src) => new Promise((resolve) => {
        if (loaded.has(src)) return resolve();
        loaded.add(src);
        const s = document.createElement("script");
        s.src = src;
        s.async = false;
        s.onload = () => resolve();
        s.onerror = () => { console.warn("Skript yüklənmədi:", src); resolve(); };
        document.body.appendChild(s);
    });
    DE.loadScripts = async (list) => { for (const src of list) await DE.loadScript(src); };

    // ---------- Bildiriş (səhifə keçidindən sonra da) ----------
    DE.flash = (message, type) => {
        try {
            const list = JSON.parse(sessionStorage.getItem("de_flash") || "[]");
            list.push({ message, type: type || "success" });
            sessionStorage.setItem("de_flash", JSON.stringify(list));
        } catch (e) { /* */ }
    };
    DE.toast = (message, type) => {
        if (window.deUI) window.deUI.toast(message, type || "info");
        else DE.flash(message, type);
    };
    function showFlash() {
        try {
            const list = JSON.parse(sessionStorage.getItem("de_flash") || "[]");
            sessionStorage.removeItem("de_flash");
            list.forEach((f) => window.deUI && window.deUI.toast(f.message, f.type));
        } catch (e) { /* */ }
    }

    // ---------- Saytın ümumi məlumatı ----------
    DE.home = () => DE.api.cached("/api/v1/content/home", 60);

    function brandIcon(site) {
        return site && site.logoUrl
            ? `<img src="${esc(DE.upload(site.logoUrl))}" alt="" class="de-brand-logo" />`
            : `<i class="${esc((site && site.logoIcon) || "fa-solid fa-car")}" aria-hidden="true"></i>`;
    }
    DE.brandIcon = brandIcon;

    function brandName(name) {
        const i = name.indexOf("Engine");
        return i >= 0
            ? `<span>${esc(name.slice(0, i))}<span class="de-brand-engine">Engine</span>${esc(name.slice(i + 6))}</span>`
            : esc(name);
    }

    // ---------- HEADER ----------
    function header(home, nav) {
        const site = home.site || { name: "DoctorEngine.az" };
        const contact = home.contact || {};
        const phone = contact.phone;
        const lang = DE.lang;
        const user = DE.auth.user();
        const statusI18n = JSON.stringify({
            openSoon: DE.t("status.openSoon"), openUntil: DE.t("status.openUntil"), opensToday: DE.t("status.opensToday"),
            opensTomorrow: DE.t("status.opensTomorrow"), opensNext: DE.t("status.opensNext"), closed: DE.t("status.closed")
        });
        const active = (...names) => (names.includes(nav) ? ` active" aria-current="page` : "");
        const langName = (DE.i18n.languages.find((l) => l[0] === lang) || ["az", "Azərbaycanca"])[1];

        let userHtml;
        if (user) {
            const initial = (user.userName || "?").slice(0, 1);
            userHtml = `
                <div class="nav-item dropdown">
                    <a class="nav-link dropdown-toggle de-user-toggle" href="#" id="userDropdown" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                        ${user.avatarUrl ? `<img class="de-avatar" src="${esc(DE.upload(user.avatarUrl))}" alt="" aria-hidden="true" style="object-fit:cover" />` : `<span class="de-avatar" aria-hidden="true">${esc(initial)}</span>`}
                        <span class="de-user-name">${esc(user.userName)}</span>
                    </a>
                    <ul class="dropdown-menu dropdown-menu-end" aria-labelledby="userDropdown">
                        <li class="de-dropdown-head"><strong>${esc(user.userName)}</strong><small>${DE.auth.isAdmin() ? t("user.admin") : t("user.user")}</small></li>
                        <li><hr class="dropdown-divider" /></li>
                        <li><a class="dropdown-item" href="/Account/Profile"><i class="fa-regular fa-id-card"></i> ${t("user.profile")}</a></li>
                        <li><a class="dropdown-item" href="/Chat"><i class="fa-regular fa-comments"></i> ${t("user.chat")}</a></li>
                        <li><a class="dropdown-item" href="/Reservation"><i class="fa-regular fa-calendar-check"></i> ${t("user.book")}</a></li>
                        ${DE.auth.isAdmin() ? `<li><a class="dropdown-item" href="/Admin"><i class="fa-solid fa-gauge-high"></i> ${t("user.adminPanel")}</a></li>` : ""}
                        <li><hr class="dropdown-divider" /></li>
                        <li><button type="button" class="dropdown-item is-danger" data-de-logout><i class="fa-solid fa-arrow-right-from-bracket"></i> ${t("user.logout")}</button></li>
                    </ul>
                </div>`;
        } else {
            const loginUrl = "/Account/Login?returnUrl=" + encodeURIComponent(location.pathname + location.search);
            userHtml = `
                <a href="${esc(loginUrl)}" class="de-login-btn" aria-label="${t("user.login")}" title="${t("user.login")}">
                    <svg class="de-login-btn__icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                    <defs><mask id="deLoginCut"><rect width="24" height="24" fill="#fff"/><path d="M10.8 16.8H18M16.3 11.6h2.9a2 2 0 0 1 2 2v6.4a2 2 0 0 1-2 2h-2.9" fill="none" stroke="#000" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/></mask></defs>
                    <g mask="url(#deLoginCut)" fill="currentColor"><circle cx="9.8" cy="6.4" r="4.7"/><path d="M1.6 20.9v-2.6c0-3 3.6-5 8.2-5 2.2 0 4.2.45 5.6 1.2V22H2.7a1.1 1.1 0 0 1-1.1-1.1z"/></g>
                    <path d="M10.8 16.8H18M15.8 14.4l2.4 2.4-2.4 2.4M16.3 11.6h2.9a2 2 0 0 1 2 2v6.4a2 2 0 0 1-2 2h-2.9" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>
                    </svg>
                    <span class="de-login-btn__text">${t("user.login")}</span>
                </a>`;
        }

        const wa = home.whatsApp;
        return `
    <div class="de-topbar" data-de-hours data-weekdays="${esc(contact.weekdays || "")}" data-weekend="${esc(contact.weekend || "")}" data-i18n="${esc(statusI18n)}">
        <div class="de-topbar__inner">
            <span class="de-status" data-de-status hidden>
                <span class="de-status__dot" aria-hidden="true"></span>
                <span class="de-status__text" aria-live="polite"></span>
            </span>
            ${contact.weekdays ? `<span class="de-topbar__item d-none d-lg-inline-flex"><i class="fa-regular fa-clock" aria-hidden="true"></i> ${t("topbar.weekdays")}: ${esc(contact.weekdays)} ${contact.weekend ? `<span>· ${t("topbar.weekend")}: ${esc(contact.weekend)}</span>` : ""}</span>` : ""}
            <span class="de-topbar__right">
                <a href="/Assistant" class="de-topbar__item d-none d-lg-inline-flex"><i class="fa-solid fa-wand-magic-sparkles" aria-hidden="true"></i> ${t("topbar.askAi")}</a>
                <span class="dropdown de-lang">
                    <button type="button" class="de-topbar__item de-lang__btn" data-bs-toggle="dropdown" aria-expanded="false" aria-label="${t("lang.label")}: ${esc(langName)}">
                        <i class="fa-solid fa-globe" aria-hidden="true"></i>
                        <span>${lang.toUpperCase()}</span>
                        <i class="fa-solid fa-chevron-down de-lang__caret" aria-hidden="true"></i>
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end de-lang__menu" data-no-i18n>
                        ${DE.i18n.languages.map(([code, name]) => `
                        <li><a class="dropdown-item ${code === lang ? "active" : ""}" lang="${code}" hreflang="${code}" ${code === lang ? `aria-current="true"` : ""} href="#" data-de-lang="${code}">
                            <span class="de-lang__code">${code.toUpperCase()}</span> ${esc(name)}
                        </a></li>`).join("")}
                    </ul>
                </span>
                ${phone ? `<a href="tel:${esc(phone.replace(/ /g, ""))}" class="de-topbar__item"><i class="fa-solid fa-phone" aria-hidden="true"></i> ${esc(phone)}</a>` : ""}
            </span>
        </div>
    </div>

<header class="s-header">
    <nav class="navbar navbar-expand-lg bg-white navbar-light shadow sticky-top p-0" aria-label="${t("nav.main")}">
        <a href="/" class="navbar-brand d-flex align-items-center px-4 px-lg-5">
            <h2 class="m-0 text-primary">${brandIcon(site)}${brandName(site.name || "")}</h2>
        </a>
        <button type="button" class="navbar-toggler me-4" data-bs-toggle="collapse" data-bs-target="#navbarCollapse"
                aria-controls="navbarCollapse" aria-expanded="false" aria-label="${t("nav.toggle")}">
            <span class="navbar-toggler-icon"></span>
        </button>
        <div class="collapse navbar-collapse" id="navbarCollapse">
            <div class="navbar-nav ms-auto align-items-lg-center p-4 p-lg-0 pe-lg-5">
                <a href="/" class="nav-item nav-link${active("Home")}">${t("nav.home")}</a>
                <a href="/About" class="nav-item nav-link${active("About")}">${t("nav.about")}</a>
                <a href="/Service" class="nav-item nav-link${active("Service")}">${t("nav.services")}</a>
                <a href="/Reservation" class="nav-item nav-link${active("Reservation")}">${t("nav.reservation")}</a>
                <a href="/Contact" class="nav-item nav-link${active("Contact", "Chat")}">${t("nav.contact")}</a>
                <a href="/Assistant" class="nav-item nav-link de-nav-ai${active("Assistant")}"><i class="fa-solid fa-wand-magic-sparkles" aria-hidden="true"></i> ${t("nav.ai")}</a>
                <div class="de-user">${userHtml}</div>
            </div>
        </div>
    </nav>
</header>
${wa ? `<a href="${esc(wa.link)}" class="whatsapp-btn" target="_blank" rel="noopener" aria-label="${t("wa.aria")}"><i class="bi bi-whatsapp"></i></a>` : ""}`;
    }

    // ---------- FOOTER ----------
    function footer(home) {
        const site = home.site || { name: "DoctorEngine.az" };
        const c = home.contact || {};
        const whatsappLink = c.whatsApp ? "https://wa.me/" + c.whatsApp.replace(/[+ ]/g, "") + "?text=Salam" : null;
        const cats = (home.categories || []).slice().sort((a, b) => b.id - a.id);
        const latest = cats.slice(0, 5);
        return `
<footer>
    <div class="container-fluid text-light footer pt-5">
        <div class="container py-5">
            <div class="row g-5">
                <div class="col-xl-3 col-md-6">
                    <a href="/" class="de-footer-brand"><span class="de-footer-brand__icon">${brandIcon(site)}</span> ${esc(site.name)}</a>
                    <p class="mb-0">${t("footer.about")}</p>
                    <div class="de-social">
                        ${c.youTube ? `<a href="${esc(c.youTube)}" target="_blank" rel="noopener" class="yt" aria-label="YouTube"><i class="fab fa-youtube"></i></a>` : ""}
                        ${c.facebook ? `<a href="${esc(c.facebook)}" target="_blank" rel="noopener" class="fb" aria-label="Facebook"><i class="fab fa-facebook-f"></i></a>` : ""}
                        ${c.tikTok ? `<a href="${esc(c.tikTok)}" target="_blank" rel="noopener" class="tt" aria-label="TikTok"><i class="fab fa-tiktok"></i></a>` : ""}
                        ${whatsappLink ? `<a href="${esc(whatsappLink)}" target="_blank" rel="noopener" class="wa" aria-label="WhatsApp"><i class="fab fa-whatsapp"></i></a>` : ""}
                    </div>
                </div>
                <div class="col-xl-3 col-md-6">
                    <h4 class="text-light mb-4">${t("nav.contact")}</h4>
                    ${c.address ? `<p class="mb-3 d-flex"><i class="fa fa-map-marker-alt me-3 mt-1"></i><span>${esc(c.address)}</span></p>` : ""}
                    ${c.phone ? `<p class="mb-3 d-flex"><i class="fa fa-phone-alt me-3 mt-1"></i><a class="text-reset" href="tel:${esc(c.phone.replace(/ /g, ""))}">${esc(c.phone)}</a></p>` : ""}
                    ${c.email ? `<p class="mb-3 d-flex"><i class="fa fa-envelope me-3 mt-1"></i><a class="text-reset text-break" href="mailto:${esc(c.email)}">${esc(c.email)}</a></p>` : ""}
                </div>
                <div class="col-xl-3 col-md-6">
                    <h4 class="text-light mb-4">${t("footer.hours")}</h4>
                    <div class="de-hours">
                        <div data-de-days="weekdays"><span>${t("topbar.weekdays")}</span><strong>${esc(c.weekdays || "")}</strong></div>
                        <div data-de-days="weekend"><span>${t("topbar.weekend")}</span><strong>${esc(c.weekend || "")}</strong></div>
                    </div>
                </div>
                <div class="col-xl-3 col-md-6">
                    <h4 class="text-light mb-4">${t("nav.services")}</h4>
                    ${latest.length ? `<ul class="de-footer-services">${latest.map((s) => `<li><a class="btn btn-link" href="/Service#cat-${s.id}" title="${esc(s.name)}">${esc(s.name)}</a></li>`).join("")}</ul>
                        ${cats.length > latest.length ? `<a class="de-footer-more" href="/Service">${t("footer.allServices")} <i class="fa-solid fa-arrow-right"></i></a>` : ""}`
                : `<p class="mb-0">${t("footer.noServices")}</p>`}
                </div>
            </div>
        </div>
        <div class="container">
            <div class="copyright">
                <span>© ${new Date().getFullYear()} ${esc(site.name)} — ${t("footer.rights")}</span>
                <span>Developed by <a href="https://www.linkedin.com/in/asgarkhanb/" target="_blank" rel="noopener noreferrer" class="de-dev-link">Asgarkhan Bayramov</a></span>
            </div>
        </div>
    </div>
</footer>`;
    }

    // ---------- Yuxarı qayıt + çat düyməsi/paneli ----------
    function chrome(inlineChat) {
        return `
    <button type="button" class="de-totop" aria-label="${t("layout.toTop")}" title="${t("layout.toTop")}">
        <svg class="de-totop__ring" viewBox="0 0 48 48" aria-hidden="true" focusable="false">
            <circle class="track" cx="24" cy="24" r="22" /><circle class="bar" cx="24" cy="24" r="22" pathLength="100" />
        </svg>
        <i class="fa-solid fa-arrow-up" aria-hidden="true"></i>
    </button>
    <button type="button" class="dc-launcher" title="${t("layout.chatTitle")}" aria-label="${t("layout.chatOpen")}" aria-expanded="false" ${inlineChat ? "" : `aria-controls="dcDrawer"`}>
        <i class="fa-solid fa-comment-dots dc-launcher__icon" aria-hidden="true"></i>
        <i class="fa-solid fa-xmark dc-launcher__icon dc-launcher__icon--close" aria-hidden="true"></i>
        <span class="dc-launcher__badge" hidden></span>
    </button>
    ${inlineChat ? "" : `<div class="dc-drawer" id="dcDrawer" aria-hidden="true">${DE.chatWidget.html({ drawer: true })}</div>`}`;
    }

    function splash(home) {
        const site = home.site || { name: "DoctorEngine.az" };
        const el = document.querySelector(".de-splash");
        if (el) el.innerHTML = `<div class="de-splash__inner"><span class="de-splash__logo">${brandIcon(site)}</span><span class="de-splash__name">${esc(site.name)}</span><span class="de-splash__bar"><span></span></span></div>`;
    }

    // ---------- Ümumi hadisələr ----------
    document.addEventListener("click", async (e) => {
        const langLink = e.target.closest("[data-de-lang]");
        if (langLink) {
            e.preventDefault();
            DE.i18n.setLang(langLink.getAttribute("data-de-lang"));
            return;
        }
        if (e.target.closest("[data-de-logout]")) {
            e.preventDefault();
            await DE.auth.signOut();
            location.href = "/";
        }
    });
    window.addEventListener("de:logout", () => { if (document.body.dataset.deAuth) location.reload(); });

    // Şablonun skriptləri (köhnə saytın _Layout.cshtml-i ilə eyni ardıcıllıq)
    const VENDOR = [
        "/lib/jquery/dist/jquery.min.js",
        "/lib/bootstrap/dist/js/bootstrap.bundle.min.js",
        "/js/site.js",
        "/js/no-autocomplete.js",
        "/assets/lib/wow/wow.min.js",
        "/assets/lib/easing/easing.min.js",
        "/assets/lib/waypoints/waypoints.min.js",
        "/assets/lib/counterup/counterup.min.js",
        "/assets/lib/owlcarousel/owl.carousel.min.js",
        "/assets/lib/tempusdominus/js/moment.min.js",
        "/assets/lib/tempusdominus/js/moment-timezone.min.js",
        "/assets/lib/tempusdominus/js/tempusdominus-bootstrap-4.min.js",
        "/assets/js/main.js",
        "/js/theme.js",
        "/js/chat-launcher.js",
        "/js/de-ui.js",
        "/js/video-frame.js",
        "/js/motion.js"
    ];

    /**
     * Səhifəni qur.
     *  opts.title      — başlıq (azərbaycanca; tərcümə olunur)
     *  opts.nav        — aktiv menyu: Home | About | Service | Reservation | Contact | Assistant
     *  opts.auth       — "user" | "admin" (giriş tələb olunur)
     *  opts.inlineChat — səhifədə çat varsa (Əlaqə, Çat) — panel yaradılmır
     *  opts.layout     — false: header/footer yoxdur (giriş səhifələri, zəng)
     *  opts.vendor     — false: şablon skriptləri yüklənmir
     *  opts.render     — async (home) => { ... }  səhifənin məzmunu
     *  opts.scripts    — əlavə skriptlər (render-dən sonra)
     *  opts.after      — skriptlər yükləndikdən sonra
     */
    DE.page = async function (opts) {
        opts = opts || {};
        if (opts.auth) {
            document.body.dataset.deAuth = "1";
            if (!DE.auth.require(opts.auth === "admin")) return;
            await DE.auth.token();   // token vaxtı keçibsə — əvvəlcə yenilə
        }

        let home = {};
        try {
            [, home] = await Promise.all([DE.i18n.ready, opts.layout === false && !opts.needHome ? Promise.resolve({}) : DE.home()]);
        } catch (e) {
            await DE.i18n.ready;
            showApiDown(e);
            return;
        }
        home = home || {};
        DE.site = home.site || { name: "DoctorEngine.az" };

        if (opts.title) document.title = DE.t(opts.title) + " — " + DE.site.name;
        if (opts.layout !== false) {
            splash(home);
            const h = document.getElementById("de-header");
            if (h) h.outerHTML = header(home, opts.nav);
            const f = document.getElementById("de-footer");
            if (f) f.outerHTML = footer(home) + chrome(!!opts.inlineChat);
        }

        try {
            if (opts.render) await opts.render(home);
        } catch (e) {
            console.error(e);
            if (e instanceof DE.ApiError && e.status === 404) { location.replace("/Error/404"); return; }
            DE.flash(e.message || DE.t("Xəta baş verdi. Yenidən cəhd edin."), "error");
        }

        DE.i18n.translate(document.body);
        if (opts.vendor !== false) await DE.loadScripts(VENDOR);
        else await DE.loadScripts(["/lib/jquery/dist/jquery.min.js", "/lib/bootstrap/dist/js/bootstrap.bundle.min.js", "/js/de-ui.js"]);
        if (opts.scripts) await DE.loadScripts(opts.scripts);

        if (opts.layout !== false && !opts.inlineChat) DE.chatWidget.start({ drawer: true });
        if (opts.after) await opts.after(home);
        showFlash();
        document.documentElement.classList.add("de-ready");
    };

    /** API cavab vermir — səhifə əvəzinə aydın mesaj */
    function showApiDown(e) {
        console.error(e);
        document.body.innerHTML = `
        <main style="min-height:100vh;display:grid;place-items:center;font-family:system-ui,Segoe UI,Arial,sans-serif;background:#f6f7f9">
          <div style="max-width:520px;margin:16px;padding:32px;background:#fff;border-radius:14px;box-shadow:0 6px 24px rgba(0,0,0,.08);text-align:center">
            <h1 style="font-size:22px;margin:0 0 10px">${t("Sayt müvəqqəti əlçatan deyil")}</h1>
            <p style="color:#4b5563">${t("Server cavab vermir. Bir azdan yenidən cəhd edin.")}</p>
            ${location.hostname === "localhost" ? `<p style="font-size:14px;background:#fff7ed;border:1px solid #fed7aa;padding:10px;border-radius:8px;color:#9a3412;text-align:left">
              Development: API <code>${esc(DE.apiUrl)}</code> cavab vermir. Əvvəlcə <b>DoctorEngine.Api</b> layihəsini işə salın.</p>` : ""}
            <a href="" style="display:inline-block;margin-top:12px;padding:10px 18px;border-radius:8px;background:#c62828;color:#fff;text-decoration:none">${t("Yenidən cəhd et")}</a>
          </div>
        </main>`;
    }
})();
