/* =====================================================
   DoctorEngine — API ilə əlaqə (bütün səhifələrdə ən əvvəl yüklənir, config.js-dən sonra)

   - DE.api.get / post / form / del  → API sorğuları (JSON), xəta → DE.ApiError
   - DE.auth                         → giriş (access + refresh token localStorage-da), avtomatik yeniləmə
   - Köhnə skriptlər (chat.js, booking.js, assistant.js, call.js ...) "/api/...", "/uploads/...",
     "/chatHub" kimi nisbi ünvanlar işlədir — burada fetch, XMLHttpRequest və SignalR elə uyğunlaşdırılır ki,
     bu sorğular avtomatik API-yə, istifadəçinin tokeni ilə getsin. O skriptləri dəyişmək lazım deyil.
   ===================================================== */
(function () {
    "use strict";

    const cfg = window.DE_CONFIG || {};
    const API = String(cfg.apiUrl || "").replace(/\/+$/, "");
    const STORE = "de_auth";
    const DEVICE = "Sayt (brauzer)";
    const DE = window.DE = window.DE || {};

    // ---------- Dil (i18n.js də bunu işlədir) ----------
    function readLang() {
        // ?lang=en — ünvanla dil seçimi (mobil tətbiqin WebView-u, paylaşılan linklər); yadda saxlanılır
        const q = new URLSearchParams(location.search).get("lang");
        if (["az", "en", "ru", "tr"].includes(q)) {
            document.cookie = "de_lang=" + q + "; path=/; max-age=31536000; SameSite=Lax";
            return q;
        }
        const m = document.cookie.match(/(?:^|;\s*)de_lang=([a-z]{2})/);
        const v = m ? m[1] : null;
        return ["az", "en", "ru", "tr"].includes(v) ? v : "az";
    }
    DE.lang = readLang();

    // ---------- Ünvanlar ----------
    /** "/api/x" → "https://api.../api/x". Artıq tam ünvandırsa — dəyişmir. */
    function abs(path) {
        if (!path || /^[a-z]+:\/\//i.test(path) || path.startsWith("//") || path.startsWith("data:") || path.startsWith("blob:")) return path;
        return API + (path.startsWith("/") ? path : "/" + path);
    }
    /** API-yə aid nisbi ünvan? */
    function isApiPath(url) {
        if (typeof url !== "string") return false;
        if (url.startsWith(API + "/")) return true;
        return /^\/(api|uploads|chatHub|reservationHub|callHub)(\/|\?|$)/.test(url);
    }
    DE.apiUrl = API;
    DE.abs = abs;
    /** Yüklənmiş fayl: "/uploads/x.png" → API ünvanı ilə. */
    DE.upload = (path) => (path ? abs(path) : path);

    // ---------- Token saxlama ----------
    function read() {
        try { return JSON.parse(localStorage.getItem(STORE) || "null"); } catch (e) { return null; }
    }
    function save(auth) {
        try { localStorage.setItem(STORE, JSON.stringify(auth)); } catch (e) { /* gizli rejim */ }
    }
    function clear() {
        try { localStorage.removeItem(STORE); } catch (e) { /* */ }
    }

    const origFetch = window.fetch.bind(window);
    let refreshing = null;

    /** Refresh token ilə yeni access token (eyni anda bir dəfə; başqa tab artıq yeniləyibsə — onunku). */
    function refresh() {
        if (refreshing) return refreshing;
        refreshing = (async () => {
            const s = read();
            if (!s || !s.refreshToken) return null;
            let res;
            try {
                res = await origFetch(API + "/api/v1/auth/refresh", {
                    method: "POST",
                    headers: { "Content-Type": "application/json", "Accept-Language": DE.lang },
                    body: JSON.stringify({ refreshToken: s.refreshToken, deviceName: DEVICE })
                });
            } catch (e) {
                return null;   // şəbəkə xətası — istifadəçini çıxarmırıq
            }
            if (res.ok) {
                const auth = await res.json();
                save(auth);
                return auth.accessToken;
            }
            const now = read();
            if (now && now.refreshToken !== s.refreshToken) return now.accessToken;   // başqa tab yenilədi
            if (res.status === 401 || res.status === 400) {
                clear();
                window.dispatchEvent(new CustomEvent("de:logout"));
            }
            return null;
        })().finally(() => { refreshing = null; });
        return refreshing;
    }

    /** Etibarlı access token (1 dəqiqədən az qalıbsa — yenilənir). Daxil olmayıbsa null. */
    async function token() {
        const s = read();
        if (!s) return null;
        if (new Date(s.accessTokenExpiresAt).getTime() - Date.now() > 60000) return s.accessToken;
        return await refresh();
    }

    DE.auth = {
        /** {id, userName, fullName, email, avatarUrl, roles} və ya null */
        user() { const s = read(); return s ? s.user : null; },
        isAuthenticated() { return !!read(); },
        isAdmin() { const u = this.user(); return !!(u && (u.roles || []).includes("Admin")); },
        token,
        /** API-nin login / reset-password cavabını yadda saxla */
        signIn(auth) { save(auth); },
        /** Profil dəyişdi (ad, şəkil) — yeni tokenlə istifadəçi məlumatı da yenilənir */
        async reload() { const s = read(); if (!s) return null; s.accessTokenExpiresAt = 0; save(s); await refresh(); return this.user(); },
        async signOut() {
            const s = read();
            clear();
            if (s && s.refreshToken) {
                try {
                    await origFetch(API + "/api/v1/auth/logout", {
                        method: "POST", headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ refreshToken: s.refreshToken })
                    });
                } catch (e) { /* API əlçatan deyilsə də çıxış olsun */ }
            }
        },
        /** Giriş tələb olunan səhifə: daxil olmayıbsa → /Account/Login?returnUrl=... */
        require(adminOnly) {
            if (!this.isAuthenticated()) {
                location.replace("/Account/Login?returnUrl=" + encodeURIComponent(location.pathname + location.search));
                return false;
            }
            if (adminOnly && !this.isAdmin()) { location.replace("/"); return false; }
            return true;
        },
        deviceName: DEVICE
    };

    // Başqa tabda çıxış / giriş edildi — səhifə yenilənsin
    window.addEventListener("storage", (e) => {
        if (e.key === STORE && !!e.oldValue !== !!e.newValue) location.reload();
    });
    // Açıq səhifədə token vaxtında yenilənsin (XHR və SignalR köhnə token götürməsin)
    setInterval(() => { if (read()) token(); }, 30000);

    // ---------- fetch: nisbi API ünvanları → API + token ----------
    window.fetch = async function (input, init) {
        const url = typeof input === "string" ? input : (input instanceof URL ? input.href : null);
        if (!url || !isApiPath(url)) return origFetch(input, init);

        const options = Object.assign({}, init || {});
        const headers = new Headers(options.headers || {});
        headers.set("Accept-Language", DE.lang);
        headers.delete("RequestVerificationToken");   // köhnə saytın CSRF başlığı API-yə lazım deyil
        options.credentials = "omit";
        const hadToken = !headers.has("Authorization") && !!read();
        if (hadToken) {
            const t = await token();
            if (t) headers.set("Authorization", "Bearer " + t);
        }
        options.headers = headers;

        let res = await origFetch(abs(url), options);
        // Token vaxtından əvvəl etibarsız olubsa (şifrə / rol dəyişib) — bir dəfə yenilə, təkrarla
        if (res.status === 401 && hadToken && !(options.body instanceof ReadableStream)) {
            const t = await refresh();
            if (t) {
                headers.set("Authorization", "Bearer " + t);
                res = await origFetch(abs(url), options);
            }
        }
        return res;
    };

    // ---------- XMLHttpRequest (çatda fayl yükləmə faizi) ----------
    const xOpen = XMLHttpRequest.prototype.open;
    const xSend = XMLHttpRequest.prototype.send;
    const xHeader = XMLHttpRequest.prototype.setRequestHeader;
    XMLHttpRequest.prototype.open = function (method, url) {
        const args = Array.prototype.slice.call(arguments);
        this.__deApi = isApiPath(String(url));
        if (this.__deApi) args[1] = abs(String(url));
        return xOpen.apply(this, args);
    };
    XMLHttpRequest.prototype.setRequestHeader = function (name, value) {
        if (this.__deApi && /^requestverificationtoken$/i.test(name)) return;
        return xHeader.call(this, name, value);
    };
    XMLHttpRequest.prototype.send = function (body) {
        if (this.__deApi) {
            const s = read();
            if (s) xHeader.call(this, "Authorization", "Bearer " + s.accessToken);
            xHeader.call(this, "Accept-Language", DE.lang);
        }
        return xSend.call(this, body);
    };

    // ---------- SignalR: "/chatHub" → API + token ----------
    function patchSignalR(sr) {
        if (!sr || !sr.HubConnectionBuilder || sr.__dePatched) return;
        sr.__dePatched = true;
        const proto = sr.HubConnectionBuilder.prototype;
        const withUrl = proto.withUrl;
        proto.withUrl = function (url, opts) {
            let options;
            if (opts && typeof opts === "object") options = Object.assign({}, opts);
            else options = opts !== undefined ? { transport: opts } : {};
            if (typeof url === "string" && isApiPath(url)) url = abs(url);
            if (!options.accessTokenFactory)
                options.accessTokenFactory = async () => (await token()) || "";
            return withUrl.call(this, url, options);
        };
    }
    let signalRLib = window.signalR;
    if (signalRLib) patchSignalR(signalRLib);
    Object.defineProperty(window, "signalR", {
        configurable: true,
        get() { return signalRLib; },
        set(v) { signalRLib = v; patchSignalR(v); }
    });

    // ---------- Yüklənmiş fayllar DOM-da: src="/uploads/..." → API ünvanı ----------
    const ATTRS = ["src", "href", "poster", "data-src", "data-bg"];
    function fixElement(el) {
        if (el.nodeType !== 1) return;
        for (const a of ATTRS) {
            const v = el.getAttribute(a);
            if (v && v.startsWith("/uploads/")) el.setAttribute(a, API + v);
        }
        const st = el.getAttribute("style");
        if (st && st.includes("/uploads/")) el.setAttribute("style", st.replace(/url\((['"]?)\/uploads\//g, "url($1" + API + "/uploads/"));
    }
    function fixTree(root) {
        if (root.nodeType !== 1) return;
        fixElement(root);
        root.querySelectorAll("[src^='/uploads/'],[href^='/uploads/'],[poster^='/uploads/'],[data-src^='/uploads/'],[style*='/uploads/']").forEach(fixElement);
    }
    new MutationObserver((mutations) => {
        for (const m of mutations) {
            if (m.type === "attributes") fixElement(m.target);
            else m.addedNodes.forEach(fixTree);
        }
    }).observe(document.documentElement, { subtree: true, childList: true, attributes: true, attributeFilter: ATTRS.concat("style") });

    // ---------- Sadə API köməkçiləri ----------
    class ApiError extends Error {
        constructor(status, message, errors) {
            super(message);
            this.status = status;
            this.errors = errors || null;
        }
    }
    DE.ApiError = ApiError;

    async function handle(res) {
        if (res.status === 204) return null;
        const text = await res.text();
        let data = null;
        try { data = text ? JSON.parse(text) : null; } catch (e) { data = null; }
        if (!res.ok) {
            const msg = (data && (data.error || data.message)) ||
                (res.status === 401 ? DE.t("Hesabınıza daxil olun.") :
                    res.status === 403 ? DE.t("Bu əməliyyat üçün icazəniz yoxdur.") :
                        res.status === 429 ? DE.t("Çox sayda cəhd. Bir dəqiqə gözləyib yenidən cəhd edin.") :
                            DE.t("Server cavab vermir. Bir azdan yenidən cəhd edin."));
            throw new ApiError(res.status, msg, data && data.errors);
        }
        return data;
    }

    async function send(method, path, body, isForm) {
        let res;
        try {
            res = await window.fetch(path, {
                method,
                headers: isForm || body === undefined ? {} : { "Content-Type": "application/json" },
                body: body === undefined ? undefined : (isForm ? body : JSON.stringify(body))
            });
        } catch (e) {
            throw new ApiError(0, DE.t("Serverə qoşulmaq mümkün olmadı. İnternet bağlantınızı yoxlayın."));
        }
        return handle(res);
    }

    DE.api = {
        get: (path) => send("GET", path),
        post: (path, body) => send("POST", path, body === undefined ? {} : body),
        put: (path, body) => send("PUT", path, body),
        del: (path) => send("DELETE", path),
        /** multipart/form-data (fayllar) */
        form: (path, formData, method) => send(method || "POST", path, formData, true),
        /** Faylı (PDF) yüklə və aç */
        async file(path) {
            const res = await window.fetch(path);
            if (!res.ok) await handle(res);
            const blob = await res.blob();
            const cd = res.headers.get("Content-Disposition") || "";
            const m = cd.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
            return { blob, name: m ? decodeURIComponent(m[1]) : "fayl" };
        },
        /** Hamıya açıq məlumat (header, footer, ana səhifə) — 60 san. sessionStorage-da */
        async cached(path, seconds) {
            const key = "de_c:" + DE.lang + ":" + path;
            try {
                const hit = JSON.parse(sessionStorage.getItem(key) || "null");
                if (hit && Date.now() - hit.t < (seconds || 60) * 1000) return hit.v;
            } catch (e) { /* */ }
            const v = await send("GET", path);
            try { sessionStorage.setItem(key, JSON.stringify({ t: Date.now(), v })); } catch (e) { /* */ }
            return v;
        },
        /** Keşi sıfırla (admin dəyişiklikdən sonra) */
        clearCache() {
            try { Object.keys(sessionStorage).filter((k) => k.startsWith("de_c:")).forEach((k) => sessionStorage.removeItem(k)); } catch (e) { /* */ }
        }
    };

    // i18n.js yüklənməmişdən əvvəl çağırılarsa
    if (!DE.t) DE.t = (s) => s;
})();
