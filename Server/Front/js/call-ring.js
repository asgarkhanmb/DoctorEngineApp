/* =====================================================
   DoctorEngine — gələn zəng bildirişi (saytın istənilən səhifəsində)
   /callHub → IncomingCall: "Ali video zəng edir" — Cavab ver / Rədd et
   window.dcCalls.start(conversation, video, title) — çatdan zəng etmək (ayrıca pəncərədə /Call)
   ===================================================== */
(function () {
    "use strict";
    if (!window.signalR || window.dcCalls) return;
    const T = window.deT || ((s, ...a) => s.replace(/\{(\d+)\}/g, (_, i) => a[i]));

    function openCallWindow(url, name) {
        const w = Math.min(1100, screen.availWidth - 40), h = Math.min(760, screen.availHeight - 60);
        const win = window.open(url, name, "width=" + w + ",height=" + h + ",left=" + Math.max(0, (screen.availWidth - w) / 2) + ",top=" + Math.max(0, (screen.availHeight - h) / 2));
        if (!win) location.href = url;   // pop-up bloklanıb — eyni pəncərədə
        return win;
    }

    window.dcCalls = {
        start(conversation, video, title) {
            try { sessionStorage.setItem("dc-call-title", title || ""); } catch (e) { /* yox */ }
            openCallWindow("/Call?c=" + encodeURIComponent(conversation) + "&video=" + (video ? 1 : 0), "dc-call");
        }
    };

    // ---------- Zəng səsi ----------
    let ctx = null, ringTimer = 0;
    ["pointerdown", "keydown", "touchstart"].forEach((ev) => document.addEventListener(ev, () => {
        try {
            ctx = ctx || new (window.AudioContext || window.webkitAudioContext)();
            if (ctx.state === "suspended") ctx.resume().catch(() => {});
        } catch (e) { /* səs yoxdur */ }
    }, { passive: true }));

    function ring() {
        stopRing();
        const play = () => {
            if (!ctx || ctx.state !== "running") return;
            const t = ctx.currentTime;
            [[660, 0], [880, 0.18], [660, 0.36], [880, 0.54]].forEach(([f, d]) => {
                const o = ctx.createOscillator(), g = ctx.createGain();
                o.type = "sine";
                o.frequency.value = f;
                g.gain.setValueAtTime(0.0001, t + d);
                g.gain.exponentialRampToValueAtTime(0.14, t + d + 0.02);
                g.gain.exponentialRampToValueAtTime(0.0001, t + d + 0.17);
                o.connect(g); g.connect(ctx.destination);
                o.start(t + d); o.stop(t + d + 0.18);
            });
        };
        play();
        ringTimer = setInterval(play, 2200);
        if (navigator.vibrate) navigator.vibrate([400, 200, 400]);
    }
    function stopRing() { clearInterval(ringTimer); ringTimer = 0; }

    // ---------- Bildiriş pəncərəsi ----------
    let current = null;   // { callId, node, timeout }

    function close(callId) {
        if (!current || (callId && current.callId !== callId)) return;
        stopRing();
        clearTimeout(current.timeout);
        current.node.remove();
        current = null;
    }

    function show(call) {
        if (current && current.callId === call.callId) return;
        close();
        const node = document.createElement("div");
        node.className = "dc-ring";
        node.setAttribute("role", "alertdialog");

        const av = document.createElement("div");
        av.className = "dc-ring__avatar";
        const i = document.createElement("i");
        i.className = call.isGroup ? "fa-solid fa-users" : call.video ? "fa-solid fa-video" : "fa-solid fa-phone";
        av.appendChild(i);

        const text = document.createElement("div");
        text.className = "dc-ring__text";
        const b = document.createElement("b");
        b.textContent = call.title || call.fromName || call.from;
        const s = document.createElement("span");
        s.textContent = call.isGroup
            ? T("{0} qrup zəngi başladı", call.fromName || call.from)
            : call.video ? T("Video zəng edir…") : T("Səsli zəng edir…");
        text.append(b, s);

        const decline = document.createElement("button");
        decline.type = "button";
        decline.className = "dc-ring__btn is-decline";
        decline.title = T("Rədd et");
        decline.setAttribute("aria-label", T("Rədd et"));
        decline.innerHTML = '<i class="fa-solid fa-phone-slash" aria-hidden="true"></i>';
        decline.addEventListener("click", () => {
            hub.invoke("Decline", call.callId).catch(() => {});
            close(call.callId);
        });

        const accept = document.createElement("button");
        accept.type = "button";
        accept.className = "dc-ring__btn is-accept";
        accept.title = T("Cavab ver");
        accept.setAttribute("aria-label", T("Cavab ver"));
        accept.innerHTML = '<i class="fa-solid ' + (call.video ? "fa-video" : "fa-phone") + '" aria-hidden="true"></i>';
        accept.addEventListener("click", () => {
            close(call.callId);
            openCallWindow("/Call?id=" + encodeURIComponent(call.callId) + "&video=" + (call.video ? 1 : 0), "dc-call");
        });

        node.append(av, text, decline, accept);
        document.body.appendChild(node);
        current = { callId: call.callId, node, timeout: setTimeout(() => close(call.callId), 45000) };
        ring();

        // Sayt arxa plandadırsa — sistem bildirişi
        if (document.visibilityState !== "visible" && "Notification" in window && Notification.permission === "granted") {
            try {
                const n = new Notification(b.textContent, { body: s.textContent, tag: "dc-call-" + call.callId, requireInteraction: true });
                n.onclick = () => { window.focus(); n.close(); };
            } catch (e) { /* yox */ }
        }
    }

    const hub = new signalR.HubConnectionBuilder()
        .withUrl("/callHub")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .build();

    hub.on("IncomingCall", show);
    hub.on("CallAnswered", (c) => close(c.callId));
    hub.on("CallCancelled", (c) => close(c.callId));

    async function start() {
        try { await hub.start(); }
        catch (e) { setTimeout(start, 8000); }
    }
    hub.onclose(() => setTimeout(start, 5000));
    start();
})();
