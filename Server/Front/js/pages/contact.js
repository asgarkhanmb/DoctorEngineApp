/* Əlaqə: canlı çat (səhifədə), əlaqə kartları, xəritə, mesaj forması (API: content/contact) */
DE.page({
    title: "Əlaqə",
    nav: "Contact",
    inlineChat: true,
    async render() {
        const page = await DE.api.cached("/api/v1/content/contact", 60);
        const ui = DE.ui, esc = DE.esc, t = (s, ...a) => esc(DE.t(s, ...a));
        const c = page.contact || {};
        const map = page.map;
        const card = (delay, icon, title, body) => `
            <div class="col-lg-4 wow fadeInUp" data-wow-delay="${delay}s">
                <div class="de-contact-card">
                    <span class="de-contact-card__icon"><i class="${icon}"></i></span>
                    <div><h5>${title}</h5>${body}</div>
                </div>
            </div>`;
        const field = (name, label, input) => `
            <label for="${name}">${label}</label>${input}
            <span class="text-danger field-validation-valid" data-valmsg-for="${name}"></span>`;

        document.getElementById("de-main").innerHTML = `
            ${ui.pageHeader("Əlaqə", "Sualınız var? Usta ilə canlı yazışın, zəng edin və ya mesaj göndərin.")}
            <div class="container-xxl py-5" id="chat">
                <div class="container">
                    <div class="text-center mb-4 wow fadeInUp" data-wow-delay="0.1s">
                        <h6 class="text-primary text-uppercase">${t("Canlı Əlaqə")}</h6>
                        <h2 class="mb-3">${t("Usta ilə Birbaşa Yazışın")}</h2>
                        <p class="mb-0">${t("Probleminizi yazın, şəkil və ya video göndərin — ustalarımız tezliklə cavab verəcək.")}</p>
                    </div>
                    ${DE.chatWidget.html({})}
                </div>
            </div>
            <div class="container-xxl py-5">
                <div class="container">
                    <div class="text-center mb-5 wow fadeInUp" data-wow-delay="0.1s">
                        <h6 class="text-primary text-uppercase">${t("Bizimlə Əlaqə")}</h6>
                        <h1 class="mb-0">${t("İstənilən sual üçün əlaqə saxlayın")}</h1>
                    </div>
                    <div class="row g-4 mb-4">
                        ${card(0.1, "fa-solid fa-location-dot", t("Ünvan"), `<p>${c.address ? esc(c.address) : "—"}</p>`)}
                        ${card(0.2, "fa-solid fa-phone", t("Telefon"), c.phone ? `<a href="tel:${esc(c.phone.replace(/ /g, ""))}">${esc(c.phone)}</a>` : "<p>—</p>")}
                        ${card(0.3, "fa-solid fa-envelope", "Email", c.email ? `<a href="mailto:${esc(c.email)}">${esc(c.email)}</a>` : "<p>—</p>")}
                    </div>
                    <div class="row g-4">
                        <div class="col-lg-6 wow fadeInUp" data-wow-delay="0.1s">
                            ${map && map.mapUrl ? `<iframe class="de-map" src="${esc(map.mapUrl)}" title="${t("Xəritə: {0}", map.address || "")}" loading="lazy" referrerpolicy="no-referrer-when-downgrade" allowfullscreen></iframe>` : ""}
                        </div>
                        <div class="col-lg-6 wow fadeInUp" data-wow-delay="0.2s" id="contact-form">
                            <div class="de-form-card">
                                <h4>${t("Mesaj göndərin")}</h4>
                                <p class="lead-sm">${t("Formu doldurun, ən qısa zamanda sizinlə əlaqə saxlayacağıq.")}</p>
                                <form novalidate data-contact-form>
                                    <div class="alert alert-success d-flex align-items-center gap-2" role="status" data-ok hidden></div>
                                    <div class="alert alert-danger py-2 mb-3" role="alert" data-error hidden></div>
                                    <div class="row g-3">
                                        <div class="col-sm-6">${field("FullName", t("Ad Soyad"), `<input id="FullName" name="FullName" class="form-control" autocomplete="off" maxlength="100" required placeholder="${t("Adınız")}" />`)}</div>
                                        <div class="col-sm-6">${field("Email", "Email", `<input id="Email" name="Email" type="email" class="form-control" autocomplete="off" required placeholder="${t("ad@numune.az")}" />`)}</div>
                                        <div class="col-12">${field("Subject", t("Mövzu"), `<input id="Subject" name="Subject" class="form-control" maxlength="150" required placeholder="${t("Məs: Mühərrik diaqnostikası")}" />`)}</div>
                                        <div class="col-12">${field("Message", t("Mesaj"), `<textarea id="Message" name="Message" class="form-control" rows="4" maxlength="2000" required placeholder="${t("Mesajınızı yazın…")}"></textarea>`)}</div>
                                        <div class="col-12">
                                            <button type="submit" class="btn btn-primary w-100 py-3"><i class="fa-regular fa-paper-plane me-2"></i> ${t("Göndər")}</button>
                                        </div>
                                    </div>
                                </form>
                            </div>
                        </div>
                    </div>
                </div>
            </div>`;

        const form = document.querySelector("[data-contact-form]");
        DE.forms.bind(form, async (data) => {
            const res = await DE.api.post("/api/v1/content/contact", data);
            form.reset();
            const ok = form.querySelector("[data-ok]");
            ok.innerHTML = `<i class="fa-solid fa-circle-check"></i> ${esc(res.message)}`;
            ok.hidden = false;
        });
    },
    after() { DE.chatWidget.start({}); }
});
