/* =====================================================
   DoctorEngine — admin paneli (köhnə Areas/Admin/_Layout.cshtml ilə eyni görünüş)

   DE.admin.page({ title, active, render: async () => "<html>", after })
   - yalnız Admin rolu (token) — yoxdursa giriş səhifəsinə
   - sol menyu, yuxarı zolaq, bildirişlər, təsdiq pəncərəsi
   - admin.js (cədvəl axtarışı/səhifələmə, təsdiq pəncərəsi), admin-live.js (canlı rezervasiyalar)

   Formalar: köhnə MVC ünvanları ilə yazılır (məs. <form action="/Admin/Reservation/Confirm/5">) —
   aşağıdakı ROUTES onları API sorğusuna çevirir. Uğurdan sonra səhifə yenilənir (köhnə saytdakı kimi).
   Admin paneli həmişə azərbaycancadır.
   ===================================================== */
(function () {
    "use strict";
    const DE = window.DE;
    const esc = DE.esc;

    // ---------- Köhnə MVC əməliyyatları → API ----------
    const ROUTES = [
        [/^\/Admin\/Reservation\/Confirm\/(\d+)$/i, (m) => DE.api.post(`/api/v1/admin/reservations/${m[1]}/confirm`), "Rezervasiya təsdiqləndi."],
        [/^\/Admin\/Reservation\/Delete\/(\d+)$/i, (m) => DE.api.del(`/api/v1/admin/reservations/${m[1]}`), "Rezervasiya silindi."],
        [/^\/Admin\/ContactMessage\/Delete\/(\d+)$/i, (m) => DE.api.del(`/api/v1/admin/messages/${m[1]}`), "Mesaj silindi."],
        [/^\/Admin\/Section\/(\w+)\/Delete\/(\d+)$/i, (m) => DE.api.del(`/api/v1/admin/sections/${m[1]}/${m[2]}`), "Silindi."],
        [/^\/Admin\/SubService\/Move\/(\d+)$/i, (m, form) => DE.api.post(`/api/v1/subservices/${m[1]}/move?categoryId=${encodeURIComponent(form.elements.categoryId.value)}`), null],
        [/^\/Admin\/User\/RemoveRole$/i, (m, form) => DE.api.del(`/api/v1/admin/users/${encodeURIComponent(form.elements.userId.value)}/roles/${encodeURIComponent(form.elements.role.value)}`), "Rol silindi."]
    ];

    document.addEventListener("submit", async (e) => {
        const form = e.target;
        if (!(form instanceof HTMLFormElement) || form.hasAttribute("data-js")) return;
        const path = new URL(form.getAttribute("action") || "", location.origin).pathname;
        const route = ROUTES.find((r) => r[0].test(path));
        if (!route) return;
        e.preventDefault();
        if (form.dataset.busy) return;
        form.dataset.busy = "1";
        const btn = form.querySelector("button[type=submit]");
        if (btn) btn.disabled = true;
        try {
            const res = await route[1](path.match(route[0]), form);
            DE.flash((res && res.message) || route[2] || "Yadda saxlanıldı.", "success");
            const next = sessionStorage.getItem("de_after_delete");   // məs. detaldan silindi → siyahıya
            sessionStorage.removeItem("de_after_delete");
            if (next) location.href = next; else location.reload();
        } catch (err) {
            delete form.dataset.busy;
            if (btn) btn.disabled = false;
            DE.admin.toast(err.message || "Xəta baş verdi.", "error");
        }
    });

    // ---------- Bildirişlər (köhnə .adm-toast) ----------
    function toastHtml(message, type) {
        const ok = type !== "error";
        return `<div class="adm-toast ${ok ? "is-success" : "is-error"}" role="${ok ? "status" : "alert"}"><i class="fa-solid ${ok ? "fa-circle-check" : "fa-circle-exclamation"}"></i><span>${esc(message)}</span><button type="button" aria-label="Bağla" data-toast-close>&times;</button></div>`;
    }
    function toast(message, type) {
        const box = document.querySelector(".adm-toasts");
        if (!box) return;
        box.insertAdjacentHTML("beforeend", toastHtml(message, type));
        const el = box.lastElementChild;
        setTimeout(() => el && el.remove(), 5000);
    }
    document.addEventListener("click", (e) => {
        const close = e.target.closest("[data-toast-close]");
        if (close) close.closest(".adm-toast").remove();
    });

    function nav(active, badges) {
        const link = (href, key, icon, text, extra) =>
            `<a href="${href}" class="adm-nav__link ${active === key ? "is-active" : ""}" ${extra || ""}><i class="${icon}"></i> ${text}</a>`;
        const pending = badges ? badges.pendingReservations : 0;
        const newMsgs = badges ? badges.newMessages : 0;
        return `
        <nav class="adm-nav">
            <div class="adm-nav__label">Ümumi</div>
            ${link("/Admin", "Dashboard", "fa-solid fa-gauge-high", "İdarə paneli")}
            <a href="/Admin/Reservation" class="adm-nav__link ${active === "Reservation" ? "is-active" : ""}" data-res-link>
                <i class="fa-regular fa-calendar-check"></i> Rezervasiyalar
                <span class="adm-nav__badge" title="Gözləyən rezervasiyalar" data-res-badge ${pending > 0 ? "" : "hidden"}>${pending}</span>
            </a>
            <a href="/Admin/ContactMessage" class="adm-nav__link ${active === "ContactMessage" ? "is-active" : ""}">
                <i class="fa-regular fa-envelope"></i> Əlaqə mesajları
                ${newMsgs > 0 ? `<span class="adm-nav__badge is-muted" title="Son 7 gündə">${newMsgs}</span>` : ""}
            </a>
            <a href="/Chat" class="adm-nav__link" target="_blank" rel="noopener"><i class="fa-regular fa-comments"></i> Canlı çat <i class="fa-solid fa-arrow-up-right-from-square adm-nav__ext"></i></a>
            ${link("/Admin/Warranty", "warranties", "fa-solid fa-shield-halved", "Zəmanətlər")}

            <div class="adm-nav__label">Sayt məzmunu</div>
            ${link("/Admin/Slider", "sliders", "fa-regular fa-images", "Slayder")}
            ${link("/Admin/Inform", "informs", "fa-regular fa-star", "Üstünlüklər")}
            ${link("/Admin/About", "about", "fa-regular fa-address-card", "Haqqımızda")}
            ${link("/Admin/Category", "categories", "fa-solid fa-layer-group", "Kateqoriyalar")}
            ${link("/Admin/SubService", "videos", "fa-solid fa-film", "Kateqoriya videoları")}
            ${link("/Admin/Service", "services", "fa-solid fa-screwdriver-wrench", "Rezervasiya xidmətləri")}
            ${link("/Admin/Technician", "technicians", "fa-solid fa-user-gear", "Ustalar")}

            <div class="adm-nav__label">Əlaqə ayarları</div>
            ${link("/Admin/Contact", "contact", "fa-solid fa-address-book", "Əlaqə məlumatları")}
            ${link("/Admin/ContactPhone", "phones", "fa-solid fa-phone", "Telefon bloku")}
            ${link("/Admin/Map", "map", "fa-solid fa-map-location-dot", "Xəritə")}
            ${link("/Admin/WhatsApp", "whatsapp", "fa-brands fa-whatsapp", "WhatsApp düyməsi")}

            <div class="adm-nav__label">Sistem</div>
            ${link("/Admin/SiteSetting", "site", "fa-solid fa-sliders", "Sayt ayarları")}
            ${link("/Admin/User", "User", "fa-solid fa-users", "İstifadəçilər")}
        </nav>`;
    }

    function shell(site, title, active, badges, content) {
        const user = DE.auth.user() || { userName: "Admin" };
        return `
    <aside class="adm-sidebar" id="admSidebar" aria-label="Admin menyusu">
        <a href="/Admin" class="adm-brand">
            <span class="adm-brand__icon">${DE.brandIcon(site)}</span>
            <span>${esc(site.name)}<small>İdarəetmə paneli</small></span>
        </a>
        ${nav(active, badges)}
        <a href="/" class="adm-sidebar__site" target="_blank" rel="noopener"><i class="fa-solid fa-globe"></i> Sayta bax</a>
    </aside>
    <div class="adm-backdrop" data-adm-close></div>
    <div class="adm-main">
        <header class="adm-topbar">
            <button type="button" class="adm-icon-btn adm-menu-btn" data-adm-toggle aria-label="Menyunu aç" aria-controls="admSidebar"><i class="fa-solid fa-bars"></i></button>
            <div class="adm-topbar__title">${esc(title)}</div>
            <div class="adm-topbar__right">
                <a href="/" target="_blank" rel="noopener" class="btn btn-light btn-sm d-none d-md-inline-flex align-items-center gap-2"><i class="fa-solid fa-arrow-up-right-from-square"></i> Sayt</a>
                <div class="dropdown">
                    <button class="adm-user" type="button" data-bs-toggle="dropdown" aria-expanded="false">
                        <span class="adm-user__avatar">${esc((user.userName || "A").slice(0, 1).toUpperCase())}</span>
                        <span class="adm-user__name d-none d-sm-inline">${esc(user.userName)}</span>
                        <i class="fa-solid fa-chevron-down small text-muted"></i>
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end">
                        <li class="px-3 py-2"><strong class="d-block">${esc(user.userName)}</strong><small class="text-muted">Administrator</small></li>
                        <li><hr class="dropdown-divider" /></li>
                        <li><a class="dropdown-item" href="/"><i class="fa-solid fa-house me-2"></i> Ana səhifə</a></li>
                        <li><button type="button" class="dropdown-item text-danger" data-de-logout><i class="fa-solid fa-arrow-right-from-bracket me-2"></i> Çıxış</button></li>
                    </ul>
                </div>
            </div>
        </header>
        <main class="adm-content">${content}</main>
    </div>
    <div class="adm-toasts" aria-live="polite"></div>
    <div class="modal fade" id="admConfirm" tabindex="-1" aria-hidden="true">
        <div class="modal-dialog modal-dialog-centered modal-sm">
            <div class="modal-content adm-confirm">
                <div class="adm-confirm__icon"><i class="fa-solid fa-triangle-exclamation"></i></div>
                <h5 class="adm-confirm__title">Əminsiniz?</h5>
                <p class="adm-confirm__text">Bu əməliyyat geri qaytarıla bilməz.</p>
                <div class="adm-confirm__actions">
                    <button type="button" class="btn btn-light" data-bs-dismiss="modal">Ləğv et</button>
                    <button type="button" class="btn btn-danger" data-confirm-ok>Bəli, sil</button>
                </div>
            </div>
        </div>
    </div>`;
    }

    /** Səhifə başlığı (köhnə _PageHead): title, subtitle, primary: { href, text, icon }, back: { href, text } */
    function pageHead(o) {
        return `
<div class="adm-page-head">
    <div>
        ${o.back ? `<a href="${esc(o.back.href)}" class="adm-back"><i class="fa-solid fa-arrow-left"></i> ${esc(o.back.text || "Geri")}</a>` : ""}
        <h1>${esc(o.title)}</h1>
        ${o.subtitle ? `<p>${esc(o.subtitle)}</p>` : ""}
    </div>
    ${o.primary ? `<div class="adm-page-head__actions"><a href="${esc(o.primary.href)}" class="btn btn-primary"><i class="${o.primary.icon || "fa-solid fa-plus"}"></i> ${esc(o.primary.text)}</a></div>` : ""}
    ${o.actionsHtml ? `<div class="adm-page-head__actions">${o.actionsHtml}</div>` : ""}
</div>`;
    }

    /** Sil düyməsi (köhnə _DeleteButton): admin.js təsdiq pəncərəsi → ROUTES → API */
    function deleteButton(action, confirm) {
        return `<form action="${esc(action)}" method="post"><button type="submit" class="adm-act is-danger" title="Sil" aria-label="Sil" data-confirm="${esc(confirm)}"><i class="fa-regular fa-trash-can"></i></button></form>`;
    }

    function statusBadge(s) {
        return s === "Pending" ? `<span class="adm-badge is-pending"><i class="fa-regular fa-hourglass-half"></i> Gözləyir</span>`
            : s === "Confirmed" ? `<span class="adm-badge is-confirmed"><i class="fa-solid fa-check"></i> Təsdiqləndi</span>`
                : s === "Rejected" ? `<span class="adm-badge is-rejected"><i class="fa-solid fa-xmark"></i> Rədd edildi</span>`
                    : `<span class="adm-badge is-neutral">${esc(s)}</span>`;
    }

    /** "2026-11-20T10:30:00" → Date (yerli vaxt kimi — API Bakı vaxtı ilə yazır) */
    function date(v) {
        const m = String(v || "").match(/^(\d{4})-(\d{2})-(\d{2})T?(\d{2})?:?(\d{2})?/);
        return m ? new Date(+m[1], +m[2] - 1, +m[3], +(m[4] || 0), +(m[5] || 0)) : new Date(v);
    }
    const pad = (n) => String(n).padStart(2, "0");
    const fmt = {
        date: (v) => { const d = date(v); return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()}`; },
        time: (v) => { const d = date(v); return `${pad(d.getHours())}:${pad(d.getMinutes())}`; },
        short: (v) => { const d = date(v); return `${pad(d.getDate())}.${pad(d.getMonth() + 1)} ${pad(d.getHours())}:${pad(d.getMinutes())}`; }
    };

    async function page(opts) {
        document.documentElement.lang = "az";
        if (!DE.auth.require(true)) return;
        await DE.auth.token();

        let site = { name: "DoctorEngine.az" }, badges = null, content = "";
        try {
            const [home, b] = await Promise.all([DE.home().catch(() => null), DE.api.get("/api/v1/admin/overview/badges").catch(() => null)]);
            if (home && home.site) site = home.site;
            badges = b;
            content = await opts.render();
        } catch (e) {
            console.error(e);
            if (e instanceof DE.ApiError && (e.status === 401 || e.status === 403)) {
                await DE.auth.signOut();
                location.replace("/Account/Login?returnUrl=" + encodeURIComponent(location.pathname + location.search));
                return;
            }
            content = `<div class="adm-card"><div class="adm-empty"><i class="fa-solid fa-triangle-exclamation"></i><strong>Məlumat yüklənmədi</strong>${esc(e.message || "")}</div></div>`;
        }

        document.title = opts.title + " — Admin | " + site.name;
        document.body.className = "adm";
        document.body.innerHTML = shell(site, opts.title, opts.active, badges, content);

        await DE.loadScripts([
            "/lib/jquery/dist/jquery.min.js",
            "/lib/bootstrap/dist/js/bootstrap.bundle.min.js",
            "/js/admin.js",
            "/js/no-autocomplete.js",
            "/js/video-frame.js",
            "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js",
            "/js/admin-live.js"
        ]);
        if (opts.after) await opts.after();

        try {
            const list = JSON.parse(sessionStorage.getItem("de_flash") || "[]");
            sessionStorage.removeItem("de_flash");
            list.forEach((f) => toast(f.message, f.type));
        } catch (e) { /* */ }
    }

    DE.admin = { page, pageHead, deleteButton, statusBadge, toast, fmt, date };
})();
