/* =====================================================
   DoctorEngine — giriş / qeydiyyat / şifrə səhifələrinin çərçivəsi (köhnə _AuthLayout.cshtml)
   DE.authPage({ title, guestOnly, render: () => "<kartın HTML-i>", after: () => {...} })
   ===================================================== */
(function () {
    "use strict";
    const DE = window.DE;
    const esc = DE.esc, t = (s) => esc(DE.t(s));

    function shell(site, card) {
        const brand = DE.brandIcon(site);
        return `
    <div class="auth-shell">
        <aside class="auth-aside" aria-hidden="true">
            <a href="/" class="auth-brand auth-brand--light" tabindex="-1"><span class="auth-brand__icon">${brand}</span> ${esc(site.name)}</a>
            <div class="auth-aside__content">
                <span class="auth-aside__eyebrow">${t("Mühərrik təmiri mərkəzi")}</span>
                <h2>${t("Mühərrikiniz")}<br />${t("etibarlı əllərdə.")}</h2>
                <p>${t("Hesabınızla usta ilə canlı yazışın, şəkil və video göndərin, rezervasiyalarınızı rahat idarə edin.")}</p>
                <ul class="auth-perks">
                    <li><i class="fa-regular fa-comments"></i><span><strong>${t("Canlı usta çatı")}</strong>${t("Suallarınıza real vaxtda cavab")}</span></li>
                    <li><i class="fa-regular fa-images"></i><span><strong>${t("Şəkil və video")}</strong>${t("Problemi göstərin — daha dəqiq diaqnoz")}</span></li>
                    <li><i class="fa-regular fa-calendar-check"></i><span><strong>${t("Onlayn rezervasiya")}</strong>${t("Növbə gözləmədən xidmət")}</span></li>
                </ul>
            </div>
            <small class="auth-aside__foot">© ${new Date().getFullYear()} ${esc(site.name)}</small>
        </aside>
        <main class="auth-main">
            <div class="auth-top">
                <a href="/" class="auth-brand auth-brand--mobile"><span class="auth-brand__icon">${brand}</span><span class="auth-brand__name">${esc(site.name)}</span></a>
                <a href="/" class="auth-back" aria-label="${t("Ana səhifəyə qayıt")}"><i class="fa-solid fa-arrow-left" aria-hidden="true"></i> <span class="auth-back__text">${t("Ana səhifə")}</span></a>
                <details class="auth-lang">
                    <summary aria-label="${t("lang.label")}"><i class="fa-solid fa-globe" aria-hidden="true"></i> ${DE.lang.toUpperCase()}</summary>
                    <div class="auth-lang__menu" data-no-i18n>
                        ${DE.i18n.languages.map(([code, name]) => `<a href="#" data-de-lang="${code}" class="${code === DE.lang ? "is-active" : ""}" lang="${code}" hreflang="${code}"><b>${code.toUpperCase()}</b> ${esc(name)}</a>`).join("")}
                    </div>
                </details>
            </div>
            <div class="auth-card">${card}</div>
        </main>
    </div>`;
    }

    /** Sahə: label + ikon + input + xəta yeri. type=password olanda göz düyməsi və Caps Lock xəbərdarlığı. */
    DE.authField = function (o) {
        const isPwd = o.type === "password";
        const attrs = o.attrs || "";
        return `
    <div class="auth-field">
        <label for="${o.name}">${esc(o.label)}</label>
        <div class="auth-input${isPwd ? " auth-input--password" : ""}">
            <i class="${o.icon}"></i>
            <input type="${o.type || "text"}" id="${o.name}" name="${o.name}" placeholder="${esc(o.placeholder || "")}" ${attrs} />
            ${isPwd ? `<button type="button" class="auth-eye" data-toggle-password="${o.name}" aria-label="${t("Şifrəni göstər")}" aria-pressed="false"><i class="fa-regular fa-eye"></i></button>` : ""}
        </div>
        <span class="field-validation-valid" data-valmsg-for="${o.name}"></span>
        ${isPwd ? `<small class="auth-caps" data-caps-for="${o.name}"><i class="fa-solid fa-triangle-exclamation"></i> ${t("Caps Lock açıqdır")}</small>` : ""}
        ${o.after || ""}
    </div>`;
    };

    /** Şifrə tələbləri (auth.js canlı yoxlayır) */
    DE.passwordRules = (name) => `
        <div class="auth-meter" data-meter-for="${name}" data-level="0" aria-hidden="true"><span></span><span></span><span></span><span></span><span></span></div>
        <ul class="auth-rules" data-password-rules="${name}" aria-label="${t("Şifrə tələbləri")}">
            <li data-rule="length">${t("Minimum 6 simvol")}</li>
            <li data-rule="upper">${t("Böyük hərf (A–Z)")}</li>
            <li data-rule="lower">${t("Kiçik hərf (a–z)")}</li>
            <li data-rule="digit">${t("Rəqəm (0–9)")}</li>
            <li data-rule="symbol">${t("Xüsusi simvol (! @ # $)")}</li>
        </ul>`;

    /** Yalnız saytın öz səhifəsinə qayıt (açıq yönləndirmə olmasın) */
    DE.safeReturnUrl = function (url) {
        if (!url || !url.startsWith("/") || url.startsWith("//") || url.startsWith("/\\")) return "/";
        return url;
    };

    DE.authPage = async function (opts) {
        if (opts.guestOnly && DE.auth.isAuthenticated()) {
            location.replace(DE.safeReturnUrl(DE.qs("returnUrl")));
            return;
        }
        let site = { name: "DoctorEngine.az" };
        try {
            const [, home] = await Promise.all([DE.i18n.ready, DE.home()]);
            site = (home && home.site) || site;
        } catch (e) { await DE.i18n.ready; }

        document.title = DE.t(opts.title) + " — " + site.name;
        document.getElementById("de-auth").innerHTML = shell(site, opts.render());
        DE.i18n.translate(document.body);
        await DE.loadScripts(["/js/auth.js", "/js/de-ui.js", "/js/no-autocomplete.js"]);
        if (opts.after) await opts.after();
        try {
            const list = JSON.parse(sessionStorage.getItem("de_flash") || "[]");
            sessionStorage.removeItem("de_flash");
            list.forEach((f) => window.deUI && window.deUI.toast(f.message, f.type));
        } catch (e) { /* */ }
        const first = document.querySelector(".auth-card [autofocus]");
        if (first) first.focus();
    };
})();
