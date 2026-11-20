/* Rezervasiya: üstünlüklər, canlı rezervasiya forması, zəng bloku (API: reservations/options, content/home) */
DE.page({
    title: "Rezervasiya",
    nav: "Reservation",
    scripts: ["/js/booking.js"],
    async render(home) {
        const options = await DE.api.cached("/api/v1/reservations/options", 60);
        const ui = DE.ui, esc = DE.esc, t = (s) => esc(DE.t(s));
        const serviceId = parseInt(DE.qs("serviceId") || "0", 10);
        const cp = (home.phones || [])[0];
        const call = !(cp && cp.phoneNumber) ? "" : `
        <section class="rs-call">
            <div class="container">
                <div class="rs-call__box wow fadeInUp" data-wow-delay="0.1s">
                    <div class="rs-call__text">
                        ${cp.subtitle ? `<span class="rs-call__eyebrow">${esc(cp.subtitle)}</span>` : ""}
                        <h2>${esc(cp.title)}</h2>
                        <p>${esc(cp.description)}</p>
                    </div>
                    <a class="rs-call__btn" href="tel:${esc(cp.phoneNumber.replace(/ /g, ""))}">
                        <span class="rs-call__icon"><i class="fa fa-phone-alt"></i></span>
                        <span class="rs-call__num"><small>${t("İndi zəng edin")}</small><strong>${esc(cp.phoneNumber)}</strong></span>
                        <i class="fa-solid fa-arrow-right rs-call__arrow" aria-hidden="true"></i>
                    </a>
                </div>
            </div>
        </section>`;
        document.getElementById("de-main").innerHTML = `
            ${ui.pageHeader("Rezervasiya", "Mühərrik diaqnostikası və təmiri üçün onlayn növbə götürün — gözləmədən, rahat.")}
            ${ui.informCards(home.informs)}
            ${ui.bookingForm(options, home.contact, serviceId)}
            ${call}`;
    }
});
