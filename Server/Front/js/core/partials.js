/* =====================================================
   DoctorEngine — ortaq bloklar (köhnə saytın partial-ları ilə eyni HTML)
   Məlumat API-nin content/* cavablarından gəlir (mətnlər artıq seçilmiş dildədir).

   DE.ui.pageHeader(title, subtitle)   DE.ui.slider(slides)          DE.ui.marquee(categories)
   DE.ui.informCards(informs)          DE.ui.aboutHero(about, informs, contact, isHome)
   DE.ui.serviceGrid(categories, opts) DE.ui.bookingForm(options, contact, selectedServiceId)
   DE.ui.team(technicians)             DE.ui.videoFrame(src)
   ===================================================== */
(function () {
    "use strict";
    const DE = window.DE;
    const esc = DE.esc;
    const t = (s, ...a) => esc(DE.t(s, ...a));
    const up = (u) => esc(DE.upload(u));

    function pageHeader(title, subtitle) {
        return `
<section class="de-page-header">
    <div class="container">
        <h1>${t(title)}</h1>
        ${subtitle ? `<p>${t(subtitle)}</p>` : ""}
        <nav aria-label="breadcrumb">
            <ol class="de-breadcrumb">
                <li><a href="/"><i class="fa-solid fa-house"></i> ${t("Ana Səhifə")}</a></li>
                <li class="sep" aria-hidden="true"><i class="fa-solid fa-chevron-right"></i></li>
                <li aria-current="page">${t(title)}</li>
            </ol>
        </nav>
    </div>
</section>`;
    }

    function heroActions() {
        return `
            <div class="de-hero__actions">
                <a href="/Reservation" class="btn btn-primary de-hero__cta">${t("Rezervasiya")} <i class="fa fa-arrow-right ms-2"></i></a>
                <a href="/Contact#chat" class="btn de-hero__ghost"><i class="fa-regular fa-comments me-2"></i> ${t("Usta ilə yazış")}</a>
            </div>`;
    }

    function slider(slides) {
        const interval = 6000;
        slides = slides || [];
        const multiple = slides.length > 1;
        let h = `<section class="de-hero" aria-roledescription="carousel" aria-label="${t("Əsas slayder")}" style="--de-hero-interval:${interval}ms">`;
        if (!slides.length) {
            h += `
    <div class="de-hero__slide is-static">
        <img class="de-hero__bg" src="/assets/img/bmwserv.jpg" alt="" fetchpriority="high" />
        <div class="de-hero__overlay"></div>
        <div class="container de-hero__content"><div class="de-hero__text">
            <span class="de-hero__badge">${t("Avto xidmətlər")}</span>
            <h1 class="de-hero__title">${t("Dizel və benzin mühərriklərinin peşəkar təmiri")}</h1>
            ${heroActions()}
        </div></div>
    </div>`;
        } else {
            h += `<div id="header-carousel" class="carousel slide carousel-fade" data-bs-ride="${multiple ? "carousel" : "false"}" data-bs-interval="${interval}" data-bs-pause="hover" data-bs-touch="true">`;
            if (multiple) {
                h += `<div class="carousel-indicators de-hero__dots">` + slides.map((s, i) =>
                    `<button type="button" data-bs-target="#header-carousel" data-bs-slide-to="${i}" class="${i === 0 ? "active" : ""}" ${i === 0 ? `aria-current="true"` : ""} aria-label="${t("Slayd {0}", i + 1)}"></button>`).join("") + `</div>`;
            }
            h += `<div class="carousel-inner">` + slides.map((s, i) => {
                const first = i === 0;
                const tag = first ? "h1" : "h2";
                return `
        <div class="carousel-item de-hero__slide ${first ? "active" : ""}" role="group" aria-roledescription="slide" aria-label="${i + 1} / ${slides.length}">
            <img class="de-hero__bg" src="${up(s.backgroundUrl)}" alt="" loading="${first ? "eager" : "lazy"}" fetchpriority="${first ? "high" : "low"}" decoding="async" />
            <div class="de-hero__overlay"></div>
            <div class="container de-hero__content">
                <div class="row align-items-center g-4"><div class="col-lg-7"><div class="de-hero__text">
                    <span class="de-hero__badge">${esc(s.title)}</span>
                    <${tag} class="de-hero__title">${esc(s.subtitle)}</${tag}>
                    ${heroActions()}
                </div></div></div>
            </div>
            ${multiple ? `<div class="de-hero__progress" aria-hidden="true"><span></span></div>` : ""}
        </div>`;
            }).join("") + `</div>`;
            if (multiple) {
                h += `
        <button class="carousel-control-prev de-hero__nav" type="button" data-bs-target="#header-carousel" data-bs-slide="prev">
            <span class="de-hero__nav-icon" aria-hidden="true"><i class="fa-solid fa-chevron-left"></i></span><span class="visually-hidden">${t("Əvvəlki")}</span>
        </button>
        <button class="carousel-control-next de-hero__nav" type="button" data-bs-target="#header-carousel" data-bs-slide="next">
            <span class="de-hero__nav-icon" aria-hidden="true"><i class="fa-solid fa-chevron-right"></i></span><span class="visually-hidden">${t("Növbəti")}</span>
        </button>`;
            }
            h += `</div>`;
        }
        h += `<a href="#de-after-hero" class="de-hero__scroll" aria-label="${t("Aşağı sürüşdür")}"><span></span></a></section><div id="de-after-hero"></div>`;
        return h;
    }

    function marquee(categories) {
        const items = (categories || []).filter((c) => c.name && c.name.trim());
        if (!items.length) return "";
        let repeated = [];
        while (repeated.length < 8) repeated = repeated.concat(items);
        const group = (copy) => `<ul class="de-marquee__group" aria-hidden="${copy === 1}">` + repeated.map((c) => `
                <li><a href="/Service#cat-${c.id}" tabindex="${copy === 1 ? "-1" : "0"}">
                    ${c.iconUrl ? `<img src="${up(c.iconUrl)}" alt="" width="26" height="26" loading="lazy" />` : `<i class="fa-solid fa-screwdriver-wrench" aria-hidden="true"></i>`}
                    <span>${esc(c.name)}</span></a></li>
                <li class="de-marquee__sep" aria-hidden="true"><i class="fa-solid fa-gear"></i></li>`).join("") + `</ul>`;
        return `<section class="de-marquee" aria-label="${t("Xidmətlərimiz")}"><div class="de-marquee__track">${group(0)}${group(1)}</div></section>`;
    }

    function informCards(informs) {
        informs = informs || [];
        if (!informs.length) return "";
        return `
<div class="container-xxl py-5"><div class="container"><div class="row g-4">
    ${informs.map((it, i) => `
    <div class="col-lg-4 ${informs.length % 2 === 1 && i === informs.length - 1 ? "col-md-12" : "col-md-6"} wow fadeInUp" data-wow-delay="${(i * 0.12 + 0.1).toFixed(2)}s">
        <div class="de-feature">
            ${it.iconUrl ? `<div class="de-feature__icon"><img src="${up(it.iconUrl)}" alt="" loading="lazy" /></div>` : ""}
            <div><h5>${esc(it.title)}</h5><p>${esc(it.description)}</p></div>
        </div>
    </div>`).join("")}
</div></div></div>`;
    }

    function videoFrame(src) {
        const s = src.includes("#") ? src : src + "#t=0.5";
        return `
<div class="de-video-stage"><div class="de-video is-site is-tall">
    <canvas class="de-video__backdrop" aria-hidden="true"></canvas>
    <video class="de-video__player" src="${up(s)}" preload="metadata" playsinline></video>
    <button type="button" class="de-video__play" aria-label="${t("Videonu oynat")}"><i class="fa-solid fa-play" aria-hidden="true"></i></button>
</div></div>`;
    }

    function aboutHero(about, informs, contact, isHome) {
        if (!about) return "";
        informs = informs || [];
        const phone = contact && contact.phone;
        const telHref = phone ? "tel:" + phone.replace(/[^\d+]/g, "") : null;
        return `
<section class="container ab-hero">
    ${about.videoUrl ? `<div class="ab-hero__media wow fadeInUp" data-wow-delay="0.1s">${videoFrame(about.videoUrl)}</div>` : ""}
    <div class="ab-hero__text wow fadeInUp" data-wow-delay="0.2s">
        <span class="sv-eyebrow">${t("Haqqımızda")}</span>
        <h2>${esc(about.title)}</h2>
        ${about.description ? `<p class="ab-lead">${esc(about.description)}</p>` : ""}
        ${informs.length ? `<ul class="ab-ticks">${informs.map((i) => `<li><i class="fa-solid fa-check"></i> ${esc(i.title)}</li>`).join("")}</ul>` : ""}
        <div class="ab-hero__actions">
            <a href="/Reservation" class="sv-btn sv-btn--primary sv-btn--lg">${t("Rezervasiya et")} <i class="fa-solid fa-arrow-right"></i></a>
            ${isHome ? `<a href="/About" class="sv-btn sv-btn--ghost sv-btn--lg">${t("Daha ətraflı")}</a>` : `<a href="/Service" class="sv-btn sv-btn--ghost sv-btn--lg">${t("Xidmətlərimiz")}</a>`}
        </div>
        ${contact && (telHref || contact.address) ? `
        <ul class="ab-contact">
            ${telHref ? `<li><span class="ab-contact__icon"><i class="fa-solid fa-phone"></i></span><span><small>${t("Zəng edin")}</small><a href="${esc(telHref)}">${esc(phone)}</a></span></li>` : ""}
            ${contact.address ? `<li><span class="ab-contact__icon"><i class="fa-solid fa-location-dot"></i></span><span><small>${t("Ünvan")}</small><a href="/Contact">${esc(contact.address)}</a></span></li>` : ""}
        </ul>` : ""}
    </div>
</section>`;
    }

    /** opts: { limit, showFilter } — kateqoriyalar API-nin CategoryDto-larıdır (videos, reservationServiceId) */
    function serviceGrid(categories, opts) {
        opts = opts || {};
        const items = (categories || []).map((c) => ({ cat: c, sub: (c.videos || [])[0] || null }));
        const shown = opts.limit ? items.slice(0, opts.limit) : items;
        if (!items.length)
            return `<div class="sv-empty"><i class="fa-solid fa-screwdriver-wrench"></i><p>${t("Xidmətlər tezliklə əlavə olunacaq.")}</p></div>`;

        let h = "";
        if (opts.showFilter) {
            h += `<div class="sv-filter" role="toolbar" aria-label="${t("Kateqoriyaya görə süz")}">
                <button type="button" class="sv-chip is-active" data-filter="all" aria-pressed="true">${t("Hamısı")} <span class="sv-chip__count">${items.length}</span></button>
                ${items.map(({ cat, sub }) => `
                <button type="button" class="sv-chip" data-filter="cat-${cat.id}" aria-pressed="false">
                    ${cat.iconUrl ? `<img src="${up(cat.iconUrl)}" alt="" width="20" height="20" />` : ""}
                    ${esc(cat.name)}
                    ${!sub ? `<span class="sv-chip__count">${t("tezliklə")}</span>` : ""}
                </button>`).join("")}
            </div>`;
        }
        h += `<div class="sv-grid">` + shown.map(({ cat, sub }) => {
            const reserveUrl = cat.reservationServiceId ? "/Reservation?serviceId=" + cat.reservationServiceId : "/Reservation";
            const catIcon = cat.iconUrl ? up(cat.iconUrl) : null;
            const videoUrl = sub && sub.videoUrl ? up(sub.videoUrl) : null;
            return `
    <article class="sv-card ${!sub ? "sv-card--soon" : ""}" data-cat="cat-${cat.id}">
        ${videoUrl ? `
        <button type="button" class="sv-card__media" data-video="${videoUrl}" data-title="${esc(sub.title)}" aria-label="${t("Videoya bax: {0}", sub.title)}">
            <video src="${videoUrl}#t=0.5" preload="metadata" muted loop playsinline tabindex="-1" aria-hidden="true"></video>
            <span class="sv-card__play" aria-hidden="true"><i class="fa-solid fa-play"></i></span>
            <span class="sv-badge" aria-hidden="true"><i class="fa-solid fa-video"></i> <span data-duration>${t("Video")}</span></span>
        </button>` : `
        <div class="sv-card__soon-media">
            ${catIcon ? `<img src="${catIcon}" alt="" />` : `<i class="fa-solid fa-screwdriver-wrench"></i>`}
            <span class="sv-badge"><i class="fa-regular fa-clock"></i> ${t("Video tezliklə")}</span>
        </div>`}
        <div class="sv-card__body">
            <span class="sv-card__cat">${catIcon ? `<img src="${catIcon}" alt="" width="18" height="18" />` : ""} ${esc(cat.name)}</span>
            ${sub ? `<h3>${esc(sub.title)}</h3><p>${esc(sub.description)}</p>`
                    : `<h3>${esc(cat.name)}</h3><p>${t("Bu xidmət üzrə ətraflı məlumat tezliklə əlavə olunacaq. Sualınız varsa, usta ilə yazışın və ya elə indi rezervasiya edin.")}</p>`}
            <div class="sv-card__actions">
                <a href="${esc(reserveUrl)}" class="sv-btn sv-btn--primary">${t("Rezervasiya et")} <i class="fa-solid fa-arrow-right"></i></a>
                ${sub ? `<a href="/Assistant" class="sv-btn sv-btn--ghost"><i class="fa-solid fa-wand-magic-sparkles"></i> ${t("Sual ver")}</a>`
                      : `<a href="/Contact#chat" class="sv-btn sv-btn--ghost"><i class="fa-regular fa-comments"></i> ${t("Ustaya yaz")}</a>`}
            </div>
        </div>
    </article>`;
        }).join("") + `</div>
<div class="modal fade sv-modal" id="svVideoModal" tabindex="-1" aria-labelledby="svVideoTitle" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered"><div class="modal-content">
        <div class="sv-modal__head">
            <h2 class="sv-modal__title" id="svVideoTitle"></h2>
            <button type="button" class="sv-modal__close" data-bs-dismiss="modal" aria-label="${t("Bağla")}"><i class="fa-solid fa-xmark"></i></button>
        </div>
        <video class="sv-modal__video" controls playsinline></video>
    </div></div>
</div>`;
        return h;
    }

    function team(technicians) {
        technicians = technicians || [];
        if (!technicians.length) return "";
        const single = technicians.length === 1;
        return `
<section class="container ab-team">
    <div class="sv-head wow fadeInUp" data-wow-delay="0.1s">
        <span class="sv-eyebrow">${t("Komandamız")}</span>
        <h2>${t("Mütəxəssislərimiz")}</h2>
        <p>${t("Mühərrikinizə baxacaq ustalarla tanış olun.")}</p>
    </div>
    <div class="ab-team__grid ${single ? "is-single" : ""}">
        ${technicians.map((m) => {
            const social = m.facebookUrl || m.instagramUrl || m.tiktokUrl;
            return `
        <article class="ab-member wow fadeInUp" data-wow-delay="0.1s">
            <div class="ab-member__photo"><img src="${up(m.imageUrl)}" alt="${esc(m.fullName)}" loading="lazy" /></div>
            <div class="ab-member__body">
                ${single ? `
                <span class="ab-member__role">${esc(m.position)}</span>
                <h3>${esc(m.fullName)}</h3>
                ${m.bio ? `<p class="ab-member__note">${esc(m.bio)}</p>` : ""}
                <div class="ab-member__actions">
                    <a href="/Contact#chat" class="sv-btn sv-btn--primary"><i class="fa-regular fa-comments"></i> ${t("Ustaya yaz")}</a>
                    <a href="/Reservation" class="sv-btn sv-btn--light">${t("Rezervasiya et")} <i class="fa-solid fa-arrow-right"></i></a>
                </div>` : `<h3>${esc(m.fullName)}</h3><span class="ab-member__role">${esc(m.position)}</span>`}
                ${social ? `<div class="ab-member__social">
                    ${m.facebookUrl ? `<a href="${esc(m.facebookUrl)}" target="_blank" rel="noopener" aria-label="${esc(m.fullName)} — Facebook"><i class="fab fa-facebook-f"></i></a>` : ""}
                    ${m.instagramUrl ? `<a href="${esc(m.instagramUrl)}" target="_blank" rel="noopener" aria-label="${esc(m.fullName)} — Instagram"><i class="fab fa-instagram"></i></a>` : ""}
                    ${m.tiktokUrl ? `<a href="${esc(m.tiktokUrl)}" target="_blank" rel="noopener" aria-label="${esc(m.fullName)} — TikTok"><i class="fab fa-tiktok"></i></a>` : ""}
                </div>` : ""}
            </div>
        </article>`;
        }).join("")}
    </div>
</section>`;
    }

    // ---------- Rezervasiya forması ----------
    /** Bakı vaxtı ilə indiki an (komponentlər) */
    function bakuNow() {
        const p = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Baku", year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hour12: false })
            .formatToParts(new Date()).reduce((a, x) => (a[x.type] = x.value, a), {});
        return new Date(+p.year, +p.month - 1, +p.day, +p.hour % 24, +p.minute);
    }
    DE.bakuNow = bakuNow;

    /** options: API reservations/options ({ services, rules }); contact: ContactDto */
    function bookingForm(options, contact, selectedServiceId) {
        const rules = (options && options.rules) || { openingHour: 10, closingHour: 20, slotMinutes: 30, maxDaysAhead: 10 };
        const services = (options && options.services) || [];
        const callPhone = contact && contact.phone;
        const callHref = (callPhone || "").replace(/[^\d+]/g, "");
        const now = bakuNow();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const lastSlotToday = new Date(today.getTime() + rules.closingHour * 3600000 - rules.slotMinutes * 60000);
        const firstDay = now >= lastSlotToday ? new Date(today.getTime() + 86400000) : today;
        const days = Array.from({ length: rules.maxDaysAhead + 1 }, (_, i) => new Date(today.getFullYear(), today.getMonth(), today.getDate() + i));
        const selected = services.some((s) => s.id === selectedServiceId) ? selectedServiceId : (services[0] ? services[0].id : 0);
        const azDays = ["B.", "B.e.", "Ç.a.", "Ç.", "C.a.", "C.", "Ş."];
        const azMonths = ["Yan", "Fev", "Mar", "Apr", "May", "İyn", "İyl", "Avq", "Sen", "Okt", "Noy", "Dek"];
        const locale = { en: "en-GB", ru: "ru-RU", tr: "tr-TR" }[DE.lang];
        const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
        const dayLabel = (d) => {
            if (+d === +today) return t("Bu gün");
            if (+d === +today + 86400000 || iso(d) === iso(new Date(today.getFullYear(), today.getMonth(), today.getDate() + 1))) return t("Sabah");
            return locale ? esc(new Intl.DateTimeFormat(locale, { weekday: "short" }).format(d)) : azDays[d.getDay()];
        };
        const monthLabel = (d) => locale ? esc(new Intl.DateTimeFormat(locale, { month: "short" }).format(d).replace(/\.$/, "")) : azMonths[d.getMonth()];
        const pad = (n) => String(n).padStart(2, "0");

        return `
<link rel="stylesheet" href="/css/booking.css" />
<section class="bk" id="booking" data-bk data-slots-url="/api/v1/reservations/slots" data-lookup-url="/api/v1/reservations/lookup" data-status-url="/Reservation/Status?id=__code__">
    <div class="container"><div class="bk-shell">
        <aside class="bk-info wow fadeInUp" data-wow-delay="0.1s">
            <span class="bk-eyebrow">${t("Onlayn rezervasiya")}</span>
            <h2>${t("Növbəni indi götürün")}</h2>
            <p>${t("Xidməti, günü və boş saatı seçin — rezervasiyanızın statusunu səhifədən çıxmadan canlı izləyin.")}</p>
            <div class="bk-live" title="${t("Saatlar real vaxtda yenilənir")}">
                <span class="bk-live__dot" aria-hidden="true"></span>
                <span data-bk-live-text>${t("Canlı: boş saatlar real vaxtda yenilənir")}</span>
            </div>
            <ul class="bk-perks">
                <li><i class="fa-regular fa-clock"></i><span><b>${t("İş saatları")}</b>${pad(rules.openingHour)}:00 – ${pad(rules.closingHour)}:00</span></li>
                <li><i class="fa-brands fa-whatsapp"></i><span><b>${t("Təsdiq")}</b>${t("WhatsApp ilə göndərilir")}</span></li>
                <li><i class="fa-regular fa-calendar"></i><span><b>${t("Öncədən")}</b>${t("10 günə qədər rezerv")}</span></li>
            </ul>
            ${callPhone ? `<a class="bk-call" href="tel:${esc(callHref)}"><span class="bk-call__icon"><i class="fa-solid fa-phone"></i></span><span><small>${t("Zənglə də rezerv edə bilərsiniz")}</small><b>${esc(callPhone)}</b></span></a>` : ""}
            <div class="bk-mine" data-bk-mine hidden>
                <h3><i class="fa-regular fa-bookmark"></i> ${t("Rezervasiyalarım")}</h3>
                <ul data-bk-mine-list></ul>
            </div>
        </aside>

        <div class="bk-card wow fadeInUp" data-wow-delay="0.2s">
            <form id="reservationForm" action="/api/v1/reservations/form" method="post" novalidate data-bk-form>
                <fieldset class="bk-step">
                    <legend><span class="bk-step__num">1</span> ${t("Xidmət seçin")}</legend>
                    <div class="bk-chips" data-bk-services>
                        ${services.map((s) => `<label class="bk-chip"><input type="radio" name="ServiceId" value="${s.id}" ${s.id === selected ? "checked" : ""} required /><span><i class="fa-solid fa-screwdriver-wrench"></i> ${esc(s.name)}</span></label>`).join("")}
                    </div>
                </fieldset>
                <fieldset class="bk-step">
                    <legend><span class="bk-step__num">2</span> ${t("Gün seçin")}</legend>
                    <div class="bk-days" data-bk-days>
                        ${days.map((d) => `<label class="bk-day"><input type="radio" name="bkDay" value="${iso(d)}" ${iso(d) === iso(firstDay) ? "checked" : ""} /><span><small>${dayLabel(d)}</small><b>${d.getDate()}</b><small>${monthLabel(d)}</small></span></label>`).join("")}
                    </div>
                </fieldset>
                <fieldset class="bk-step">
                    <legend><span class="bk-step__num">3</span> ${t("Saat seçin")}</legend>
                    <div class="bk-slots" data-bk-slots aria-live="polite">
                        <p class="bk-slots__hint"><i class="fa-regular fa-hand-pointer"></i> ${t("Boş saatları görmək üçün xidmət seçin.")}</p>
                    </div>
                    <div class="bk-legend" aria-hidden="true">
                        <span><i class="is-free"></i> ${t("Boş")}</span><span><i class="is-busy"></i> ${t("Tutulub")}</span><span><i class="is-selected"></i> ${t("Seçdiyiniz")}</span>
                    </div>
                    <div class="bk-notice" data-bk-notice hidden role="alert"></div>
                </fieldset>
                <fieldset class="bk-step">
                    <legend><span class="bk-step__num">4</span> ${t("Əlaqə məlumatları")}</legend>
                    <div class="bk-fields">
                        <div class="bk-field">
                            <label for="FullName">${t("Ad Soyad")}</label>
                            <div class="bk-input"><i class="fa-regular fa-user"></i><input id="FullName" name="FullName" maxlength="100" autocomplete="name" required placeholder="${t("Adınız və soyadınız")}" /></div>
                        </div>
                        <div class="bk-field">
                            <label for="phoneInput">${t("Telefon")}</label>
                            <div class="bk-input"><i class="fa-solid fa-phone"></i><input name="Phone" id="phoneInput" type="tel" autocomplete="tel" required placeholder="+994 XX XXX XX XX" /></div>
                        </div>
                        <div class="bk-field bk-field--full">
                            <label class="bk-check" for="sameAsPhone"><input type="checkbox" id="sameAsPhone" /><span>${t("WhatsApp nömrəsi telefonla eyni olsun")}</span></label>
                            <div class="bk-input"><i class="fa-brands fa-whatsapp"></i><input name="WhatsApp" id="whatsappInput" type="tel" required placeholder="WhatsApp: +994 XX XXX XX XX" /></div>
                        </div>
                        <div class="bk-field bk-field--full">
                            <label for="Note">${t("Qeyd")} <small>${t("(ixtiyari)")}</small></label>
                            <textarea id="Note" name="Note" rows="2" maxlength="500" placeholder="${t("Məs: avtomobil modeli, ili, mühərrik həcmi")}"></textarea>
                        </div>
                    </div>
                </fieldset>
                <input type="hidden" name="DateTime" data-bk-datetime />
                <div class="bk-submit">
                    <div class="bk-summary" data-bk-summary><i class="fa-regular fa-calendar-check"></i><span>${t("Xidmət, gün və saat seçin")}</span></div>
                    <button type="submit" class="bk-btn bk-btn--primary">${t("Rezerv et")} <i class="fa-solid fa-arrow-right"></i></button>
                </div>
                <div id="reservationMessage" role="status" aria-live="polite"></div>
            </form>
            <div class="bk-done" data-bk-done hidden>
                <div class="bk-done__icon"><i class="fa-solid fa-check"></i></div>
                <h3>${t("Rezervasiyanız göndərildi!")}</h3>
                <p data-bk-done-summary></p>
                <div data-bk-done-tracker></div>
                <div class="bk-done__actions">
                    <a href="#" class="bk-btn bk-btn--ghost" data-bk-done-link><i class="fa-solid fa-link"></i> ${t("İzləmə linki")}</a>
                    <button type="button" class="bk-btn bk-btn--primary" data-bk-again><i class="fa-solid fa-plus"></i> ${t("Yeni rezervasiya")}</button>
                </div>
                <small class="bk-done__hint">${t("Bu səhifəni bağlasanız da, statusu \"Rezervasiyalarım\" bölməsindən və ya izləmə linkindən görə bilərsiniz.")}</small>
            </div>
        </div>
    </div></div>
</section>`;
    }

    DE.ui = { pageHeader, slider, marquee, informCards, aboutHero, serviceGrid, bookingForm, team, videoFrame };
})();
