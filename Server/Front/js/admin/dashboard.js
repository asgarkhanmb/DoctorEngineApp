/* Admin: idarə paneli — statistika, yaxın rezervasiyalar, son mesajlar və çat (API: admin/overview) */
(function () {
    "use strict";
    const esc = DE.esc, A = DE.admin;
    const months = ["yan", "fev", "mar", "apr", "may", "iyn", "iyl", "avq", "sen", "okt", "noy", "dek"];

    A.page({
        title: "İdarə paneli",
        active: "Dashboard",
        async render() {
            const m = await DE.api.get("/api/v1/admin/overview");
            const hour = DE.bakuNow ? DE.bakuNow().getHours() : new Date().getHours();
            const greeting = hour < 12 ? "Sabahınız xeyir" : hour < 18 ? "Günortanız xeyir" : "Axşamınız xeyir";
            const user = DE.auth.user() || {};
            const stat = (href, cls, icon, label, value, hint, ext) => `
                <a href="${href}" class="adm-card adm-stat" ${ext ? `target="_blank" rel="noopener"` : ""}>
                    <span class="adm-stat__icon ${cls}"><i class="${icon}"></i></span>
                    <span><span class="adm-stat__label">${label}</span><span class="adm-stat__value d-block">${value}</span><span class="adm-stat__hint">${hint}</span></span>
                </a>`;
            const upcoming = m.upcomingReservations || [], messages = m.latestMessages || [], chat = m.latestChat || [];

            return `
<div class="adm-page-head">
    <div><h1>${greeting}, ${esc(user.userName)} 👋</h1><p>Saytın vəziyyəti bir baxışda.</p></div>
    <div class="adm-page-head__actions"><a href="/Admin/Reservation" class="btn btn-primary"><i class="fa-regular fa-calendar-check"></i> Rezervasiyalar</a></div>
</div>
<div class="adm-stats">
    ${stat("/Admin/Reservation", "is-amber", "fa-regular fa-hourglass-half", "Gözləyən rezervasiya", m.pendingReservations, "təsdiq gözləyir")}
    ${stat("/Admin/Reservation", "", "fa-regular fa-calendar-day", "Bu günkü rezervasiya", m.todayReservations, "cəmi: " + m.totalReservations)}
    ${stat("/Admin/ContactMessage", "is-navy", "fa-regular fa-envelope", "Əlaqə mesajı (7 gün)", m.messagesLast7Days, "cəmi: " + m.totalMessages)}
    ${stat("/Chat", "is-green", "fa-regular fa-comments", "Çat mesajı (bu gün)", m.chatMessagesToday, `istifadəçi: ${m.users} · zəmanət: ${m.warranties}`, true)}
</div>
<div class="row g-4">
    <div class="col-xl-7">
        <div class="adm-card h-100">
            <div class="adm-card__head"><h2>Yaxın rezervasiyalar</h2><a href="/Admin/Reservation" class="small">Hamısı <i class="fa-solid fa-arrow-right ms-1"></i></a></div>
            ${!upcoming.length ? `<div class="adm-empty"><i class="fa-regular fa-calendar"></i><strong>Yaxın rezervasiya yoxdur</strong>Yeni rezervasiyalar burada görünəcək.</div>` : `
            <ul class="adm-list">${upcoming.map((r) => { const d = A.date(r.dateTime); return `
                <li>
                    <span class="adm-date"><b>${d.getDate()}</b><small>${months[d.getMonth()]}</small></span>
                    <div class="adm-list__main"><div class="adm-list__title">${esc(r.fullName)}</div><div class="adm-list__sub">${esc(r.serviceName)} · ${A.fmt.time(r.dateTime)} · ${esc(r.phone)}</div></div>
                    ${A.statusBadge(r.status)}
                </li>`; }).join("")}</ul>`}
        </div>
    </div>
    <div class="col-xl-5">
        <div class="adm-card">
            <div class="adm-card__head"><h2>Son əlaqə mesajları</h2><a href="/Admin/ContactMessage" class="small">Hamısı <i class="fa-solid fa-arrow-right ms-1"></i></a></div>
            ${!messages.length ? `<div class="adm-empty"><i class="fa-regular fa-envelope-open"></i><strong>Mesaj yoxdur</strong></div>` : `
            <ul class="adm-list">${messages.map((x) => `
                <li>
                    <span class="adm-list__icon"><i class="fa-regular fa-envelope"></i></span>
                    <a class="adm-list__main" href="/Admin/ContactMessage?id=${x.id}" style="color:inherit"><div class="adm-list__title">${esc(x.subject)}</div><div class="adm-list__sub">${esc(x.fullName)} · ${esc(x.email)}</div></a>
                    <span class="adm-list__meta">${A.fmt.short(x.createdDate)}</span>
                </li>`).join("")}</ul>`}
        </div>
        <div class="adm-card">
            <div class="adm-card__head"><h2>Canlı çat</h2><a href="/Chat" target="_blank" rel="noopener" class="small">Çatı aç <i class="fa-solid fa-arrow-up-right-from-square ms-1"></i></a></div>
            ${!chat.length ? `<div class="adm-empty"><i class="fa-regular fa-comments"></i><strong>Hələ mesaj yoxdur</strong></div>` : `
            <ul class="adm-list">${chat.map((c) => {
                const icon = { image: "fa-regular fa-image", video: "fa-solid fa-film", audio: "fa-solid fa-microphone" }[c.attachmentType];
                const text = c.message || { image: "Şəkil", video: "Video", audio: "Səs mesajı" }[c.attachmentType] || "Mesaj";
                return `
                <li>
                    <span class="adm-list__icon">${c.senderIsAdmin ? `<i class="fa-solid fa-wrench"></i>` : esc((c.userName || "?").slice(0, 1).toUpperCase())}</span>
                    <div class="adm-list__main">
                        <div class="adm-list__title">${esc(c.userName)} ${c.senderIsAdmin ? `<span class="adm-badge is-admin ms-1">Usta</span>` : ""}</div>
                        <div class="adm-list__sub">${icon ? `<i class="${icon} me-1"></i>` : ""}${esc(text)}</div>
                    </div>
                    <span class="adm-list__meta">${A.fmt.short(c.createdDate)}</span>
                </li>`; }).join("")}</ul>`}
        </div>
    </div>
</div>`;
        }
    });
})();
