/* =====================================================
   DoctorEngine — Video / səsli zəng (WebRTC)
   /Call?c={yazışma}&video=1  — zəng et
   /Call?id={zəng}&video=1    — cavab ver (qoşul)
   Mobil tətbiq: token "#t=..." ilə gəlir; zəng bitəndə "doctorengine://call-ended" açılır (tətbiq pəncərəni bağlayır).
   Qrup zəngi: hər iştirakçı hər biri ilə birbaşa bağlantı qurur (mesh). Yeni qoşulan mövcud iştirakçılara "offer" göndərir.
   ===================================================== */
(function () {
    "use strict";

    const cfg = JSON.parse(document.getElementById("dcCallConfig").textContent);
    const T = window.deT || ((s, ...a) => s.replace(/\{(\d+)\}/g, (_, i) => a[i]));
    const params = new URLSearchParams(location.search);
    const hash = new URLSearchParams(location.hash.slice(1));
    const token = hash.get("t");
    if (token) history.replaceState(null, "", location.pathname + location.search);   // token ünvanda qalmasın
    const inApp = !!token || params.get("app") === "1";

    const startConversation = params.get("c");
    const joinCallId = params.get("id");
    let wantVideo = params.get("video") !== "0";

    const root = document.querySelector(".dc-call");
    const grid = root.querySelector(".dc-call__grid");
    const titleEl = root.querySelector(".dc-call__title");
    const statusEl = root.querySelector(".dc-call__status");
    const selfVideo = root.querySelector(".dc-call__self");
    const timerEl = root.querySelector(".dc-call__timer");
    const btn = (act) => root.querySelector('[data-act="' + act + '"]');

    let localStream = null;
    let callId = null;
    let ended = false;
    let answeredAt = 0;
    let timerHandle = 0;
    let ringTimeout = 0;
    const peers = new Map();   // connectionId -> { pc, tile, name, pendingIce: [] }

    function setState(state, text) {
        root.dataset.state = state;
        if (text !== undefined) statusEl.textContent = text;
    }

    function el(tag, cls, text) {
        const n = document.createElement(tag);
        if (cls) n.className = cls;
        if (text != null) n.textContent = text;
        return n;
    }

    // ---------- Zəng səsi (fayl yoxdur — Web Audio) ----------
    let audioCtx = null, ringTimer = 0;
    function startRingback() {
        stopRingback();
        try {
            audioCtx = audioCtx || new (window.AudioContext || window.webkitAudioContext)();
            const beep = () => {
                if (!audioCtx) return;
                const t = audioCtx.currentTime;
                [0, 0.45].forEach((d) => {
                    const o = audioCtx.createOscillator(), g = audioCtx.createGain();
                    o.frequency.value = 425;
                    g.gain.setValueAtTime(0.0001, t + d);
                    g.gain.exponentialRampToValueAtTime(0.08, t + d + 0.02);
                    g.gain.exponentialRampToValueAtTime(0.0001, t + d + 0.38);
                    o.connect(g); g.connect(audioCtx.destination);
                    o.start(t + d); o.stop(t + d + 0.4);
                });
            };
            beep();
            ringTimer = setInterval(beep, 3000);
        } catch (e) { /* səs olmadan */ }
    }
    function stopRingback() { clearInterval(ringTimer); ringTimer = 0; }

    // ---------- Vaxt sayğacı ----------
    function startTimer() {
        if (answeredAt) return;
        answeredAt = Date.now();
        timerEl.hidden = false;
        timerHandle = setInterval(() => {
            const s = Math.floor((Date.now() - answeredAt) / 1000);
            timerEl.textContent = Math.floor(s / 60) + ":" + String(s % 60).padStart(2, "0");
        }, 1000);
    }

    // ---------- Kamera / mikrofon ----------
    async function getMedia() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            throw new Error(T("Bu brauzer zəngi dəstəkləmir (HTTPS lazımdır)."));
        }
        try {
            return await navigator.mediaDevices.getUserMedia({
                audio: { echoCancellation: true, noiseSuppression: true },
                video: wantVideo ? { facingMode: "user", width: { ideal: 1280 }, height: { ideal: 720 } } : false
            });
        } catch (e) {
            if (wantVideo) {
                // Kamera yoxdur / icazə verilmədi — səsli davam et
                wantVideo = false;
                return await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
            }
            throw e;
        }
    }

    function renderLocal() {
        selfVideo.srcObject = localStream;
        const hasVideo = localStream.getVideoTracks().length > 0;
        root.classList.toggle("has-self-video", hasVideo);
        btn("camera").hidden = false;
        btn("camera").classList.toggle("is-off", !hasVideo || !localStream.getVideoTracks()[0].enabled);
        btn("flip").hidden = !hasVideo || !/Android|iPhone|iPad|Mobile/i.test(navigator.userAgent);
    }

    // ---------- Uzaq iştirakçı ----------
    function tileFor(connectionId, name) {
        let p = peers.get(connectionId);
        if (p && p.tile) return p.tile;
        const tile = el("div", "dc-call__tile");
        const video = el("video");
        video.autoplay = true;
        video.playsInline = true;
        tile.appendChild(video);
        const label = el("span", "dc-call__name", name || "");
        tile.appendChild(label);
        const avatar = el("div", "dc-call__tile-avatar", (name || "?").trim().charAt(0).toUpperCase());
        tile.appendChild(avatar);
        grid.appendChild(tile);
        updateGrid();
        return tile;
    }

    function updateGrid() {
        const n = grid.children.length;
        grid.dataset.count = String(Math.min(n, 6));
        root.classList.toggle("has-remote", n > 0);
    }

    function createPeer(connectionId, name) {
        const pc = new RTCPeerConnection({ iceServers: cfg.iceServers || [] });
        const entry = { pc, name, tile: null, pendingIce: [] };
        peers.set(connectionId, entry);
        entry.tile = tileFor(connectionId, name);

        localStream.getTracks().forEach((t) => pc.addTrack(t, localStream));

        pc.onicecandidate = (e) => {
            if (e.candidate) signal(connectionId, { type: "ice", candidate: e.candidate });
        };
        pc.ontrack = (e) => {
            const video = entry.tile.querySelector("video");
            if (video.srcObject !== e.streams[0]) video.srcObject = e.streams[0];
            const hasVideo = e.streams[0].getVideoTracks().length > 0;
            entry.tile.classList.toggle("has-video", hasVideo);
            e.track.onmute = () => entry.tile.classList.toggle("has-video", e.streams[0].getVideoTracks().some((t) => !t.muted));
            e.track.onunmute = () => entry.tile.classList.toggle("has-video", e.streams[0].getVideoTracks().some((t) => !t.muted));
        };
        pc.onconnectionstatechange = () => {
            entry.tile.dataset.state = pc.connectionState;
            if (pc.connectionState === "connected") {
                stopRingback();
                setState("connected", "");
                startTimer();
            }
        };
        return entry;
    }

    function removePeer(connectionId) {
        const p = peers.get(connectionId);
        if (!p) return;
        try { p.pc.close(); } catch (e) { /* yox */ }
        if (p.tile) p.tile.remove();
        peers.delete(connectionId);
        updateGrid();
    }

    // ---------- SignalR ----------
    const hubOptions = token ? { accessTokenFactory: () => token } : {};
    const hub = new signalR.HubConnectionBuilder()
        .withUrl("/callHub", hubOptions)
        .withAutomaticReconnect([0, 1000, 3000, 5000])
        .build();

    function signal(to, payload) {
        hub.invoke("Signal", to, payload).catch(() => {});
    }

    hub.on("ParticipantJoined", (p) => {
        // Yeni qoşulan bizə "offer" göndərəcək — hələlik kafel
        if (!peers.has(p.connectionId)) {
            const entry = { pc: null, name: p.name, tile: tileFor(p.connectionId, p.name), pendingIce: [] };
            peers.set(p.connectionId, entry);
        }
        clearTimeout(ringTimeout);
        stopRingback();
        setState("connecting", T("Qoşulur…"));
    });

    hub.on("Signal", async (from, data) => {
        let p = peers.get(from);
        if (!p || !p.pc) {
            const name = p ? p.name : "";
            if (p && p.tile) p.tile.remove();
            peers.delete(from);
            p = createPeer(from, name);
        }
        const pc = p.pc;
        try {
            if (data.type === "offer") {
                await pc.setRemoteDescription({ type: "offer", sdp: data.sdp });
                const answer = await pc.createAnswer();
                await pc.setLocalDescription(answer);
                signal(from, { type: "answer", sdp: answer.sdp });
                flushIce(p);
            } else if (data.type === "answer") {
                await pc.setRemoteDescription({ type: "answer", sdp: data.sdp });
                flushIce(p);
            } else if (data.type === "ice" && data.candidate) {
                if (pc.remoteDescription) await pc.addIceCandidate(data.candidate);
                else p.pendingIce.push(data.candidate);
            }
        } catch (e) {
            console.warn("Siqnal xətası", e);
        }
    });

    async function flushIce(p) {
        while (p.pendingIce.length) {
            try { await p.pc.addIceCandidate(p.pendingIce.shift()); } catch (e) { /* köhnə */ }
        }
    }

    hub.on("ParticipantLeft", (connectionId) => {
        removePeer(connectionId);
        if (peers.size === 0 && answeredAt) setState("waiting", T("Gözlənilir…"));
    });
    hub.on("CallDeclined", () => finish(T("Zəng rədd edildi")));
    hub.on("CallEnded", () => finish(T("Zəng bitdi")));

    async function offerTo(participant) {
        const entry = createPeer(participant.connectionId, participant.name);
        const offer = await entry.pc.createOffer();
        await entry.pc.setLocalDescription(offer);
        signal(participant.connectionId, { type: "offer", sdp: offer.sdp });
    }

    // ---------- Başla ----------
    async function start() {
        if (!token && !cfg.isAuthenticated) {
            location.href = cfg.loginUrl;
            return;
        }
        if (!startConversation && !joinCallId) {
            finish(T("Zəng tapılmadı"));
            return;
        }
        setState("init", T("Kamera və mikrofon açılır…"));
        try {
            localStream = await getMedia();
        } catch (e) {
            finish(e && e.message && e.name !== "NotAllowedError" ? e.message : T("Kamera / mikrofona icazə verilmədi."));
            return;
        }
        renderLocal();
        root.classList.toggle("is-audio", !wantVideo);

        try {
            await hub.start();
        } catch (e) {
            finish(T("Serverə qoşulmaq alınmadı."));
            return;
        }

        try {
            if (joinCallId) {
                const info = await hub.invoke("Join", joinCallId);
                callId = info.callId;
                titleEl.textContent = info.title || "";
                document.title = (info.title || T("Zəng")) + " · DoctorEngine";
                if (!info.participants.length) { finish(T("Zəng bitdi")); return; }
                setState("connecting", T("Qoşulur…"));
                for (const p of info.participants) await offerTo(p);
            } else {
                const info = await hub.invoke("StartCall", startConversation, wantVideo);
                callId = info.callId;
                if (info.title) titleEl.textContent = info.title;
                setState("ringing", info.isGroup ? T("Üzvlərə zəng edilir…") : T("Zəng edilir…"));
                startRingback();
                // 45 saniyə cavab yoxdur — bitir
                ringTimeout = setTimeout(() => { if (!answeredAt && peers.size === 0) finish(T("Cavab yoxdur")); }, 45000);
            }
        } catch (e) {
            finish((e && e.message && e.message.replace(/^.*HubException:\s*/, "")) || T("Zəng alınmadı."));
        }
    }

    // Başlıq: zəng edən tərəfdə yazışmanın adı (sessionStorage ilə ötürülür)
    try {
        const t = hash.get("title") || sessionStorage.getItem("dc-call-title");
        if (t && !titleEl.textContent) titleEl.textContent = t;
    } catch (e) { /* yox */ }

    // ---------- Düymələr ----------
    btn("mic").addEventListener("click", () => {
        if (!localStream) return;
        const t = localStream.getAudioTracks()[0];
        if (!t) return;
        t.enabled = !t.enabled;
        btn("mic").classList.toggle("is-off", !t.enabled);
        btn("mic").querySelector("i").className = t.enabled ? "fa-solid fa-microphone" : "fa-solid fa-microphone-slash";
    });

    btn("camera").addEventListener("click", async () => {
        if (!localStream) return;
        let t = localStream.getVideoTracks()[0];
        if (!t) {
            // Səsli zəngdə kameranı sonradan açmaq
            try {
                const cam = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "user" } });
                t = cam.getVideoTracks()[0];
                localStream.addTrack(t);
                for (const [id, p] of peers) {
                    if (!p.pc) continue;
                    p.pc.addTrack(t, localStream);
                    const offer = await p.pc.createOffer();
                    await p.pc.setLocalDescription(offer);
                    signal(id, { type: "offer", sdp: offer.sdp });
                }
                wantVideo = true;
                root.classList.remove("is-audio");
                renderLocal();
            } catch (e) { /* icazə yoxdur */ }
            return;
        }
        t.enabled = !t.enabled;
        btn("camera").classList.toggle("is-off", !t.enabled);
        btn("camera").querySelector("i").className = t.enabled ? "fa-solid fa-video" : "fa-solid fa-video-slash";
        root.classList.toggle("has-self-video", t.enabled);
    });

    let facing = "user";
    btn("flip").addEventListener("click", async () => {
        const old = localStream && localStream.getVideoTracks()[0];
        if (!old) return;
        facing = facing === "user" ? "environment" : "user";
        try {
            const cam = await navigator.mediaDevices.getUserMedia({ video: { facingMode: facing } });
            const t = cam.getVideoTracks()[0];
            for (const p of peers.values()) {
                const sender = p.pc && p.pc.getSenders().find((s) => s.track && s.track.kind === "video");
                if (sender) await sender.replaceTrack(t);
            }
            localStream.removeTrack(old);
            old.stop();
            localStream.addTrack(t);
            selfVideo.srcObject = localStream;
            selfVideo.classList.toggle("is-back", facing === "environment");
        } catch (e) { /* ikinci kamera yoxdur */ }
    });

    btn("end").addEventListener("click", () => finish(T("Zəng bitdi")));

    function finish(text) {
        if (ended) return;
        ended = true;
        stopRingback();
        clearTimeout(ringTimeout);
        clearInterval(timerHandle);
        if (callId && hub.state === signalR.HubConnectionState.Connected) hub.invoke("Leave", callId).catch(() => {});
        for (const id of Array.from(peers.keys())) removePeer(id);
        if (localStream) localStream.getTracks().forEach((t) => t.stop());
        setState("ended", text || T("Zəng bitdi"));
        setTimeout(() => {
            if (inApp) location.href = "doctorengine://call-ended";
            else { window.close(); setTimeout(() => { if (!window.closed) history.length > 1 ? history.back() : (location.href = "/Chat"); }, 300); }
        }, 1600);
    }

    window.addEventListener("pagehide", () => {
        if (!ended && callId) { try { hub.invoke("Leave", callId); } catch (e) { /* yox */ } }
    });

    start();
})();
