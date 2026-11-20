/* Rezervasiya statusu: /Reservation/Status?id=KOD — status canlı yenilənir (js/booking.js + /reservationHub) */
DE.page({
    title: "Rezervasiya statusu",
    nav: "Reservation",
    scripts: ["/js/booking.js"],
    async render() {
        const code = DE.qs("id") || "";
        const items = code ? await DE.api.get("/api/v1/reservations/lookup?codes=" + encodeURIComponent(code)) : [];
        const r = (items || [])[0];
        if (!r) throw new DE.ApiError(404, "Not found");

        const esc = DE.esc, t = (s) => esc(DE.t(s));
        const [cls, icon, text] = r.status === "Confirmed" ? ["is-confirmed", "fa-solid fa-check", t("Təsdiqləndi")]
            : r.status === "Rejected" ? ["is-rejected", "fa-solid fa-xmark", t("Rədd edildi")]
                : ["is-pending", "fa-regular fa-hourglass-half", t("Gözləyir")];

        document.getElementById("de-main").innerHTML = `
            ${DE.ui.pageHeader("Rezervasiya statusu", "Rezervasiyanızın vəziyyəti burada canlı yenilənir.")}
            <div class="bk-status-page">
                <div class="container">
                    <div class="bk-status" data-bk-status data-code="${esc(r.code)}" data-status="${esc(r.status)}" data-reason="${esc(r.reason || "")}">
                        <span data-bk-status-pill><span class="bk-pill ${cls}"><i class="${icon}"></i> ${text}</span></span>
                        <h2>${esc(r.service)}</h2>
                        <div class="bk-status__meta">
                            <span><i class="fa-regular fa-calendar"></i> ${esc(r.date)}</span>
                            <span><i class="fa-regular fa-clock"></i> ${esc(r.time)}</span>
                        </div>
                        <div data-bk-status-tracker></div>
                        <div class="bk-live" data-on-text="${t("Canlı: status real vaxtda yenilənir")}">
                            <span class="bk-live__dot" aria-hidden="true"></span>
                            <span data-bk-live-text>${t("Canlı bağlantı qurulur…")}</span>
                        </div>
                        <div class="bk-status__actions">
                            <a href="/Reservation" class="bk-btn bk-btn--ghost"><i class="fa-solid fa-plus"></i> ${t("Yeni rezervasiya")}</a>
                            <a href="/Contact#chat" class="bk-btn bk-btn--primary"><i class="fa-regular fa-comments"></i> ${t("Usta ilə çat")}</a>
                        </div>
                    </div>
                </div>
            </div>`;
    }
});
