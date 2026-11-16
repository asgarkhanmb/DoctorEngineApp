/* =====================================================
   DoctorEngine — müasir bildirişlər və təsdiq pəncərəsi
   Brauzerin boz alert()/confirm() pəncərələri əvəzinə saytın üslubunda.

   deUI.toast("Yadda saxlanıldı", "success")          // success | error | warning | info
   const ok = await deUI.confirm({ title: "Silinsin?", text: "Geri qaytarıla bilməz.", okText: "Sil", danger: true });

   Server mesajları (TempData["Toast"] / ["ToastError"]) layout-da
   <div data-de-toast="..." data-type="..."> kimi yazılır və səhifə açılanda göstərilir.
   Təhlükəsizlik: mətnlər yalnız textContent ilə yazılır.
   ===================================================== */
(function () {
    "use strict";
    if (window.deUI) return;

    const ICONS = {
        success: "fa-solid fa-circle-check",
        error: "fa-solid fa-circle-exclamation",
        warning: "fa-solid fa-triangle-exclamation",
        info: "fa-solid fa-circle-info"
    };

    function el(tag, cls, text) {
        const n = document.createElement(tag);
        if (cls) n.className = cls;
        if (text != null) n.textContent = text;
        return n;
    }

    function icon(cls) {
        const i = el("i", cls);
        i.setAttribute("aria-hidden", "true");
        return i;
    }

    // ---------- Toast ----------
    let stack = null;
    function getStack() {
        if (!stack || !document.body.contains(stack)) {
            stack = el("div", "de-toasts");
            stack.setAttribute("aria-live", "polite");
            document.body.appendChild(stack);
        }
        return stack;
    }

    function toast(message, type = "info", opts = {}) {
        if (!message) return;
        type = ICONS[type] ? type : "info";
        const node = el("div", "de-toast is-" + type);
        node.setAttribute("role", type === "error" ? "alert" : "status");

        node.appendChild(icon(ICONS[type] + " de-toast__icon"));
        const body = el("div", "de-toast__body");
        if (opts.title) body.appendChild(el("b", null, opts.title));
        body.appendChild(el("span", null, message));
        node.appendChild(body);

        const close = el("button", "de-toast__close");
        close.type = "button";
        close.setAttribute("aria-label", deT("Bağla"));
        close.appendChild(icon("fa-solid fa-xmark"));
        node.appendChild(close);

        const bar = el("span", "de-toast__bar");
        node.appendChild(bar);

        const duration = opts.duration ?? (type === "error" ? 7000 : 4500);
        bar.style.animationDuration = duration + "ms";

        let timer = 0;
        const hide = () => {
            clearTimeout(timer);
            node.classList.add("is-leaving");
            setTimeout(() => node.remove(), 260);
        };
        close.addEventListener("click", hide);
        // Üzərinə gələndə vaxt dayansın
        node.addEventListener("mouseenter", () => { clearTimeout(timer); node.classList.add("is-paused"); });
        node.addEventListener("mouseleave", () => { node.classList.remove("is-paused"); timer = setTimeout(hide, 1800); });

        getStack().appendChild(node);
        requestAnimationFrame(() => node.classList.add("is-visible"));
        timer = setTimeout(hide, duration);
        return { close: hide };
    }

    // ---------- Təsdiq pəncərəsi ----------
    function confirmDialog(opts = {}) {
        const o = Object.assign({
            title: deT("Əminsiniz?"),
            text: "",
            okText: deT("Bəli"),
            cancelText: deT("Ləğv et"),
            danger: false,
            icon: null
        }, typeof opts === "string" ? { title: opts } : opts);

        return new Promise((resolve) => {
            const previousFocus = document.activeElement;
            const backdrop = el("div", "de-dialog-backdrop");
            const dialog = el("div", "de-dialog" + (o.danger ? " is-danger" : ""));
            dialog.setAttribute("role", "alertdialog");
            dialog.setAttribute("aria-modal", "true");
            const titleId = "deDlgT" + Date.now();
            dialog.setAttribute("aria-labelledby", titleId);

            const ic = el("div", "de-dialog__icon");
            ic.appendChild(icon(o.icon || (o.danger ? "fa-solid fa-trash-can" : "fa-solid fa-circle-question")));
            const h = el("h3", "de-dialog__title", o.title);
            h.id = titleId;
            dialog.append(ic, h);
            if (o.text) dialog.appendChild(el("p", "de-dialog__text", o.text));

            const actions = el("div", "de-dialog__actions");
            const cancel = el("button", "de-dialog__btn is-light", o.cancelText);
            cancel.type = "button";
            const ok = el("button", "de-dialog__btn " + (o.danger ? "is-danger" : "is-primary"), o.okText);
            ok.type = "button";
            actions.append(cancel, ok);
            dialog.appendChild(actions);
            backdrop.appendChild(dialog);
            document.body.appendChild(backdrop);
            document.body.classList.add("de-dialog-open");

            const finish = (value) => {
                document.removeEventListener("keydown", onKey, true);
                backdrop.classList.remove("is-visible");
                document.body.classList.remove("de-dialog-open");
                setTimeout(() => backdrop.remove(), 200);
                if (previousFocus && previousFocus.focus) previousFocus.focus({ preventScroll: true });
                resolve(value);
            };

            const onKey = (e) => {
                if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); finish(false); }
                if (e.key === "Tab") {   // fokus pəncərədən çıxmasın
                    const f = [cancel, ok];
                    const i = f.indexOf(document.activeElement);
                    e.preventDefault();
                    f[(i + (e.shiftKey ? -1 : 1) + f.length) % f.length].focus();
                }
            };

            cancel.addEventListener("click", () => finish(false));
            ok.addEventListener("click", () => finish(true));
            backdrop.addEventListener("click", (e) => { if (e.target === backdrop) finish(false); });
            document.addEventListener("keydown", onKey, true);

            requestAnimationFrame(() => {
                backdrop.classList.add("is-visible");
                (o.danger ? cancel : ok).focus();   // təhlükəli əməliyyatda standart — Ləğv et
            });
        });
    }

    window.deUI = { toast, confirm: confirmDialog };

    // ---------- Serverdən gələn mesajlar ----------
    function flushServerToasts() {
        document.querySelectorAll("[data-de-toast]").forEach((n) => {
            toast(n.getAttribute("data-de-toast"), n.getAttribute("data-type") || "info");
            n.remove();
        });
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", flushServerToasts);
    else flushServerToasts();

    // ---------- İnternet bağlantısı ----------
    window.addEventListener("offline", () => toast(deT("İnternet bağlantısı kəsildi. Bərpa olunanda səhifə yenidən işləyəcək."), "warning", { duration: 6000 }));
    window.addEventListener("online", () => toast(deT("İnternet bağlantısı bərpa olundu."), "success", { duration: 3000 }));

    // ---------- Service worker: push + server işləməyəndə "offline" səhifəsi ----------
    if ("serviceWorker" in navigator && window.isSecureContext) {
        window.addEventListener("load", () => {
            navigator.serviceWorker.register("/sw.js").catch(() => { /* dəstəklənmir */ });
        });
    }
})();
