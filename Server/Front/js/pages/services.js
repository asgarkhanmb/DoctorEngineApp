/* Xidmətlər: giriş, kateqoriya süzgəci + video kartlar, proses, çağırış (API: content/services) */
DE.page({
    title: "Xidmətlər",
    nav: "Service",
    scripts: ["/js/services.js"],
    async render() {
        const categories = await DE.api.cached("/api/v1/content/services", 60);
        const ui = DE.ui, t = (s) => DE.esc(DE.t(s));
        const step = (delay, icon, title, text) => `
            <li class="wow fadeInUp" data-wow-delay="${delay}s">
                <span class="sv-steps__icon"><i class="${icon}"></i></span>
                <h3>${t(title)}</h3><p>${t(text)}</p>
            </li>`;
        document.getElementById("de-main").innerHTML = `
            ${ui.pageHeader("Xidmətlər", "Mühərrik diaqnostikasından tam təmirə qədər — bütün xidmətlərimizi kəşf edin.")}
            <div class="sv">
                <section class="container sv-intro">
                    <div class="sv-intro__text wow fadeInUp" data-wow-delay="0.1s">
                        <span class="sv-eyebrow">${t("Xidmətlərimiz")}</span>
                        <h2>${t("Mühərrikiniz üçün peşəkar həllər")}</h2>
                        <p>${t("Hər xidmətin videosuna baxın, işin necə görüldüyünü öz gözünüzlə görün. Uyğun xidməti seçib bir kliklə rezervasiya edin.")}</p>
                    </div>
                    <ul class="sv-perks wow fadeInUp" data-wow-delay="0.2s">
                        <li><i class="fa-regular fa-calendar-check"></i><span><b>${t("Onlayn rezervasiya")}</b>${t("Növbə gözləmədən")}</span></li>
                        <li><i class="fa-regular fa-comments"></i><span><b>${t("Usta ilə canlı çat")}</b>${t("Şəkil və video göndərin")}</span></li>
                        <li><i class="fa-solid fa-shield-halved"></i><span><b>${t("Zəmanət sənədi")}</b>${t("Görülən işə zəmanət")}</span></li>
                    </ul>
                </section>
                <section class="container sv-list" aria-labelledby="sv-list-title">
                    <h2 id="sv-list-title" class="visually-hidden">${t("Xidmətlərin siyahısı")}</h2>
                    ${ui.serviceGrid(categories, { showFilter: true })}
                </section>
                <section class="sv-steps-wrap">
                    <div class="container">
                        <div class="sv-head wow fadeInUp" data-wow-delay="0.1s">
                            <span class="sv-eyebrow">${t("Proses")}</span>
                            <h2>${t("Necə işləyirik?")}</h2>
                            <p>${t("Müraciətdən zəmanətə qədər 4 sadə addım.")}</p>
                        </div>
                        <ol class="sv-steps">
                            ${step(0.1, "fa-regular fa-calendar-check", "Müraciət", "Onlayn rezervasiya edin və ya usta ilə çatda problemi yazın.")}
                            ${step(0.2, "fa-solid fa-magnifying-glass-chart", "Diaqnostika", "Usta mühərriki yoxlayır, nasazlığın səbəbini müəyyən edir.")}
                            ${step(0.3, "fa-solid fa-screwdriver-wrench", "Təmir", "Razılaşdırılmış işlər peşəkar şəkildə yerinə yetirilir.")}
                            ${step(0.4, "fa-solid fa-shield-halved", "Zəmanət", "Görülən iş üçün zəmanət sənədi təqdim olunur.")}
                        </ol>
                    </div>
                </section>
                <section class="container sv-cta-wrap">
                    <div class="sv-cta wow fadeInUp" data-wow-delay="0.1s">
                        <div>
                            <h2>${t("Problemin nə olduğunu bilmirsiniz?")}</h2>
                            <p>${t("Əlamətləri AI köməkçiyə yazın və ya birbaşa usta ilə danışın — sizə uyğun xidməti birlikdə tapaq.")}</p>
                        </div>
                        <div class="sv-cta__actions">
                            <a href="/Reservation" class="sv-btn sv-btn--primary sv-btn--lg">${t("Rezervasiya et")} <i class="fa-solid fa-arrow-right"></i></a>
                            <a href="/Assistant" class="sv-btn sv-btn--light sv-btn--lg"><i class="fa-solid fa-wand-magic-sparkles"></i> ${t("AI Köməkçi")}</a>
                            <a href="/Contact#chat" class="sv-btn sv-btn--light sv-btn--lg"><i class="fa-regular fa-comments"></i> ${t("Usta ilə çat")}</a>
                        </div>
                    </div>
                </section>
            </div>`;
    }
});
