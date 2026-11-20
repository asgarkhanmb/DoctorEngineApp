/* =====================================================
   DoctorEngine — canlı çat vidjeti (köhnə saytın _ChatWidget.cshtml-i ilə eyni HTML)

   DE.chatWidget.html({ fullPage, drawer })  → vidjetin HTML-i (istifadəçiyə görə)
   DE.chatWidget.start({ fullPage, drawer }) → ayarları API-dən alır (api/chat/config) və chat.js-i işə salır
   Mesajlar, qruplar, zənglər — js/chat.js (dəyişməyib): "/api/chat/*" və "/chatHub" sorğuları api.js ilə API-yə gedir.
   ===================================================== */
(function () {
    "use strict";
    const DE = window.DE;
    const esc = (s) => String(s == null ? "" : s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c]));
    const t = (s) => esc(DE.t(s));

    function html(opts) {
        const fullPage = !!(opts && opts.fullPage);
        const drawer = !!(opts && opts.drawer);
        const auth = DE.auth.isAuthenticated();
        const isAdmin = auth && DE.auth.isAdmin();
        const returnUrl = location.pathname + location.search;
        const loginUrl = "/Account/Login?returnUrl=" + encodeURIComponent(returnUrl);
        const registerUrl = "/Account/Register";
        const cls = ["dc-chat", "dc-wa", fullPage && "dc-chat--full", drawer && "dc-chat--drawer", isAdmin && "dc-chat--admin",
            auth && "dc-chat--list", auth && !isAdmin && "has-conversation"].filter(Boolean).join(" ");

        let h = `<section class="${cls}" id="dcChat" data-view="${isAdmin ? "list" : "chat"}" aria-label="${t("Canlı çat")}">`;

        if (auth) {
            h += `
        <aside class="dc-convs" aria-label="${t("Yazışmalar")}">
            <header class="dc-convs__header">
                <button type="button" class="dc-convs__me" data-dc-profile title="${t("Profilim")}" aria-label="${t("Profilim")}">
                    <span class="dc-avatar"><i class="fa-solid fa-user" aria-hidden="true"></i></span>
                </button>
                <h3>${t("Çatlar")}</h3>
                <span class="dc-convs__total" hidden></span>
                <button type="button" class="dc-chat__icon-btn dc-convs__new" data-dc-new-chat title="${t("Yeni çat")}" aria-label="${t("Yeni çat")}">
                    <i class="fa-solid fa-plus"></i>
                </button>
                ${!fullPage ? `<a class="dc-chat__icon-btn" href="/Chat" title="${t("Tam ekranda aç")}"><i class="fa-solid fa-up-right-and-down-left-from-center"></i></a>` : ""}
                ${drawer ? `<button type="button" class="dc-chat__icon-btn" data-dc-close title="${t("Bağla")}" aria-label="${t("Çatı bağla")}"><i class="fa-solid fa-xmark"></i></button>` : ""}
            </header>
            <div class="dc-convs__search">
                <i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i>
                <input type="search" placeholder="${t("Axtar")}" aria-label="${t("Yazışmalarda axtar")}" />
            </div>
            <div class="dc-convs__filters" role="tablist">
                <button type="button" class="is-active" data-filter="all">${t("Hamısı")}</button>
                <button type="button" data-filter="unread">${t("Oxunmamış")}</button>
                <button type="button" data-filter="groups">${t("Qruplar")}</button>
            </div>
            <ul class="dc-convs__list" role="listbox"></ul>
            <div class="dc-convs__empty" hidden>
                <i class="fa-regular fa-comments" aria-hidden="true"></i>
                <p>${t("Hələ yazışma yoxdur.")}</p>
            </div>
            <div class="dc-convs__loading"><span class="dc-chat__spinner"></span></div>
        </aside>`;
        }

        h += `<div class="dc-pane">`;
        if (auth) {
            h += `
            <div class="dc-pane__placeholder">
                <div class="dc-pane__placeholder-icon"><i class="fa-solid fa-screwdriver-wrench" aria-hidden="true"></i></div>
                <h4>${t("Canlı Usta Dəstəyi")}</h4>
                <p>${isAdmin ? t("Müştəri ilə yazışmanı açmaq üçün soldan çat seçin.") : t("Yazışmanı açmaq üçün soldan çat seçin.")}</p>
                <span><i class="fa-solid fa-lock" aria-hidden="true"></i> ${t("Hər müştəri yalnız öz yazışmasını görür.")}</span>
            </div>`;
        }

        h += `
        <header class="dc-chat__header">
            ${auth ? `<button type="button" class="dc-chat__icon-btn dc-chat__back" title="${t("Geri")}" aria-label="${t("Yazışmalara qayıt")}"><i class="fa-solid fa-arrow-left"></i></button>` : ""}
            <div class="dc-chat__logo" aria-hidden="true"><i class="fa-solid fa-screwdriver-wrench"></i></div>
            <div class="dc-chat__heading" role="button" tabindex="0" title="${t("Məlumat")}">
                <h3 class="dc-chat__title">${isAdmin ? "" : t("Canlı Usta Dəstəyi")}</h3>
                <span class="dc-chat__status" data-state="${auth ? "connecting" : "offline"}">
                    <span class="dc-chat__dot"></span>
                    <span class="dc-chat__status-text">${auth ? t("Qoşulur…") : t("Giriş tələb olunur")}</span>
                </span>
            </div>`;

        if (auth) {
            h += `
            <button type="button" class="dc-chat__icon-btn" data-dc-call="video" title="${t("Video zəng")}" aria-label="${t("Video zəng")}" hidden><i class="fa-solid fa-video"></i></button>
            <button type="button" class="dc-chat__icon-btn" data-dc-call="audio" title="${t("Səsli zəng")}" aria-label="${t("Səsli zəng")}" hidden><i class="fa-solid fa-phone"></i></button>
            <button type="button" class="dc-chat__icon-btn dc-chat__tools-btn" data-dc-tools title="${t("Daha çox")}" aria-label="${t("Daha çox")}" aria-expanded="false" aria-haspopup="true"><i class="fa-solid fa-ellipsis-vertical"></i></button>
            <div class="dc-chat__tools" role="menu">
                <button type="button" class="dc-chat__icon-btn" data-dc-search role="menuitem" title="${t("Axtar")}" aria-label="${t("Yazışmada axtar")}">
                    <i class="fa-solid fa-magnifying-glass"></i><span class="dc-tool-label">${t("Mesajlarda axtar")}</span>
                </button>
                <button type="button" class="dc-chat__icon-btn" data-dc-sound role="menuitem" title="${t("Bildiriş səsini söndür")}" aria-label="${t("Bildiriş səsini söndür")}" aria-pressed="false">
                    <i class="fa-solid fa-bell"></i><span class="dc-tool-label">${t("Bildiriş səsi")}</span>
                </button>
                <button type="button" class="dc-chat__icon-btn dc-chat__clear-btn" data-dc-clear role="menuitem" title="${t("Yazışmanı sil")}" aria-label="${t("Yazışmanı sil")}">
                    <i class="fa-solid fa-trash-can"></i><span class="dc-tool-label">${t("Yazışmanı sil")}</span>
                </button>
                ${!fullPage && !isAdmin ? `<a class="dc-chat__icon-btn" role="menuitem" href="/Chat" title="${t("Tam ekranda aç")}"><i class="fa-solid fa-up-right-and-down-left-from-center"></i><span class="dc-tool-label">${t("Tam ekranda aç")}</span></a>` : ""}
            </div>`;
        }
        if (drawer && !isAdmin)
            h += `<button type="button" class="dc-chat__icon-btn" data-dc-close title="${t("Bağla")}" aria-label="${t("Çatı bağla")}"><i class="fa-solid fa-xmark"></i></button>`;
        h += `</header>`;

        if (auth) {
            h += `
        <div class="dc-search" hidden>
            <div class="dc-search__bar">
                <button type="button" class="dc-chat__icon-btn dc-search__close" title="${t("Bağla")}" aria-label="${t("Axtarışı bağla")}"><i class="fa-solid fa-arrow-left"></i></button>
                <input type="search" class="dc-search__input" placeholder="${t("Mesajlarda axtar…")}" aria-label="${t("Mesajlarda axtar")}" />
            </div>
            <ul class="dc-search__results"></ul>
        </div>
        <div class="dc-chat__push" hidden>
            <i class="fa-solid fa-bell" aria-hidden="true"></i>
            <span>${t("Sayt bağlı olanda da yeni mesajdan xəbər tutun.")}</span>
            <button type="button" class="dc-chat__push-on">${t("Bildirişləri aç")}</button>
            <button type="button" class="dc-chat__push-x" aria-label="${t("Bağla")}" title="${t("Bağla")}"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button>
        </div>
        <div class="dc-chat__body" id="dcChatBody" role="log" aria-live="polite" aria-relevant="additions">
            <div class="dc-chat__e2e">
                <i class="fa-solid fa-lock" aria-hidden="true"></i>
                ${isAdmin ? t("Bu yazışmanı yalnız müştəri və ustalar görür.") : t("Bu yazışmanı yalnız siz və ustalarımız görür.")}
            </div>
            <div class="dc-chat__more-wrap">
                <button type="button" class="dc-chat__more" hidden><i class="fa-solid fa-clock-rotate-left"></i> ${t("Köhnə mesajları göstər")}</button>
            </div>
            <div class="dc-chat__list"></div>
            <div class="dc-chat__loading"><span class="dc-chat__spinner"></span> ${t("Mesajlar yüklənir…")}</div>
            <div class="dc-chat__empty" hidden>
                <i class="fa-regular fa-comments"></i>
                <p>${t("Hələ mesaj yoxdur.")}<br />${isAdmin ? t("Müştəriyə ilk mesajı yazın.") : t("Sualınızı yazın — ustalarımız cavab verəcək.")}</p>
            </div>
        </div>
        <button type="button" class="dc-chat__jump" hidden aria-label="${t("Aşağı")}">
            <i class="fa-solid fa-chevron-down"></i><span class="dc-chat__jump-count" hidden></span>
        </button>
        <div class="dc-chat__readonly" hidden>
            <i class="fa-solid fa-bullhorn" aria-hidden="true"></i> ${t("Bu qrupda yalnız ustalar yaza bilər.")}
        </div>
        <form class="dc-chat__composer" novalidate>
            <div class="dc-chat__error" role="alert" hidden></div>
            <div class="dc-reply" hidden>
                <div class="dc-reply__body"><span class="dc-reply__name"></span><span class="dc-reply__text"></span></div>
                <button type="button" class="dc-chat__icon-btn dc-reply__cancel" title="${t("Ləğv et")}" aria-label="${t("Cavabı ləğv et")}"><i class="fa-solid fa-xmark"></i></button>
            </div>
            <div class="dc-chat__preview" hidden>
                <div class="dc-chat__preview-thumb"></div>
                <div class="dc-chat__preview-meta"><span class="dc-chat__preview-name"></span><span class="dc-chat__preview-size"></span></div>
                <button type="button" class="dc-chat__icon-btn dc-chat__preview-remove" title="${t("Faylı çıxar")}"><i class="fa-solid fa-xmark"></i></button>
            </div>
            <div class="dc-chat__progress" hidden><div class="dc-chat__progress-bar"></div></div>
            <div class="dc-emoji" hidden role="dialog" aria-label="${t("Emoji")}"><div class="dc-emoji__tabs"></div><div class="dc-emoji__grid"></div></div>
            <div class="dc-chat__row">
                <div class="dc-chat__field">
                    <button type="button" class="dc-chat__emoji-btn" title="${t("Emoji")}" aria-label="${t("Emoji")}" aria-expanded="false"><i class="fa-regular fa-face-smile"></i></button>
                    <textarea class="dc-chat__input" rows="1" maxlength="${(opts && opts.maxLength) || 1000}" placeholder="${t("Mesaj")}" aria-label="${t("Mesaj")}"></textarea>
                    <label class="dc-chat__attach" title="${t("Şəkil və ya video əlavə et")}">
                        <i class="fa-solid fa-paperclip"></i>
                        <input type="file" class="dc-chat__file" accept="image/jpeg,image/png,image/webp,video/mp4,video/webm,video/quicktime,.mov,.m4v" />
                    </label>
                </div>
                <button type="submit" class="dc-chat__send" title="${t("Göndər")}" disabled><i class="fa-solid fa-paper-plane"></i></button>
                <button type="button" class="dc-chat__send dc-chat__mic" title="${t("Səs mesajı yaz")}" aria-label="${t("Səs mesajı yaz")}" hidden><i class="fa-solid fa-microphone"></i></button>
            </div>
            <div class="dc-rec" hidden>
                <button type="button" class="dc-rec__cancel" title="${t("Ləğv et")}" aria-label="${t("Səs yazmanı ləğv et")}"><i class="fa-solid fa-trash"></i></button>
                <span class="dc-rec__dot" aria-hidden="true"></span>
                <span class="dc-rec__time" aria-live="off">0:00</span>
                <canvas class="dc-rec__wave" aria-hidden="true"></canvas>
                <button type="button" class="dc-rec__pause" title="${t("Fasilə")}" aria-label="${t("Fasilə")}"><i class="fa-solid fa-pause"></i></button>
                <button type="button" class="dc-chat__send dc-rec__send" title="${t("Göndər")}" aria-label="${t("Səs mesajını göndər")}"><i class="fa-solid fa-paper-plane"></i></button>
            </div>
        </form>
        <div class="dc-chat__dropzone" hidden>
            <i class="fa-solid fa-cloud-arrow-up"></i><span>${t("Şəkil və ya videonu buraya buraxın")}</span>
        </div>`;
        } else {
            const link = (href, text) => `<a href="${esc(href)}">${t(text)}</a>`;
            const sentence = esc(DE.t("Mesaj yazmaq üçün {0} və ya {1}.")).replace("{0}", link(loginUrl, "daxil olun")).replace("{1}", link(registerUrl, "qeydiyyatdan keçin"));
            h += `
        <div class="dc-chat__body" id="dcChatBody">
            <div class="dc-chat__list">
                <div class="dc-sep"><span>${t("Bu gün")}</span></div>
                <div class="dc-msg is-admin"><div class="dc-msg__content"><div class="dc-msg__bubble">
                    <div class="dc-msg__text">${t("Salam! 👋 Canlı Usta Dəstəyinə xoş gəlmisiniz.")}<br />${t("Sualınızı ustalarımıza yaza bilərsiniz. Yazışmaları görmək və mesaj göndərmək üçün hesabınıza daxil olun.")}</div>
                </div></div></div>
            </div>
        </div>
        <div class="dc-chat__guest"><i class="fa-solid fa-lock"></i><span>${sentence}</span></div>`;
        }

        h += `</div></section>
<div class="dc-menu" hidden role="menu"><div class="dc-menu__reactions"></div><div class="dc-menu__actions"></div></div>
<div class="dc-lightbox" hidden role="dialog" aria-modal="true" aria-label="${t("Şəkil")}">
    <button type="button" class="dc-lightbox__close" title="${t("Bağla")}"><i class="fa-solid fa-xmark"></i></button>
    <img alt="" />
</div>`;
        return h;
    }

    /** Daxil olmuş istifadəçi üçün: ayarlar (API) + chat.js. Qonaq üçün heç nə yüklənmir. */
    async function start(opts) {
        if (!DE.auth.isAuthenticated() || !document.getElementById("dcChat")) return;
        let c = null;
        try { c = await DE.api.get("/api/chat/config"); } catch (e) { /* standart limitlər */ }
        const user = DE.auth.user() || {};
        const params = new URLSearchParams(location.search);
        const config = {
            isAuthenticated: true,
            currentUser: user.userName,
            isAdmin: DE.auth.isAdmin(),
            token: "",
            loginUrl: "/Account/Login?returnUrl=" + encodeURIComponent(location.pathname + location.search),
            maxLength: c ? c.maxLength : 1000,
            maxImageBytes: c ? c.maxImageBytes : 10485760,
            maxVideoBytes: c ? c.maxVideoBytes : 104857600,
            maxAudioBytes: c ? c.maxAudioBytes : 16777216,
            drawer: !!(opts && opts.drawer),
            fullPage: !!(opts && opts.fullPage),
            unreadCount: c ? c.unreadCount : 0,
            reactions: c ? c.reactions : [],
            openConversation: opts && opts.fullPage && params.get("c") ? params.get("c") : null,
            pushKey: c ? c.pushKey : null
        };
        const el = document.createElement("script");
        el.id = "dcChatConfig";
        el.type = "application/json";
        el.textContent = JSON.stringify(config);
        document.body.appendChild(el);

        await DE.loadScripts([
            "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js",
            "/js/call-ring.js",
            "/js/chat.js"
        ]);
    }

    DE.chatWidget = { html, start };
})();
