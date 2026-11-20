/* Haqqımızda: biz kimik, niyə biz, komanda, çağırış (API: content/about) */
DE.page({
    title: "Haqqımızda",
    nav: "About",
    async render() {
        const page = await DE.api.cached("/api/v1/content/about", 60);
        const ui = DE.ui, esc = DE.esc, t = (s) => esc(DE.t(s));
        const informs = page.informs || [];
        const contact = page.contact;
        const tel = contact && contact.phone ? "tel:" + contact.phone.replace(/[^\d+]/g, "") : null;

        const why = !informs.length ? "" : `
        <section class="ab-why">
            <div class="container">
                <div class="sv-head wow fadeInUp" data-wow-delay="0.1s">
                    <span class="sv-eyebrow">${t("Niyə biz?")}</span>
                    <h2>${t("Üstünlüklərimiz")}</h2>
                    <p>${t("Avtomobilinizi etibar edə biləcəyiniz səbəblər.")}</p>
                </div>
                <div class="ab-why__grid">
                    ${informs.map((inf, i) => {
                        // Təsvir admin paneldə vergüllə yazılır → siyahı
                        const points = String(inf.description || "").split(",").map((p) => p.trim()).filter(Boolean);
                        return `
                    <article class="ab-why__card wow fadeInUp" data-wow-delay="${(i * 0.1 + 0.1).toFixed(1)}s">
                        <div class="ab-why__top">
                            ${inf.iconUrl ? `<span class="ab-why__icon"><img src="${esc(DE.upload(inf.iconUrl))}" alt="" loading="lazy" /></span>` : ""}
                            <span class="ab-why__num" aria-hidden="true">0${i + 1}</span>
                        </div>
                        <h3>${esc(inf.title)}</h3>
                        ${points.length > 1 ? `<ul>${points.map((p) => `<li>${esc(p)}</li>`).join("")}</ul>` : `<p>${esc(inf.description)}</p>`}
                    </article>`;
                    }).join("")}
                </div>
            </div>
        </section>`;

        document.getElementById("de-main").innerHTML = `
            ${ui.pageHeader("Haqqımızda", "Mühərrik təmirində təcrübə, müasir avadanlıq və hər müştəriyə fərdi yanaşma.")}
            <div class="sv ab">
                ${ui.aboutHero(page.about || { title: "" }, informs, contact, false)}
                ${why}
                ${ui.team(page.technicians)}
                <section class="container sv-cta-wrap ab-cta-wrap">
                    <div class="sv-cta wow fadeInUp" data-wow-delay="0.1s">
                        <div>
                            <h2>${t("Mühərrikinizlə bağlı sualınız var?")}</h2>
                            <p>${t("Bizə zəng edin, usta ilə yazışın və ya elə indi rezervasiya edin — vaxtınıza qənaət edin.")}</p>
                        </div>
                        <div class="sv-cta__actions">
                            <a href="/Reservation" class="sv-btn sv-btn--primary sv-btn--lg">${t("Rezervasiya et")} <i class="fa-solid fa-arrow-right"></i></a>
                            ${tel ? `<a href="${esc(tel)}" class="sv-btn sv-btn--light sv-btn--lg"><i class="fa-solid fa-phone"></i> ${t("Zəng et")}</a>` : ""}
                            <a href="/Contact#chat" class="sv-btn sv-btn--light sv-btn--lg"><i class="fa-regular fa-comments"></i> ${t("Usta ilə çat")}</a>
                        </div>
                    </div>
                </section>
            </div>`;
    }
});
