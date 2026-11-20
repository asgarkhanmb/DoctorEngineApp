/* =====================================================
   DoctorEngine — Service Worker (Web Push)
   Sayt bağlı və ya arxa planda olanda yeni çat mesajı üçün
   sistem bildirişi göstərir (əməliyyat sisteminin bildiriş səsi ilə).
   ===================================================== */
"use strict";

// ---------- "Server işləmir" səhifəsi ----------
// Səhifə açılarkən server cavab vermirsə (söndürülüb, yenidən başladılır, internet yoxdur)
// və ya IIS/proxy 502/503/504 qaytarırsa — saytın öz üslubunda offline.html göstərilir.
const CACHE = "de-offline-v2";   // v2: offline.html çoxdilli oldu
const OFFLINE_URL = "/offline.html";

self.addEventListener("install", (event) => {
    event.waitUntil(
        caches.open(CACHE)
            .then((c) => c.add(new Request(OFFLINE_URL, { cache: "reload" })))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener("activate", (event) => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(keys.filter((k) => k.startsWith("de-offline-") && k !== CACHE).map((k) => caches.delete(k)));
        // Səhifə yükləməsini paralel başlat (sürət itkisi olmasın)
        if (self.registration.navigationPreload) await self.registration.navigationPreload.enable();
        await self.clients.claim();
    })());
});

self.addEventListener("fetch", (event) => {
    const req = event.request;
    // Yalnız səhifə keçidləri (GET). Şəkil, API, SignalR və s. — toxunulmur.
    if (req.mode !== "navigate" || req.method !== "GET") return;

    event.respondWith((async () => {
        try {
            const res = (await event.preloadResponse) || (await fetch(req));
            // Bizim tətbiq 502-504 qaytarmır — bunlar IIS/proxy-nin "tətbiq işləmir" cavablarıdır
            if (res.status === 502 || res.status === 503 || res.status === 504) {
                return (await caches.match(OFFLINE_URL)) || res;
            }
            return res;
        } catch (e) {
            return (await caches.match(OFFLINE_URL)) ||
                new Response("<h1>Server əlçatan deyil</h1><p>Bir azdan yenidən cəhd edin.</p>",
                    { status: 503, headers: { "Content-Type": "text/html; charset=utf-8" } });
        }
    })());
});

self.addEventListener("push", (event) => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch (e) { data = { body: event.data && event.data.text() }; }

    event.waitUntil((async () => {
        // Sayt hazırda ekrandadırsa, səhifə özü səs və bildiriş göstərir — təkrarlamırıq
        const windows = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
        if (windows.some((c) => c.visibilityState === "visible")) return;

        await self.registration.showNotification(data.title || "Yeni mesaj", {
            body: data.body || "",
            icon: "/assets/img/favicon.ico",
            badge: "/assets/img/favicon.ico",
            tag: data.tag || "dc-chat",
            renotify: true,          // eyni tag-lı yeni mesajda da səs çıxsın
            silent: false,
            data: { url: data.url || "/?openChat=1" }
        });
    })());
});

self.addEventListener("notificationclick", (event) => {
    event.notification.close();
    const target = new URL((event.notification.data && event.notification.data.url) || "/?openChat=1", self.location.origin);

    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
        const existing = windows.find((c) => new URL(c.url).origin === self.location.origin);
        if (existing) {
            await existing.focus();
            existing.postMessage({ type: "dc:open-chat" });
            return;
        }
        await self.clients.openWindow(target.href);
    })());
});
