/* =====================================================
   DoctorEngine — formalar
   DE.forms.bind(form, async (data, form) => { ... }, { files: true })
     - brauzer yoxlaması (required, type=email, minlength, maxlength, pattern, data-match="DigərSahə")
     - göndərmə zamanı düymə bloklanır ("Göndərilir…")
     - API xətası: sahə xətaları (errors) uyğun sahənin altına, ümumi mesaj [data-error]-a (yoxdursa — toast)
     - data: { sahə: dəyər } (files: true olanda FormData)
   ===================================================== */
(function () {
    "use strict";
    const DE = window.DE;

    function messageFor(input) {
        const v = input.validity;
        const label = (input.labels && input.labels[0] && input.labels[0].textContent.trim()) || input.name;
        // Fərdi mesajlar (azərbaycanca yazılır, tərcümə olunur): data-val-required, data-val-length, data-val-pattern, data-val-email
        const d = input.dataset;
        if (v.valueMissing) return d.valRequired ? DE.t(d.valRequired) : DE.t("{0} daxil edin", label);
        if (v.typeMismatch && input.type === "email") return DE.t(d.valEmail || "Email düzgün formatda deyil");
        if (v.tooShort) return d.valLength ? DE.t(d.valLength) : DE.t("Minimum {0} simvol olmalıdır", input.minLength);
        if (v.tooLong) return d.valLength ? DE.t(d.valLength) : DE.t("Maksimum {0} simvol ola bilər", input.maxLength);
        if (v.patternMismatch) return d.valPattern ? DE.t(d.valPattern) : DE.t("Düzgün formatda yazın");
        if (v.customError) return input.validationMessage;
        return input.validationMessage;
    }

    function slot(form, name) {
        return form.querySelector(`[data-valmsg-for="${CSS.escape(name)}"]`);
    }

    function clearErrors(form) {
        form.querySelectorAll("[data-valmsg-for]").forEach((s) => { s.textContent = ""; s.classList.remove("field-validation-error"); });
        form.querySelectorAll(".is-invalid").forEach((i) => i.classList.remove("is-invalid"));
        const box = form.querySelector("[data-error]");
        if (box) { box.hidden = true; box.textContent = ""; }
    }

    function showFieldError(form, name, message) {
        const input = form.elements[name];
        const s = slot(form, name);
        if (input && input.classList) input.classList.add("is-invalid");
        if (s) { s.textContent = message; s.classList.add("field-validation-error"); return true; }
        return false;
    }

    function showError(form, message) {
        const box = form.querySelector("[data-error]");
        if (box) { box.textContent = message; box.hidden = false; box.scrollIntoView({ block: "nearest", behavior: "smooth" }); }
        else DE.toast(message, "error");
    }

    /** API-nin sahə adları (fullName, Info.UserName, "") → formadakı ad (böyük/kiçik hərf fərqi nəzərə alınmır) */
    function matchField(form, key) {
        if (!key) return null;
        const names = Array.from(form.elements).map((e) => e.name).filter(Boolean);
        const k = key.toLowerCase();
        return names.find((n) => n.toLowerCase() === k)
            || names.find((n) => n.toLowerCase().endsWith("." + k))
            || names.find((n) => k.endsWith("." + n.toLowerCase()))
            || null;
    }

    function validate(form) {
        let ok = true;
        for (const input of form.elements) {
            if (!input.name || input.disabled || input.type === "hidden") continue;
            input.setCustomValidity("");
            const match = input.dataset.match;
            if (match && form.elements[match] && input.value !== form.elements[match].value)
                input.setCustomValidity(input.dataset.matchMessage || DE.t("Şifrələr eyni deyil"));
            if (!input.checkValidity()) {
                if (ok) input.focus();
                ok = false;
                if (!showFieldError(form, input.name, messageFor(input))) showError(form, messageFor(input));
            }
        }
        return ok;
    }

    function collect(form, asFormData) {
        const fd = new FormData(form);
        if (asFormData) return fd;
        const data = {};
        for (const [k, v] of fd.entries()) {
            if (v instanceof File) continue;
            const el = form.elements[k];
            data[k] = el && el.type === "checkbox" ? el.checked : v;
        }
        return data;
    }

    function bind(form, handler, opts) {
        if (!form) return;
        opts = opts || {};
        form.setAttribute("novalidate", "");
        form.addEventListener("input", (e) => {
            const name = e.target.name;
            if (!name) return;
            e.target.classList.remove("is-invalid");
            const s = slot(form, name);
            if (s) { s.textContent = ""; s.classList.remove("field-validation-error"); }
        });
        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            if (form.dataset.busy) return;
            clearErrors(form);
            if (opts.beforeValidate) opts.beforeValidate(form);
            if (!validate(form)) return;

            const btn = form.querySelector("button[type=submit]");
            const label = btn ? btn.innerHTML : "";
            form.dataset.busy = "1";
            if (btn) {
                btn.disabled = true;
                btn.innerHTML = `<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> ${DE.esc(DE.t("Göndərilir…"))}`;
            }
            try {
                await handler(collect(form, opts.files), form);
            } catch (err) {
                if (err instanceof DE.ApiError) {
                    let shownOnField = false;
                    if (err.errors) {
                        for (const [key, msgs] of Object.entries(err.errors)) {
                            const name = matchField(form, key);
                            const text = (msgs || []).join(" ");
                            if (name && text && showFieldError(form, name, text)) shownOnField = true;
                            else if (text && key === "") showError(form, text);
                        }
                    }
                    if (!shownOnField || !err.errors) showError(form, err.message);
                } else {
                    console.error(err);
                    showError(form, DE.t("Xəta baş verdi. Yenidən cəhd edin."));
                }
            } finally {
                delete form.dataset.busy;
                if (btn && document.body.contains(btn)) { btn.disabled = false; btn.innerHTML = label; }
            }
        });
    }

    DE.forms = { bind, showError, showFieldError, clearErrors };
})();
