/* =====================================================
   Hesab səhifələri: giriş, qeydiyyat, şifrəni unutdum / yenilə, email təsdiqi.
   Hər səhifə <body data-account="login|register|..."> ilə hansının açılacağını bildirir.
   Hamısı API-dədir: api/v1/auth/*
   ===================================================== */
(function () {
    "use strict";
    const esc = DE.esc, t = (s) => esc(DE.t(s));
    const F = DE.authField;
    const page = document.body.dataset.account;
    const head = (icon, title, text) => `
        <div class="auth-head">
            <div class="auth-head__icon"><i class="${icon}"></i></div>
            <h1>${t(title)}</h1>
            <p>${t(text)}</p>
        </div>`;
    const errorBox = `<div class="auth-alert auth-alert--error" role="alert" data-error hidden></div>`;

    const pages = {
        // ---------------- GİRİŞ ----------------
        login: {
            title: "Daxil ol", guestOnly: true,
            render: () => `
                ${head("fa-solid fa-right-to-bracket", "Xoş gəlmisiniz", "Hesabınıza daxil olun və usta ilə əlaqədə qalın.")}
                <form class="auth-form" novalidate>
                    ${errorBox}
                    ${F({ name: "EmailOrUsername", label: DE.t("Email və ya istifadəçi adı"), icon: "fa-regular fa-user", placeholder: "ad@numune.az", attrs: `autocomplete="off" autofocus required data-val-required="Email və ya istifadəçi adını daxil edin"` })}
                    ${F({ name: "Password", type: "password", label: DE.t("Şifrə"), icon: "fa-solid fa-lock", placeholder: "••••••••", attrs: `autocomplete="current-password" required data-val-required="Şifrəni daxil edin"` })}
                    <div class="auth-options">
                        <label class="auth-check"><input type="checkbox" id="RememberMe" name="RememberMe" value="true" /> ${t("Məni xatırla")}</label>
                        <a href="/Account/ForgotPassword">${t("Şifrəni unutmusunuz?")}</a>
                    </div>
                    <button type="submit" class="auth-btn">${t("Daxil ol")} <i class="fa-solid fa-arrow-right"></i></button>
                </form>
                <div class="auth-switch">${t("Hesabınız yoxdur?")} <a href="/Account/Register">${t("Qeydiyyatdan keçin")}</a></div>`,
            after() {
                DE.forms.bind(document.querySelector(".auth-form"), async (d) => {
                    // API yoxlayır: şifrə, blok (5 səhv cəhd), email təsdiqi
                    const auth = await DE.api.post("/api/v1/auth/login", {
                        emailOrUsername: d.EmailOrUsername.trim(), password: d.Password, deviceName: DE.auth.deviceName
                    });
                    DE.auth.signIn(auth);
                    location.href = DE.safeReturnUrl(DE.qs("returnUrl"));
                });
            }
        },

        // ---------------- QEYDİYYAT ----------------
        register: {
            title: "Qeydiyyat", guestOnly: true,
            render: () => `
                ${head("fa-solid fa-user-plus", "Hesab yaradın", "Bir dəqiqəyə qeydiyyatdan keçin — usta ilə yazışın və rezervasiya edin.")}
                <form class="auth-form" novalidate>
                    ${errorBox}
                    ${F({ name: "FullName", label: DE.t("Ad Soyad"), icon: "fa-regular fa-id-card", placeholder: DE.t("Əli Məmmədov"), attrs: `autocomplete="off" autofocus required maxlength="100" data-val-required="Ad və soyadı daxil edin"` })}
                    <div class="auth-row">
                        ${F({ name: "UserName", label: DE.t("İstifadəçi adı"), icon: "fa-solid fa-at", placeholder: "ali.m", attrs: `autocomplete="off" autocapitalize="off" spellcheck="false" required minlength="3" maxlength="30" pattern="[a-zA-Z0-9._\\-]+" data-val-required="İstifadəçi adını daxil edin" data-val-length="İstifadəçi adı 3–30 simvol olmalıdır" data-val-pattern="Yalnız latın hərfləri, rəqəm və . _ - istifadə edin"` })}
                        ${F({ name: "Email", type: "email", label: "Email", icon: "fa-regular fa-envelope", placeholder: "ad@numune.az", attrs: `autocomplete="off" required data-val-required="Email ünvanını daxil edin"` })}
                    </div>
                    ${F({ name: "Password", type: "password", label: DE.t("Şifrə"), icon: "fa-solid fa-lock", placeholder: DE.t("Güclü şifrə yaradın"), attrs: `autocomplete="new-password" required minlength="6" maxlength="100" data-val-required="Şifrəni daxil edin" data-val-length="Şifrə minimum 6 simvol olmalıdır"`, after: DE.passwordRules("Password") })}
                    ${F({ name: "ConfirmPassword", type: "password", label: DE.t("Şifrə təkrarı"), icon: "fa-solid fa-lock", placeholder: DE.t("Şifrəni təkrar yazın"), attrs: `autocomplete="new-password" required data-match="Password" data-val-required="Şifrəni təkrar daxil edin"`, after: `<small class="auth-match" data-match-for="ConfirmPassword" data-match-with="Password"><i></i><span></span></small>` })}
                    <button type="submit" class="auth-btn">${t("Qeydiyyatdan keç")} <i class="fa-solid fa-arrow-right"></i></button>
                </form>
                <div class="auth-switch">${t("Artıq hesabınız var?")} <a href="/Account/Login">${t("Daxil olun")}</a></div>`,
            after() {
                DE.forms.bind(document.querySelector(".auth-form"), async (d) => {
                    const res = await DE.api.post("/api/v1/auth/register", {
                        fullName: d.FullName.trim(), userName: d.UserName.trim(), email: d.Email.trim(), password: d.Password
                    });
                    // Hesab yaradıldı; məktub getmədisə API bunu mesajda bildirir
                    const notSent = DE.t("Hesab yaradıldı, amma təsdiq məktubu göndərilmədi. Bir azdan «Şifrəni unutmusunuz» ilə yenidən cəhd edin.");
                    if (res && res.message === notSent) DE.flash(res.message, "error");
                    location.href = "/Account/VerifyEmail";
                });
            }
        },

        // ---------------- EMAILİ YOXLAYIN ----------------
        verify: {
            title: "Emailinizi yoxlayın",
            render: () => `
                ${head("fa-regular fa-envelope-open", "Emailinizi yoxlayın", "Qeydiyyat uğurla tamamlandı! Email ünvanınıza təsdiq linki göndərdik.")}
                <div class="auth-form">
                    <div class="auth-alert auth-alert--ok" role="status">
                        <i class="fa-solid fa-circle-check"></i>
                        <span>${esc(DE.t("Məktubdakı {0} linkinə klik edin, sonra hesabınıza daxil ola bilərsiniz.")).replace("{0}", `<strong>“${t("Təsdiqlə")}”</strong>`)}</span>
                    </div>
                    <small class="auth-hint"><i class="fa-regular fa-lightbulb"></i> ${t("Məktubu görmürsünüz? “Spam” və ya “Promotions” qovluğunu yoxlayın.")}</small>
                    <a class="auth-btn" href="/Account/Login">${t("Daxil ol")} <i class="fa-solid fa-arrow-right"></i></a>
                    <a class="auth-btn auth-btn--ghost" href="/">${t("Ana səhifəyə qayıt")}</a>
                </div>`
        },

        // ---------------- ŞİFRƏNİ UNUTDUM ----------------
        forgot: {
            title: "Şifrəni unutmusunuz?", guestOnly: true,
            render: () => `
                ${head("fa-solid fa-key", "Şifrəni unutmusunuz?", "Qeydiyyatdan keçdiyiniz email ünvanını yazın — şifrəni yeniləmək üçün link göndərəcəyik.")}
                <form class="auth-form" novalidate>
                    ${errorBox}
                    ${F({ name: "Email", type: "email", label: "Email", icon: "fa-regular fa-envelope", placeholder: "ad@numune.az", attrs: `autocomplete="off" autofocus required data-val-required="Email mütləqdir"` })}
                    <button type="submit" class="auth-btn">${t("Link göndər")} <i class="fa-regular fa-paper-plane"></i></button>
                </form>
                <div class="auth-switch"><a href="/Account/Login"><i class="fa-solid fa-arrow-left"></i> ${t("Girişə qayıt")}</a></div>`,
            after() {
                DE.forms.bind(document.querySelector(".auth-form"), async (d) => {
                    await DE.api.post("/api/v1/auth/forgot-password", { email: d.Email.trim() });
                    DE.flash(DE.t("Şifrə yeniləmə linki emailinizə göndərildi."), "success");
                    location.href = "/Account/Login";
                });
            }
        },

        // ---------------- YENİ ŞİFRƏ (məktubdakı link) ----------------
        reset: {
            title: "Yeni şifrə",
            render: () => `
                ${head("fa-solid fa-shield-halved", "Yeni şifrə təyin edin", "Hesabınız üçün yeni, güclü şifrə yaradın.")}
                <form class="auth-form" novalidate>
                    ${errorBox}
                    ${F({ name: "Password", type: "password", label: DE.t("Yeni şifrə"), icon: "fa-solid fa-lock", placeholder: DE.t("Yeni şifrə"), attrs: `autocomplete="new-password" autofocus required minlength="6" data-val-required="Yeni şifrə mütləqdir" data-val-length="Şifrə minimum 6 simvol olmalıdır"`, after: DE.passwordRules("Password") })}
                    ${F({ name: "ConfirmPassword", type: "password", label: DE.t("Şifrə təkrarı"), icon: "fa-solid fa-lock", placeholder: DE.t("Şifrəni təkrar yazın"), attrs: `autocomplete="new-password" required data-match="Password" data-val-required="Şifrə təkrarı mütləqdir"`, after: `<small class="auth-match" data-match-for="ConfirmPassword" data-match-with="Password"><i></i><span></span></small>` })}
                    <button type="submit" class="auth-btn">${t("Şifrəni yenilə")} <i class="fa-solid fa-check"></i></button>
                </form>
                <div class="auth-switch"><a href="/Account/Login"><i class="fa-solid fa-arrow-left"></i> ${t("Girişə qayıt")}</a></div>`,
            after() {
                const email = DE.qs("email"), token = DE.qs("token");
                const form = document.querySelector(".auth-form");
                if (!email || !token) {
                    DE.forms.showError(form, DE.t("Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin."));
                    form.querySelector("button[type=submit]").disabled = true;
                    return;
                }
                DE.forms.bind(form, async (d) => {
                    // API: şifrə yenilənir, blok sıfırlanır, tokenlər qaytarılır (avtomatik giriş)
                    const auth = await DE.api.post("/api/v1/auth/reset-password", { email, token, password: d.Password, deviceName: DE.auth.deviceName });
                    DE.auth.signIn(auth);
                    location.href = "/";
                });
            }
        },

        // ---------------- EMAIL TƏSDİQİ (məktubdakı link) ----------------
        confirm: {
            title: "Email təsdiqi",
            render: () => `
                ${head("fa-regular fa-envelope-open", "Email təsdiqi", "Email ünvanınız təsdiqlənir…")}
                <div class="auth-form" data-result><div class="text-center py-3"><span class="spinner-border" aria-hidden="true"></span></div></div>`,
            async after() {
                const userId = DE.qs("userId"), token = DE.qs("token");
                try {
                    if (!userId || !token) throw new DE.ApiError(400, DE.t("Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin."));
                    const res = await DE.api.post("/api/v1/auth/confirm-email", { userId, token });
                    DE.flash(res.message, "success");
                    location.replace("/Account/Login");
                } catch (e) {
                    document.querySelector("[data-result]").innerHTML = `
                        <div class="auth-alert auth-alert--error" role="alert"><i class="fa-solid fa-circle-exclamation"></i> <span>${esc(e.message)}</span></div>
                        <a class="auth-btn" href="/Account/Login">${t("Daxil ol")} <i class="fa-solid fa-arrow-right"></i></a>`;
                }
            }
        },

        // ---------------- EMAIL DƏYİŞİKLİYİNİN TƏSDİQİ (yeni emaildəki link) ----------------
        confirmChange: {
            title: "Email dəyişikliyinin təsdiqi",
            render: () => `
                ${head("fa-regular fa-envelope-open", "Email dəyişikliyinin təsdiqi", "Yeni email ünvanınız təsdiqlənir…")}
                <div class="auth-form" data-result><div class="text-center py-3"><span class="spinner-border" aria-hidden="true"></span></div></div>`,
            async after() {
                const userId = DE.qs("userId"), email = DE.qs("email"), token = DE.qs("token");
                const me = DE.auth.user();
                try {
                    if (!userId || !email || !token) throw new DE.ApiError(400, "");
                    await DE.api.post("/api/v1/auth/confirm-email-change", { userId, email, token });
                    if (me && me.id === userId) {
                        await DE.auth.reload();   // yeni email tokenə də düşsün
                        DE.flash(DE.t("Email ünvanınız uğurla dəyişdirildi."), "success");
                        location.replace("/Account/Profile#email");
                    } else {
                        DE.flash(DE.t("Email ünvanınız dəyişdirildi. Yeni email ilə daxil ola bilərsiniz."), "success");
                        location.replace("/Account/Login");
                    }
                } catch (e) {
                    DE.flash(DE.t("Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin."), "error");
                    location.replace(me ? "/Account/Profile#email" : "/Account/Login");
                }
            }
        }
    };

    const p = pages[page];
    if (p) DE.authPage(p);
})();
