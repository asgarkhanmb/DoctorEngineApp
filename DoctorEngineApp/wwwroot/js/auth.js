/* =====================================================
   DoctorEngine.Az — Giriş / Qeydiyyat UX
   ===================================================== */
(function () {
    "use strict";

    // ---------- Şifrəni göstər / gizlət ----------
    document.querySelectorAll("[data-toggle-password]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const input = document.getElementById(btn.dataset.togglePassword);
            if (!input) return;
            const show = input.type === "password";
            input.type = show ? "text" : "password";
            btn.setAttribute("aria-pressed", String(show));
            btn.setAttribute("aria-label", show ? "Şifrəni gizlət" : "Şifrəni göstər");
            const icon = btn.querySelector("i");
            if (icon) icon.className = show ? "fa-regular fa-eye-slash" : "fa-regular fa-eye";
            input.focus({ preventScroll: true });
        });
    });

    // ---------- Caps Lock xəbərdarlığı ----------
    document.querySelectorAll("input[type=password]").forEach((input) => {
        const hint = document.querySelector('[data-caps-for="' + input.id + '"]');
        if (!hint) return;
        const check = (e) => {
            if (e.getModifierState) hint.classList.toggle("is-on", e.getModifierState("CapsLock"));
        };
        input.addEventListener("keydown", check);
        input.addEventListener("keyup", check);
        input.addEventListener("blur", () => hint.classList.remove("is-on"));
    });

    // ---------- Şifrə qaydaları (serverdəki Identity qaydaları ilə eyni) ----------
    const RULES = {
        length: (v) => v.length >= 6,
        upper: (v) => /[A-Z]/.test(v),
        lower: (v) => /[a-z]/.test(v),
        digit: (v) => /\d/.test(v),
        symbol: (v) => /[^A-Za-z0-9]/.test(v)
    };

    document.querySelectorAll("[data-password-rules]").forEach((list) => {
        const input = document.getElementById(list.dataset.passwordRules);
        const meter = document.querySelector('[data-meter-for="' + list.dataset.passwordRules + '"]');
        if (!input) return;

        const update = () => {
            let passed = 0;
            list.querySelectorAll("[data-rule]").forEach((li) => {
                const ok = RULES[li.dataset.rule](input.value);
                li.classList.toggle("is-ok", ok);
                if (ok) passed++;
            });
            if (meter) meter.dataset.level = input.value ? String(passed) : "0";
        };

        input.addEventListener("input", update);
        update();
    });

    // ---------- Şifrə təkrarı uyğunluğu ----------
    document.querySelectorAll("[data-match-for]").forEach((hint) => {
        const confirm = document.getElementById(hint.dataset.matchFor);
        const original = document.getElementById(hint.dataset.matchWith);
        if (!confirm || !original) return;

        const update = () => {
            const has = confirm.value.length > 0;
            const ok = has && confirm.value === original.value;
            hint.classList.toggle("is-ok", ok);
            hint.classList.toggle("is-bad", has && !ok);
            hint.querySelector("span").textContent = ok ? "Şifrələr eynidir" : "Şifrələr eyni deyil";
            hint.querySelector("i").className = ok ? "fa-solid fa-circle-check" : "fa-solid fa-circle-xmark";
        };

        confirm.addEventListener("input", update);
        original.addEventListener("input", () => { if (confirm.value) update(); });
    });

    // ---------- Göndərərkən yüklənmə vəziyyəti ----------
    document.querySelectorAll("form[data-loading-text]").forEach((form) => {
        form.addEventListener("submit", (e) => {
            if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;
            const btn = form.querySelector("button[type=submit]");
            if (!btn) return;
            if (btn.disabled) { e.preventDefault(); return; }
            btn.dataset.label = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = '<span class="auth-spinner" aria-hidden="true"></span>';
            btn.append(document.createTextNode(form.dataset.loadingText));
        });
    });

    window.addEventListener("pageshow", () => {
        document.querySelectorAll("form[data-loading-text] button[type=submit][data-label]").forEach((btn) => {
            btn.innerHTML = btn.dataset.label;
            btn.disabled = false;
        });
    });

    // ---------- İlk xətalı sahəyə fokus ----------
    const firstError = document.querySelector(".input-validation-error");
    if (firstError) firstError.focus();
})();
