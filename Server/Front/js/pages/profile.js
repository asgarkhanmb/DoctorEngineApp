/* Hesab ayarları: profil (şəkil, ad, istifadəçi adı), email dəyişikliyi, şifrə (API: auth/me, profile/*) */
DE.page({
    title: "Hesab ayarları",
    auth: "user",
    async render() {
        const me = await DE.api.get("/api/v1/auth/me");
        draw(me);
    }
});

function draw(me) {
    const esc = DE.esc, t = (s) => esc(DE.t(s));
    const initial = (me.fullName || "?").trim().slice(0, 1).toUpperCase() || "?";
    const avatar = me.avatarUrl ? esc(DE.upload(me.avatarUrl)) : null;
    const row = (label, help, control) => `
        <div class="ds-row">
            <div class="ds-row__label"><h3>${label}</h3><p>${help}</p></div>
            <div class="ds-row__control">${control}</div>
        </div>`;
    const input = (name, icon, attrs, password) => `
        <div class="ds-input${password ? " ds-input--password" : ""}">
            <i class="${icon}"></i>
            <input id="${name.replace(".", "_")}" name="${name}" ${attrs} />
            ${password ? `<button type="button" class="ds-eye" aria-label="${t("Şifrəni göstər")}"><i class="fa-regular fa-eye"></i></button>` : ""}
        </div>
        <span class="ds-error" data-valmsg-for="${name}"></span>`;
    const label = (name, text) => `<label for="${name.replace(".", "_")}">${t(text)}</label>`;
    const alert = `<div class="ds-alert is-error" role="alert" data-error hidden></div>`;

    document.getElementById("de-main").innerHTML = `
        ${DE.ui.pageHeader("Hesab ayarları", "Profil məlumatlarınızı, email ünvanınızı və şifrənizi idarə edin.")}
        <div class="container py-5">
            <div class="ds">
                <aside class="ds-side">
                    <div class="ds-user">
                        ${avatar ? `<img class="ds-avatar" src="${avatar}" alt="" />` : `<span class="ds-avatar" aria-hidden="true">${esc(initial)}</span>`}
                        <div class="ds-user__text"><strong>${esc(me.fullName)}</strong><small>@${esc(me.userName)}</small></div>
                    </div>
                    <nav class="ds-nav" role="tablist" aria-label="${t("Ayarlar bölmələri")}">
                        <button type="button" class="ds-nav__item" role="tab" id="tab-profile" data-tab="profile" aria-controls="panel-profile" aria-selected="true">
                            <i class="fa-regular fa-user"></i><span><b>${t("Profil")}</b><small>${t("Şəkil, ad, istifadəçi adı")}</small></span>
                        </button>
                        <button type="button" class="ds-nav__item" role="tab" id="tab-email" data-tab="email" aria-controls="panel-email" aria-selected="false">
                            <i class="fa-regular fa-envelope"></i><span><b>Email</b><small>${t("Email ünvanının dəyişdirilməsi")}</small></span>
                        </button>
                        <button type="button" class="ds-nav__item" role="tab" id="tab-password" data-tab="password" aria-controls="panel-password" aria-selected="false">
                            <i class="fa-solid fa-shield-halved"></i><span><b>${t("Təhlükəsizlik")}</b><small>${t("Şifrənin dəyişdirilməsi")}</small></span>
                        </button>
                    </nav>
                    <div class="ds-logout">
                        <button type="button" class="ds-nav__item is-danger" data-de-logout><i class="fa-solid fa-arrow-right-from-bracket"></i><span><b>${t("Çıxış")}</b></span></button>
                    </div>
                </aside>

                <main class="ds-main">
                    <section class="ds-panel" id="panel-profile" role="tabpanel" aria-labelledby="tab-profile">
                        <header class="ds-panel__head"><h2>${t("Profil")}</h2><p>${t("Bu məlumatlar çatda və hesabınızda göstərilir.")}</p></header>
                        ${row(t("Profil şəkli"), t("JPG, PNG və ya WEBP, maksimum 5 MB."), `
                            <div class="ds-photo">
                                ${avatar ? `<img class="ds-avatar ds-avatar--lg" src="${avatar}" alt="${t("Profil şəkli")}" />` : `<span class="ds-avatar ds-avatar--lg" aria-hidden="true">${esc(initial)}</span>`}
                                <div class="ds-photo__actions">
                                    <label class="ds-btn ds-btn--primary" for="avatarInput"><i class="fa-solid fa-camera"></i> ${avatar ? t("Şəkli dəyiş") : t("Şəkil yüklə")}</label>
                                    <input type="file" name="photo" id="avatarInput" accept=".jpg,.jpeg,.png,.webp" class="visually-hidden" />
                                    ${avatar ? `<button type="button" class="ds-btn ds-btn--ghost" data-remove-avatar><i class="fa-regular fa-trash-can"></i> ${t("Sil")}</button>` : ""}
                                </div>
                            </div>`)}
                        <form novalidate data-form="info">
                            ${alert}
                            ${row(label("Info.FullName", "Ad Soyad"), t("Tam adınız."), input("Info.FullName", "fa-regular fa-id-card", `autocomplete="name" required maxlength="100" value="${esc(me.fullName)}" data-val-required="Ad və soyadı daxil edin"`))}
                            ${row(label("Info.UserName", "İstifadəçi adı"), t("Latın hərfləri, rəqəm və . _ - (3–30 simvol)."), input("Info.UserName", "fa-solid fa-at", `autocomplete="username" required minlength="3" maxlength="30" pattern="[a-zA-Z0-9._\\-]+" value="${esc(me.userName)}" data-val-required="İstifadəçi adını daxil edin" data-val-length="İstifadəçi adı 3–30 simvol olmalıdır" data-val-pattern="Yalnız latın hərfləri, rəqəm və . _ - istifadə edin"`))}
                            <footer class="ds-panel__foot"><button type="submit" class="ds-btn ds-btn--primary"><i class="fa-solid fa-floppy-disk"></i> ${t("Yadda saxla")}</button></footer>
                        </form>
                    </section>

                    <section class="ds-panel" id="panel-email" role="tabpanel" aria-labelledby="tab-email" hidden>
                        <header class="ds-panel__head"><h2>${t("Email ünvanı")}</h2><p>${t("Yeni ünvana təsdiq linki göndəriləcək. Email yalnız linkə klik etdikdən sonra dəyişəcək.")}</p></header>
                        ${row(t("Hazırkı email"), t("Giriş və bildirişlər üçün."), `
                            <div class="ds-current"><i class="fa-regular fa-envelope"></i><span>${esc(me.email)}</span><em class="ds-badge"><i class="fa-solid fa-check"></i> ${t("Təsdiqlənib")}</em></div>`)}
                        <form novalidate data-form="email">
                            ${alert}
                            ${row(label("Email.NewEmail", "Yeni email"), t("Təsdiq linki bu ünvana gələcək."), input("Email.NewEmail", "fa-regular fa-envelope", `type="email" autocomplete="email" required placeholder="${t("yeni@email.com")}" data-val-required="Yeni email ünvanını daxil edin"`))}
                            ${row(label("Email.CurrentPassword", "Hazırkı şifrə"), t("Təhlükəsizlik üçün şifrənizi təsdiqləyin."), input("Email.CurrentPassword", "fa-solid fa-lock", `type="password" autocomplete="current-password" required data-val-required="Hazırkı şifrəni daxil edin"`, true))}
                            <footer class="ds-panel__foot"><button type="submit" class="ds-btn ds-btn--primary"><i class="fa-regular fa-paper-plane"></i> ${t("Təsdiq linki göndər")}</button></footer>
                        </form>
                    </section>

                    <section class="ds-panel" id="panel-password" role="tabpanel" aria-labelledby="tab-password" hidden>
                        <header class="ds-panel__head"><h2>${t("Təhlükəsizlik")}</h2><p>${t("Güclü şifrə: böyük və kiçik hərf, rəqəm və xüsusi simvol.")}</p></header>
                        <form novalidate data-form="password">
                            ${alert}
                            ${row(label("Password.CurrentPassword", "Hazırkı şifrə"), t("İndi istifadə etdiyiniz şifrə."), input("Password.CurrentPassword", "fa-solid fa-lock", `type="password" autocomplete="current-password" required data-val-required="Hazırkı şifrəni daxil edin"`, true))}
                            ${row(label("Password.NewPassword", "Yeni şifrə"), t("Minimum 6 simvol."), input("Password.NewPassword", "fa-solid fa-key", `type="password" autocomplete="new-password" required minlength="6" maxlength="100" data-val-required="Yeni şifrəni daxil edin" data-val-length="Şifrə minimum 6 simvol olmalıdır"`, true) + `
                                <ul class="ds-rules" id="pwRules" aria-label="${t("Şifrə tələbləri")}">
                                    <li data-rule="length">${t("6+ simvol")}</li><li data-rule="upper">A–Z</li><li data-rule="lower">a–z</li><li data-rule="digit">0–9</li><li data-rule="symbol">! @ # $</li>
                                </ul>`)}
                            ${row(label("Password.ConfirmPassword", "Şifrə təkrarı"), t("Yeni şifrəni təkrar yazın."), input("Password.ConfirmPassword", "fa-solid fa-key", `type="password" autocomplete="new-password" required data-match="Password.NewPassword" data-val-required="Şifrəni təkrar daxil edin"`, true))}
                            <footer class="ds-panel__foot"><button type="submit" class="ds-btn ds-btn--primary"><i class="fa-solid fa-check"></i> ${t("Şifrəni yenilə")}</button></footer>
                        </form>
                    </section>
                </main>
            </div>
        </div>`;
    wire();
}

function wire() {
    const root = document.querySelector(".ds");
    const tabs = root.querySelectorAll("[data-tab]");
    const show = (name, updateHash) => {
        if (!["profile", "email", "password"].includes(name)) name = "profile";
        tabs.forEach((tb) => {
            const on = tb.dataset.tab === name;
            tb.classList.toggle("is-active", on);
            tb.setAttribute("aria-selected", on ? "true" : "false");
            document.getElementById("panel-" + tb.dataset.tab).hidden = !on;
        });
        if (updateHash) history.replaceState(null, "", "#" + name);
    };
    tabs.forEach((tb) => tb.addEventListener("click", () => show(tb.dataset.tab, true)));
    show(location.hash.replace("#", ""), false);

    root.querySelectorAll(".ds-eye").forEach((btn) => btn.addEventListener("click", () => {
        const input = btn.parentElement.querySelector("input");
        const hidden = input.type === "password";
        input.type = hidden ? "text" : "password";
        btn.querySelector("i").className = hidden ? "fa-regular fa-eye-slash" : "fa-regular fa-eye";
        btn.setAttribute("aria-label", DE.t(hidden ? "Şifrəni gizlət" : "Şifrəni göstər"));
    }));

    const pw = document.getElementById("Password_NewPassword"), rules = document.getElementById("pwRules");
    const tests = { length: (v) => v.length >= 6, upper: (v) => /[A-Z]/.test(v), lower: (v) => /[a-z]/.test(v), digit: (v) => /[0-9]/.test(v), symbol: (v) => /[^A-Za-z0-9]/.test(v) };
    pw.addEventListener("input", () => rules.querySelectorAll("[data-rule]").forEach((li) => li.classList.toggle("is-ok", tests[li.dataset.rule](pw.value))));

    // Yenidən çək (profil dəyişəndə header-dəki ad / şəkil də yenilənsin)
    const reload = async (message, tab) => {
        await DE.auth.reload();
        DE.flash(message, "success");
        location.hash = tab || "profile";
        location.reload();
    };

    // Profil şəkli: seçilən kimi yüklənir
    const file = document.getElementById("avatarInput");
    file.addEventListener("change", async () => {
        const f = file.files && file.files[0];
        if (!f) return;
        if (f.size > 5 * 1024 * 1024) { DE.toast(DE.t("Şəkil ölçüsü maksimum 5 MB ola bilər."), "error"); file.value = ""; return; }
        const fd = new FormData();
        fd.append("photo", f);
        try { await DE.api.form("/api/v1/profile/avatar", fd); await reload(DE.t("Profil şəkliniz yeniləndi.")); }
        catch (e) { DE.toast(e.message, "error"); file.value = ""; }
    });
    const remove = root.querySelector("[data-remove-avatar]");
    if (remove) remove.addEventListener("click", async () => {
        try { await DE.api.del("/api/v1/profile/avatar"); await reload(DE.t("Profil şəkli silindi.")); }
        catch (e) { DE.toast(e.message, "error"); }
    });

    DE.forms.bind(root.querySelector("[data-form=info]"), async (d) => {
        // API köhnə çat mesajlarını da yeni istifadəçi adına keçirir
        await DE.api.put("/api/v1/profile", { fullName: d["Info.FullName"].trim(), userName: d["Info.UserName"].trim() });
        await reload(DE.t("Profil məlumatlarınız yeniləndi."), "profile");
    });

    DE.forms.bind(root.querySelector("[data-form=email]"), async (d, form) => {
        try {
            const res = await DE.api.post("/api/v1/profile/email", { newEmail: d["Email.NewEmail"].trim(), currentPassword: d["Email.CurrentPassword"] });
            form.reset();
            DE.toast(res.message, "success");
        } catch (e) {
            if (e instanceof DE.ApiError && e.status === 400) {
                DE.forms.showFieldError(form, e.message === DE.t("Şifrə yanlışdır") ? "Email.CurrentPassword" : "Email.NewEmail", e.message);
                return;
            }
            throw e;
        }
    });

    DE.forms.bind(root.querySelector("[data-form=password]"), async (d, form) => {
        try {
            await DE.api.post("/api/v1/profile/password", { currentPassword: d["Password.CurrentPassword"], newPassword: d["Password.NewPassword"] });
        } catch (e) {
            if (e instanceof DE.ApiError && e.status === 400) {
                const current = /hazırkı|yanlış|incorrect|current|неверн|текущ|mevcut|yanlış/i.test(e.message);
                DE.forms.showFieldError(form, current ? "Password.CurrentPassword" : "Password.NewPassword", e.message);
                return;
            }
            throw e;
        }
        // Şifrə dəyişəndə köhnə access token etibarsız olur — dərhal yenisini al
        await reload(DE.t("Şifrəniz uğurla dəyişdirildi."), "password");
    });
}
