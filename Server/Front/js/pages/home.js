/* Ana səhifə: slayder, xidmət lenti, üstünlüklər, haqqımızda, son 3 xidmət, rezervasiya, ustalar */
DE.page({
    title: "Ana Səhifə",
    nav: "Home",
    scripts: ["/js/services.js", "/js/booking.js"],
    async render(home) {
        const options = await DE.api.cached("/api/v1/reservations/options", 60);
        const ui = DE.ui, t = (s) => DE.esc(DE.t(s));
        const latest = (home.categories || []).slice().sort((a, b) => b.id - a.id).slice(0, 3);
        document.getElementById("de-main").innerHTML = `
            ${ui.slider(home.slides)}
            ${ui.marquee(home.categories)}
            ${ui.informCards(home.informs)}
            ${home.about ? `<div class="sv ab ab-home">${ui.aboutHero(home.about, home.informs, home.contact, true)}</div>` : ""}
            <section class="sv sv-home">
                <div class="container">
                    <div class="sv-home__head wow fadeInUp" data-wow-delay="0.1s">
                        <div>
                            <span class="sv-eyebrow">${t("Xidmətlərimiz")}</span>
                            <h2>${t("Xidmətlərimizi araşdırın")}</h2>
                            <p>${t("Videolara baxın, işin necə görüldüyünü görün və uyğun xidməti bir kliklə rezervasiya edin.")}</p>
                        </div>
                        <a href="/Service" class="sv-btn sv-btn--ghost">${t("Bütün xidmətlər")} <i class="fa-solid fa-arrow-right"></i></a>
                    </div>
                    ${ui.serviceGrid(latest, { limit: 3 })}
                </div>
            </section>
            ${ui.bookingForm(options, home.contact, 0)}
            <div class="sv ab ab-home-team">${ui.team(home.technicians)}</div>`;
    }
});
