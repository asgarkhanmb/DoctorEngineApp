/* =====================================================
   DoctorEngine — Üzən çat düyməsi və sağ panel
   - Düyməyə basanda sağda çat paneli açılır / bağlanır
   - Səhifədə çat artıq varsa (Əlaqə, Canlı Çat) — həmin çata sürüşdürür
   - chat.js "dc:unread" hadisəsi ilə oxunmamış sayını göndərir → badge + bildiriş
   Təhlükəsizlik: istifadəçi mətni yalnız textContent ilə yazılır.
   ===================================================== */
(function () {
    "use strict";

    const btn = document.querySelector(".dc-launcher");
    if (!btn) return;

    const drawer = document.getElementById("dcDrawer");
    const badge = btn.querySelector(".dc-launcher__badge");
    const baseTitle = document.title;
    const STORE_KEY = "dc-drawer-open";

    let open = false;
    let toast = null;
    let toastTimer = 0;

    // ---------- Panel ----------

    function setOpen(next, detail) {
        if (!drawer || next === open) return;
        open = next;
        drawer.classList.toggle("is-open", open);
        drawer.setAttribute("aria-hidden", String(!open));
        btn.classList.toggle("is-open", open);
        btn.setAttribute("aria-expanded", String(open));
        btn.setAttribute("aria-label", open ? deT("Çatı bağla") : deT("Canlı çatı aç"));
        document.body.classList.toggle("dc-drawer-open", open);
        if (open) hideToast();
        try { sessionStorage.setItem(STORE_KEY, open ? "1" : "0"); } catch (e) { /* yaddaş bağlıdır */ }
        document.dispatchEvent(new CustomEvent(open ? "dc:drawer-open" : "dc:drawer-close", { detail: detail || null }));
        if (!open) btn.focus({ preventScroll: true });
    }

    function goToInlineChat() {
        const chat = document.getElementById("dcChat");
        if (!chat) return;
        chat.scrollIntoView({ behavior: "smooth", block: "center" });
        const input = chat.querySelector(".dc-chat__input");
        if (input) setTimeout(() => input.focus({ preventScroll: true }), 450);
    }

    btn.addEventListener("click", () => {
        if (drawer) setOpen(!open);
        else goToInlineChat();
    });

    if (drawer) {
        drawer.querySelectorAll("[data-dc-close]").forEach((b) => b.addEventListener("click", () => setOpen(false)));

        document.addEventListener("keydown", (e) => {
            if (e.key !== "Escape" || !open) return;
            // Şəkil böyüdülübsə və ya səs yazılırsa, Esc əvvəlcə onları bağlasın
            const lightbox = document.querySelector(".dc-lightbox");
            if (lightbox && !lightbox.hidden) return;
            const recBar = drawer.querySelector(".dc-rec");
            if (recBar && !recBar.hidden) return;
            setOpen(false);
        });

        // Başqa səhifəyə keçəndə panel açıq idisə, açıq qalsın
        let wasOpen = false;
        try { wasOpen = sessionStorage.getItem(STORE_KEY) === "1"; } catch (e) { /* yox */ }
        if (wasOpen) {
            if (document.readyState === "complete") setOpen(true);
            else window.addEventListener("load", () => setOpen(true), { once: true });
        }
    }

    // ---------- Sistem bildirişinə basanda çatı aç ----------

    function openChat() {
        if (drawer) setOpen(true);
        else goToInlineChat();
    }

    // Sayt bağlı idi: bildiriş /?openChat=1 açır
    try {
        const url = new URL(window.location.href);
        if (url.searchParams.get("openChat") === "1") {
            url.searchParams.delete("openChat");
            history.replaceState(null, "", url.pathname + url.search + url.hash);
            if (document.readyState === "complete") openChat();
            else window.addEventListener("load", openChat, { once: true });
        }
    } catch (e) { /* köhnə brauzer */ }

    // Sayt açıq idi (arxa planda): service worker mesaj göndərir
    if ("serviceWorker" in navigator) {
        navigator.serviceWorker.addEventListener("message", (e) => {
            if (e.data && e.data.type === "dc:open-chat") openChat();
        });
    }

    // ---------- Bildiriş ----------

    function hideToast() {
        clearTimeout(toastTimer);
        if (toast) {
            const t = toast;
            toast = null;
            t.classList.remove("is-visible");
            setTimeout(() => t.remove(), 250);
        }
    }

    function showToast(title, text, isAdmin, conversation) {
        hideToast();

        const node = document.createElement("div");
        node.className = "dc-toast";
        node.setAttribute("role", "status");

        const ico = document.createElement("span");
        ico.className = "dc-toast__icon" + (isAdmin ? " is-admin" : "");
        const i = document.createElement("i");
        i.className = isAdmin ? "fa-solid fa-wrench" : "fa-solid fa-comment-dots";
        i.setAttribute("aria-hidden", "true");
        ico.appendChild(i);

        const body = document.createElement("span");
        body.className = "dc-toast__body";
        const b = document.createElement("b");
        b.textContent = title;
        const p = document.createElement("span");
        p.className = "dc-toast__text";
        p.textContent = text;
        body.append(b, p);

        const close = document.createElement("button");
        close.type = "button";
        close.className = "dc-toast__close";
        close.setAttribute("aria-label", deT("Bildirişi bağla"));
        const x = document.createElement("i");
        x.className = "fa-solid fa-xmark";
        x.setAttribute("aria-hidden", "true");
        close.appendChild(x);
        close.addEventListener("click", (e) => {
            e.stopPropagation();
            hideToast();
        });

        node.append(ico, body, close);
        node.addEventListener("click", () => {
            hideToast();
            // Usta: həmin müştərinin yazışması açılsın
            if (drawer) setOpen(true, conversation ? { conversation } : null);
            else goToInlineChat();
        });

        document.body.appendChild(node);
        toast = node;
        requestAnimationFrame(() => node.classList.add("is-visible"));
        toastTimer = setTimeout(hideToast, 7000);
    }

    document.addEventListener("dc:unread", (e) => {
        const { count, preview, initial } = e.detail || {};

        badge.hidden = !count;
        badge.textContent = count > 99 ? "99+" : String(count || "");
        btn.classList.toggle("has-unread", !!count);
        document.title = count ? "(" + (count > 99 ? "99+" : count) + ") " + baseTitle : baseTitle;

        if (!count || open) return;

        if (initial) {
            showToast(deT("Yeni mesajınız var"), count === 1
                ? deT("Çatda sizin üçün 1 oxunmamış mesaj var.")
                : deT("Çatda sizin üçün {0} oxunmamış mesaj var.", count), false);
        } else if (preview) {
            showToast(preview.userName + (preview.isAdmin ? " · " + deT("Usta") : ""), preview.text || deT("Yeni mesaj"), preview.isAdmin, preview.conversation);
        }
    });
})();
