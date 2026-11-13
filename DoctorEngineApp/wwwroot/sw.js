/* =====================================================
   DoctorEngine — Service Worker (Web Push)
   Sayt bağlı və ya arxa planda olanda yeni çat mesajı üçün
   sistem bildirişi göstərir (əməliyyat sisteminin bildiriş səsi ilə).
   ===================================================== */
"use strict";

self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (event) => event.waitUntil(self.clients.claim()));

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
