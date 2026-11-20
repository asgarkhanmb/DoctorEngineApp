/* =====================================================
   DoctorEngine — çoxdillilik (az / en / ru / tr)

   Səhifələr azərbaycanca yazılıb. Başqa dil seçiləndə mətnlər API-dən gələn lüğətlə
   (GET api/v1/languages/{dil}/ui) avtomatik çevrilir: mətn düyünləri və placeholder/title/aria-label/alt.
   Bazadakı məzmun (slayder, xidmət, usta ...) API-dən artıq seçilmiş dildə gəlir (Accept-Language).

   JS-də:  DE.t("nav.home")  və ya  DE.t("Azərbaycanca mətn {0}", arg)
   Köhnə skriptlər üçün: window.deT(...), window.DE_I18N, window.DE_LANG
   ===================================================== */
(function () {
    "use strict";

    const DE = window.DE = window.DE || {};
    const lang = DE.lang || "az";
    const LANGS = [["az", "Azərbaycanca"], ["en", "English"], ["ru", "Русский"], ["tr", "Türkçe"]];
    const VERSION = "1";

    let dict = {};      // açar və ya az mətn → cari dildə
    let reverse = {};   // az mətn (açarlı mətnlərin az variantı da daxil) → cari dildə

    function format(s, args) {
        return String(s).replace(/\{(\d+)\}/g, (m, i) => (args[+i] === undefined ? m : args[+i]));
    }

    DE.t = function (text) {
        const args = Array.prototype.slice.call(arguments, 1);
        if (text == null) return "";
        const v = dict[text] !== undefined ? dict[text] : (reverse[text] !== undefined ? reverse[text] : text);
        return format(v, args);
    };
    window.deT = DE.t;
    window.DE_LANG = lang;
    document.documentElement.lang = lang;

    async function load(code) {
        const key = "de_i18n:" + VERSION + ":" + code;
        try {
            const hit = JSON.parse(localStorage.getItem(key) || "null");
            if (hit && Date.now() - hit.t < 3600 * 1000) return hit.v;
        } catch (e) { /* */ }
        const res = await fetch("/api/v1/languages/" + code + "/ui");
        if (!res.ok) throw new Error("i18n " + res.status);
        const v = await res.json();
        try { localStorage.setItem(key, JSON.stringify({ t: Date.now(), v })); } catch (e) { /* */ }
        return v;
    }

    // ---------- DOM-u tərcümə et ----------
    const SKIP = new Set(["SCRIPT", "STYLE", "TEXTAREA", "CODE", "PRE", "NOSCRIPT"]);
    const ATTRS = ["placeholder", "title", "aria-label", "alt", "data-bs-original-title"];

    function tr(s) {
        if (!s) return null;
        const t = s.trim();
        if (!t) return null;
        const v = reverse[t];
        if (v === undefined || v === t) return null;
        return s.replace(t, v);
    }

    function translate(root) {
        if (lang === "az" || !root) return;
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
            acceptNode(n) {
                const p = n.parentElement;
                if (!p || SKIP.has(p.tagName) || p.closest("[data-no-i18n]")) return NodeFilter.FILTER_REJECT;
                return NodeFilter.FILTER_ACCEPT;
            }
        });
        const nodes = [];
        while (walker.nextNode()) nodes.push(walker.currentNode);
        for (const n of nodes) {
            const v = tr(n.nodeValue);
            if (v !== null) n.nodeValue = v;
        }
        const els = root.nodeType === 1 ? [root, ...root.querySelectorAll("*")] : root.querySelectorAll("*");
        for (const el of els) {
            if (el.closest && el.closest("[data-no-i18n]")) continue;
            for (const a of ATTRS) {
                const v = el.getAttribute && el.getAttribute(a);
                const t = v && tr(v);
                if (t !== null && t !== undefined) el.setAttribute(a, t);
            }
            if (el.tagName === "INPUT" && (el.type === "submit" || el.type === "button")) {
                const t = tr(el.value);
                if (t !== null) el.value = t;
            }
        }
    }

    DE.i18n = {
        lang,
        languages: LANGS,
        translate,
        /** Dili dəyiş (1 il yadda saxlanılır) və səhifəni yenilə */
        setLang(code) {
            if (!LANGS.some((l) => l[0] === code)) return;
            document.cookie = "de_lang=" + code + "; path=/; max-age=31536000; SameSite=Lax";
            location.reload();
        },
        /** Lüğət yüklənəndə həll olur (xəta olsa da — azərbaycanca qalır) */
        ready: (async function () {
            try {
                const az = await load("az");
                if (lang === "az") {
                    dict = az;
                } else {
                    dict = await load(lang);
                    // açarlı mətnlərin az variantı → cari dil (məs. "Ana Səhifə" → "Home")
                    for (const k of Object.keys(az)) if (dict[k] !== undefined) reverse[az[k]] = dict[k];
                    for (const k of Object.keys(dict)) if (az[k] === undefined) reverse[k] = dict[k];
                }
            } catch (e) {
                console.warn("Tərcümələr yüklənmədi", e);
            }
            window.DE_I18N = lang === "az" ? {} : reverse;
        })()
    };

    // Dinamik əlavə olunan mətnlər də (çat, bildirişlər) tərcümə olunsun
    if (lang !== "az") {
        DE.i18n.ready.then(() => {
            new MutationObserver((ms) => {
                for (const m of ms) m.addedNodes.forEach((n) => {
                    if (n.nodeType === 1) translate(n);
                    else if (n.nodeType === 3 && n.parentElement && !SKIP.has(n.parentElement.tagName)) {
                        const v = tr(n.nodeValue);
                        if (v !== null) n.nodeValue = v;
                    }
                });
            }).observe(document.documentElement, { childList: true, subtree: true });
        });
    }
})();
