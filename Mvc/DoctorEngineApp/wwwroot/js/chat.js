/* =====================================================
   DoctorEngine — Canlı çat
   Təhlükəsizlik: istifadəçi məlumatı HEÇ VAXT innerHTML ilə yazılmır,
   bütün DOM createElement + textContent ilə qurulur.
   ===================================================== */
(function () {
    "use strict";

    const root = document.getElementById("dcChat");
    const configEl = document.getElementById("dcChatConfig");
    if (!root || !configEl) return;

    const cfg = JSON.parse(configEl.textContent);

    const PAGE_SIZE = 30;
    const IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp"];
    const IMAGE_EXT = [".jpg", ".jpeg", ".png", ".webp"];
    const VIDEO_EXT = [".mp4", ".webm", ".mov", ".m4v", ".ogg"];

    // ---------- DOM ----------
    const body = root.querySelector(".dc-chat__body");
    const list = root.querySelector(".dc-chat__list");
    const loadingEl = root.querySelector(".dc-chat__loading");
    const emptyEl = root.querySelector(".dc-chat__empty");
    const moreBtn = root.querySelector(".dc-chat__more");
    const jumpBtn = root.querySelector(".dc-chat__jump");
    const statusEl = root.querySelector(".dc-chat__status");
    const statusText = root.querySelector(".dc-chat__status-text");

    const form = root.querySelector(".dc-chat__composer");
    const input = root.querySelector(".dc-chat__input");
    const fileInput = root.querySelector(".dc-chat__file");
    const sendBtn = root.querySelector(".dc-chat__send");
    const errorEl = root.querySelector(".dc-chat__error");
    const previewEl = root.querySelector(".dc-chat__preview");
    const progressEl = root.querySelector(".dc-chat__progress");
    const progressBar = root.querySelector(".dc-chat__progress-bar");
    const dropzone = root.querySelector(".dc-chat__dropzone");

    const lightbox = document.querySelector(".dc-lightbox");
    const lightboxImg = lightbox ? lightbox.querySelector("img") : null;

    // ---------- STATE ----------
    const messages = new Map();      // id -> dto
    let oldestId = null;
    let hasMore = false;
    let pendingFile = null;
    let pendingPreviewUrl = null;
    let sending = false;

    // ---------- ŞƏXSİ ÇAT (WhatsApp) ----------
    // Usta (admin): solda yazışmalar siyahısı, seçilmiş müştəri ilə yazışma. Müştəri: yalnız öz yazışması.
    const isAdminView = !!cfg.isAdmin;
    const hasList = true;   // "Çatlar" siyahısı: usta — bütün yazışmalar və qruplar, müştəri — ustalarla yazışma və qrupları
    const isGroupConv = (c) => typeof c === "string" && c.startsWith("g:");
    /** İki istifadəçinin şəxsi yazışması ("+" → istifadəçi seç). */
    const isDirectConv = (c) => typeof c === "string" && c.startsWith("d:");
    /** Üzvlərə əsaslanan yazışma (qrup və ya şəxsi): "mənim mesajım" = mənim adımla göndərilən. */
    const isMemberConv = (c) => isGroupConv(c) || isDirectConv(c);
    const readOnlyNote = root.querySelector(".dc-chat__readonly");
    let peerTypingName = null;
    const titleEl = root.querySelector(".dc-chat__title");
    const logoEl = root.querySelector(".dc-chat__logo");
    const backBtn = root.querySelector(".dc-chat__back");
    const jumpCount = root.querySelector(".dc-chat__jump-count");
    const replyBar = root.querySelector(".dc-reply");
    const emojiBtn = root.querySelector(".dc-chat__emoji-btn");
    const emojiPanel = root.querySelector(".dc-emoji");
    const searchBtn = root.querySelector("[data-dc-search]");
    const searchBox = root.querySelector(".dc-search");
    const menu = document.querySelector(".dc-menu");
    const convsEl = root.querySelector(".dc-convs");
    const convList = convsEl ? convsEl.querySelector(".dc-convs__list") : null;

    let conversation = isAdminView ? null : cfg.currentUser;   // açıq yazışma (müştərinin adı)
    let peer = null;                                             // qarşı tərəf: ad, şəkil, onlayn, ✓✓
    let peerTyping = false;
    let replyTo = null;                                          // cavab verilən mesaj (dto)
    let newWhileAway = 0;                                        // aşağıda olmayanda gələn mesajlar

    // =====================================================
    // KÖMƏKÇİLƏR
    // =====================================================

    function el(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }

    function icon(classes) {
        const i = document.createElement("i");
        i.className = classes;
        i.setAttribute("aria-hidden", "true");
        return i;
    }

    function formatSize(bytes) {
        if (bytes < 1024 * 1024) return Math.max(1, Math.round(bytes / 1024)) + " KB";
        return (bytes / 1024 / 1024).toFixed(1) + " MB";
    }

    function extOf(name) {
        const i = name.lastIndexOf(".");
        return i >= 0 ? name.slice(i).toLowerCase() : "";
    }

    function fileKind(file) {
        const ext = extOf(file.name);
        if (IMAGE_EXT.includes(ext) && IMAGE_TYPES.includes(file.type)) return "image";
        if (VIDEO_EXT.includes(ext) && (file.type.startsWith("video/") || ext === ".mov")) return "video";
        return null;
    }

    /** Fayl uyğundursa null, deyilsə xəta mətni qaytarır. */
    function validateFile(file) {
        const kind = fileKind(file);
        if (!kind) return deT("Yalnız şəkil (jpg, png, webp) və ya video (mp4, webm, mov) göndərilə bilər.");
        const max = kind === "image" ? cfg.maxImageBytes : cfg.maxVideoBytes;
        if (file.size > max) {
            return deT("{0} maksimum {1} MB ola bilər.", kind === "image" ? deT("Şəkil") : deT("Video"), Math.round(max / 1024 / 1024));
        }
        if (file.size === 0) return deT("Fayl boşdur.");
        return null;
    }

    function isSafeUploadUrl(url) {
        return typeof url === "string" && /^\/uploads\/chat\/[A-Za-z0-9]+\.[a-z0-9]+$/.test(url);
    }

    // İstifadəçi adına görə sabit rəng
    function colorFor(name) {
        let h = 0;
        for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) % 360;
        return "hsl(" + h + ", 55%, 45%)";
    }

    function dateLabel(isoDate) {
        const [y, m, d] = isoDate.split("-").map(Number);
        const date = new Date(y, m - 1, d);
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        const diff = Math.round((today - date) / 86400000);
        if (diff === 0) return deT("Bu gün");
        if (diff === 1) return deT("Dünən");
        const months = ["yanvar", "fevral", "mart", "aprel", "may", "iyun", "iyul", "avqust", "sentyabr", "oktyabr", "noyabr", "dekabr"];
        return d + " " + months[m - 1] + (y !== today.getFullYear() ? " " + y : "");
    }

    /** Mətndəki http(s) linkləri təhlükəsiz şəkildə <a> ilə əvəz edir. */
    function appendLinkified(parent, text) {
        const re = /\bhttps?:\/\/[^\s<>"']+/gi;
        let last = 0, match;
        while ((match = re.exec(text)) !== null) {
            if (match.index > last) parent.appendChild(document.createTextNode(text.slice(last, match.index)));
            const url = match[0].replace(/[.,!?)]+$/, "");
            const a = el("a", null, url);
            a.href = url;
            a.target = "_blank";
            a.rel = "noopener noreferrer nofollow";
            parent.appendChild(a);
            last = match.index + url.length;
            re.lastIndex = last;
        }
        if (last < text.length) parent.appendChild(document.createTextNode(text.slice(last)));
    }

    /** Mənim tərəfimin mesajı? (usta üçün bütün ustaların mesajları, müştəri üçün özününkülər) — sağda, yaşıl. */
    function isOutgoing(dto) {
        // Qrupda — yalnız öz mesajlarım sağdadır
        if (isMemberConv(dto.conversation)) return dto.userName === cfg.currentUser;
        return isAdminView ? !!dto.isAdmin : !dto.isAdmin;
    }

    function previewText(dto) {
        if (!dto) return "";
        if (dto.message) return dto.message;
        return dto.attachmentType === "audio" ? "🎤 " + deT("Səs mesajı")
            : dto.attachmentType === "video" ? "🎬 " + deT("Video")
            : dto.attachmentType === "image" ? "📷 " + deT("Şəkil") : "";
    }

    /** Göndərənin görünən adı (cavab sitatında, qrup başlığında). */
    function senderName(dto) {
        if (!dto) return "";
        if (dto.userName === cfg.currentUser) return deT("Siz");
        if (dto.isAdmin && !isAdminView) return deT("Usta");
        return dto.userName;
    }

    const SVG_NS = "http://www.w3.org/2000/svg";
    /** WhatsApp ✓ / ✓✓ işarəsi. state: "sent" | "delivered" | "read" | "pending" */
    function tickNode(state) {
        const span = el("span", "dc-ticks");
        span.dataset.state = state;
        if (state === "pending") {
            span.appendChild(icon("fa-regular fa-clock"));
            return span;
        }
        // WhatsApp kimi: ✓ (bir işarə) və ✓✓ (iki üst-üstə işarə) — xətlə çəkilir, istənilən ölçüdə səliqəli
        const svg = document.createElementNS(SVG_NS, "svg");
        svg.setAttribute("viewBox", state === "sent" ? "0 0 12 11" : "0 0 17 11");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("fill", "none");
        svg.setAttribute("stroke", "currentColor");
        svg.setAttribute("stroke-width", "1.5");
        svg.setAttribute("stroke-linecap", "round");
        svg.setAttribute("stroke-linejoin", "round");
        const lines = state === "sent"
            ? ["1.5 6 4.5 9 10.5 1.5"]
            : ["1.5 6 4.5 9 10.5 1.5", "7.6 8.1 8.5 9 14.5 1.5"];
        lines.forEach((pts) => {
            const pl = document.createElementNS(SVG_NS, "polyline");
            pl.setAttribute("points", pts);
            svg.appendChild(pl);
        });
        span.appendChild(svg);
        const label = state === "read" ? deT("Oxundu") : state === "delivered" ? deT("Çatdı") : deT("Göndərildi");
        span.title = label;
        span.setAttribute("aria-label", label);
        return span;
    }

    function tickState(dto) {
        if (!peer) return "sent";
        if (peer.readUpToId >= dto.id) return "read";
        if (peer.deliveredUpToId >= dto.id) return "delivered";
        return "sent";
    }

    /** "son görülmə bu gün 14:20" */
    function lastSeenLabel(iso) {
        if (!iso) return "";
        const [d, t] = iso.split("T");
        const hm = (t || "").slice(0, 5);
        const label = dateLabel(d);
        return deT("son görülmə {0}", (label === deT("Bu gün") ? deT("bu gün") : label === deT("Dünən") ? deT("dünən") : label) + " " + hm);
    }

    /** Dairəvi avatar: şəkil və ya adın ilk hərfi (rəngli). */
    function avatarNode(url, name, className) {
        const node = el("div", className || "dc-avatar");
        if (url && /^\/uploads\/(avatars|site)\/[A-Za-z0-9_.-]+$/.test(url)) {
            const img = document.createElement("img");
            img.src = url;
            img.alt = "";
            img.loading = "lazy";
            node.appendChild(img);
        } else {
            node.textContent = (name || "?").trim().charAt(0).toUpperCase();
            node.style.background = colorFor(name || "?");
        }
        return node;
    }

    function isNearBottom() {
        return body.scrollHeight - body.scrollTop - body.clientHeight < 120;
    }

    function scrollToBottom(smooth) {
        body.style.scrollBehavior = smooth ? "smooth" : "auto";
        body.scrollTop = body.scrollHeight;
        body.style.scrollBehavior = "";
        jumpBtn.hidden = true;
        newWhileAway = 0;
        if (typeof renderJump === "function") renderJump();
    }

    function setStatus(state, text) {
        statusEl.dataset.state = state;
        statusText.textContent = text;
    }

    function showError(message) {
        if (!errorEl) {
            if (window.deUI) window.deUI.toast(message, "error");
            return;
        }
        errorEl.textContent = message;
        errorEl.hidden = false;
        clearTimeout(showError._t);
        showError._t = setTimeout(() => (errorEl.hidden = true), 6000);
    }

    function clearError() {
        if (errorEl) errorEl.hidden = true;
    }

    /** Server xətasını oxunaqlı mətnə çevirir. */
    function errorFromResponse(status, data) {
        if (data && data.error) return data.error;
        if (data && data.errors) {
            const first = Object.values(data.errors).flat()[0];
            if (first) return first;
        }
        if (status === 413) return deT("Fayl çox böyükdür.");
        if (status === 404) return deT("Mesaj tapılmadı (silinmiş ola bilər).");
        if (status === 400) return deT("Sorğu qəbul olunmadı. Səhifəni yeniləyib yenidən cəhd edin.");
        return deT("Xəta baş verdi. Yenidən cəhd edin.");
    }

    /**
     * POST sorğusu (XHR — yükləmə faizini göstərmək üçün).
     * Antiforgery token header ilə göndərilir.
     */
    function post(url, formData, onProgress) {
        return new Promise((resolve) => {
            const xhr = new XMLHttpRequest();
            xhr.open("POST", url);
            xhr.setRequestHeader("RequestVerificationToken", cfg.token);
            xhr.responseType = "json";

            if (onProgress && xhr.upload) {
                xhr.upload.onprogress = (e) => {
                    if (e.lengthComputable) onProgress(e.loaded / e.total);
                };
            }

            xhr.onload = () => {
                if (xhr.status === 401) {
                    window.location.href = cfg.loginUrl;
                    return;
                }
                const ok = xhr.status >= 200 && xhr.status < 300;
                resolve({ ok, data: xhr.response, error: ok ? null : errorFromResponse(xhr.status, xhr.response) });
            };
            xhr.onerror = () => resolve({ ok: false, error: deT("Şəbəkə xətası. İnternet bağlantınızı yoxlayın.") });
            xhr.send(formData || null);
        });
    }

    // =====================================================
    // MESAJIN QURULMASI
    // =====================================================

    function buildMedia(dto) {
        if (!dto.attachmentUrl || !isSafeUploadUrl(dto.attachmentUrl)) return null;

        const wrap = el("div", "dc-msg__media");

        if (dto.attachmentType === "image") {
            const img = document.createElement("img");
            img.src = dto.attachmentUrl;
            img.alt = deT("Şəkil");
            img.loading = "lazy";
            img.addEventListener("click", () => openLightbox(dto.attachmentUrl));
            img.addEventListener("load", keepBottomOnMediaLoad, { once: true });
            wrap.appendChild(img);
        } else if (dto.attachmentType === "video") {
            const video = document.createElement("video");
            video.src = dto.attachmentUrl;
            video.controls = true;
            video.preload = "metadata";
            video.playsInline = true;
            video.addEventListener("loadedmetadata", keepBottomOnMediaLoad, { once: true });
            wrap.appendChild(video);
        } else {
            return null;
        }

        return wrap;
    }

    // =====================================================
    // SƏS MESAJI PLEYERİ (WhatsApp üslubu: oynat düyməsi, dalğa, müddət, sürət)
    // =====================================================

    const VOICE_BARS = 40;
    const peaksCache = new Map();   // url -> Promise<{ peaks, duration } | null>
    let decodeCtx = null;
    let currentVoice = null;        // eyni anda yalnız bir səs oynasın

    function formatDuration(sec) {
        if (!isFinite(sec) || sec < 0) sec = 0;
        sec = Math.round(sec);
        return Math.floor(sec / 60) + ":" + String(sec % 60).padStart(2, "0");
    }

    // Fayl olmayanda / dekod alınmayanda: id-yə görə sabit "dalğa"
    function seededPeaks(seed) {
        let x = (Number(seed) || 1) * 9301 + 49297;
        return Array.from({ length: VOICE_BARS }, () => {
            x = (x * 9301 + 49297) % 233280;
            return 0.25 + (x / 233280) * 0.75;
        });
    }

    /** Səs faylını dekod edib həqiqi dalğa formasını və müddəti qaytarır. */
    function loadPeaks(url) {
        if (peaksCache.has(url)) return peaksCache.get(url);
        const job = (async () => {
            try {
                const Ctx = window.AudioContext || window.webkitAudioContext;
                if (!Ctx) return null;
                decodeCtx = decodeCtx || new Ctx();
                const buf = await (await fetch(url)).arrayBuffer();
                const audio = await new Promise((res, rej) => {
                    const p = decodeCtx.decodeAudioData(buf, res, rej);
                    if (p && p.then) p.then(res, rej);
                });
                const data = audio.getChannelData(0);
                const block = Math.max(1, Math.floor(data.length / VOICE_BARS));
                const peaks = [];
                for (let i = 0; i < VOICE_BARS; i++) {
                    let sum = 0;
                    for (let j = 0; j < block; j++) sum += Math.abs(data[i * block + j] || 0);
                    peaks.push(sum / block);
                }
                const max = Math.max(...peaks) || 1;

                // Səviyyə normallaşdırması üçün: danışığın orta səviyyəsi və pik (oxutmada sakit yazılar qaldırılır)
                const win = Math.max(1, Math.round(audio.sampleRate * 0.02));
                let sum = 0, cnt = 0, pk = 0;
                for (let i = 0; i < data.length; i++) { const v = Math.abs(data[i]); if (v > pk) pk = v; }
                for (let i = 0; i + win <= data.length; i += win) {
                    let s = 0;
                    for (let j = 0; j < win; j++) s += data[i + j] * data[i + j];
                    const lv = 10 * Math.log10(s / win + 1e-12);
                    if (lv > -45) { sum += lv; cnt++; }
                }
                const speechDb = cnt ? sum / cnt : -30;
                const peakDb = 20 * Math.log10(pk + 1e-12);
                // Hədəf: danışıq −20 dB; ən çox +12 dB; pik (EQ +4 dB nəzərə alınmaqla) 1.5 dB ehtiyatla 0 dB-ni keçməsin
                const gainDb = Math.max(0, Math.min(12, -20 - speechDb, -peakDb - 4 - 1.5));

                return { peaks: peaks.map((p) => 0.15 + 0.85 * (p / max)), duration: audio.duration, gainDb };
            } catch (e) {
                return null;
            }
        })();
        peaksCache.set(url, job);
        return job;
    }

    // ---------- Oxutma zamanı səsin aydınlaşdırılması ----------
    // Yazı xam mikrofondan aparılır (gecikməsiz); səs oxudulanda emal olunur.
    // Real yazıların analizi: aşağı uğultu (<150 Hz) danışıq qədər güclü, aydınlıq (3–6 kHz) 15–24 dB zəif idi → "boğuq" səs.
    //   1) 150 Hz-dən aşağı kəsilir (uğultu)
    //   2) 3 kHz ətrafı +4 dB (samitlər — s, ş, t, k — aydın eşidilir)
    //   3) 10 kHz-dən yuxarı yumşaq kəsilir (xışıltı; danışığı küt etmir)
    //   4) sakit yazılar qaldırılır (ən çox +12 dB, pik qorunur → kəsilmə/təhrif yoxdur)
    // Sınaq (3 real yazı): uğultu səsdən 8–12 dB aşağı, kəsilmə 0. Kompressor qəsdən YOXDUR —
    // Chrome-un kompressoru avtomatik gücləndirmə əlavə edib xışıltını artırırdı.
    let playCtx = null;

    function connectPlaybackFilter(audio) {
        if (audio._filtered) return;
        audio._filtered = true;   // createMediaElementSource hər element üçün yalnız bir dəfə çağırıla bilər
        try {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) return;
            playCtx = playCtx || new Ctx();
            const src = playCtx.createMediaElementSource(audio);
            // Diqqət: Web Audio-da highpass/lowpass üçün Q desibeldir, peaking üçün isə adi ədəddir
            const hp = playCtx.createBiquadFilter();
            hp.type = "highpass"; hp.frequency.value = 150; hp.Q.value = -3.01;   // 0.707
            const presence = playCtx.createBiquadFilter();
            presence.type = "peaking"; presence.frequency.value = 3000; presence.Q.value = 0.8; presence.gain.value = 4;
            const lp = playCtx.createBiquadFilter();
            lp.type = "lowpass"; lp.frequency.value = 10000; lp.Q.value = -3.01;
            const gain = playCtx.createGain();
            gain.gain.value = Math.pow(10, (audio._gainDb || 0) / 20);
            audio._gainNode = gain;
            src.connect(hp).connect(presence).connect(lp).connect(gain).connect(playCtx.destination);
        } catch (e) {
            console.warn("Oxutma emalı qoşulmadı:", e);   // səs emalsız çalınır
        }
    }

    function stopVoiceIn(node) {
        const v = node.querySelector(".dc-voice");
        if (v && v._audio) v._audio.pause();
    }

    function buildVoice(dto) {
        const wrap = el("div", "dc-voice");

        const playBtn = el("button", "dc-voice__play");
        playBtn.type = "button";
        playBtn.setAttribute("aria-label", deT("Səs mesajını oynat"));
        const playIcon = icon("fa-solid fa-play");
        playBtn.appendChild(playIcon);

        const main = el("div", "dc-voice__main");
        const track = el("div", "dc-voice__track");
        track.setAttribute("role", "slider");
        track.setAttribute("aria-label", deT("Səs mesajı"));
        track.tabIndex = 0;
        const bars = seededPeaks(dto.id).map((h) => {
            const b = el("span", "dc-voice__bar");
            b.style.height = Math.round(h * 100) + "%";
            track.appendChild(b);
            return b;
        });

        const info = el("div", "dc-voice__info");
        const timeEl = el("span", "dc-voice__time", "0:00");
        const speedBtn = el("button", "dc-voice__speed", "1×");
        speedBtn.type = "button";
        speedBtn.title = deT("Oxutma sürəti");
        info.append(timeEl, speedBtn);
        main.append(track, info);

        wrap.append(playBtn, main);

        const audio = new Audio();
        wrap._audio = audio;   // mesaj silinəndə / yenilənəndə dayandırmaq üçün
        audio.preload = "metadata";
        audio.src = dto.attachmentUrl;

        let duration = 0;
        const getDuration = () => duration || (isFinite(audio.duration) ? audio.duration : 0);

        function render() {
            const d = getDuration();
            const pos = audio.currentTime || 0;
            const ratio = d ? Math.min(1, pos / d) : 0;
            const played = Math.round(ratio * VOICE_BARS);
            bars.forEach((b, i) => b.classList.toggle("is-played", i < played));
            timeEl.textContent = formatDuration(!audio.paused || pos > 0 ? pos : d);
            track.setAttribute("aria-valuenow", String(Math.round(ratio * 100)));
        }

        // MediaRecorder webm faylında müddət "Infinity" olur — brauzeri onu hesablamağa məcbur et.
        // Bu müddətdə "oynat" basılsa, səs sondan başlayıb atlanırdı — hazır olana qədər gözlənilir.
        let ready = Promise.resolve();
        audio.addEventListener("loadedmetadata", () => {
            if (audio.duration === Infinity) {
                ready = new Promise((resolve) => {
                    const fix = () => {
                        audio.removeEventListener("durationchange", fix);
                        audio.currentTime = 0;
                        render();
                        resolve();
                    };
                    audio.addEventListener("durationchange", fix);
                    setTimeout(fix, 3000);   // ehtiyat: hadisə gəlməsə də oynatma bloklanmasın
                });
                audio.currentTime = 1e101;
            } else {
                render();
            }
        });

        loadPeaks(dto.attachmentUrl).then((res) => {
            if (!res) return;
            duration = res.duration;
            // Səviyyə normallaşdırması: oxutma artıq başlayıbsa da yenilənir
            audio._gainDb = res.gainDb || 0;
            if (audio._gainNode) audio._gainNode.gain.value = Math.pow(10, audio._gainDb / 20);
            res.peaks.forEach((h, i) => (bars[i].style.height = Math.round(h * 100) + "%"));
            render();
        });

        audio.addEventListener("timeupdate", render);
        audio.addEventListener("play", () => {
            if (currentVoice && currentVoice !== audio) currentVoice.pause();
            currentVoice = audio;
            playIcon.className = "fa-solid fa-pause";
            playBtn.setAttribute("aria-label", deT("Fasilə"));
            wrap.classList.add("is-playing");
        });
        audio.addEventListener("pause", () => {
            playIcon.className = "fa-solid fa-play";
            playBtn.setAttribute("aria-label", deT("Səs mesajını oynat"));
            wrap.classList.remove("is-playing");
        });
        audio.addEventListener("ended", () => {
            audio.currentTime = 0;
            render();
        });
        audio.addEventListener("error", () => {
            wrap.classList.add("is-error");
            timeEl.textContent = deT("Fayl açılmadı");
        });

        playBtn.addEventListener("click", async () => {
            if (!audio.paused) { audio.pause(); return; }
            connectPlaybackFilter(audio);
            if (playCtx && playCtx.state === "suspended") await playCtx.resume().catch(() => {});
            await ready;
            audio.play().catch(() => {});
        });

        function seek(clientX) {
            const d = getDuration();
            if (!d) return;
            const r = track.getBoundingClientRect();
            const ratio = Math.min(1, Math.max(0, (clientX - r.left) / r.width));
            audio.currentTime = ratio * d;
            render();
        }

        track.addEventListener("pointerdown", (e) => {
            seek(e.clientX);
            track.setPointerCapture(e.pointerId);
            const move = (ev) => seek(ev.clientX);
            const up = () => {
                track.removeEventListener("pointermove", move);
                track.removeEventListener("pointerup", up);
            };
            track.addEventListener("pointermove", move);
            track.addEventListener("pointerup", up);
        });

        track.addEventListener("keydown", (e) => {
            const d = getDuration();
            if (!d) return;
            if (e.key === "ArrowRight") audio.currentTime = Math.min(d, audio.currentTime + 5);
            else if (e.key === "ArrowLeft") audio.currentTime = Math.max(0, audio.currentTime - 5);
            else if (e.key === " " || e.key === "Enter") playBtn.click();
            else return;
            e.preventDefault();
            render();
        });

        const speeds = [1, 1.5, 2];
        speedBtn.addEventListener("click", () => {
            const next = speeds[(speeds.indexOf(audio.playbackRate) + 1) % speeds.length];
            audio.playbackRate = next;
            speedBtn.textContent = next + "×";
        });

        return wrap;
    }

    // Şəkil yüklənəndə hündürlük dəyişir — aşağıdaydısa aşağıda saxla
    let stickToBottom = true;
    function keepBottomOnMediaLoad() {
        if (stickToBottom) scrollToBottom(false);
    }

    function buildMessage(dto) {
        const isMine = cfg.currentUser !== null && dto.userName === cfg.currentUser;
        const outgoing = isOutgoing(dto);

        const node = el("div", "dc-msg");
        node.dataset.id = dto.id;
        node.dataset.user = dto.userName;
        node.dataset.date = dto.date;
        node.classList.add(outgoing ? "is-out" : "is-in");
        if (isMine) node.classList.add("is-mine");
        if (dto.isAdmin) node.classList.add("is-admin");

        const content = el("div", "dc-msg__content");
        const bubble = el("div", "dc-msg__bubble");

        // Ad: başqa ustanın mesajı (usta görünüşündə) və ya ustanın mesajı (müştəri görünüşündə)
        if (!isMine && !isDirectConv(dto.conversation) && (dto.isAdmin || isGroupConv(dto.conversation))) {
            const name = el("div", "dc-msg__name");
            name.appendChild(el("span", null, dto.userName));
            name.style.color = colorFor(dto.userName || "?");
            if (!isAdminView && dto.isAdmin) {
                const badge = el("span", "dc-badge");
                badge.appendChild(icon("fa-solid fa-screwdriver-wrench"));
                badge.appendChild(document.createTextNode(deT("Usta")));
                name.appendChild(badge);
            }
            bubble.appendChild(name);
        }

        // Cavab verilən mesaj (sitat)
        if (dto.replyTo) bubble.appendChild(buildQuote(dto.replyTo));

        if (dto.attachmentType === "audio" && isSafeUploadUrl(dto.attachmentUrl)) {
            bubble.classList.add("has-voice");
            bubble.appendChild(buildVoice(dto));
        } else {
            const media = buildMedia(dto);
            if (media) {
                bubble.classList.add("has-media");
                // WhatsApp kimi: başlıqsız şəkildə saat şəklin üstündə görünür
                if (!dto.message && dto.attachmentType === "image") bubble.classList.add("is-media-only");
                bubble.appendChild(media);
            }
        }

        if (dto.message) {
            const text = el("div", "dc-msg__text");
            // Yalnız emoji (1–3 ədəd) — WhatsApp kimi böyük
            if (/^(\p{Extended_Pictographic}|\p{Emoji_Component}|️|‍|\s){1,12}$/u.test(dto.message) &&
                !/\d/.test(dto.message) && Array.from(dto.message.replace(/\s/g, "")).length <= 8) {
                text.classList.add("is-jumbo");
            }
            appendLinkified(text, dto.message);
            bubble.appendChild(text);
        }

        const meta = el("div", "dc-msg__meta");
        if (dto.isEdited) meta.appendChild(el("span", "dc-msg__edited", deT("redaktə olunub")));
        const time = el("time", null, dto.time);
        time.dateTime = dto.date + "T" + dto.time;
        meta.appendChild(time);
        if (outgoing) meta.appendChild(tickNode(tickState(dto)));
        bubble.appendChild(meta);

        content.appendChild(bubble);

        // Reaksiyalar (balonun altında)
        if (dto.reactions && dto.reactions.length) content.appendChild(buildReactions(dto));

        node.appendChild(content);

        // Hover əməliyyatları: reaksiya + menyu (WhatsApp Web kimi)
        const actions = el("div", "dc-msg__actions");
        const reactBtn = el("button", "dc-msg__action");
        reactBtn.type = "button";
        reactBtn.title = deT("Reaksiya");
        reactBtn.setAttribute("aria-label", deT("Reaksiya"));
        reactBtn.appendChild(icon("fa-regular fa-face-smile"));
        reactBtn.addEventListener("click", (e) => { e.stopPropagation(); openMenu(dto.id, reactBtn); });
        actions.appendChild(reactBtn);

        const moreBtn = el("button", "dc-msg__action");
        moreBtn.type = "button";
        moreBtn.title = deT("Menyu");
        moreBtn.setAttribute("aria-label", deT("Mesaj menyusu"));
        moreBtn.appendChild(icon("fa-solid fa-chevron-down"));
        moreBtn.addEventListener("click", (e) => { e.stopPropagation(); openMenu(dto.id, moreBtn); });
        actions.appendChild(moreBtn);
        node.appendChild(actions);

        attachGestures(node, dto.id);
        return node;
    }

    /** Balonun içində cavab verilən mesajın sitatı — klikləyəndə həmin mesaja keçir. */
    function buildQuote(r) {
        const q = el("button", "dc-quote");
        q.type = "button";
        if (r.deleted) {
            q.classList.add("is-deleted");
            q.appendChild(el("span", "dc-quote__text", deT("Bu mesaj silinib")));
            return q;
        }
        const author = { userName: r.userName, isAdmin: r.isAdmin };
        q.classList.add(isOutgoing(author) ? "is-out" : "is-in");
        const name = el("span", "dc-quote__name", senderName(author));
        name.style.color = r.userName === cfg.currentUser ? "" : colorFor(r.userName || "?");
        q.appendChild(name);
        q.appendChild(el("span", "dc-quote__text", previewText(r) || deT("Mesaj")));
        q.addEventListener("click", (e) => { e.stopPropagation(); jumpTo(r.id); });
        return q;
    }

    function buildReactions(dto) {
        const wrap = el("div", "dc-reacts");
        dto.reactions.forEach((r) => {
            const b = el("button", "dc-react");
            b.type = "button";
            const mine = r.users.includes(cfg.currentUser);
            if (mine) b.classList.add("is-mine");
            b.appendChild(el("span", "dc-react__emoji", r.emoji));
            if (r.users.length > 1) b.appendChild(el("span", "dc-react__count", String(r.users.length)));
            b.title = r.users.map((u) => (u === cfg.currentUser ? deT("Siz") : u)).join(", ");
            b.addEventListener("click", (e) => { e.stopPropagation(); react(dto.id, r.emoji); });
            wrap.appendChild(b);
        });
        return wrap;
    }

    // ---------- Toxunma: uzun basma → menyu, sağa sürüşdürmə → cavab (WhatsApp kimi) ----------
    function attachGestures(node, id) {
        let startX = 0, startY = 0, dx = 0, pressTimer = 0, tracking = false;
        node.addEventListener("pointerdown", (e) => {
            if (e.pointerType === "mouse" || e.target.closest("button, a, video, audio, input, textarea, .dc-voice")) return;
            startX = e.clientX; startY = e.clientY; dx = 0; tracking = true;
            pressTimer = setTimeout(() => { tracking = false; openMenu(id, node.querySelector(".dc-msg__bubble")); }, 520);
        });
        node.addEventListener("pointermove", (e) => {
            if (!tracking) return;
            const mx = e.clientX - startX, my = e.clientY - startY;
            if (Math.abs(my) > 12 && Math.abs(my) > Math.abs(mx)) { tracking = false; clearTimeout(pressTimer); node.style.transform = ""; return; }
            if (Math.abs(mx) > 6) clearTimeout(pressTimer);
            dx = Math.max(0, Math.min(80, mx));
            node.style.transform = dx ? "translateX(" + dx + "px)" : "";
            node.classList.toggle("is-swiping", dx > 55);
        });
        const end = () => {
            clearTimeout(pressTimer);
            if (!tracking) return;
            tracking = false;
            node.style.transition = "transform .18s ease";
            node.style.transform = "";
            setTimeout(() => (node.style.transition = ""), 200);
            node.classList.remove("is-swiping");
            if (dx > 55) setReply(id);
        };
        node.addEventListener("pointerup", end);
        node.addEventListener("pointercancel", end);
        node.addEventListener("pointerleave", end);
        // Kompüterdə: iki dəfə klik → cavab
        node.addEventListener("dblclick", (e) => {
            if (e.target.closest("button, a, video, audio, img, .dc-voice")) return;
            window.getSelection && window.getSelection().removeAllRanges();
            setReply(id);
        });
        node.addEventListener("contextmenu", (e) => {
            if (e.target.closest("a, video, img")) return;
            e.preventDefault();
            openMenu(id, node.querySelector(".dc-msg__bubble"));
        });
    }

    // =====================================================
    // MESAJ MENYUSU (reaksiyalar + cavab ver / kopyala / redaktə / sil)
    // =====================================================

    let menuFor = null;

    function openMenu(id, anchor) {
        const dto = messages.get(id);
        if (!menu || !dto || !anchor) return;
        menuFor = id;

        const reactRow = menu.querySelector(".dc-menu__reactions");
        const actionsRow = menu.querySelector(".dc-menu__actions");
        reactRow.replaceChildren();
        actionsRow.replaceChildren();

        const myReaction = (dto.reactions || []).find((r) => r.users.includes(cfg.currentUser));
        const all = cfg.reactions || ["👍", "❤️", "😂", "😮", "😢", "🙏"];
        const renderReactions = (list, expanded) => {
            reactRow.replaceChildren();
            reactRow.classList.toggle("is-expanded", expanded);
            list.forEach((emoji) => {
                const b = el("button", "dc-menu__emoji", emoji);
                b.type = "button";
                if (myReaction && myReaction.emoji === emoji) b.classList.add("is-mine");
                b.addEventListener("click", () => { closeMenu(); react(id, emoji); });
                reactRow.appendChild(b);
            });
            if (!expanded && all.length > 6) {
                const more = el("button", "dc-menu__emoji dc-menu__more");
                more.type = "button";
                more.setAttribute("aria-label", deT("Daha çox"));
                more.appendChild(icon("fa-solid fa-plus"));
                more.addEventListener("click", (e) => { e.stopPropagation(); renderReactions(all, true); });
                reactRow.appendChild(more);
            }
        };
        renderReactions(all.slice(0, 6), false);

        const isMine = dto.userName === cfg.currentUser;
        const addAction = (iconClass, label, handler, danger) => {
            const b = el("button", "dc-menu__item" + (danger ? " is-danger" : ""));
            b.type = "button";
            b.setAttribute("role", "menuitem");
            b.appendChild(icon(iconClass));
            b.appendChild(el("span", null, label));
            b.addEventListener("click", () => { closeMenu(); handler(); });
            actionsRow.appendChild(b);
        };
        addAction("fa-solid fa-reply", deT("Cavab ver"), () => setReply(id));
        if (dto.message) addAction("fa-regular fa-copy", deT("Kopyala"), () => copyText(dto.message));
        if (isMine) addAction("fa-solid fa-pen", deT("Redaktə et"), () => startEdit(id));
        if (isMine || cfg.isAdmin) addAction("fa-solid fa-trash", deT("Sil"), () => deleteMessage(id), true);

        menu.hidden = false;
        // Yerləşdirmə: anker elementin yanında, ekrandan çıxmasın
        const r = anchor.getBoundingClientRect();
        const mw = menu.offsetWidth, mh = menu.offsetHeight;
        let top = r.bottom + 6;
        if (top + mh > window.innerHeight - 8) top = Math.max(8, r.top - mh - 6);
        let left = isOutgoing(dto) ? r.right - mw : r.left;
        left = Math.min(Math.max(8, left), window.innerWidth - mw - 8);
        menu.style.top = top + "px";
        menu.style.left = left + "px";
        const first = menu.querySelector("button");
        if (first) first.focus({ preventScroll: true });
    }

    function closeMenu() {
        if (!menu || menu.hidden) return;
        menu.hidden = true;
        menuFor = null;
    }

    if (menu) {
        document.addEventListener("pointerdown", (e) => { if (!menu.hidden && !menu.contains(e.target)) closeMenu(); });
        document.addEventListener("keydown", (e) => { if (e.key === "Escape") closeMenu(); });
        window.addEventListener("resize", closeMenu);
    }

    async function copyText(text) {
        try {
            await navigator.clipboard.writeText(text);
            if (window.deUI) window.deUI.toast(deT("Kopyalandı"), "success");
        } catch (e) { /* brauzer icazə vermədi */ }
    }

    async function react(id, emoji) {
        const dto = messages.get(id);
        if (!dto) return;
        const fd = new FormData();
        fd.append("id", String(id));
        fd.append("emoji", emoji);
        const res = await post("/api/chat/react", fd);
        if (res.ok && res.data && res.data.id) {
            upsert(res.data);
            refreshLayout();
        } else if (!res.ok) {
            showError(res.error);
        }
    }

    // =====================================================
    // CAVAB VER
    // =====================================================

    function setReply(id) {
        const dto = messages.get(id);
        if (!dto || !replyBar || !input) return;
        replyTo = dto;
        const name = replyBar.querySelector(".dc-reply__name");
        name.textContent = senderName(dto);
        name.style.color = dto.userName === cfg.currentUser ? "" : colorFor(dto.userName || "?");
        replyBar.querySelector(".dc-reply__text").textContent = previewText(dto) || deT("Mesaj");
        replyBar.classList.toggle("is-out", isOutgoing(dto));
        replyBar.hidden = false;
        input.focus();
    }

    function clearReply() {
        replyTo = null;
        if (replyBar) replyBar.hidden = true;
    }

    if (replyBar) replyBar.querySelector(".dc-reply__cancel").addEventListener("click", clearReply);

    /** Mesaja keç (lazım olsa köhnə səhifələri yükləyir) və qısa müddət vurğula. */
    async function jumpTo(id) {
        let guard = 0;
        while (!messages.has(id) && hasMore && guard++ < 30) {
            await loadOlder();
        }
        const node = list.querySelector('.dc-msg[data-id="' + id + '"]');
        if (!node) {
            showError(deT("Mesaj tapılmadı (silinmiş ola bilər)."));
            return;
        }
        node.scrollIntoView({ block: "center", behavior: "smooth" });
        node.classList.remove("is-flash");
        void node.offsetWidth;
        node.classList.add("is-flash");
    }

    /** ✓✓ işarələrini qarşı tərəfin vəziyyətinə görə yeniləyir (yenidən qurmadan). */
    function refreshTicks() {
        list.querySelectorAll(".dc-msg.is-out").forEach((node) => {
            const dto = messages.get(Number(node.dataset.id));
            const old = node.querySelector(".dc-msg__meta .dc-ticks");
            if (!dto || !old) return;
            const state = tickState(dto);
            if (old.dataset.state !== state) old.replaceWith(tickNode(state));
        });
    }

    /** Tarix ayırıcılarını və ardıcıl mesaj qruplaşdırmasını yeniləyir. */
    function refreshLayout() {
        list.querySelectorAll(".dc-sep").forEach((s) => s.remove());

        let prev = null;
        list.querySelectorAll(".dc-msg").forEach((node) => {
            const newDay = !prev || prev.dataset.date !== node.dataset.date;
            if (newDay) {
                const sep = el("div", "dc-sep");
                sep.appendChild(el("span", null, dateLabel(node.dataset.date)));
                list.insertBefore(sep, node);
            }
            node.classList.toggle("is-grouped", !newDay && prev.dataset.user === node.dataset.user);
            prev = node;
        });

        emptyEl.hidden = messages.size > 0;
    }

    /** Mesajı əlavə edir və ya mövcudunu yeniləyir (id-yə görə, dublikat olmur). */
    function upsert(dto, { prepend = false } = {}) {
        const existing = list.querySelector('.dc-msg[data-id="' + dto.id + '"]');
        messages.set(dto.id, dto);
        const node = buildMessage(dto);

        if (existing) {
            // Redaktə rejimindədirsə — redaktoru pozma
            if (existing.classList.contains("is-editing")) return node;
            stopVoiceIn(existing);
            existing.replaceWith(node);
            node.style.animation = "none";
        } else if (prepend) {
            list.insertBefore(node, list.firstChild);
        } else {
            // id sırasına görə düzgün yerə qoy
            const after = Array.from(list.querySelectorAll(".dc-msg")).find((n) => Number(n.dataset.id) > dto.id);
            list.insertBefore(node, after || null);
        }
        if (oldestId === null || dto.id < oldestId) oldestId = dto.id;
        return node;
    }

    function removeMessage(id) {
        messages.delete(id);
        const node = list.querySelector('.dc-msg[data-id="' + id + '"]');
        if (!node) return;
        stopVoiceIn(node);
        node.classList.add("is-removing");
        setTimeout(() => {
            node.remove();
            refreshLayout();
        }, 250);
    }

    // =====================================================
    // YÜKLƏMƏ
    // =====================================================

    function convQuery() {
        return conversation ? "&conversation=" + encodeURIComponent(conversation) : "";
    }

    function appendConv(fd) {
        if (conversation) fd.append("conversation", conversation);
        return fd;
    }

    async function loadPage(beforeId) {
        const url = "/api/chat/messages?take=" + PAGE_SIZE + (beforeId ? "&beforeId=" + beforeId : "") + convQuery();
        const res = await fetch(url, { headers: { Accept: "application/json" } });
        if (!res.ok) throw new Error("HTTP " + res.status);
        return res.json();
    }

    // ---------- Qarşı tərəf: başlıq (ad, şəkil, onlayn / son görülmə / yazır…) ----------
    function setPeer(p) {
        if (!p) return;
        peer = p;
        if (titleEl) titleEl.textContent = p.title || "";
        if (logoEl) {
            const av = avatarNode(p.avatarUrl, p.title, "dc-chat__logo");
            if (p.isGroup) {
                av.replaceChildren(icon("fa-solid fa-users"));
                av.style.background = "";
            } else if (!p.avatarUrl && !isAdminView && !p.isDirect && !isDirectConv(p.conversation)) {
                av.replaceChildren(icon("fa-solid fa-screwdriver-wrench"));
                av.style.background = "";
            }
            logoEl.replaceChildren(...av.childNodes);
            logoEl.style.background = av.style.background;
            logoEl.classList.toggle("has-img", !!av.querySelector("img"));
        }
        root.classList.toggle("is-group", !!p.isGroup);
        const e2e = root.querySelector(".dc-chat__e2e");
        if (e2e) {
            if (!e2e.dataset.text) e2e.dataset.text = e2e.textContent.trim();
            e2e.replaceChildren(icon("fa-solid fa-lock"), document.createTextNode(" " + (p.isGroup
                ? (p.allUsers ? deT("Bu qrupda bütün istifadəçilər var — mesajları hamı görür.") : deT("Bu qrupun mesajlarını yalnız üzvlər və ustalar görür."))
                : (p.isDirect || isDirectConv(p.conversation)) ? deT("Bu yazışmanı yalnız siz və {0} görür.", p.title || "")
                : e2e.dataset.text)));
        }
        // "Yalnız ustalar yaza bilər" qrupu — müştəri yaza bilmir
        const readOnly = p.canWrite === false;
        root.classList.toggle("is-readonly", readOnly);
        if (readOnlyNote) readOnlyNote.hidden = !readOnly;
        if (form) form.hidden = readOnly;
        // Zəng düymələri: qrupda qrup zəngi
        root.querySelectorAll("[data-dc-call]").forEach((b) => (b.hidden = !window.dcCalls));
        renderPeerStatus();
        refreshTicks();
    }

    function renderPeerStatus() {
        if (!statusEl) return;
        if (connectionState !== "online") return;   // "Qoşulur…" / "Bağlantı kəsildi" görünür
        if (peerTyping) setStatus("typing", peerTypingName && peer && peer.isGroup ? deT("{0} yazır…", peerTypingName) : deT("yazır…"));
        else if (!peer) setStatus("online", "");
        else if (peer.isGroup) setStatus("online", peer.allUsers ? deT("Bütün istifadəçilər") : deT("{0} üzv", peer.memberCount || 0));
        else if (peer.online) setStatus("online", deT("onlayn"));
        else setStatus("away", peer.lastSeen ? lastSeenLabel(peer.lastSeen) : (isAdminView || peer.isDirect || isDirectConv(conversation) ? "" : deT("Ustalar tezliklə cavab verəcək")));
    }

    async function loadInitial() {
        try {
            const page = await loadPage(null);
            if (page.peer) setPeer(page.peer);
            page.messages.forEach((m) => upsert(m));
            hasMore = page.hasMore;
            moreBtn.hidden = !hasMore;
            refreshLayout();
            stickToBottom = true;
            scrollToBottom(false);
            markDeliveredSoon();
        } catch (e) {
            console.error(e);
            emptyEl.hidden = false;
            emptyEl.querySelector("p").textContent = deT("Mesajları yükləmək mümkün olmadı. Səhifəni yeniləyin.");
        } finally {
            loadingEl.hidden = true;
        }
    }

    async function loadOlder() {
        if (!hasMore || oldestId === null) return;
        moreBtn.disabled = true;
        const prevHeight = body.scrollHeight;
        try {
            const page = await loadPage(oldestId);
            page.messages.slice().reverse().forEach((m) => upsert(m, { prepend: true }));
            hasMore = page.hasMore;
            moreBtn.hidden = !hasMore;
            refreshLayout();
            // Ekrandakı yeri saxla
            body.style.scrollBehavior = "auto";
            body.scrollTop = body.scrollHeight - prevHeight;
            body.style.scrollBehavior = "";
        } catch (e) {
            showError(deT("Köhnə mesajlar yüklənmədi."));
        } finally {
            moreBtn.disabled = false;
        }
    }

    /** Yenidən qoşulduqda qaçırılan mesajları gətir. */
    async function resync() {
        if (!conversation) return;
        try {
            const page = await loadPage(null);
            if (page.peer) setPeer(page.peer);
            const ids = new Set(page.messages.map((m) => m.id));
            const minId = page.messages.length ? page.messages[0].id : Infinity;
            // Bu aralıqda olub serverdə olmayanlar silinib
            Array.from(messages.keys()).forEach((id) => {
                if (id >= minId && !ids.has(id)) removeMessage(id);
            });
            const wasBottom = isNearBottom();
            page.messages.forEach((m) => upsert(m));
            refreshLayout();
            if (wasBottom) scrollToBottom(false);
        } catch (e) {
            console.error(e);
        }
    }

    // =====================================================
    // GECİKMİŞ YÜKLƏMƏ + OXUNMAMIŞ MESAJLAR
    // =====================================================

    let loaded = false;
    let loadPromise = null;
    let drawerOpen = false;
    let unread = 0;
    let latestId = 0;
    let markTimer = 0;

    function ensureLoaded() {
        if (!conversation) return Promise.resolve();   // usta: hələ yazışma seçilməyib
        if (!loadPromise) {
            loaded = true;   // bu andan canlı mesajlar da siyahıya əlavə olunur (upsert dublikat yaratmır)
            loadPromise = loadInitial();
        }
        return loadPromise;
    }

    /** Usta: siyahıdan yazışmanı açır (əvvəlki yazışmanın vəziyyəti sıfırlanır). */
    function openConversation(conv, row) {
        if (!conv) return Promise.resolve();
        if (rec) cancelRecording();
        if (currentVoice) currentVoice.pause();
        closeMenu();
        closeSearch();
        clearReply();
        clearPendingFile();

        closeSide();
        conversation = conv;
        peer = row ? { conversation: conv, title: row.title, avatarUrl: row.avatarUrl, online: row.online, lastSeen: row.lastSeen, isGroup: !!row.isGroup, isDirect: !!row.isDirect || isDirectConv(conv), readUpToId: 0, deliveredUpToId: 0 } : null;
        peerTyping = false;
        messages.clear();
        list.querySelectorAll(".dc-msg").forEach((n) => stopVoiceIn(n));
        list.replaceChildren();
        oldestId = null;
        hasMore = false;
        moreBtn.hidden = true;
        emptyEl.hidden = true;
        loadingEl.hidden = false;
        latestId = 0;
        newWhileAway = 0;
        renderJump();
        if (peer) setPeer(peer);

        root.dataset.view = "chat";
        root.classList.add("has-conversation");
        if (convList) convList.querySelectorAll(".dc-conv").forEach((li) =>
            li.classList.toggle("is-active", li.dataset.conv === conv));

        // Ünvan sətrində saxla (yenilənəndə eyni yazışma açılsın)
        if (cfg.fullPage && window.history && window.history.replaceState) {
            const u = new URL(window.location.href);
            u.searchParams.set("c", conv);
            window.history.replaceState(null, "", u);
        }

        loadPromise = null;
        return ensureLoaded().then(() => {
            if (isChatVisible()) markRead();
            if (input) input.focus({ preventScroll: true });
        });
    }

    function closeConversation() {
        if (typeof closeSide === "function") closeSide();
        root.dataset.view = "list";
        root.classList.remove("has-conversation");
        if (rec) cancelRecording();
        if (currentVoice) currentVoice.pause();
        closeMenu();
        closeSearch();
        conversation = null;
        loaded = false;
        loadPromise = null;
        if (convList) convList.querySelectorAll(".dc-conv.is-active").forEach((li) => li.classList.remove("is-active"));
        if (cfg.fullPage && window.history && window.history.replaceState) {
            const u = new URL(window.location.href);
            u.searchParams.delete("c");
            window.history.replaceState(null, "", u);
        }
    }

    if (backBtn) backBtn.addEventListener("click", closeConversation);

    /** İstifadəçi çatı hazırda görürmü? */
    function isChatVisible() {
        if (document.visibilityState !== "visible") return false;
        if (!conversation) return false;
        if (hasList && root.dataset.view !== "chat" && !isWide()) return false;
        return cfg.drawer ? drawerOpen : loaded;
    }

    /** Geniş ekran: siyahı və yazışma yan-yana (usta, tam səhifə). */
    function isWide() {
        return cfg.fullPage && window.matchMedia("(min-width: 900px)").matches;
    }

    /** Badge-i yeniləyir (chat-launcher.js "dc:unread" hadisəsini dinləyir). */
    function setUnread(count, preview, initial) {
        unread = Math.max(0, count);
        document.dispatchEvent(new CustomEvent("dc:unread", {
            detail: { count: unread, preview: preview || null, initial: !!initial }
        }));
    }

    async function markRead() {
        clearTimeout(markTimer);
        if (!conversation) return;
        const maxLoaded = messages.size ? Math.max(...messages.keys()) : 0;
        const lastId = Math.max(latestId, maxLoaded);
        if (hasList) {
            // Yalnız açıq yazışmanın oxunmamışları sıfırlanır
            const row = convRows.get(conversation);
            if (row) { row.unread = 0; renderConv(row); }
            emitAdminUnread();
        } else {
            setUnread(0);
        }
        if (!lastId) return;
        const fd = appendConv(new FormData());
        fd.append("lastId", String(lastId));
        await post("/api/chat/read", fd);
    }

    // "Çatdı" (✓✓): mesaj ekrana gəlməsə də cihaza çatıb
    let deliverTimer = 0;
    let deliverHint = 0;   // panel bağlı olanda da (mesajlar yüklənməyib) gələn mesajın id-si
    function markDeliveredSoon(id) {
        if (id) deliverHint = Math.max(deliverHint, id);
        clearTimeout(deliverTimer);
        deliverTimer = setTimeout(async () => {
            if (!conversation) return;
            const incoming = Array.from(messages.values()).filter((m) => !isOutgoing(m));
            const lastId = Math.max(deliverHint, incoming.length ? Math.max(...incoming.map((m) => m.id)) : 0);
            deliverHint = 0;
            if (!lastId) return;
            const fd = appendConv(new FormData());
            fd.append("lastId", String(lastId));
            await post("/api/chat/delivered", fd);
        }, 600);
    }

    // ---------- Bildiriş səsi (fayl yoxdur — Web Audio ilə qısa "dinq") ----------
    const SOUND_KEY = "dc-sound-muted";
    const soundBtn = root.querySelector("[data-dc-sound]");
    let soundMuted = false;
    try { soundMuted = localStorage.getItem(SOUND_KEY) === "1"; } catch (e) { /* yaddaş bağlıdır */ }
    let chimeCtx = null;
    let lastChimeAt = 0;

    // Brauzerlər səsə yalnız istifadəçi sayta toxunduqdan sonra icazə verir
    function unlockAudio() {
        const Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) return;
        try {
            chimeCtx = chimeCtx || new Ctx();
            if (chimeCtx.state === "suspended") chimeCtx.resume().catch(() => {});
        } catch (e) { /* səs olmadan davam */ }
    }
    ["pointerdown", "keydown", "touchstart"].forEach((ev) =>
        document.addEventListener(ev, unlockAudio, { passive: true }));

    function playChime() {
        if (soundMuted || !chimeCtx || chimeCtx.state !== "running") return;
        const now = Date.now();
        if (now - lastChimeAt < 1200) return;   // ardıcıl mesajlarda səs üst-üstə düşməsin
        lastChimeAt = now;

        const t = chimeCtx.currentTime;
        [[880, 0], [1318.5, 0.11]].forEach(([freq, delay]) => {
            const osc = chimeCtx.createOscillator();
            const gain = chimeCtx.createGain();
            osc.type = "sine";
            osc.frequency.value = freq;
            gain.gain.setValueAtTime(0.0001, t + delay);
            gain.gain.exponentialRampToValueAtTime(0.16, t + delay + 0.015);
            gain.gain.exponentialRampToValueAtTime(0.0001, t + delay + 0.38);
            osc.connect(gain);
            gain.connect(chimeCtx.destination);
            osc.start(t + delay);
            osc.stop(t + delay + 0.42);
        });
    }

    function renderSoundBtn() {
        if (!soundBtn) return;
        soundBtn.querySelector("i").className = soundMuted ? "fa-solid fa-bell-slash" : "fa-solid fa-bell";
        const label = soundMuted ? deT("Bildiriş səsini aç") : deT("Bildiriş səsini söndür");
        soundBtn.title = label;
        soundBtn.setAttribute("aria-label", label);
        soundBtn.setAttribute("aria-pressed", String(soundMuted));
    }

    // ---------- Web Push: sayt bağlı / arxa planda olanda sistem bildirişi + səs ----------
    const pushBar = root.querySelector(".dc-chat__push");
    const PUSH_DISMISS_KEY = "dc-push-dismissed";
    const pushSupported = !!(cfg.pushKey && window.isSecureContext && "serviceWorker" in navigator &&
        "PushManager" in window && "Notification" in window);
    let pushActive = false;

    function keyBytes(b64) {
        const pad = "=".repeat((4 - (b64.length % 4)) % 4);
        const raw = atob((b64 + pad).replace(/-/g, "+").replace(/_/g, "/"));
        return Uint8Array.from(raw, (ch) => ch.charCodeAt(0));
    }

    function sameKey(buf, bytes) {
        if (!buf) return false;
        const a = new Uint8Array(buf);
        return a.length === bytes.length && a.every((v, i) => v === bytes[i]);
    }

    function renderPushBar() {
        if (!pushBar) return;
        let dismissed = false;
        try { dismissed = localStorage.getItem(PUSH_DISMISS_KEY) === "1"; } catch (e) { /* yox */ }
        pushBar.hidden = !pushSupported || soundMuted || dismissed || Notification.permission !== "default";
    }

    /** askPermission yalnız istifadəçi kliki ilə true olur (brauzer tələbi). */
    async function enablePush(askPermission) {
        if (!pushSupported) return false;
        try {
            let perm = Notification.permission;
            if (perm === "default" && askPermission) perm = await Notification.requestPermission();
            if (perm !== "granted") {
                pushActive = false;
                if (perm === "denied" && askPermission) {
                    showError(deT("Bildirişlər brauzerdə bloklanıb. Ünvan sətrindəki kilid işarəsindən icazə verin."));
                }
                return false;
            }

            await navigator.serviceWorker.register("/sw.js");
            const reg = await navigator.serviceWorker.ready;
            const key = keyBytes(cfg.pushKey);

            let sub = await reg.pushManager.getSubscription();
            // Server açarı dəyişibsə köhnə abunəlik işləməz — yenisini al
            if (sub && sub.options && sub.options.applicationServerKey && !sameKey(sub.options.applicationServerKey, key)) {
                await sub.unsubscribe();
                sub = null;
            }
            if (!sub) sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });

            const json = sub.toJSON();
            const fd = new FormData();
            fd.append("endpoint", json.endpoint);
            fd.append("p256dh", json.keys.p256dh);
            fd.append("auth", json.keys.auth);
            pushActive = (await post("/api/chat/push/subscribe", fd)).ok;
        } catch (e) {
            console.warn("Push aktivləşmədi:", e);
            pushActive = false;
        } finally {
            renderPushBar();
        }
        return pushActive;
    }

    async function disablePush() {
        pushActive = false;
        renderPushBar();
        if (!pushSupported) return;
        try {
            const reg = await navigator.serviceWorker.getRegistration("/");
            const sub = reg && await reg.pushManager.getSubscription();
            if (!sub) return;
            const fd = new FormData();
            fd.append("endpoint", sub.endpoint);
            await post("/api/chat/push/unsubscribe", fd);
            await sub.unsubscribe();
        } catch (e) { /* növbəti dəfə */ }
    }

    if (pushBar) {
        pushBar.querySelector(".dc-chat__push-on").addEventListener("click", () => enablePush(true));
        pushBar.querySelector(".dc-chat__push-x").addEventListener("click", () => {
            try { localStorage.setItem(PUSH_DISMISS_KEY, "1"); } catch (e) { /* yox */ }
            renderPushBar();
        });
    }

    if (pushSupported && !soundMuted && Notification.permission === "granted") enablePush(false);
    renderPushBar();

    if (soundBtn) {
        soundBtn.addEventListener("click", () => {
            soundMuted = !soundMuted;
            try { localStorage.setItem(SOUND_KEY, soundMuted ? "1" : "0"); } catch (e) { /* yox */ }
            renderSoundBtn();
            if (soundMuted) {
                disablePush();
            } else {
                unlockAudio();
                lastChimeAt = 0;
                setTimeout(playChime, 50);   // nümunə səs
                enablePush(true);            // klik = istifadəçi jesti, icazə soruşmaq olar
            }
        });
        renderSoundBtn();
    }

    function markReadSoon() {
        clearTimeout(markTimer);
        markTimer = setTimeout(markRead, 800);
    }

    async function refreshUnread() {
        if (hasList) {
            if (isChatVisible()) markReadSoon();
            await loadConversations();
            return;
        }
        if (isChatVisible()) { markReadSoon(); return; }
        try {
            const res = await fetch("/api/chat/unread", { headers: { Accept: "application/json" } });
            if (res.ok) setUnread((await res.json()).count || 0);
        } catch (e) { /* növbəti dəfə */ }
    }

    document.addEventListener("visibilitychange", () => {
        if (isChatVisible() && unread > 0) markRead();
    });

    moreBtn.addEventListener("click", loadOlder);

    body.addEventListener("scroll", () => {
        stickToBottom = isNearBottom();
        // WhatsApp kimi: yuxarı sürüşdürəndə "aşağı" düyməsi görünür
        jumpBtn.hidden = stickToBottom;
        if (stickToBottom && newWhileAway) { newWhileAway = 0; renderJump(); }
        if (body.scrollTop < 40 && hasMore && !moreBtn.disabled) loadOlder();
    });

    jumpBtn.addEventListener("click", () => scrollToBottom(true));

    // =====================================================
    // SIGNALR
    // =====================================================

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
        .build();

    connection.on("ReceiveMessage", (dto) => {
        const isMine = dto.userName === cfg.currentUser;
        const current = dto.conversation === conversation;

        if (current) {
            if (dto.id > latestId) latestId = dto.id;
            // Qarşı tərəf mesaj yazıbsa — "yazır…" bitib
            if (!isOutgoing(dto) && peerTyping) { peerTyping = false; renderPeerStatus(); }

            if (loaded) {
                const wasBottom = isNearBottom();
                upsert(dto);
                refreshLayout();
                if (wasBottom || isMine) scrollToBottom(true);
                else { newWhileAway++; renderJump(); }
            }
        }

        if (isMine || isOutgoing(dto)) return;
        // Səhifə arxa plandadırsa və push aktivdirsə, sistem bildirişi səsi çalınır — ikiqat olmasın
        if (document.visibilityState === "visible" || !pushActive) playChime();

        if (current && isChatVisible()) {
            markReadSoon();
        } else {
            if (current) markDeliveredSoon(dto.id);
            const row = convRows.get(dto.conversation);
            const preview = {
                userName: isGroupConv(dto.conversation)
                    ? ((row && row.title) || "") + " · " + dto.userName
                    : isAdminView ? (row && row.title) || dto.userName : dto.userName,
                isAdmin: !!dto.isAdmin,
                conversation: dto.conversation,
                text: previewText(dto)
            };
            if (row) { row.unread = (row.unread || 0) + 1; }
            emitAdminUnread(preview);
            if (!current) {
                // Başqa yazışmaya gələn mesaj da "çatdı" sayılır (onlayndır)
                const fd = new FormData();
                fd.append("conversation", dto.conversation);
                fd.append("lastId", String(dto.id));
                post("/api/chat/delivered", fd);
            }
        }
    });

    connection.on("EditMessage", (dto) => {
        if (!messages.has(dto.id)) return;
        upsert(dto);
        refreshLayout();
    });

    connection.on("DeleteMessage", (id) => removeMessage(id));

    // ✓✓: qarşı tərəf (qrupda — hamı) aldı / oxudu
    connection.on("Receipt", (r) => {
        if (!r || r.conversation !== conversation || !peer) return;
        const peerSide = isMemberConv(conversation) ? "group" : isAdminView ? "user" : "admin";
        if (r.side !== peerSide) return;
        peer.readUpToId = Math.max(peer.readUpToId || 0, r.readUpToId);
        peer.deliveredUpToId = Math.max(peer.deliveredUpToId || 0, r.deliveredUpToId);
        refreshTicks();
    });

    // "yazır…"
    const typingTimers = new Map();
    connection.on("Typing", (t) => {
        if (!t || t.userName === cfg.currentUser) return;
        const fromPeer = t.isGroup ? true : isAdminView ? !t.fromAdmin : t.fromAdmin;
        if (!fromPeer) return;
        const key = t.conversation;
        clearTimeout(typingTimers.get(key));
        if (t.isTyping) typingTimers.set(key, setTimeout(() => setTyping(key, false), 6000));
        setTyping(key, !!t.isTyping, t.userName);
    });

    function setTyping(conv, on, name) {
        if (conv === conversation) {
            peerTyping = on;
            peerTypingName = on ? name || null : null;
            renderPeerStatus();
        }
        const row = convRows.get(conv);
        if (row) { row.typing = on; row.typingName = on && row.isGroup ? name : null; renderConv(row); }
    }

    // Onlayn / son görülmə
    connection.on("Presence", (p) => {
        if (!p) return;
        if (p.isSupport) {
            if (isAdminView) return;
            convRows.forEach((r) => { if (r.isSupport) { r.online = p.online; r.lastSeen = p.lastSeen; renderConv(r); } });
            if (!peer || peer.isGroup || conversation !== cfg.currentUser) return;
            peer.online = p.online;
            peer.lastSeen = p.lastSeen;
            renderPeerStatus();
            return;
        }
        if (!isAdminView) return;
        const row = convRows.get(p.conversation);
        if (row) { row.online = p.online; row.lastSeen = p.lastSeen; renderConv(row); }
        if (peer && p.conversation === conversation) {
            peer.online = p.online;
            peer.lastSeen = p.lastSeen;
            renderPeerStatus();
        }
    });

    // Yazışmalar siyahısı: şəxsi yazışmanın hazır sətri (ustalar)
    connection.on("ConversationUpdated", (row) => {
        if (!isAdminView || !row) return;
        const old = convRows.get(row.conversation);
        if (old && old.typing) row.typing = old.typing;
        // Açıq və görünən yazışma oxunmuş sayılır
        if (row.conversation === conversation && isChatVisible()) row.unread = 0;
        convRows.set(row.conversation, row);
        renderConvList();
        emitAdminUnread();
    });

    // Qrup / yazışma dəyişdi — siyahını yenilə (hər istifadəçinin sətri fərqlidir)
    let convReloadTimer = 0;
    connection.on("ConversationChanged", (conv) => {
        clearTimeout(convReloadTimer);
        convReloadTimer = setTimeout(async () => {
            await loadConversations();
            // Açıq qrupun adı / üzvləri / "yalnız ustalar" dəyişibsə — başlıq yenilənsin
            if (conv === conversation && isGroupConv(conv)) {
                try { const page = await loadPage(null); if (page.peer) setPeer(page.peer); } catch (e) { /* növbəti dəfə */ }
            }
        }, 500);
    });

    connection.on("ConversationCleared", (e) => {
        if (!e || !e.conversation) return;
        onConversationCleared(e.conversation, !!e.forEveryone, false);
    });

    connection.on("ConversationRemoved", (conv) => {
        convRows.delete(conv);
        renderConvList();
        emitAdminUnread();
        if (conv === conversation) {
            closeConversation();
            if (window.deUI) window.deUI.toast(deT("Qrup silindi və ya siz qrupdan çıxarıldınız."), "info");
        }
    });
    let connectionState = "connecting";
    function setConnState(state, text) {
        connectionState = state;
        if (state === "online") renderPeerStatus();
        else setStatus(state, text);
    }

    connection.onreconnecting(() => setConnState("connecting", deT("Yenidən qoşulur…")));
    connection.onreconnected(() => {
        setConnState("online");
        if (loaded) resync();
        if (isAdminView) loadConversations();
        refreshUnread();
    });
    connection.onclose(() => {
        setConnState("offline", deT("Bağlantı kəsildi"));
        setTimeout(startConnection, 5000);
    });

    async function startConnection() {
        try {
            await connection.start();
            setConnState("online");
        } catch (e) {
            setConnState("offline", deT("Bağlantı yoxdur"));
            setTimeout(startConnection, 5000);
        }
    }

    // Onlayn vəziyyəti köhnəlməsin (server 90 saniyə siqnal almasa "son görülmə" göstərir)
    setInterval(() => {
        if (connection.state === signalR.HubConnectionState.Connected) connection.invoke("Heartbeat").catch(() => {});
    }, 30000);

    // ---------- Mən yazıram… (qarşı tərəfə) ----------
    let typingSentAt = 0, typingStopTimer = 0, typingOn = false;
    function notifyTyping() {
        if (!conversation || connection.state !== signalR.HubConnectionState.Connected) return;
        const now = Date.now();
        if (!typingOn || now - typingSentAt > 3000) {
            typingOn = true;
            typingSentAt = now;
            connection.invoke("Typing", isAdminView || isMemberConv(conversation) ? conversation : null, true).catch(() => {});
        }
        clearTimeout(typingStopTimer);
        typingStopTimer = setTimeout(stopTyping, 3500);
    }
    function stopTyping() {
        clearTimeout(typingStopTimer);
        if (!typingOn) return;
        typingOn = false;
        if (connection.state === signalR.HubConnectionState.Connected)
            connection.invoke("Typing", isAdminView || isMemberConv(conversation) ? conversation : null, false).catch(() => {});
    }

    // =====================================================
    // USTA: YAZIŞMALAR SİYAHISI
    // =====================================================

    const convRows = new Map();   // conversation -> ChatConversationDto
    let convFilter = "all";
    let convSearch = "";

    function emitAdminUnread(preview) {
        let total = 0;
        convRows.forEach((r) => { if (!(r.conversation === conversation && isChatVisible())) total += r.unread || 0; });
        const totalEl = convsEl ? convsEl.querySelector(".dc-convs__total") : null;
        if (totalEl) { totalEl.textContent = total; totalEl.hidden = !total; }
        setUnread(total, preview || null);
    }

    /** "14:20" (bu gün), "Dünən", "12.11.2026" */
    function convTime(iso) {
        if (!iso) return "";
        const [d, t] = iso.split("T");
        const label = dateLabel(d);
        if (label === deT("Bu gün")) return (t || "").slice(0, 5);
        if (label === deT("Dünən")) return label;
        const [y, m, day] = d.split("-");
        return day + "." + m + "." + y;
    }

    function renderConv(row) {
        if (!convList) return;
        let li = convList.querySelector('.dc-conv[data-conv="' + CSS.escape(row.conversation) + '"]');
        const fresh = buildConv(row);
        if (li) li.replaceWith(fresh);
        else convList.appendChild(fresh);
    }

    function buildConv(row) {
        const li = el("li", "dc-conv");
        li.dataset.conv = row.conversation;
        li.setAttribute("role", "option");
        li.tabIndex = 0;
        if (row.conversation === conversation) li.classList.add("is-active");
        if (row.unread) li.classList.add("has-unread");

        const av = avatarNode(row.avatarUrl, row.title, "dc-avatar dc-conv__avatar");
        if (row.isGroup) { av.replaceChildren(icon("fa-solid fa-users")); av.classList.add("is-group"); av.style.background = ""; }
        else if (row.isSupport && !row.avatarUrl) { av.replaceChildren(icon("fa-solid fa-screwdriver-wrench")); av.classList.add("is-support"); av.style.background = ""; }
        if (row.online && !row.isGroup) av.classList.add("is-online");
        li.appendChild(av);

        const main = el("div", "dc-conv__main");
        const top = el("div", "dc-conv__top");
        top.appendChild(el("span", "dc-conv__title", row.title || row.conversation));
        top.appendChild(el("span", "dc-conv__time", convTime(row.lastDate)));
        main.appendChild(top);

        const bottom = el("div", "dc-conv__bottom");
        const last = el("span", "dc-conv__last");
        if (row.typing) {
            last.classList.add("is-typing");
            last.textContent = row.typingName ? deT("{0} yazır…", row.typingName) : deT("yazır…");
        } else {
            // Müştəri üçün: ustalarla yazışmada öz son mesajım (✓✓); usta üçün: ustaların son mesajı; qrupda: mənim mesajım
            const mineLast = row.isSupport ? !row.lastFromAdmin && !!row.lastDate : row.lastFromAdmin;
            if (mineLast) last.appendChild(tickNode(row.lastRead ? "read" : row.lastDelivered ? "delivered" : "sent"));
            if (row.isGroup && row.lastSender) last.appendChild(el("b", "dc-conv__sender", row.lastSender + ": "));
            last.appendChild(document.createTextNode(row.lastMessage || ""));
        }
        bottom.appendChild(last);
        if (row.unread) bottom.appendChild(el("span", "dc-conv__badge", row.unread > 99 ? "99+" : String(row.unread)));
        main.appendChild(bottom);
        li.appendChild(main);

        const open = () => openConversation(row.conversation, row);
        li.addEventListener("click", (e) => { if (!e.target.closest(".dc-conv__more")) open(); });
        li.addEventListener("keydown", (e) => {
            if (e.key === "Enter" || e.key === " ") { e.preventDefault(); open(); }
            else if (e.key === "Delete") { e.preventDefault(); confirmClear(row); }
        });

        // Menyu: kompüterdə "⌄" düyməsi / sağ klik, telefonda uzun basma (WhatsApp kimi)
        const more = el("button", "dc-conv__more");
        more.type = "button";
        more.setAttribute("aria-label", deT("Yazışma menyusu"));
        more.appendChild(icon("fa-solid fa-chevron-down"));
        more.addEventListener("click", (e) => { e.stopPropagation(); openConvMenu(row, more); });
        bottom.appendChild(more);
        li.addEventListener("contextmenu", (e) => { e.preventDefault(); openConvMenu(row, li, e.clientX, e.clientY); });
        let press = 0, sx = 0, sy = 0;
        li.addEventListener("pointerdown", (e) => {
            if (e.pointerType === "mouse") return;
            sx = e.clientX; sy = e.clientY;
            clearTimeout(press);
            press = setTimeout(() => {
                li.dataset.longpress = "1";
                if (navigator.vibrate) navigator.vibrate(15);
                openConvMenu(row, li, sx, sy);
            }, 500);
        });
        li.addEventListener("pointermove", (e) => { if (Math.abs(e.clientX - sx) > 8 || Math.abs(e.clientY - sy) > 8) clearTimeout(press); });
        ["pointerup", "pointercancel", "pointerleave"].forEach((ev) => li.addEventListener(ev, () => clearTimeout(press)));
        // Uzun basmadan sonra gələn "click" yazışmanı açmasın
        li.addEventListener("click", (e) => { if (li.dataset.longpress) { e.stopImmediatePropagation(); delete li.dataset.longpress; } }, true);
        return li;
    }

    // =====================================================
    // YAZIŞMANI SİL (WhatsApp kimi)
    // =====================================================

    let convMenu = null;
    function closeConvMenu() {
        if (!convMenu) return;
        convMenu.remove();
        convMenu = null;
        if (convList) convList.querySelectorAll(".dc-conv.is-menu").forEach((n) => n.classList.remove("is-menu"));
    }

    function openConvMenu(row, anchor, x, y) {
        closeConvMenu();
        const m = el("div", "dc-cmenu");
        m.setAttribute("role", "menu");
        const item = (iconClass, label, handler, danger) => {
            const b = el("button", "dc-cmenu__item" + (danger ? " is-danger" : ""));
            b.type = "button";
            b.setAttribute("role", "menuitem");
            b.appendChild(icon(iconClass));
            b.appendChild(el("span", null, label));
            b.addEventListener("click", (e) => { e.stopPropagation(); closeConvMenu(); handler(); });
            m.appendChild(b);
        };
        item("fa-regular fa-comment", deT("Yazışmanı aç"), () => openConversation(row.conversation, row));
        item("fa-regular fa-trash-can", deT("Yazışmanı sil"), () => confirmClear(row), true);
        document.body.appendChild(m);
        convMenu = m;
        const li = anchor.closest(".dc-conv");
        if (li) li.classList.add("is-menu");

        const r = anchor.getBoundingClientRect();
        const mw = m.offsetWidth, mh = m.offsetHeight;
        let left = x != null ? x : r.right - mw;
        let top = y != null ? y : r.bottom + 4;
        if (top + mh > window.innerHeight - 8) top = Math.max(8, (y != null ? y : r.top) - mh - 4);
        left = Math.min(Math.max(8, left), window.innerWidth - mw - 8);
        m.style.left = left + "px";
        m.style.top = top + "px";
        const first = m.querySelector("button");
        if (first) first.focus({ preventScroll: true });
    }
    document.addEventListener("pointerdown", (e) => { if (convMenu && !convMenu.contains(e.target)) closeConvMenu(); }, true);
    document.addEventListener("keydown", (e) => { if (e.key === "Escape") closeConvMenu(); });
    window.addEventListener("resize", closeConvMenu);
    if (convList) convList.addEventListener("scroll", closeConvMenu, { passive: true });

    /** "Yazışma silinsin?" — usta üçün "Mənim üçün" / "Hamı üçün", müştəri üçün yalnız özü üçün. */
    function confirmClear(row) {
        if (!row || !row.conversation) return;
        const title = row.title || row.conversation;
        const body = el("div", "dc-clear");
        body.appendChild(el("p", "dc-clear__text", isAdminView
            ? (row.isGroup
                ? deT("\"{0}\" qrupunun mesajları silinsin? \"Hamı üçün\" seçsəniz, mesajlar bütün üzvlərdən silinəcək.", title)
                : deT("{0} ilə yazışma silinsin? \"Hamı üçün\" seçsəniz, mesajlar və fayllar hər iki tərəfdən silinəcək.", title))
            : deT("{0} ilə yazışma silinsin? Mesajlar yalnız sizdən silinəcək, qarşı tərəfdə qalacaq.", title)));
        body.appendChild(el("p", "dc-clear__hint", deT("Yeni mesaj gələndə yazışma yenidən görünəcək.")));

        let modal;
        const run = async (forEveryone, btn) => {
            if (btn) btn.disabled = true;
            const fd = new FormData();
            fd.append("conversation", row.conversation);
            fd.append("forEveryone", forEveryone ? "true" : "false");
            const r = await post("/api/chat/clear", fd);
            if (btn) btn.disabled = false;
            if (!r.ok) { showError(r.error); return; }
            modal.close();
            onConversationCleared(row.conversation, forEveryone, true);
        };
        const buttons = [modalButton(deT("Ləğv et"), null, () => modal.close())];
        if (isAdminView) {
            const me = modalButton(deT("Mənim üçün sil"), "primary", () => run(false, me));
            const all = modalButton(deT("Hamı üçün sil"), "danger", () => run(true, all));
            buttons.push(me, all);
        } else {
            const del = modalButton(deT("Sil"), "danger", () => run(false, del));
            buttons.push(del);
        }
        modal = openModal(deT("Yazışma silinsin?"), body, buttons, "dc-modal--clear");
    }

    /** Yazışma silindi (bu cihazda, digər cihazımda və ya usta hamı üçün sildi). */
    function onConversationCleared(conv, forEveryone, byMe) {
        const row = convRows.get(conv);
        if (row && !row.isSupport && !(forEveryone && row.isGroup)) convRows.delete(conv);
        if (conv === conversation) {
            if (isWide() || !hasList) openConversation(conv, row || null);
            else closeConversation();
        }
        renderConvList();
        loadConversations();
        if (byMe && window.deUI) window.deUI.toast(deT("Yazışma silindi"), "success");
    }

    function renderConvList() {
        if (!convList) return;
        const q = convSearch.trim().toLowerCase();
        const rows = Array.from(convRows.values())
            .filter((r) => convFilter !== "unread" || r.unread > 0)
            .filter((r) => convFilter !== "groups" || r.isGroup)
            .filter((r) => !q || (r.title || "").toLowerCase().includes(q) || r.conversation.toLowerCase().includes(q))
            .sort((a, b) => b.lastMessageId - a.lastMessageId);
        convList.replaceChildren(...rows.map(buildConv));
        const emptyConv = convsEl.querySelector(".dc-convs__empty");
        emptyConv.hidden = rows.length > 0;
        emptyConv.querySelector("p").textContent = q || convFilter === "unread"
            ? deT("Heç nə tapılmadı.") : deT("Hələ yazışma yoxdur.");
    }

    async function loadConversations() {
        try {
            const res = await fetch("/api/chat/conversations", { headers: { Accept: "application/json" } });
            if (!res.ok) throw new Error("HTTP " + res.status);
            const rows = await res.json();
            convRows.clear();
            rows.forEach((r) => {
                if (r.conversation === conversation && isChatVisible()) r.unread = 0;
                convRows.set(r.conversation, r);
            });
            renderConvList();
            emitAdminUnread();
        } catch (e) {
            console.error(e);
        } finally {
            const l = convsEl && convsEl.querySelector(".dc-convs__loading");
            if (l) l.hidden = true;
        }
    }

    if (convsEl) {
        const search = convsEl.querySelector(".dc-convs__search input");
        search.addEventListener("input", () => { convSearch = search.value; renderConvList(); });
        convsEl.querySelectorAll(".dc-convs__filters button").forEach((b) => b.addEventListener("click", () => {
            convFilter = b.dataset.filter;
            convsEl.querySelectorAll(".dc-convs__filters button").forEach((x) => x.classList.toggle("is-active", x === b));
            renderConvList();
        }));
    }

    // =====================================================
    // YAZIŞMADA AXTARIŞ
    // =====================================================

    function openSearch() {
        if (!searchBox || !conversation) return;
        searchBox.hidden = false;
        const inp = searchBox.querySelector(".dc-search__input");
        inp.value = "";
        searchBox.querySelector(".dc-search__results").replaceChildren();
        inp.focus();
    }

    function closeSearch() {
        if (!searchBox) return;
        searchBox.hidden = true;
        list.querySelectorAll(".dc-msg__text mark").forEach((m) => m.replaceWith(document.createTextNode(m.textContent)));
    }

    function highlight(text, q) {
        const frag = document.createDocumentFragment();
        const lower = text.toLowerCase(), ql = q.toLowerCase();
        let i = 0, idx;
        while ((idx = lower.indexOf(ql, i)) !== -1) {
            if (idx > i) frag.appendChild(document.createTextNode(text.slice(i, idx)));
            frag.appendChild(el("mark", null, text.slice(idx, idx + q.length)));
            i = idx + q.length;
        }
        if (i < text.length) frag.appendChild(document.createTextNode(text.slice(i)));
        return frag;
    }

    if (searchBox) {
        const inp = searchBox.querySelector(".dc-search__input");
        const results = searchBox.querySelector(".dc-search__results");
        let searchTimer = 0, searchSeq = 0;
        inp.addEventListener("input", () => {
            clearTimeout(searchTimer);
            const q = inp.value.trim();
            if (q.length < 2) { results.replaceChildren(); return; }
            searchTimer = setTimeout(async () => {
                const seq = ++searchSeq;
                try {
                    const res = await fetch("/api/chat/search?q=" + encodeURIComponent(q) + convQuery(), { headers: { Accept: "application/json" } });
                    if (!res.ok || seq !== searchSeq) return;
                    const items = await res.json();
                    results.replaceChildren();
                    if (!items.length) {
                        results.appendChild(el("li", "dc-search__none", deT("Heç nə tapılmadı.")));
                        return;
                    }
                    items.forEach((m) => {
                        const li = el("li", "dc-search__item");
                        li.tabIndex = 0;
                        const head = el("div", "dc-search__head");
                        head.appendChild(el("span", null, senderName(m)));
                        head.appendChild(el("span", null, dateLabel(m.date) + " " + m.time));
                        li.appendChild(head);
                        const body = el("div", "dc-search__text");
                        body.appendChild(highlight(m.message || "", q));
                        li.appendChild(body);
                        const go = () => { closeSearch(); jumpTo(m.id); };
                        li.addEventListener("click", go);
                        li.addEventListener("keydown", (e) => { if (e.key === "Enter") go(); });
                        results.appendChild(li);
                    });
                } catch (e) { /* şəbəkə */ }
            }, 300);
        });
        inp.addEventListener("keydown", (e) => { if (e.key === "Escape") closeSearch(); });
        searchBox.querySelector(".dc-search__close").addEventListener("click", closeSearch);
    }
    if (searchBtn) searchBtn.addEventListener("click", () => (searchBox && !searchBox.hidden ? closeSearch() : openSearch()));

    // =====================================================
    // EMOJİ PANELİ
    // =====================================================

    const EMOJI = {
        "😀": "😀 😃 😄 😁 😆 😅 😂 🤣 😊 😇 🙂 🙃 😉 😌 😍 🥰 😘 😗 😙 😚 😋 😛 😝 😜 🤪 🤨 🧐 🤓 😎 🤩 🥳 😏 😒 😞 😔 😟 😕 🙁 😣 😖 😫 😩 🥺 😢 😭 😤 😠 😡 🤯 😳 🥵 🥶 😱 😨 😰 😥 😓 🤗 🤔 🤭 🤫 🤥 😶 😐 😑 😬 🙄 😯 😦 😧 😮 😲 🥱 😴 🤤 😪 😵 🤐 🥴 🤢 🤮 🤧 😷 🤒 🤕",
        "👍": "👍 👎 👌 🤌 ✌️ 🤞 🤟 🤘 🤙 👈 👉 👆 👇 ☝️ ✋ 🤚 🖐️ 🖖 👋 👏 🙌 👐 🤲 🤝 🙏 ✍️ 💪 🦾 👀 👁️ 🧠 🫶",
        "❤️": "❤️ 🧡 💛 💚 💙 💜 🖤 🤍 🤎 💔 ❣️ 💕 💞 💓 💗 💖 💘 💝 ✅ ❌ ❗ ❓ ⚠️ 💯 🔥 ⭐ 🌟 ✨ 🎉 🎊 💥",
        "🚗": "🚗 🚕 🚙 🏎️ 🚓 🚑 🚒 🚐 🛻 🚚 🚛 🚜 🏍️ 🛵 🚲 ⛽ 🛞 🔧 🔩 🛠️ ⚙️ 🔨 🪛 🧰 🔋 🪫 🔌 💡 🛢️ 📍 🗺️ 🏁 🚦 🚧",
        "☕": "☕ 🍵 🥤 🍔 🍕 🌭 🥪 🌮 🍟 🍩 🍪 🎂 🍰 🍫 🍬 🍎 🍌 🍉 🍇 🍓 🍒 🥝 💰 💳 🧾 📅 ⏰ ⌛ 📞 📱 💬 📷 🎥 🎤"
    };

    function buildEmojiPanel() {
        if (!emojiPanel) return;
        const tabs = emojiPanel.querySelector(".dc-emoji__tabs");
        const grid = emojiPanel.querySelector(".dc-emoji__grid");
        const show = (key) => {
            tabs.querySelectorAll("button").forEach((b) => b.classList.toggle("is-active", b.dataset.key === key));
            grid.replaceChildren();
            EMOJI[key].split(" ").forEach((e) => {
                const b = el("button", "dc-emoji__item", e);
                b.type = "button";
                b.addEventListener("click", () => insertAtCursor(e));
                grid.appendChild(b);
            });
        };
        Object.keys(EMOJI).forEach((key, i) => {
            const t = el("button", null, key);
            t.type = "button";
            t.dataset.key = key;
            t.addEventListener("click", () => show(key));
            tabs.appendChild(t);
            if (i === 0) show(key);
        });
    }

    function insertAtCursor(text) {
        if (!input) return;
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? input.value.length;
        input.value = input.value.slice(0, start) + text + input.value.slice(end);
        const pos = start + text.length;
        input.setSelectionRange(pos, pos);
        input.focus({ preventScroll: true });
        input.dispatchEvent(new Event("input"));
    }

    function toggleEmoji(open) {
        if (!emojiPanel) return;
        const willOpen = open ?? emojiPanel.hidden;
        if (willOpen && !emojiPanel.querySelector(".dc-emoji__item")) buildEmojiPanel();
        emojiPanel.hidden = !willOpen;
        emojiBtn.setAttribute("aria-expanded", String(willOpen));
        emojiBtn.classList.toggle("is-active", willOpen);
    }

    if (emojiBtn) {
        emojiBtn.addEventListener("click", (e) => { e.stopPropagation(); toggleEmoji(); });
        document.addEventListener("pointerdown", (e) => {
            if (emojiPanel && !emojiPanel.hidden && !emojiPanel.contains(e.target) && !emojiBtn.contains(e.target)) toggleEmoji(false);
        });
    }

    // ---------- "Aşağı" düyməsi + yeni mesaj sayı ----------
    function renderJump() {
        if (!jumpCount) return;
        jumpCount.textContent = newWhileAway > 99 ? "99+" : String(newWhileAway);
        jumpCount.hidden = !newWhileAway;
        if (newWhileAway) jumpBtn.hidden = false;
    }

    // =====================================================
    // GÖNDƏRMƏ
    // =====================================================

    // ---------- Səs yazma ----------
    const micBtn = root.querySelector(".dc-chat__mic");
    const rowEl = root.querySelector(".dc-chat__row");
    const recBar = root.querySelector(".dc-rec");
    const canRecord = !!(micBtn && navigator.mediaDevices && navigator.mediaDevices.getUserMedia && window.MediaRecorder);
    const MAX_VOICE_SECONDS = 15 * 60;
    let rec = null;

    function updateSendState() {
        if (!sendBtn) return;
        const hasContent = !!input.value.trim() || !!pendingFile;
        // WhatsApp kimi: boş olanda mikrofon, mətn/fayl olanda göndər düyməsi
        // Mikrofon düyməsi həmişə görünür (WhatsApp kimi); qeyri-təhlükəsiz ünvanda basanda səbəbi izah olunur
        const showMic = !!micBtn && !!recBar && !hasContent && !sending;
        sendBtn.hidden = showMic;
        if (micBtn) micBtn.hidden = !showMic;
        sendBtn.disabled = sending || !hasContent;
    }

    function pickRecorderMime() {
        const types = ["audio/webm;codecs=opus", "audio/ogg;codecs=opus", "audio/mp4", "audio/webm"];
        return types.find((t) => MediaRecorder.isTypeSupported && MediaRecorder.isTypeSupported(t)) || "";
    }

    function extForAudio(type) {
        if (type.includes("ogg")) return ".ogg";
        if (type.includes("mp4") || type.includes("aac")) return ".m4a";
        if (type.includes("mpeg")) return ".mp3";
        return ".webm";
    }

    function recElapsedMs() {
        if (!rec || rec.starting) return 0;
        return rec.elapsed + (rec.paused ? 0 : Date.now() - rec.resumedAt);
    }

    function drawRecWave() {
        if (!rec) return;
        const canvas = rec.canvas;
        const dpr = window.devicePixelRatio || 1;
        const w = canvas.clientWidth, h = canvas.clientHeight;
        if (canvas.width !== w * dpr) { canvas.width = w * dpr; canvas.height = h * dpr; }
        const g = canvas.getContext("2d");
        g.setTransform(dpr, 0, 0, dpr, 0, 0);
        g.clearRect(0, 0, w, h);

        if (rec.analyser && !rec.paused) {
            const data = new Uint8Array(rec.analyser.fftSize);
            rec.analyser.getByteTimeDomainData(data);
            let peak = 0;
            for (let i = 0; i < data.length; i++) peak = Math.max(peak, Math.abs(data[i] - 128) / 128);
            const now = Date.now();
            if (now - rec.lastLevelAt > 90) {
                rec.levels.push(Math.min(1, peak * 2.2));
                rec.lastLevelAt = now;
            }
        }

        const barW = 3, gap = 2, step = barW + gap;
        const count = Math.floor(w / step);
        const levels = rec.levels.slice(-count);
        g.fillStyle = getComputedStyle(canvas).color;
        levels.forEach((lv, i) => {
            const bh = Math.max(3, lv * h);
            const x = w - (levels.length - i) * step;
            g.fillRect(x, (h - bh) / 2, barW, bh);
        });

        rec.raf = requestAnimationFrame(drawRecWave);
    }

    function tickRecTimer() {
        if (!rec) return;
        const sec = recElapsedMs() / 1000;
        recBar.querySelector(".dc-rec__time").textContent = formatDuration(Math.floor(sec));
        if (sec >= MAX_VOICE_SECONDS) sendRecording();
    }

    async function startRecording() {
        if (rec || sending) return;
        clearError();

        // Yazı zolağı KLİKLƏ DƏRHAL açılır ("hazırlanır"); mikrofon açılan kimi yazı başlayır.
        // Əvvəl zolaq yalnız mikrofon + səs konteksti + 0.25 s hazırlıqdan sonra görünürdü (gecikmə).
        const me = {
            starting: true, stream: null, recorder: null, mime: "",
            chunks: [], elapsed: 0, resumedAt: 0, paused: false,
            levels: [], lastLevelAt: 0, canvas: recBar.querySelector(".dc-rec__wave"),
            analyser: null, audioCtx: null, raf: 0, timer: 0
        };
        rec = me;
        if (currentVoice) currentVoice.pause();
        rowEl.hidden = true;
        recBar.hidden = false;
        recBar.classList.remove("is-paused");
        recBar.classList.add("is-starting");
        recBar.querySelector(".dc-rec__pause i").className = "fa-solid fa-pause";
        recBar.querySelector(".dc-rec__time").textContent = "0:00";

        let stream;
        try {
            // Brauzerin mikrofon emalı — real yazıların analizinə əsasən seçilib:
            //  - noiseSuppression: BAĞLI — mikrofon səviyyəsi aşağı olanda danışığı "küy" sayıb
            //    tam susdururdu (−100 dB, ~0.5 s boşluqlar = qırılmalar)
            //  - echoCancellation: BAĞLI — o da səsi kəsə bilir; yazarkən dinamikdən səs çalınmır
            //  - autoGainControl: AÇIQ — onsuz səviyyə çox aşağı idi (pik −30 dB)
            // Xışıltı aşağıdakı filtrlə azaldılır (yüksək tezliklər).
            stream = await navigator.mediaDevices.getUserMedia({
                audio: {
                    echoCancellation: false,
                    noiseSuppression: false,
                    autoGainControl: true,
                    channelCount: 1
                }
            });
        } catch (e) {
            if (rec === me) { rec = null; recBar.hidden = true; recBar.classList.remove("is-starting"); rowEl.hidden = false; updateSendState(); }
            showError(e && e.name === "NotAllowedError"
                ? deT("Mikrofona icazə verilmədi. Brauzerin ünvan sətrindəki kilid işarəsindən icazə verin.")
                : deT("Mikrofon tapılmadı və ya istifadə olunur."));
            return;
        }

        // Mikrofon açılarkən istifadəçi "ləğv et" basıbsa — mikrofonu bağla, heç nə yazma
        if (rec !== me) {
            stream.getTracks().forEach((t) => t.stop());
            return;
        }

        // Mikrofon BİRBAŞA yazılır: əlavə səs zənciri yoxdur → gecikmə yoxdur, sözün əvvəli itmir.
        // Xışıltı filtri oxutma zamanı tətbiq olunur (bax: playbackFilter).
        const mime = pickRecorderMime();
        let recorder;
        try {
            // 48 kbps Opus: səs aydın, 1 dəqiqə ≈ 360 KB
            recorder = new MediaRecorder(stream, mime ? { mimeType: mime, audioBitsPerSecond: 48000 } : undefined);
        } catch (e) {
            stream.getTracks().forEach((t) => t.stop());
            rec = null; recBar.hidden = true; recBar.classList.remove("is-starting"); rowEl.hidden = false; updateSendState();
            showError(deT("Brauzeriniz səs yazmanı dəstəkləmir."));
            return;
        }

        me.stream = stream;
        me.recorder = recorder;
        me.mime = mime;
        recorder.ondataavailable = (e) => { if (e.data && e.data.size) me.chunks.push(e.data); };
        recorder.start(250);
        me.resumedAt = Date.now();
        me.starting = false;
        recBar.classList.remove("is-starting");
        me.timer = setInterval(tickRecTimer, 200);
        drawRecWave();

        // Dalğa göstəricisi — yazı başladıqdan SONRA, arxa planda (yazını gecikdirmir)
        try {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            const micRate = stream.getAudioTracks()[0]?.getSettings?.().sampleRate;
            let ctx;
            try { ctx = micRate ? new Ctx({ sampleRate: micRate }) : new Ctx(); } catch (e2) { ctx = new Ctx(); }
            const analyser = ctx.createAnalyser();
            analyser.fftSize = 512;
            ctx.createMediaStreamSource(stream).connect(analyser);
            if (rec === me) { me.audioCtx = ctx; me.analyser = analyser; }
            else ctx.close().catch(() => {});
        } catch (e) { /* dalğa olmadan da yazılır */ }
    }

    function togglePauseRecording() {
        if (!rec || rec.starting || !rec.recorder || rec.recorder.state === "inactive") return;
        const btnIcon = recBar.querySelector(".dc-rec__pause i");
        if (rec.paused) {
            rec.recorder.resume();
            rec.resumedAt = Date.now();
            rec.paused = false;
            btnIcon.className = "fa-solid fa-pause";
        } else {
            rec.recorder.pause();
            rec.elapsed += Date.now() - rec.resumedAt;
            rec.paused = true;
            btnIcon.className = "fa-solid fa-microphone";
        }
        recBar.classList.toggle("is-paused", rec.paused);
    }

    function stopRecorder() {
        const r = rec;
        return new Promise((resolve) => {
            if (!r.recorder) return resolve(new Blob([]));   // hələ hazırlanırdı
            const finish = () => resolve(new Blob(r.chunks, { type: (r.recorder.mimeType || r.mime || "audio/webm").split(";")[0] }));
            if (r.recorder.state === "inactive") return finish();
            r.recorder.onstop = finish;
            r.recorder.stop();
        });
    }

    function cleanupRecording() {
        if (!rec) return;
        clearInterval(rec.timer);
        cancelAnimationFrame(rec.raf);
        if (rec.stream) rec.stream.getTracks().forEach((t) => t.stop());
        if (rec.audioCtx) rec.audioCtx.close().catch(() => {});
        rec = null;
        recBar.classList.remove("is-starting");
        recBar.hidden = true;
        rowEl.hidden = false;
        updateSendState();
    }

    async function cancelRecording() {
        if (!rec) return;
        await stopRecorder();
        cleanupRecording();
    }

    async function sendRecording() {
        if (!rec || rec.sending || rec.starting) return;   // mikrofon hələ açılmayıb
        rec.sending = true;
        const ms = recElapsedMs();
        const blob = await stopRecorder();
        cleanupRecording();

        if (ms < 1000 || blob.size === 0) {
            showError(deT("Səs mesajı çox qısadır."));
            return;
        }
        if (blob.size > cfg.maxAudioBytes) {
            showError(deT("{0} maksimum {1} MB ola bilər.", deT("Səs mesajı"), Math.round(cfg.maxAudioBytes / 1024 / 1024)));
            return;
        }

        const file = new File([blob], "ses" + extForAudio(blob.type), { type: blob.type });
        const fd = appendConv(new FormData());
        fd.append("File", file, file.name);
        if (replyTo) fd.append("ReplyToId", String(replyTo.id));

        sending = true;
        updateSendState();
        progressBar.style.width = "0%";
        progressEl.hidden = false;

        const res = await post("/api/chat/send", fd, (p) => (progressBar.style.width = Math.round(p * 100) + "%"));

        sending = false;
        progressEl.hidden = true;
        updateSendState();

        if (res.ok && res.data && res.data.id) {
            clearReply();
            upsert(res.data);
            refreshLayout();
            scrollToBottom(true);
        } else if (!res.ok) {
            showError(res.error);
        }
    }

    // Brauzer mikrofona icazə vermir: adətən sayt HTTPS ilə açılmayıb (məs. telefonda http://192.168…)
    if (micBtn && !canRecord) {
        micBtn.addEventListener("click", () => {
            showError(window.isSecureContext
                ? deT("Bu brauzer səs yazmağı dəstəkləmir.")
                : deT("Səs mesajı üçün sayt təhlükəsiz ünvanla (https://) açılmalıdır. Brauzer http:// ünvanında mikrofona icazə vermir."));
        });
    }

    if (canRecord && recBar) {
        micBtn.addEventListener("click", startRecording);
        recBar.querySelector(".dc-rec__cancel").addEventListener("click", cancelRecording);
        recBar.querySelector(".dc-rec__pause").addEventListener("click", togglePauseRecording);
        recBar.querySelector(".dc-rec__send").addEventListener("click", sendRecording);
        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape" && rec) cancelRecording();
        });
        // Səhifədən çıxanda mikrofonu bağla
        window.addEventListener("pagehide", () => {
            if (!rec) return;
            if (rec.recorder && rec.recorder.state !== "inactive") rec.recorder.stop();
            cleanupRecording();
        });
    }

    function autosize(textarea) {
        textarea.style.height = "auto";
        textarea.style.height = Math.min(textarea.scrollHeight, 140) + "px";
    }

    function setPendingFile(file) {
        clearPendingFile();
        if (!file) return;

        const error = validateFile(file);
        if (error) {
            showError(error);
            return;
        }
        clearError();

        pendingFile = file;
        pendingPreviewUrl = URL.createObjectURL(file);

        const thumb = previewEl.querySelector(".dc-chat__preview-thumb");
        thumb.replaceChildren();
        if (fileKind(file) === "image") {
            const img = document.createElement("img");
            img.src = pendingPreviewUrl;
            img.alt = "";
            thumb.appendChild(img);
        } else {
            const video = document.createElement("video");
            video.src = pendingPreviewUrl;
            video.muted = true;
            video.preload = "metadata";
            thumb.appendChild(video);
        }

        previewEl.querySelector(".dc-chat__preview-name").textContent = file.name;
        previewEl.querySelector(".dc-chat__preview-size").textContent =
            (fileKind(file) === "image" ? deT("Şəkil") : deT("Video")) + " · " + formatSize(file.size);
        previewEl.hidden = false;
        updateSendState();
        input.focus();
    }

    function clearPendingFile() {
        pendingFile = null;
        if (pendingPreviewUrl) URL.revokeObjectURL(pendingPreviewUrl);
        pendingPreviewUrl = null;
        if (previewEl) previewEl.hidden = true;
        if (fileInput) fileInput.value = "";
        updateSendState();
    }

    async function sendMessage() {
        if (sending) return;

        const text = input.value.trim();
        if (!text && !pendingFile) return;

        if (text.length > cfg.maxLength) {
            showError(deT("Mesaj maksimum {0} simvol ola bilər.", cfg.maxLength));
            return;
        }

        if (!conversation) return;
        const fd = appendConv(new FormData());
        if (text) fd.append("Message", text);
        if (pendingFile) fd.append("File", pendingFile, pendingFile.name);
        if (replyTo) fd.append("ReplyToId", String(replyTo.id));
        stopTyping();

        sending = true;
        updateSendState();
        clearError();

        const showProgress = !!pendingFile;
        if (showProgress) {
            progressBar.style.width = "0%";
            progressEl.hidden = false;
        }

        const res = await post("/api/chat/send", fd, showProgress ? (p) => (progressBar.style.width = Math.round(p * 100) + "%") : null);

        sending = false;
        progressEl.hidden = true;

        if (res.ok) {
            input.value = "";
            autosize(input);
            clearPendingFile();
            clearReply();
            toggleEmoji(false);
            // SignalR gecikərsə / qoşulmayıbsa da mesaj görünsün
            if (res.data && res.data.id) {
                upsert(res.data);
                refreshLayout();
                scrollToBottom(true);
            }
        } else {
            showError(res.error);
        }

        updateSendState();
        input.focus();
    }

    if (form) {
        form.addEventListener("submit", (e) => {
            e.preventDefault();
            sendMessage();
        });

        input.addEventListener("input", () => {
            autosize(input);
            updateSendState();
            if (input.value.trim()) notifyTyping(); else stopTyping();
        });
        input.addEventListener("blur", stopTyping);

        input.addEventListener("keydown", (e) => {
            if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
                e.preventDefault();
                sendMessage();
            } else if (e.key === "Escape" && replyTo) {
                clearReply();
            }
        });

        fileInput.addEventListener("change", () => setPendingFile(fileInput.files[0]));
        previewEl.querySelector(".dc-chat__preview-remove").addEventListener("click", clearPendingFile);

        // Buferdən şəkil yapışdırma (Ctrl+V)
        input.addEventListener("paste", (e) => {
            const item = Array.from(e.clipboardData ? e.clipboardData.items : []).find((i) => i.kind === "file");
            if (item) {
                const f = item.getAsFile();
                if (f) {
                    e.preventDefault();
                    const ext = (f.type.split("/")[1] || "png").replace("jpeg", "jpg");
                    setPendingFile(new File([f], f.name && f.name !== "image.png" ? f.name : "sekil." + ext, { type: f.type }));
                }
            }
        });

        // Sürüklə-burax
        let dragDepth = 0;
        root.addEventListener("dragenter", (e) => {
            if (!e.dataTransfer || !Array.from(e.dataTransfer.types).includes("Files")) return;
            e.preventDefault();
            dragDepth++;
            dropzone.hidden = false;
        });
        root.addEventListener("dragover", (e) => {
            if (e.dataTransfer && Array.from(e.dataTransfer.types).includes("Files")) e.preventDefault();
        });
        root.addEventListener("dragleave", () => {
            dragDepth = Math.max(0, dragDepth - 1);
            if (dragDepth === 0) dropzone.hidden = true;
        });
        root.addEventListener("drop", (e) => {
            e.preventDefault();
            dragDepth = 0;
            dropzone.hidden = true;
            const f = e.dataTransfer && e.dataTransfer.files[0];
            if (f) setPendingFile(f);
        });
    }

    // =====================================================
    // REDAKTƏ
    // =====================================================

    function startEdit(id) {
        const dto = messages.get(id);
        const node = list.querySelector('.dc-msg[data-id="' + id + '"]');
        if (!dto || !node || node.classList.contains("is-editing")) return;

        // Eyni anda yalnız bir redaktə
        list.querySelectorAll(".dc-msg.is-editing").forEach((n) => cancelEdit(Number(n.dataset.id)));

        node.classList.add("is-editing");
        const content = node.querySelector(".dc-msg__content");
        const bubble = node.querySelector(".dc-msg__bubble");
        const actions = node.querySelector(".dc-msg__actions");
        if (actions) actions.hidden = true;

        let removeAttachment = false;
        let newFile = null;
        let newFileUrl = null;

        const editor = el("div", "dc-edit");

        const textarea = document.createElement("textarea");
        textarea.maxLength = cfg.maxLength;
        textarea.value = dto.message || "";
        textarea.placeholder = deT("Mesaj…");
        editor.appendChild(textarea);

        // Fayl sətri
        const fileRow = el("div", "dc-edit__file");
        const thumb = el("div", "thumb");
        const label = el("span", "label");
        fileRow.append(thumb, label);

        const fileBtns = el("div", "dc-edit__row");
        const changeBtn = el("label", "dc-btn");
        changeBtn.appendChild(icon("fa-solid fa-paperclip"));
        const changeText = el("span");
        changeBtn.appendChild(changeText);
        const changeInput = document.createElement("input");
        changeInput.type = "file";
        changeInput.accept = fileInput ? fileInput.accept : "image/*,video/*";
        changeBtn.appendChild(changeInput);

        const removeBtn = el("button", "dc-btn");
        removeBtn.type = "button";

        fileBtns.append(changeBtn, removeBtn);

        function renderFileRow() {
            thumb.replaceChildren();
            thumb.style.backgroundImage = "";
            const hasExisting = !!dto.attachmentUrl && isSafeUploadUrl(dto.attachmentUrl);

            if (newFile) {
                const kind = fileKind(newFile);
                if (kind === "image") thumb.style.backgroundImage = 'url("' + newFileUrl + '")';
                else {
                    const v = document.createElement("video");
                    v.src = newFileUrl;
                    v.muted = true;
                    thumb.appendChild(v);
                }
                label.textContent = deT("Yeni:") + " " + newFile.name + " (" + formatSize(newFile.size) + ")";
                fileRow.hidden = false;
                fileRow.classList.remove("is-removed");
                changeText.textContent = deT("Başqa fayl");
                removeBtn.hidden = false;
                removeBtn.replaceChildren(icon("fa-solid fa-rotate-left"), document.createTextNode(" " + deT("Ləğv et")));
            } else if (hasExisting) {
                if (dto.attachmentType === "image") thumb.style.backgroundImage = 'url("' + dto.attachmentUrl + '")';
                else thumb.appendChild(icon(dto.attachmentType === "audio" ? "fa-solid fa-microphone" : "fa-solid fa-film"));
                const kindName = dto.attachmentType === "image" ? deT("Şəkil") : dto.attachmentType === "audio" ? deT("Səs mesajı") : deT("Video");
                label.textContent = removeAttachment ? deT("{0} silinəcək", kindName) : kindName;
                fileRow.hidden = false;
                fileRow.classList.toggle("is-removed", removeAttachment);
                changeText.textContent = deT("Dəyiş");
                removeBtn.hidden = false;
                removeBtn.replaceChildren(
                    icon(removeAttachment ? "fa-solid fa-rotate-left" : "fa-solid fa-trash"),
                    document.createTextNode(" " + (removeAttachment ? deT("Geri qaytar") : deT("Faylı sil"))));
            } else {
                fileRow.hidden = true;
                changeText.textContent = deT("Şəkil/video əlavə et");
                removeBtn.hidden = true;
            }
        }

        changeInput.addEventListener("change", () => {
            const f = changeInput.files[0];
            changeInput.value = "";
            if (!f) return;
            const error = validateFile(f);
            if (error) { errorBox.textContent = error; errorBox.hidden = false; return; }
            errorBox.hidden = true;
            if (newFileUrl) URL.revokeObjectURL(newFileUrl);
            newFile = f;
            newFileUrl = URL.createObjectURL(f);
            removeAttachment = false;
            renderFileRow();
        });

        removeBtn.addEventListener("click", () => {
            if (newFile) {
                URL.revokeObjectURL(newFileUrl);
                newFile = null;
                newFileUrl = null;
            } else {
                removeAttachment = !removeAttachment;
            }
            renderFileRow();
        });

        editor.appendChild(fileRow);

        const errorBox = el("div", "dc-edit__error");
        errorBox.hidden = true;
        errorBox.setAttribute("role", "alert");

        const buttons = el("div", "dc-edit__row");
        buttons.appendChild(fileBtns);
        buttons.appendChild(el("span", "spacer"));

        const cancelBtn = el("button", "dc-btn", deT("Ləğv et"));
        cancelBtn.type = "button";
        const saveBtn = el("button", "dc-btn dc-btn--primary");
        saveBtn.type = "button";
        saveBtn.append(icon("fa-solid fa-check"), document.createTextNode(" " + deT("Yadda saxla")));
        buttons.append(cancelBtn, saveBtn);

        editor.appendChild(buttons);
        editor.appendChild(errorBox);

        async function save() {
            const text = textarea.value.trim();
            const willHaveFile = !!newFile || (!!dto.attachmentUrl && !removeAttachment);
            if (!text && !willHaveFile) {
                errorBox.textContent = deT("Mesaj boş qala bilməz. Mətn yazın və ya fayl əlavə edin.");
                errorBox.hidden = false;
                return;
            }

            const unchanged = text === (dto.message || "") && !newFile && !removeAttachment;
            if (unchanged) { cancelEdit(id); return; }

            const fd = new FormData();
            fd.append("Id", String(id));
            if (text) fd.append("Message", text);
            fd.append("RemoveAttachment", removeAttachment ? "true" : "false");
            if (newFile) fd.append("File", newFile, newFile.name);

            saveBtn.disabled = true;
            cancelBtn.disabled = true;
            saveBtn.lastChild.textContent = " " + (newFile ? deT("Yüklənir… {0}%", 0) : deT("Saxlanılır…"));

            const res = await post("/api/chat/edit", fd, newFile ? (p) => (saveBtn.lastChild.textContent = " " + deT("Yüklənir… {0}%", Math.round(p * 100))) : null);

            if (res.ok && res.data) {
                if (newFileUrl) URL.revokeObjectURL(newFileUrl);
                node.classList.remove("is-editing");
                upsert(res.data);
                refreshLayout();
            } else {
                saveBtn.disabled = false;
                cancelBtn.disabled = false;
                saveBtn.lastChild.textContent = " " + deT("Yadda saxla");
                errorBox.textContent = res.error;
                errorBox.hidden = false;
            }
        }

        cancelBtn.addEventListener("click", () => {
            if (newFileUrl) URL.revokeObjectURL(newFileUrl);
            cancelEdit(id);
        });
        saveBtn.addEventListener("click", save);
        textarea.addEventListener("keydown", (e) => {
            if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
                e.preventDefault();
                save();
            }
            if (e.key === "Escape") {
                e.preventDefault();
                cancelBtn.click();
            }
        });

        renderFileRow();
        bubble.hidden = true;
        content.appendChild(editor);

        textarea.focus();
        textarea.setSelectionRange(textarea.value.length, textarea.value.length);
        editor.scrollIntoView({ block: "nearest" });
    }

    function cancelEdit(id) {
        const node = list.querySelector('.dc-msg[data-id="' + id + '"]');
        if (!node) return;
        node.classList.remove("is-editing");
        const dto = messages.get(id);
        if (dto) {
            // Redaktə zamanı başqa yerdən gələn dəyişiklik də göstərilsin
            node.replaceWith(buildMessage(dto));
            refreshLayout();
        }
    }

    // =====================================================
    // SİLMƏ
    // =====================================================

    async function deleteMessage(id) {
        const dto = messages.get(id);
        if (!dto) return;
        const ownMessage = dto.userName === cfg.currentUser;
        const ok = window.deUI
            ? await window.deUI.confirm({
                title: deT("Mesaj silinsin?"),
                text: ownMessage
                    ? deT("Mesaj hamı üçün silinəcək. Bu əməliyyat geri qaytarıla bilməz.")
                    : deT("{0} istifadəçisinin mesajı admin olaraq silinəcək.", dto.userName),
                okText: deT("Sil"),
                danger: true
            })
            : window.confirm(deT("Bu mesaj silinsin?"));
        if (!ok) return;

        const node = list.querySelector('.dc-msg[data-id="' + id + '"]');
        if (node) node.style.opacity = ".5";

        const res = await post("/api/chat/delete/" + encodeURIComponent(id));
        if (res.ok) {
            removeMessage(id);
        } else {
            if (node) node.style.opacity = "";
            showError(res.error);
        }
    }

    // =====================================================
    // LIGHTBOX
    // =====================================================

    function openLightbox(url) {
        if (!lightbox || !isSafeUploadUrl(url)) return;
        lightboxImg.src = url;
        lightbox.hidden = false;
        document.body.style.overflow = "hidden";
    }

    function closeLightbox() {
        if (!lightbox) return;
        lightbox.hidden = true;
        lightboxImg.removeAttribute("src");
        document.body.style.overflow = "";
    }

    if (lightbox) {
        lightbox.addEventListener("click", closeLightbox);
        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape" && !lightbox.hidden) closeLightbox();
        });
    }

    // =====================================================
    // QRUPLAR VƏ YAYIM (pəncərələr)
    // =====================================================

    /** Sadə modal pəncərə: başlıq, məzmun, düymələr. close() qaytarır. */
    function openModal(title, body, buttons, extraClass) {
        const overlay = el("div", "dc-modal" + (extraClass ? " " + extraClass : ""));
        overlay.setAttribute("role", "dialog");
        overlay.setAttribute("aria-modal", "true");
        const box = el("div", "dc-modal__box");
        const head = el("div", "dc-modal__head");
        head.appendChild(el("h4", null, title));
        const x = el("button");
        x.type = "button";
        x.setAttribute("aria-label", deT("Bağla"));
        x.appendChild(icon("fa-solid fa-xmark"));
        head.appendChild(x);
        box.appendChild(head);
        const content = el("div", "dc-modal__body");
        content.appendChild(body);
        box.appendChild(content);
        const foot = el("div", "dc-modal__foot");
        (buttons || []).forEach((b) => foot.appendChild(b));
        if (buttons && buttons.length) box.appendChild(foot);
        overlay.appendChild(box);
        document.body.appendChild(overlay);

        const close = () => { overlay.remove(); document.removeEventListener("keydown", onKey); };
        const onKey = (e) => { if (e.key === "Escape") close(); };
        document.addEventListener("keydown", onKey);
        x.addEventListener("click", close);
        overlay.addEventListener("pointerdown", (e) => { if (e.target === overlay) close(); });
        const first = box.querySelector("input, textarea");
        if (first) setTimeout(() => first.focus(), 50);
        return { close, body: content };
    }

    function modalButton(text, kind, onClick) {
        const b = el("button", "dc-modal__btn" + (kind ? " is-" + kind : ""), text);
        b.type = "button";
        if (onClick) b.addEventListener("click", onClick);
        return b;
    }

    function switchRow(label, hint, checked) {
        const lab = el("label", "dc-switch");
        const cb = document.createElement("input");
        cb.type = "checkbox";
        cb.checked = !!checked;
        lab.appendChild(cb);
        const txt = el("span", null, label);
        if (hint) txt.appendChild(el("small", null, hint));
        lab.appendChild(txt);
        return { row: lab, input: cb };
    }

    function textField(label, value, multiline, max) {
        const lab = el("label", "dc-field");
        lab.appendChild(el("span", null, label));
        const inp = document.createElement(multiline ? "textarea" : "input");
        if (!multiline) inp.type = "text";
        inp.value = value || "";
        if (max) inp.maxLength = max;
        lab.appendChild(inp);
        return { row: lab, input: inp };
    }

    function memberRow(u, opts) {
        const li = el("li");
        li.dataset.user = u.userName;
        li.dataset.search = ((u.fullName || "") + " " + u.userName).toLowerCase();
        const wrap = opts.checkbox ? el("label") : li;
        if (opts.checkbox) {
            const cb = document.createElement("input");
            cb.type = "checkbox";
            cb.value = u.userName;
            cb.checked = !!opts.checked;
            wrap.appendChild(cb);
        }
        const av = avatarNode(u.avatarUrl, u.fullName || u.userName, "dc-avatar");
        if (u.online) av.classList.add("is-online");
        wrap.appendChild(av);
        const name = el("span", "dc-members__name", u.fullName || u.userName);
        name.appendChild(el("small", null, "@" + u.userName));
        wrap.appendChild(name);
        if (u.isAdmin) wrap.appendChild(el("span", "dc-members__tag", deT("Usta")));
        if (opts.checkbox) li.appendChild(wrap);
        if (opts.onRemove) {
            const rm = el("button", "dc-members__remove");
            rm.type = "button";
            rm.title = deT("Qrupdan çıxar");
            rm.setAttribute("aria-label", deT("Qrupdan çıxar"));
            rm.appendChild(icon("fa-solid fa-user-minus"));
            rm.addEventListener("click", () => opts.onRemove(u));
            li.appendChild(rm);
        }
        return li;
    }

    let usersCache = null;
    async function fetchUsers() {
        if (usersCache) return usersCache;
        const res = await fetch("/api/chat/users", { headers: { Accept: "application/json" } });
        if (!res.ok) throw new Error("HTTP " + res.status);
        usersCache = (await res.json()).filter((u) => u.userName !== cfg.currentUser);
        setTimeout(() => (usersCache = null), 60000);
        return usersCache;
    }

    /** Yeni qrup və ya mövcud qrupu dəyiş (group verilərsə) — WhatsApp "Yeni qrup" görünüşü. */
    async function openGroupEditor(group) {
        const existing = new Set(group ? group.members.map((m) => m.userName) : []);
        const selected = new Map();   // userName -> user (yeni seçilənlər)
        let users = [];

        const form = el("div", "dc-gform");

        // --- Qrupun şəkli + adı ---
        const top = el("div", "dc-gform__top");
        const pic = el("div", "dc-gform__pic");
        pic.appendChild(icon("fa-solid fa-users"));
        top.appendChild(pic);
        const nameWrap = el("label", "dc-gform__name");
        const nameInput = document.createElement("input");
        nameInput.type = "text";
        nameInput.maxLength = 100;
        nameInput.placeholder = deT("Qrupun adı");
        nameInput.value = (group && group.name) || "";
        const counter = el("span", "dc-gform__count");
        const updateCount = () => (counter.textContent = String(100 - nameInput.value.length));
        nameInput.addEventListener("input", updateCount);
        updateCount();
        nameWrap.append(nameInput, counter);
        top.appendChild(nameWrap);
        form.appendChild(top);

        const descInput = document.createElement("input");
        descInput.type = "text";
        descInput.className = "dc-gform__desc";
        descInput.maxLength = 300;
        descInput.placeholder = deT("Təsvir (istəyə bağlı)");
        descInput.value = (group && group.description) || "";
        form.appendChild(descInput);

        // --- Ayarlar: açar düymələr ---
        const settings = el("div", "dc-gform__settings");
        const toggle = (iconClass, title, hint, checked) => {
            const row = el("label", "dc-gform__toggle");
            const ic = el("span", "dc-gform__toggle-icon");
            ic.appendChild(icon(iconClass));
            const text = el("span", "dc-gform__toggle-text");
            text.appendChild(el("b", null, title));
            text.appendChild(el("small", null, hint));
            const input = document.createElement("input");
            input.type = "checkbox";
            input.className = "dc-switch-input";
            input.checked = !!checked;
            const sw = el("span", "dc-switch-ui");
            row.append(ic, text, input, sw);
            settings.appendChild(row);
            return input;
        };
        const allInput = toggle("fa-solid fa-earth-europe", deT("Bütün istifadəçilər"),
            deT("Hamı (yeni qeydiyyatdan keçənlər də) qrupdadır və bir-birinin mesajlarını görür."), group && group.allUsers);
        const adminsInput = toggle("fa-solid fa-bullhorn", deT("Yalnız ustalar yaza bilər"),
            deT("Elan kanalı: üzvlər oxuyur, yalnız ustalar yazır."), group && group.adminsOnly);
        form.appendChild(settings);

        // --- Üzvlər ---
        const membersBox = el("div", "dc-gform__members");
        const membersHead = el("div", "dc-gform__members-head");
        const membersTitle = el("span", null, group ? deT("Üzv əlavə et") : deT("Üzvlər"));
        const membersCount = el("span", "dc-gform__members-count");
        membersHead.append(membersTitle, membersCount);
        membersBox.appendChild(membersHead);

        const chips = el("div", "dc-gform__chips");
        membersBox.appendChild(chips);

        const searchWrap = el("label", "dc-gform__search");
        searchWrap.appendChild(icon("fa-solid fa-magnifying-glass"));
        const search = document.createElement("input");
        search.type = "search";
        search.placeholder = deT("Ad və ya istifadəçi adı ilə axtar…");
        searchWrap.appendChild(search);
        membersBox.appendChild(searchWrap);

        const list = el("ul", "dc-gform__list");
        list.appendChild(el("li", "dc-gform__empty", deT("Yüklənir…")));
        membersBox.appendChild(list);
        form.appendChild(membersBox);

        const allNote = el("div", "dc-gform__allnote");
        allNote.appendChild(icon("fa-solid fa-circle-info"));
        allNote.appendChild(document.createTextNode(" " + deT("Bütün istifadəçilər avtomatik qrupdadır — üzv seçməyə ehtiyac yoxdur.")));
        form.appendChild(allNote);

        const err = el("div", "dc-modal__error");
        err.hidden = true;
        form.appendChild(err);

        const renderChips = () => {
            chips.replaceChildren();
            selected.forEach((u) => {
                const chip = el("button", "dc-gform__chip");
                chip.type = "button";
                chip.title = deT("Çıxar");
                chip.appendChild(avatarNode(u.avatarUrl, u.fullName || u.userName, "dc-avatar"));
                chip.appendChild(el("span", null, (u.fullName || u.userName).split(" ")[0]));
                chip.appendChild(icon("fa-solid fa-xmark"));
                chip.addEventListener("click", () => { toggleUser(u, false); });
                chips.appendChild(chip);
            });
            chips.hidden = selected.size === 0;
            const total = selected.size + existing.size;
            membersCount.textContent = total ? deT("{0} seçilib", total) : "";
            save.textContent = (group ? deT("Yadda saxla") : deT("Qrup yarat")) + (selected.size ? " (" + selected.size + ")" : "");
        };

        const toggleUser = (u, on) => {
            if (existing.has(u.userName)) return;
            if (on) selected.set(u.userName, u); else selected.delete(u.userName);
            const li = list.querySelector('li[data-user="' + CSS.escape(u.userName) + '"]');
            if (li) li.classList.toggle("is-selected", on);
            renderChips();
        };

        const renderList = () => {
            const q = search.value.trim().toLowerCase();
            const rows = users.filter((u) => !q || (u.fullName || "").toLowerCase().includes(q) || u.userName.toLowerCase().includes(q));
            list.replaceChildren();
            if (!rows.length) { list.appendChild(el("li", "dc-gform__empty", users.length ? deT("Heç nə tapılmadı.") : deT("İstifadəçi yoxdur."))); return; }
            rows.forEach((u) => {
                const li = el("li", "dc-gform__user");
                li.dataset.user = u.userName;
                li.tabIndex = 0;
                const isExisting = existing.has(u.userName);
                if (isExisting) li.classList.add("is-existing");
                if (isExisting || selected.has(u.userName)) li.classList.add("is-selected");
                const av = avatarNode(u.avatarUrl, u.fullName || u.userName, "dc-avatar");
                if (u.online) av.classList.add("is-online");
                li.appendChild(av);
                const name = el("span", "dc-gform__user-name", u.fullName || u.userName);
                name.appendChild(el("small", null, isExisting ? deT("Artıq qrupdadır") : "@" + u.userName));
                li.appendChild(name);
                if (u.isAdmin) li.appendChild(el("span", "dc-members__tag", deT("Usta")));
                const check = el("span", "dc-gform__check");
                check.appendChild(icon("fa-solid fa-check"));
                li.appendChild(check);
                const flip = () => toggleUser(u, !selected.has(u.userName));
                li.addEventListener("click", flip);
                li.addEventListener("keydown", (e) => { if (e.key === " " || e.key === "Enter") { e.preventDefault(); flip(); } });
                list.appendChild(li);
            });
        };
        search.addEventListener("input", renderList);

        const syncAll = () => form.classList.toggle("is-all-users", allInput.checked);
        allInput.addEventListener("change", syncAll);
        syncAll();

        const save = modalButton(group ? deT("Yadda saxla") : deT("Qrup yarat"), "primary", async () => {
            err.hidden = true;
            if (!nameInput.value.trim()) {
                err.textContent = deT("Qrupun adını yazın.");
                err.hidden = false;
                nameInput.focus();
                return;
            }
            if (!group && !allInput.checked && selected.size === 0) {
                err.textContent = deT("Ən azı bir üzv seçin və ya \"Bütün istifadəçilər\"i aktiv edin.");
                err.hidden = false;
                return;
            }
            const fd = new FormData();
            fd.append("Name", nameInput.value.trim());
            if (descInput.value.trim()) fd.append("Description", descInput.value.trim());
            fd.append("AllUsers", allInput.checked ? "true" : "false");
            fd.append("AdminsOnly", adminsInput.checked ? "true" : "false");
            if (!allInput.checked) selected.forEach((_, name) => fd.append("Members", name));
            save.disabled = true;
            const res = await post(group ? "/api/chat/groups/" + group.id : "/api/chat/groups", fd);
            save.disabled = false;
            if (!res.ok) { err.textContent = res.error; err.hidden = false; return; }
            modal.close();
            await loadConversations();
            const row = convRows.get(res.data.conversation);
            if (!group) openConversation(res.data.conversation, row);
            else if (conversation === res.data.conversation) { try { const page = await loadPage(null); if (page.peer) setPeer(page.peer); } catch (e) { /* yox */ } }
        });

        const modal = openModal(group ? deT("Qrupu redaktə et") : deT("Yeni qrup"), form,
            [modalButton(deT("Ləğv et"), null, () => modal.close()), save], "dc-modal--group");
        renderChips();
        setTimeout(() => nameInput.focus(), 60);

        try {
            users = await fetchUsers();
            renderList();
        } catch (e) {
            list.replaceChildren(el("li", "dc-gform__empty", deT("İstifadəçilər yüklənmədi.")));
        }
    }

    /** Qrup məlumatı: üzvlər; usta — redaktə, üzvü çıxar, qrupu sil. */
    async function openGroupInfo() {
        if (!isGroupConv(conversation)) return;
        const id = conversation.slice(2);
        const res = await fetch("/api/chat/groups/" + encodeURIComponent(id), { headers: { Accept: "application/json" } });
        if (!res.ok) { showError(deT("Qrup tapılmadı.")); return; }
        const g = await res.json();

        const body = el("div");
        body.style.display = "contents";
        const head = el("div", "dc-group-head");
        const av = el("div", "dc-avatar is-group");
        av.appendChild(icon("fa-solid fa-users"));
        head.appendChild(av);
        head.appendChild(el("h5", null, g.name));
        head.appendChild(el("p", null, g.allUsers ? deT("Bütün istifadəçilər") : deT("{0} üzv", g.memberCount)));
        if (g.description) head.appendChild(el("p", null, g.description));
        if (g.adminsOnly) head.appendChild(el("p", null, "📢 " + deT("Yalnız ustalar yaza bilər")));
        body.appendChild(head);

        const list = el("ul", "dc-members");
        const render = (info) => {
            list.replaceChildren(...info.members.map((u) => memberRow(u, {
                onRemove: isAdminView && !g.allUsers && u.userName !== cfg.currentUser ? async (m) => {
                    const ok = window.deUI ? await window.deUI.confirm({ title: deT("Üzv çıxarılsın?"), text: deT("{0} qrupdan çıxarılacaq.", m.fullName || m.userName), okText: deT("Çıxar"), danger: true })
                        : window.confirm(deT("Üzv çıxarılsın?"));
                    if (!ok) return;
                    const fd = new FormData();
                    fd.append("userName", m.userName);
                    const r = await post("/api/chat/groups/" + g.id + "/remove", fd);
                    if (r.ok) render(r.data); else showError(r.error);
                } : null
            })));
        };
        if (!g.allUsers) { body.appendChild(el("p", "dc-modal__hint", deT("Üzvlər"))); body.appendChild(list); render(g); }

        const buttons = [];
        if (isAdminView) {
            buttons.push(modalButton(deT("Qrupu sil"), "danger", async () => {
                const ok = window.deUI ? await window.deUI.confirm({ title: deT("Qrup silinsin?"), text: deT("Qrup və bütün mesajları hamı üçün silinəcək."), okText: deT("Sil"), danger: true })
                    : window.confirm(deT("Qrup silinsin?"));
                if (!ok) return;
                const r = await post("/api/chat/groups/" + g.id + "/delete");
                if (!r.ok) { showError(r.error); return; }
                modal.close();
                convRows.delete(g.conversation);
                closeConversation();
                renderConvList();
            }));
            buttons.push(modalButton(deT("Redaktə et"), "primary", () => { modal.close(); openGroupEditor(g); }));
        } else {
            buttons.push(modalButton(deT("Bağla"), null, () => modal.close()));
        }
        const modal = openModal(deT("Qrup məlumatı"), body, buttons);
    }

    /** Yayım: mesaj hər müştərinin şəxsi yazışmasına ayrıca gedir. */
    function openBroadcast() {
        const body = el("div");
        body.style.display = "contents";
        body.appendChild(el("p", "dc-modal__hint", deT("Mesaj bütün istifadəçilərə ayrıca (şəxsi yazışmalarına) göndəriləcək. Cavablarını yalnız ustalar görür.")));
        const text = textField(deT("Mesaj"), "", true, cfg.maxLength);
        const fileLab = el("label", "dc-field");
        fileLab.appendChild(el("span", null, deT("Şəkil və ya video (istəyə bağlı)")));
        const file = document.createElement("input");
        file.type = "file";
        file.accept = "image/jpeg,image/png,image/webp,video/mp4,video/webm,video/quicktime,.mov,.m4v";
        fileLab.appendChild(file);
        const err = el("div", "dc-modal__error");
        err.hidden = true;
        body.append(text.row, fileLab, err);

        const send = modalButton(deT("Hamıya göndər"), "primary", async () => {
            err.hidden = true;
            const msg = text.input.value.trim();
            const f = file.files[0];
            if (!msg && !f) { err.textContent = deT("Mesaj və ya fayl əlavə edin."); err.hidden = false; return; }
            if (f) { const e = validateFile(f); if (e) { err.textContent = e; err.hidden = false; return; } }
            const fd = new FormData();
            if (msg) fd.append("Message", msg);
            if (f) fd.append("File", f, f.name);
            send.disabled = true;
            send.textContent = deT("Göndərilir…");
            const res = await post("/api/chat/broadcast", fd);
            send.disabled = false;
            send.textContent = deT("Hamıya göndər");
            if (!res.ok) { err.textContent = res.error; err.hidden = false; return; }
            modal.close();
            if (window.deUI) window.deUI.toast(deT("{0} istifadəçiyə göndərildi.", res.data.count), "success");
            loadConversations();
        });
        const modal = openModal(deT("Hamıya göndər (yayım)"), body, [modalButton(deT("Ləğv et"), null, () => modal.close()), send]);
    }

    // "⋮" menyusu (paneldə): axtarış, bildiriş səsi, tam ekran
    const clearBtn = root.querySelector("[data-dc-clear]");
    if (clearBtn) clearBtn.addEventListener("click", () => {
        if (!conversation) return;
        const row = convRows.get(conversation) || { conversation, title: peer && peer.title, isGroup: peer && peer.isGroup };
        confirmClear(row);
    });

    const toolsBtn = root.querySelector("[data-dc-tools]");
    if (toolsBtn) {
        const setTools = (open) => {
            root.classList.toggle("tools-open", open);
            toolsBtn.setAttribute("aria-expanded", String(open));
        };
        toolsBtn.addEventListener("click", (e) => { e.stopPropagation(); setTools(!root.classList.contains("tools-open")); });
        root.querySelectorAll(".dc-chat__tools > *").forEach((b) => b.addEventListener("click", () => setTools(false)));
        document.addEventListener("pointerdown", (e) => {
            if (root.classList.contains("tools-open") && !e.target.closest(".dc-chat__tools, [data-dc-tools]")) setTools(false);
        });
        document.addEventListener("keydown", (e) => { if (e.key === "Escape") setTools(false); });
    }

    // Video / səsli zəng (ayrıca pəncərədə /Call)
    root.querySelectorAll("[data-dc-call]").forEach((b) => b.addEventListener("click", () => {
        if (!conversation || !window.dcCalls) return;
        window.dcCalls.start(conversation, b.dataset.dcCall === "video", peer ? peer.title : "");
    }));
    root.querySelectorAll("[data-dc-new-group]").forEach((b) => b.addEventListener("click", () => openGroupEditor(null)));
    root.querySelectorAll("[data-dc-broadcast]").forEach((b) => b.addEventListener("click", openBroadcast));
    const headingEl = root.querySelector(".dc-chat__heading");
    if (headingEl) {
        const go = () => { if (isGroupConv(conversation)) openGroupInfo(); };
        headingEl.addEventListener("click", go);
        headingEl.addEventListener("keydown", (e) => { if (e.key === "Enter") go(); });
    }

    // =====================================================
    // PROFİL VƏ MEDİA (WhatsApp kimi yan panellər)
    //  - "Profilim": şəkil, ad, "Haqqında"
    //  - Başlığa toxunanda: əlaqə / qrup məlumatı, "Media, linklər və səs"
    // =====================================================

    let sidePanel = null;

    /** Yan panel: side = "left" (siyahının yerində) və ya "right" (yazışmanın yerində). */
    function openSide(title, side) {
        closeSide();
        const panel = el("section", "dc-side dc-side--" + side);
        panel.setAttribute("role", "dialog");
        panel.setAttribute("aria-label", title);
        // Geniş ekranda (siyahı + yazışma yan-yana) panel öz sütununu tutur, dar ekranda — hamısını
        if (isWide() && convsEl) {
            const w = convsEl.offsetWidth;
            if (side === "left") panel.style.width = w + "px";
            else panel.style.left = w + "px";
        }
        const head = el("header", "dc-side__head");
        const back = el("button", "dc-side__back");
        back.type = "button";
        back.setAttribute("aria-label", deT("Geri"));
        back.appendChild(icon("fa-solid fa-arrow-left"));
        back.addEventListener("click", closeSide);
        const h = el("h3", null, title);
        head.append(back, h);
        const body = el("div", "dc-side__body");
        panel.append(head, body);
        root.appendChild(panel);
        sidePanel = { panel, body, title: h };
        return sidePanel;
    }

    function closeSide() {
        if (!sidePanel) return;
        const p = sidePanel.panel;
        sidePanel = null;
        p.classList.add("is-leaving");
        setTimeout(() => p.remove(), 180);
    }
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && sidePanel && !document.querySelector(".dc-modal") && lightbox && lightbox.hidden) closeSide();
    });

    // =====================================================
    // YENİ ÇAT ("+") — istifadəçinin adına toxun, yaz
    // =====================================================

    async function openNewChat() {
        const side = openSide(deT("Yeni çat"), "left");
        side.panel.classList.add("dc-side--newchat");

        const search = el("div", "dc-newchat__search");
        search.appendChild(icon("fa-solid fa-magnifying-glass"));
        const input = document.createElement("input");
        input.type = "search";
        input.placeholder = deT("Ad və ya istifadəçi adı ilə axtar");
        input.setAttribute("aria-label", deT("İstifadəçi axtar"));
        input.autocomplete = "off";
        search.appendChild(input);
        side.body.appendChild(search);

        // Usta: yeni qrup və yayım da buradan
        if (isAdminView) {
            const quick = el("div", "dc-newchat__quick");
            const q = (iconClass, label, fn) => {
                const b = el("button", "dc-newchat__action");
                b.type = "button";
                const ic = el("span", "dc-newchat__action-icon");
                ic.appendChild(icon(iconClass));
                b.append(ic, el("span", null, label));
                b.addEventListener("click", () => { closeSide(); fn(); });
                quick.appendChild(b);
            };
            q("fa-solid fa-users", deT("Yeni qrup"), () => openGroupEditor(null));
            q("fa-solid fa-bullhorn", deT("Hamıya göndər (yayım)"), openBroadcast);
            side.body.appendChild(quick);
        }

        const label = el("p", "dc-newchat__label", deT("İstifadəçilər"));
        const list = el("ul", "dc-newchat__list");
        list.setAttribute("role", "listbox");
        const status = el("p", "dc-newchat__status", deT("Yüklənir…"));
        side.body.append(label, list, status);

        let seq = 0, timer = 0;
        const load = async () => {
            const my = ++seq;
            const q = input.value.trim();
            try {
                const people = await getJson("/api/chat/people" + (q ? "?q=" + encodeURIComponent(q) : ""));
                if (my !== seq || !sidePanel || sidePanel !== side) return;
                list.replaceChildren(...people.map((u) => personRow(u)));
                status.hidden = people.length > 0;
                status.textContent = deT("Heç kim tapılmadı.");
            } catch (e) {
                if (my !== seq) return;
                list.replaceChildren();
                status.hidden = false;
                status.textContent = deT("İstifadəçilər yüklənmədi.");
            }
        };
        input.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(load, 250); });
        load();
        // Telefonda klaviatura özü açılmasın (siyahını əvvəl görsün), kompüterdə — axtarışa fokus
        if (window.matchMedia("(hover: hover)").matches) setTimeout(() => input.focus(), 120);
    }

    function personRow(u) {
        const li = el("li", "dc-newchat__item");
        li.setAttribute("role", "option");
        li.tabIndex = 0;
        const av = avatarNode(u.avatarUrl, u.fullName || u.userName, "dc-avatar");
        if (u.online) av.classList.add("is-online");
        const main = el("span", "dc-newchat__main");
        const name = el("span", "dc-newchat__name", u.fullName || u.userName);
        if (u.isAdmin) name.appendChild(el("span", "dc-members__tag", deT("Usta")));
        main.appendChild(name);
        main.appendChild(el("small", "dc-newchat__sub", u.online ? deT("onlayn") : "@" + u.userName));
        li.append(av, main);
        const go = () => startChatWith(u, li);
        li.addEventListener("click", go);
        li.addEventListener("keydown", (e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); go(); } });
        return li;
    }

    async function startChatWith(u, li) {
        if (li.classList.contains("is-busy")) return;
        li.classList.add("is-busy");
        const fd = new FormData();
        fd.append("userName", u.userName);
        const r = await post("/api/chat/direct", fd);
        li.classList.remove("is-busy");
        if (!r.ok || !r.data || !r.data.conversation) { showError(r.error || deT("Yazışma açılmadı.")); return; }
        const conv = r.data.conversation;
        // Siyahıda varsa — həmin sətir; yoxdursa (yeni şəxsi yazışma) — istifadəçinin məlumatı ilə
        const row = convRows.get(conv) || {
            conversation: conv,
            title: u.fullName || u.userName,
            avatarUrl: u.avatarUrl,
            online: !!u.online,
            isDirect: isDirectConv(conv),
            isGroup: false
        };
        closeSide();
        await openConversation(conv, row);
    }

    root.querySelectorAll("[data-dc-new-chat]").forEach((b) => b.addEventListener("click", openNewChat));

    async function getJson(url) {
        const res = await fetch(url, { headers: { Accept: "application/json" } });
        if (!res.ok) throw new Error("HTTP " + res.status);
        return res.json();
    }

    // ---------- Profilim ----------
    const meBtn = root.querySelector("[data-dc-profile]");
    let myProfile = null;

    function renderMeButton() {
        if (!meBtn || !myProfile) return;
        const av = avatarNode(myProfile.avatarUrl, myProfile.fullName, "dc-avatar");
        meBtn.replaceChildren(av);
    }

    async function loadMyProfile() {
        try { myProfile = await getJson("/api/chat/profile"); renderMeButton(); } catch (e) { /* növbəti dəfə */ }
        return myProfile;
    }

    /** Redaktə olunan sətir (WhatsApp: "Ad" ✎). */
    function editableRow(label, value, placeholder, max, hint, onSave) {
        const row = el("div", "dc-prow");
        row.appendChild(el("span", "dc-prow__label", label));
        const line = el("div", "dc-prow__line");
        const text = el("span", "dc-prow__value", value || placeholder);
        if (!value) text.classList.add("is-empty");
        const edit = el("button", "dc-prow__edit");
        edit.type = "button";
        edit.setAttribute("aria-label", deT("Redaktə et"));
        edit.appendChild(icon("fa-solid fa-pen"));
        line.append(text, edit);
        row.appendChild(line);
        if (hint) row.appendChild(el("small", "dc-prow__hint", hint));

        edit.addEventListener("click", () => {
            const input = document.createElement("input");
            input.type = "text";
            input.maxLength = max;
            input.value = value || "";
            input.placeholder = placeholder;
            const counter = el("span", "dc-prow__count", String(max - input.value.length));
            input.addEventListener("input", () => (counter.textContent = String(max - input.value.length)));
            const ok = el("button", "dc-prow__edit is-ok");
            ok.type = "button";
            ok.setAttribute("aria-label", deT("Yadda saxla"));
            ok.appendChild(icon("fa-solid fa-check"));
            line.replaceChildren(input, counter, ok);
            line.classList.add("is-editing");
            input.focus();
            const commit = async () => {
                ok.disabled = true;
                const saved = await onSave(input.value.trim());
                ok.disabled = false;
                if (saved !== false) {
                    value = input.value.trim();
                    text.textContent = value || placeholder;
                    text.classList.toggle("is-empty", !value);
                    line.classList.remove("is-editing");
                    line.replaceChildren(text, edit);
                }
            };
            ok.addEventListener("click", commit);
            input.addEventListener("keydown", (e) => {
                if (e.key === "Enter") { e.preventDefault(); commit(); }
                if (e.key === "Escape") { e.stopPropagation(); line.classList.remove("is-editing"); line.replaceChildren(text, edit); }
            });
        });
        return row;
    }

    async function openMyProfile() {
        const side = openSide(deT("Profil"), "left");
        side.body.appendChild(el("div", "dc-side__loading", deT("Yüklənir…")));
        const p = await loadMyProfile();
        if (!sidePanel || sidePanel !== side) return;
        side.body.replaceChildren();
        if (!p) { side.body.appendChild(el("div", "dc-side__loading", deT("Profil yüklənmədi."))); return; }

        const saveProfile = async (fullName, about) => {
            const fd = new FormData();
            fd.append("FullName", fullName);
            if (about) fd.append("About", about);
            const res = await post("/api/chat/profile", fd);
            if (!res.ok) { showError(res.error); if (window.deUI) window.deUI.toast(res.error, "error"); return false; }
            myProfile = res.data;
            renderMeButton();
            return true;
        };

        // Böyük şəkil + kamera
        const photoBox = el("div", "dc-pphoto");
        const big = avatarNode(p.avatarUrl, p.fullName, "dc-avatar dc-pphoto__img");
        const cam = el("button", "dc-pphoto__cam");
        cam.type = "button";
        cam.appendChild(icon("fa-solid fa-camera"));
        cam.appendChild(el("span", null, p.avatarUrl ? deT("Şəkli dəyiş") : deT("Şəkil əlavə et")));
        const file = document.createElement("input");
        file.type = "file";
        file.accept = "image/jpeg,image/png,image/webp";
        file.hidden = true;
        photoBox.append(big, cam, file);
        side.body.appendChild(photoBox);

        const setPhoto = (profile) => {
            myProfile = profile;
            renderMeButton();
            photoBox.replaceChild(avatarNode(profile.avatarUrl, profile.fullName, "dc-avatar dc-pphoto__img"), photoBox.firstChild);
            cam.lastChild.textContent = profile.avatarUrl ? deT("Şəkli dəyiş") : deT("Şəkil əlavə et");
            removeBtn.hidden = !profile.avatarUrl;
        };
        cam.addEventListener("click", () => file.click());
        file.addEventListener("change", async () => {
            const f = file.files[0];
            file.value = "";
            if (!f) return;
            if (fileKind(f) !== "image") { showError(deT("Yalnız şəkil (jpg, png, webp) seçin.")); return; }
            photoBox.classList.add("is-busy");
            const fd = new FormData();
            fd.append("file", f, f.name);
            const res = await post("/api/chat/profile/avatar", fd);
            photoBox.classList.remove("is-busy");
            if (res.ok) setPhoto(res.data);
            else if (window.deUI) window.deUI.toast(res.error, "error");
        });
        const removeBtn = el("button", "dc-pphoto__remove", deT("Şəkli sil"));
        removeBtn.type = "button";
        removeBtn.hidden = !p.avatarUrl;
        removeBtn.addEventListener("click", async () => {
            const res = await post("/api/chat/profile/avatar/delete");
            if (res.ok) setPhoto(res.data);
        });
        side.body.appendChild(removeBtn);

        let currentName = p.fullName, currentAbout = p.about || "";
        side.body.appendChild(editableRow(deT("Adınız"), p.fullName, deT("Adınızı yazın"), 100,
            deT("Bu ad ustalara və qrup üzvlərinə görünür."),
            async (v) => {
                if (!v) { if (window.deUI) window.deUI.toast(deT("Adınızı yazın."), "error"); return false; }
                const ok = await saveProfile(v, currentAbout);
                if (ok) currentName = v;
                return ok;
            }));
        side.body.appendChild(editableRow(deT("Haqqında"), p.about, deT("Özünüz haqqında qısa yazın"), 140, null,
            async (v) => {
                const ok = await saveProfile(currentName, v);
                if (ok) currentAbout = v;
                return ok;
            }));

        const info = el("div", "dc-pinfo");
        const line = (iconClass, label, value) => {
            const r = el("div", "dc-pinfo__row");
            r.appendChild(icon(iconClass));
            const t = el("span");
            t.appendChild(el("small", null, label));
            t.appendChild(el("b", null, value));
            r.appendChild(t);
            info.appendChild(r);
        };
        line("fa-solid fa-at", deT("İstifadəçi adı"), p.userName);
        if (p.email) line("fa-regular fa-envelope", deT("Email"), p.email);
        side.body.appendChild(info);
    }

    if (meBtn) {
        meBtn.addEventListener("click", openMyProfile);
        loadMyProfile();
    }

    // ---------- Əlaqə / qrup məlumatı + media ----------
    function mediaThumb(m, onOpenVideo) {
        const cell = el("button", "dc-mthumb");
        cell.type = "button";
        if (m.attachmentType === "image") {
            const img = document.createElement("img");
            img.src = m.attachmentUrl;
            img.alt = "";
            img.loading = "lazy";
            cell.appendChild(img);
            cell.addEventListener("click", () => openLightbox(m.attachmentUrl));
        } else {
            const v = document.createElement("video");
            v.src = m.attachmentUrl + "#t=0.5";
            v.preload = "metadata";
            v.muted = true;
            v.playsInline = true;
            cell.appendChild(v);
            const play = el("span", "dc-mthumb__play");
            play.appendChild(icon("fa-solid fa-play"));
            cell.appendChild(play);
            cell.classList.add("is-video");
            cell.addEventListener("click", () => onOpenVideo(m));
        }
        return cell;
    }

    function openVideo(m) {
        const box = el("div");
        box.style.display = "contents";
        const v = document.createElement("video");
        v.src = m.attachmentUrl;
        v.controls = true;
        v.autoplay = true;
        v.playsInline = true;
        v.className = "dc-video-full";
        box.appendChild(v);
        openModal(deT("Video"), box, [], "dc-modal--video");
    }

    async function openInfo() {
        if (!conversation) return;
        const isGroup = isGroupConv(conversation);
        const side = openSide(isGroup ? deT("Qrup məlumatı") : deT("Əlaqə məlumatı"), "right");
        side.body.appendChild(el("div", "dc-side__loading", deT("Yüklənir…")));
        let c, group = null;
        try {
            c = await getJson("/api/chat/contact?conversation=" + encodeURIComponent(conversation));
            if (isGroup) group = await getJson("/api/chat/groups/" + encodeURIComponent(conversation.slice(2)));
        } catch (e) {
            side.body.replaceChildren(el("div", "dc-side__loading", deT("Məlumat yüklənmədi.")));
            return;
        }
        if (!sidePanel || sidePanel !== side) return;
        side.body.replaceChildren();

        // Başlıq kartı
        const card = el("div", "dc-pcard");
        const av = avatarNode(c.avatarUrl, c.title, "dc-avatar dc-pcard__img");
        if (c.isGroup) { av.replaceChildren(icon("fa-solid fa-users")); av.classList.add("is-group"); av.style.background = ""; }
        else if (c.isSupport && !c.avatarUrl) { av.replaceChildren(icon("fa-solid fa-screwdriver-wrench")); av.classList.add("is-support"); av.style.background = ""; }
        card.appendChild(av);
        card.appendChild(el("h4", null, c.title));
        const sub = c.isGroup
            ? (group && group.allUsers ? deT("Qrup · Bütün istifadəçilər") : deT("Qrup · {0} üzv", group ? group.memberCount : 0))
            : c.userName ? "@" + c.userName : "";
        if (sub) card.appendChild(el("p", "dc-pcard__sub", sub));
        if (!c.isGroup) {
            card.appendChild(el("p", "dc-pcard__status" + (c.online ? " is-online" : ""),
                c.online ? deT("onlayn") : c.lastSeen ? lastSeenLabel(c.lastSeen) : ""));
        }
        // Tez əməliyyatlar: video, səsli zəng, axtarış
        const acts = el("div", "dc-pcard__actions");
        const act = (iconClass, label, fn) => {
            const b = el("button", "dc-pcard__act");
            b.type = "button";
            b.appendChild(icon(iconClass));
            b.appendChild(el("span", null, label));
            b.addEventListener("click", fn);
            acts.appendChild(b);
        };
        if (window.dcCalls) {
            act("fa-solid fa-video", deT("Video"), () => window.dcCalls.start(conversation, true, c.title));
            act("fa-solid fa-phone", deT("Səsli"), () => window.dcCalls.start(conversation, false, c.title));
        }
        act("fa-solid fa-magnifying-glass", deT("Axtar"), () => { closeSide(); openSearch(); });
        card.appendChild(acts);
        side.body.appendChild(card);

        // Haqqında / təsvir
        if (c.about) {
            const about = el("div", "dc-pblock");
            about.appendChild(el("span", "dc-pblock__label", c.isGroup ? deT("Təsvir") : deT("Haqqında")));
            about.appendChild(el("p", "dc-pblock__text", c.about));
            side.body.appendChild(about);
        }

        // Ustalar üçün: müştərinin əlaqə məlumatı
        if (c.email || c.phone) {
            const info = el("div", "dc-pinfo");
            if (c.email) {
                const r = el("a", "dc-pinfo__row");
                r.href = "mailto:" + c.email;
                r.appendChild(icon("fa-regular fa-envelope"));
                const t = el("span"); t.appendChild(el("small", null, deT("Email"))); t.appendChild(el("b", null, c.email)); r.appendChild(t);
                info.appendChild(r);
            }
            if (c.phone) {
                const r = el("a", "dc-pinfo__row");
                r.href = "tel:" + c.phone;
                r.appendChild(icon("fa-solid fa-phone"));
                const t = el("span"); t.appendChild(el("small", null, deT("Telefon"))); t.appendChild(el("b", null, c.phone)); r.appendChild(t);
                info.appendChild(r);
            }
            side.body.appendChild(info);
        }

        // Media, linklər və səs — önizləmə
        const total = c.mediaCount + c.audioCount + c.linkCount;
        const mediaBlock = el("div", "dc-pblock dc-pmedia");
        const mhead = el("button", "dc-pmedia__head");
        mhead.type = "button";
        mhead.appendChild(el("span", "dc-pblock__label", deT("Media, linklər və səs")));
        const mcount = el("span", "dc-pmedia__count", total ? String(total) : "");
        mcount.appendChild(icon("fa-solid fa-chevron-right"));
        mhead.appendChild(mcount);
        mhead.addEventListener("click", () => openMediaView(c, "media"));
        mediaBlock.appendChild(mhead);
        const strip = el("div", "dc-pmedia__strip");
        mediaBlock.appendChild(strip);
        if (!total) strip.appendChild(el("p", "dc-pmedia__none", deT("Hələ paylaşılmış şəkil, video və ya link yoxdur.")));
        side.body.appendChild(mediaBlock);
        if (c.mediaCount) {
            getJson("/api/chat/media?kind=media&take=6&conversation=" + encodeURIComponent(conversation))
                .then((page) => page.messages.forEach((m) => strip.appendChild(mediaThumb(m, openVideo))))
                .catch(() => { /* önizləmə vacib deyil */ });
        } else if (total) {
            strip.appendChild(el("p", "dc-pmedia__none", deT("{0} səs mesajı, {1} link", c.audioCount, c.linkCount)));
        }

        // Qrup: üzvlər və idarə
        if (group) {
            if (!group.allUsers) {
                const mb = el("div", "dc-pblock");
                mb.appendChild(el("span", "dc-pblock__label", deT("{0} üzv", group.memberCount)));
                const ul = el("ul", "dc-members dc-members--flat");
                const render = (info) => {
                    ul.replaceChildren(...info.members.map((u) => memberRow(u, {
                        onRemove: isAdminView && u.userName !== cfg.currentUser ? async (m) => {
                            const ok = window.deUI ? await window.deUI.confirm({ title: deT("Üzv çıxarılsın?"), text: deT("{0} qrupdan çıxarılacaq.", m.fullName || m.userName), okText: deT("Çıxar"), danger: true })
                                : window.confirm(deT("Üzv çıxarılsın?"));
                            if (!ok) return;
                            const fd = new FormData();
                            fd.append("userName", m.userName);
                            const r = await post("/api/chat/groups/" + group.id + "/remove", fd);
                            if (r.ok) render(r.data); else showError(r.error);
                        } : null
                    })));
                };
                render(group);
                mb.appendChild(ul);
                side.body.appendChild(mb);
            }
            if (isAdminView) {
                const adm = el("div", "dc-pblock dc-pactions");
                const edit = el("button", "dc-pactions__btn");
                edit.type = "button";
                edit.appendChild(icon("fa-solid fa-pen"));
                edit.appendChild(el("span", null, deT("Qrupu redaktə et")));
                edit.addEventListener("click", () => { closeSide(); openGroupEditor(group); });
                const del = el("button", "dc-pactions__btn is-danger");
                del.type = "button";
                del.appendChild(icon("fa-solid fa-trash"));
                del.appendChild(el("span", null, deT("Qrupu sil")));
                del.addEventListener("click", async () => {
                    const ok = window.deUI ? await window.deUI.confirm({ title: deT("Qrup silinsin?"), text: deT("Qrup və bütün mesajları hamı üçün silinəcək."), okText: deT("Sil"), danger: true })
                        : window.confirm(deT("Qrup silinsin?"));
                    if (!ok) return;
                    const r = await post("/api/chat/groups/" + group.id + "/delete");
                    if (!r.ok) { showError(r.error); return; }
                    closeSide();
                    convRows.delete(group.conversation);
                    closeConversation();
                    renderConvList();
                });
                adm.append(edit, del);
                side.body.appendChild(adm);
            }
        }
    }

    /** "Media, linklər və səs": tablar + şəbəkə / siyahı. */
    function openMediaView(c, kind) {
        if (!sidePanel) return;
        sidePanel.title.textContent = c.title;
        const body = sidePanel.body;
        body.replaceChildren();
        const tabs = el("div", "dc-mtabs");
        const content = el("div", "dc-mview");
        const defs = [["media", deT("Media"), c.mediaCount], ["audio", deT("Səs"), c.audioCount], ["links", deT("Linklər"), c.linkCount]];
        let beforeId = null, hasMore = false, current = kind;

        const more = el("button", "dc-mview__more", deT("Daha çox"));
        more.type = "button";

        const load = async (reset) => {
            if (reset) { content.replaceChildren(); beforeId = null; }
            more.disabled = true;
            let page;
            try {
                page = await getJson("/api/chat/media?take=60&kind=" + current + "&conversation=" + encodeURIComponent(conversation) + (beforeId ? "&beforeId=" + beforeId : ""));
            } catch (e) { more.disabled = false; return; }
            more.disabled = false;
            if (reset && !page.messages.length) {
                content.appendChild(el("p", "dc-pmedia__none", current === "media" ? deT("Şəkil və ya video yoxdur.")
                    : current === "audio" ? deT("Səs mesajı yoxdur.") : deT("Link yoxdur.")));
            }
            let grid = content.querySelector(".dc-mgrid");
            if (current === "media" && !grid) { grid = el("div", "dc-mgrid"); content.appendChild(grid); }
            page.messages.forEach((m) => {
                if (current === "media") grid.appendChild(mediaThumb(m, openVideo));
                else {
                    const row = el("div", "dc-mrow");
                    const meta = el("div", "dc-mrow__meta");
                    meta.appendChild(el("b", null, senderName(m)));
                    meta.appendChild(el("span", null, dateLabel(m.date) + " · " + m.time));
                    row.appendChild(meta);
                    if (current === "audio") {
                        const a = document.createElement("audio");
                        a.controls = true;
                        a.preload = "none";
                        a.src = m.attachmentUrl;
                        row.appendChild(a);
                    } else {
                        const t = el("div", "dc-mrow__text");
                        appendLinkified(t, m.message || "");
                        row.appendChild(t);
                    }
                    const go = el("button", "dc-mrow__go", deT("Mesaja keç"));
                    go.type = "button";
                    go.addEventListener("click", () => { closeSide(); jumpTo(m.id); });
                    row.appendChild(go);
                    content.appendChild(row);
                }
            });
            if (page.messages.length) beforeId = page.messages[page.messages.length - 1].id;
            hasMore = page.hasMore;
            more.hidden = !hasMore;
            content.appendChild(more);
        };
        more.addEventListener("click", () => load(false));

        defs.forEach(([key, label, count]) => {
            const t = el("button", null, label + (count ? " (" + count + ")" : ""));
            t.type = "button";
            if (key === current) t.classList.add("is-active");
            t.addEventListener("click", () => {
                current = key;
                tabs.querySelectorAll("button").forEach((b) => b.classList.toggle("is-active", b === t));
                load(true);
            });
            tabs.appendChild(t);
        });
        body.append(tabs, content);
        load(true);
    }

    // Başlığa toxunanda — əlaqə / qrup məlumatı (WhatsApp kimi)
    const infoHeading = root.querySelector(".dc-chat__heading");
    if (infoHeading) {
        const open = (e) => {
            if (e.type === "keydown" && e.key !== "Enter") return;
            e.stopImmediatePropagation();
            openInfo();
        };
        infoHeading.addEventListener("click", open, true);
        infoHeading.addEventListener("keydown", open, true);
    }

    // =====================================================
    // START
    // =====================================================

    // Mobil: yazma sahəsi ekrandadırsa saytın üzən düymələri onu örtməsin (paneldə lazım deyil)
    if (form && !cfg.drawer && "IntersectionObserver" in window) {
        new IntersectionObserver((entries) => {
            document.body.classList.toggle("dc-composer-visible", entries[0].isIntersecting);
        }).observe(form);
    }

    updateSendState();

    // "Çatlar" siyahısı (usta: bütün yazışmalar və qruplar; müştəri: ustalarla yazışma + qrupları).
    // /Chat?c=... — birbaşa həmin yazışma (bildirişdən).
    loadConversations().then(() => {
        if (cfg.openConversation && cfg.openConversation !== conversation)
            openConversation(cfg.openConversation, convRows.get(cfg.openConversation));
    });

    if (cfg.drawer) {
        // Panel rejimi: mesajlar panel ilk dəfə açılanda yüklənir, bağlantı isə dərhal (bildirişlər üçün)
        document.addEventListener("dc:drawer-open", (e) => {
            drawerOpen = true;
            // Bildirişdən: usta üçün həmin müştərinin yazışması açılır
            const target = e.detail && e.detail.conversation;
            if (hasList && target && target !== conversation) {
                openConversation(target, convRows.get(target));
                return;
            }
            ensureLoaded().then(() => {
                if (!conversation) return;
                scrollToBottom(false);
                markRead();
                if (input) input.focus({ preventScroll: true });
            });
        });
        document.addEventListener("dc:drawer-close", () => {
            drawerOpen = false;
            if (rec) cancelRecording();
            if (currentVoice) currentVoice.pause();
            closeMenu();
            toggleEmoji(false);
            stopTyping();
        });
        startConnection();
    } else {
        ensureLoaded().then(() => {
            if (isChatVisible()) markRead();
            startConnection();
        });
    }

    // ---------- Telefon: klaviatura açılanda səhifə sürüşməsin ----------
    // Klaviatura yalnız "görünən hissəni" (visualViewport) kiçildir — çat onun ölçüsünə uyğunlaşır,
    // "Canlı Çat" səhifəsində çat başlığın altında sabitlənir və səhifənin özü sürüşmür.
    const phoneMq = window.matchMedia("(max-width: 576px)");
    const docStyle = document.documentElement.style;
    function fitToViewport() {
        if (!phoneMq.matches) {
            ["--dc-vvh", "--dc-vvtop", "--dc-header-h"].forEach((v) => docStyle.removeProperty(v));
            document.body.classList.remove("dc-chat-page");
            return;
        }
        const vv = window.visualViewport;
        docStyle.setProperty("--dc-vvh", (vv ? vv.height : window.innerHeight) + "px");
        docStyle.setProperty("--dc-vvtop", (vv ? vv.offsetTop : 0) + "px");
        const header = document.querySelector(".s-header");
        // Başlığın aşağı kənarı (üstündəki məlumat zolağı da daxil)
        docStyle.setProperty("--dc-header-h", (header ? Math.max(0, Math.round(header.getBoundingClientRect().bottom)) : 64) + "px");
        if (cfg.fullPage) document.body.classList.add("dc-chat-page");
    }
    fitToViewport();
    if (window.visualViewport) {
        window.visualViewport.addEventListener("resize", fitToViewport);
        window.visualViewport.addEventListener("scroll", fitToViewport);
    }
    window.addEventListener("resize", fitToViewport);
    window.addEventListener("load", fitToViewport);
    // Saytın başlığı (loqo yüklənəndə və s.) ölçüsünü dəyişəndə çatın yeri də yenilənsin
    if (window.ResizeObserver) {
        const siteHeader = document.querySelector(".s-header");
        const ro = new ResizeObserver(fitToViewport);
        if (siteHeader) ro.observe(siteHeader);
        // Başlığın üstündəki məlumat zolağı gec görünür (animasiya) — başlıq aşağı sürüşür
        const topbar = document.querySelector(".de-topbar");
        if (topbar) {
            ro.observe(topbar);
            topbar.addEventListener("transitionend", fitToViewport);
            topbar.addEventListener("animationend", fitToViewport);
        }
    }

    // Mesajlar sahəsi kiçiləndə (klaviatura, yazma sahəsi böyüyəndə) aşağıdaydısa — aşağıda qalsın
    if (window.ResizeObserver && body) {
        new ResizeObserver(() => {
            if (stickToBottom && messages.size) body.scrollTop = body.scrollHeight;
        }).observe(body);
    }
    if (phoneMq.addEventListener) phoneMq.addEventListener("change", fitToViewport);

    // iPhone: yazma sahəsinə toxunanda brauzer səhifəni sürüşdürür — geri qaytar (çat sabitdir)
    if (input) {
        input.addEventListener("focus", () => {
            if (!phoneMq.matches) return;
            setTimeout(() => {
                if (cfg.fullPage || cfg.drawer) window.scrollTo(0, cfg.fullPage ? 0 : window.scrollY);
                fitToViewport();
                if (stickToBottom) scrollToBottom(false);
            }, 250);
        });
    }

    // İlk bildiriş: launcher skripti yükləndikdən sonra göndərilir
    const emitInitial = () => setUnread(Number(cfg.unreadCount) || 0, null, true);
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", emitInitial);
    else emitInitial();
})();
