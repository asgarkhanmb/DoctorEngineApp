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
        if (!kind) return "Yalnız şəkil (jpg, png, webp) və ya video (mp4, webm, mov) göndərilə bilər.";
        const max = kind === "image" ? cfg.maxImageBytes : cfg.maxVideoBytes;
        if (file.size > max) {
            return (kind === "image" ? "Şəkil" : "Video") + " maksimum " + Math.round(max / 1024 / 1024) + " MB ola bilər.";
        }
        if (file.size === 0) return "Fayl boşdur.";
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
        if (diff === 0) return "Bu gün";
        if (diff === 1) return "Dünən";
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

    function isNearBottom() {
        return body.scrollHeight - body.scrollTop - body.clientHeight < 120;
    }

    function scrollToBottom(smooth) {
        body.style.scrollBehavior = smooth ? "smooth" : "auto";
        body.scrollTop = body.scrollHeight;
        body.style.scrollBehavior = "";
        jumpBtn.hidden = true;
    }

    function setStatus(state, text) {
        statusEl.dataset.state = state;
        statusText.textContent = text;
    }

    function showError(message) {
        if (!errorEl) { alert(message); return; }
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
        if (status === 413) return "Fayl çox böyükdür.";
        if (status === 404) return "Mesaj tapılmadı (silinmiş ola bilər).";
        if (status === 400) return "Sorğu qəbul olunmadı. Səhifəni yeniləyib yenidən cəhd edin.";
        return "Xəta baş verdi. Yenidən cəhd edin.";
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
            xhr.onerror = () => resolve({ ok: false, error: "Şəbəkə xətası. İnternet bağlantınızı yoxlayın." });
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
            img.alt = "Şəkil";
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
                return { peaks: peaks.map((p) => 0.15 + 0.85 * (p / max)), duration: audio.duration };
            } catch (e) {
                return null;
            }
        })();
        peaksCache.set(url, job);
        return job;
    }

    function stopVoiceIn(node) {
        const v = node.querySelector(".dc-voice");
        if (v && v._audio) v._audio.pause();
    }

    function buildVoice(dto) {
        const wrap = el("div", "dc-voice");

        const playBtn = el("button", "dc-voice__play");
        playBtn.type = "button";
        playBtn.setAttribute("aria-label", "Səs mesajını oynat");
        const playIcon = icon("fa-solid fa-play");
        playBtn.appendChild(playIcon);

        const main = el("div", "dc-voice__main");
        const track = el("div", "dc-voice__track");
        track.setAttribute("role", "slider");
        track.setAttribute("aria-label", "Səs mesajı");
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
        speedBtn.title = "Oxutma sürəti";
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

        // MediaRecorder webm faylında müddət "Infinity" olur — brauzeri onu hesablamağa məcbur et
        audio.addEventListener("loadedmetadata", () => {
            if (audio.duration === Infinity) {
                const fix = () => {
                    audio.removeEventListener("durationchange", fix);
                    audio.currentTime = 0;
                    render();
                };
                audio.addEventListener("durationchange", fix);
                audio.currentTime = 1e101;
            } else {
                render();
            }
        });

        loadPeaks(dto.attachmentUrl).then((res) => {
            if (!res) return;
            duration = res.duration;
            res.peaks.forEach((h, i) => (bars[i].style.height = Math.round(h * 100) + "%"));
            render();
        });

        audio.addEventListener("timeupdate", render);
        audio.addEventListener("play", () => {
            if (currentVoice && currentVoice !== audio) currentVoice.pause();
            currentVoice = audio;
            playIcon.className = "fa-solid fa-pause";
            playBtn.setAttribute("aria-label", "Fasilə");
            wrap.classList.add("is-playing");
        });
        audio.addEventListener("pause", () => {
            playIcon.className = "fa-solid fa-play";
            playBtn.setAttribute("aria-label", "Səs mesajını oynat");
            wrap.classList.remove("is-playing");
        });
        audio.addEventListener("ended", () => {
            audio.currentTime = 0;
            render();
        });
        audio.addEventListener("error", () => {
            wrap.classList.add("is-error");
            timeEl.textContent = "Fayl açılmadı";
        });

        playBtn.addEventListener("click", () => {
            if (audio.paused) audio.play().catch(() => {});
            else audio.pause();
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

        const node = el("div", "dc-msg");
        node.dataset.id = dto.id;
        node.dataset.user = dto.userName;
        node.dataset.date = dto.date;
        if (isMine) node.classList.add("is-mine");
        if (dto.isAdmin) node.classList.add("is-admin");

        // Avatar
        const avatar = el("div", "dc-msg__avatar");
        if (dto.isAdmin) {
            avatar.appendChild(icon("fa-solid fa-wrench"));
        } else {
            avatar.textContent = (dto.userName || "?").charAt(0);
            avatar.style.background = colorFor(dto.userName || "?");
        }
        node.appendChild(avatar);

        // Məzmun
        const content = el("div", "dc-msg__content");

        const name = el("div", "dc-msg__name");
        name.appendChild(el("span", null, dto.userName));
        if (dto.isAdmin) {
            const badge = el("span", "dc-badge");
            badge.appendChild(icon("fa-solid fa-screwdriver-wrench"));
            badge.appendChild(document.createTextNode("Usta"));
            name.appendChild(badge);
        }
        content.appendChild(name);

        const bubble = el("div", "dc-msg__bubble");

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
            appendLinkified(text, dto.message);
            bubble.appendChild(text);
        }

        const meta = el("div", "dc-msg__meta");
        if (dto.isEdited) meta.appendChild(el("span", null, "redaktə olunub"));
        const time = el("time", null, dto.time);
        time.dateTime = dto.date + "T" + dto.time;
        meta.appendChild(time);
        bubble.appendChild(meta);

        content.appendChild(bubble);
        node.appendChild(content);

        // Əməliyyatlar: redaktə — yalnız sahibi; silmə — sahibi və ya admin
        const canEdit = isMine;
        const canDelete = isMine || cfg.isAdmin;

        if (canEdit || canDelete) {
            const actions = el("div", "dc-msg__actions");
            if (canEdit) {
                const editBtn = el("button", "dc-msg__action");
                editBtn.type = "button";
                editBtn.title = "Redaktə et";
                editBtn.setAttribute("aria-label", "Redaktə et");
                editBtn.appendChild(icon("fa-solid fa-pen"));
                editBtn.addEventListener("click", () => startEdit(dto.id));
                actions.appendChild(editBtn);
            }
            if (canDelete) {
                const delBtn = el("button", "dc-msg__action is-danger");
                delBtn.type = "button";
                delBtn.title = "Sil";
                delBtn.setAttribute("aria-label", "Sil");
                delBtn.appendChild(icon("fa-solid fa-trash"));
                delBtn.addEventListener("click", () => deleteMessage(dto.id));
                actions.appendChild(delBtn);
            }
            node.appendChild(actions);
        }

        return node;
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

    async function loadPage(beforeId) {
        const url = "/api/chat/messages?take=" + PAGE_SIZE + (beforeId ? "&beforeId=" + beforeId : "");
        const res = await fetch(url, { headers: { Accept: "application/json" } });
        if (!res.ok) throw new Error("HTTP " + res.status);
        return res.json();
    }

    async function loadInitial() {
        try {
            const page = await loadPage(null);
            page.messages.forEach((m) => upsert(m));
            hasMore = page.hasMore;
            moreBtn.hidden = !hasMore;
            refreshLayout();
            stickToBottom = true;
            scrollToBottom(false);
        } catch (e) {
            console.error(e);
            emptyEl.hidden = false;
            emptyEl.querySelector("p").textContent = "Mesajları yükləmək mümkün olmadı. Səhifəni yeniləyin.";
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
            showError("Köhnə mesajlar yüklənmədi.");
        } finally {
            moreBtn.disabled = false;
        }
    }

    /** Yenidən qoşulduqda qaçırılan mesajları gətir. */
    async function resync() {
        try {
            const page = await loadPage(null);
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
        if (!loadPromise) {
            loaded = true;   // bu andan canlı mesajlar da siyahıya əlavə olunur (upsert dublikat yaratmır)
            loadPromise = loadInitial();
        }
        return loadPromise;
    }

    /** İstifadəçi çatı hazırda görürmü? */
    function isChatVisible() {
        if (document.visibilityState !== "visible") return false;
        return cfg.drawer ? drawerOpen : loaded;
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
        const maxLoaded = messages.size ? Math.max(...messages.keys()) : 0;
        const lastId = Math.max(latestId, maxLoaded);
        setUnread(0);
        if (!lastId) return;
        const fd = new FormData();
        fd.append("lastId", String(lastId));
        await post("/api/chat/read", fd);
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
        const label = soundMuted ? "Bildiriş səsini aç" : "Bildiriş səsini söndür";
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
                    showError("Bildirişlər brauzerdə bloklanıb. Ünvan sətrindəki kilid işarəsindən icazə verin.");
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
        if (stickToBottom) jumpBtn.hidden = true;
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
        if (dto.id > latestId) latestId = dto.id;

        if (loaded) {
            const wasBottom = isNearBottom();
            upsert(dto);
            refreshLayout();
            if (wasBottom || isMine) scrollToBottom(true);
            else jumpBtn.hidden = false;
        }

        if (isMine) return;
        // Səhifə arxa plandadırsa və push aktivdirsə, sistem bildirişi səsi çalınır — ikiqat olmasın
        if (document.visibilityState === "visible" || !pushActive) playChime();
        if (isChatVisible()) {
            markReadSoon();
        } else {
            setUnread(unread + 1, {
                userName: dto.userName,
                isAdmin: !!dto.isAdmin,
                text: dto.message || (dto.attachmentType === "audio" ? "🎤 Səs mesajı"
                    : dto.attachmentType === "video" ? "🎬 Video"
                    : dto.attachmentType === "image" ? "📷 Şəkil" : "")
            });
        }
    });

    connection.on("EditMessage", (dto) => {
        if (!messages.has(dto.id)) return;
        upsert(dto);
        refreshLayout();
    });

    connection.on("DeleteMessage", (id) => removeMessage(id));

    connection.onreconnecting(() => setStatus("connecting", "Yenidən qoşulur…"));
    connection.onreconnected(() => {
        setStatus("online", "Onlayn");
        if (loaded) resync();
        refreshUnread();
    });
    connection.onclose(() => {
        setStatus("offline", "Bağlantı kəsildi");
        setTimeout(startConnection, 5000);
    });

    async function startConnection() {
        try {
            await connection.start();
            setStatus("online", "Onlayn");
        } catch (e) {
            setStatus("offline", "Bağlantı yoxdur");
            setTimeout(startConnection, 5000);
        }
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
        const showMic = canRecord && !hasContent && !sending;
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
        if (!rec) return 0;
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

        let stream;
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
            });
        } catch (e) {
            showError(e && e.name === "NotAllowedError"
                ? "Mikrofona icazə verilmədi. Brauzerin ünvan sətrindəki kilid işarəsindən icazə verin."
                : "Mikrofon tapılmadı və ya istifadə olunur.");
            return;
        }

        const mime = pickRecorderMime();
        let recorder;
        try {
            recorder = new MediaRecorder(stream, mime ? { mimeType: mime, audioBitsPerSecond: 32000 } : undefined);
        } catch (e) {
            stream.getTracks().forEach((t) => t.stop());
            showError("Brauzeriniz səs yazmanı dəstəkləmir.");
            return;
        }

        rec = {
            stream, recorder, mime,
            chunks: [],
            elapsed: 0,
            resumedAt: Date.now(),
            paused: false,
            levels: [],
            lastLevelAt: 0,
            canvas: recBar.querySelector(".dc-rec__wave"),
            analyser: null,
            audioCtx: null,
            raf: 0,
            timer: 0
        };

        recorder.ondataavailable = (e) => { if (e.data && e.data.size) rec && rec.chunks.push(e.data); };
        recorder.start(250);

        try {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            rec.audioCtx = new Ctx();
            rec.analyser = rec.audioCtx.createAnalyser();
            rec.analyser.fftSize = 512;
            rec.audioCtx.createMediaStreamSource(stream).connect(rec.analyser);
        } catch (e) { /* dalğa olmadan da yazılır */ }

        if (currentVoice) currentVoice.pause();

        rowEl.hidden = true;
        recBar.hidden = false;
        recBar.classList.remove("is-paused");
        recBar.querySelector(".dc-rec__pause i").className = "fa-solid fa-pause";
        recBar.querySelector(".dc-rec__time").textContent = "0:00";
        rec.timer = setInterval(tickRecTimer, 200);
        drawRecWave();
    }

    function togglePauseRecording() {
        if (!rec || rec.recorder.state === "inactive") return;
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
        rec.stream.getTracks().forEach((t) => t.stop());
        if (rec.audioCtx) rec.audioCtx.close().catch(() => {});
        rec = null;
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
        if (!rec || rec.sending) return;
        rec.sending = true;
        const ms = recElapsedMs();
        const blob = await stopRecorder();
        cleanupRecording();

        if (ms < 1000 || blob.size === 0) {
            showError("Səs mesajı çox qısadır.");
            return;
        }
        if (blob.size > cfg.maxAudioBytes) {
            showError("Səs mesajı maksimum " + Math.round(cfg.maxAudioBytes / 1024 / 1024) + " MB ola bilər.");
            return;
        }

        const file = new File([blob], "ses" + extForAudio(blob.type), { type: blob.type });
        const fd = new FormData();
        fd.append("File", file, file.name);

        sending = true;
        updateSendState();
        progressBar.style.width = "0%";
        progressEl.hidden = false;

        const res = await post("/api/chat/send", fd, (p) => (progressBar.style.width = Math.round(p * 100) + "%"));

        sending = false;
        progressEl.hidden = true;
        updateSendState();

        if (res.ok && res.data && res.data.id) {
            upsert(res.data);
            refreshLayout();
            scrollToBottom(true);
        } else if (!res.ok) {
            showError(res.error);
        }
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
        window.addEventListener("pagehide", () => { if (rec) { rec.recorder.stop(); cleanupRecording(); } });
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
            (fileKind(file) === "image" ? "Şəkil" : "Video") + " · " + formatSize(file.size);
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
            showError("Mesaj maksimum " + cfg.maxLength + " simvol ola bilər.");
            return;
        }

        const fd = new FormData();
        if (text) fd.append("Message", text);
        if (pendingFile) fd.append("File", pendingFile, pendingFile.name);

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
        });

        input.addEventListener("keydown", (e) => {
            if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
                e.preventDefault();
                sendMessage();
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
        textarea.placeholder = "Mesaj…";
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
                label.textContent = "Yeni: " + newFile.name + " (" + formatSize(newFile.size) + ")";
                fileRow.hidden = false;
                fileRow.classList.remove("is-removed");
                changeText.textContent = "Başqa fayl";
                removeBtn.hidden = false;
                removeBtn.replaceChildren(icon("fa-solid fa-rotate-left"), document.createTextNode(" Ləğv et"));
            } else if (hasExisting) {
                if (dto.attachmentType === "image") thumb.style.backgroundImage = 'url("' + dto.attachmentUrl + '")';
                else thumb.appendChild(icon(dto.attachmentType === "audio" ? "fa-solid fa-microphone" : "fa-solid fa-film"));
                const kindName = dto.attachmentType === "image" ? "Şəkil" : dto.attachmentType === "audio" ? "Səs mesajı" : "Video";
                label.textContent = kindName + (removeAttachment ? " silinəcək" : "");
                fileRow.hidden = false;
                fileRow.classList.toggle("is-removed", removeAttachment);
                changeText.textContent = "Dəyiş";
                removeBtn.hidden = false;
                removeBtn.replaceChildren(
                    icon(removeAttachment ? "fa-solid fa-rotate-left" : "fa-solid fa-trash"),
                    document.createTextNode(removeAttachment ? " Geri qaytar" : " Faylı sil"));
            } else {
                fileRow.hidden = true;
                changeText.textContent = "Şəkil/video əlavə et";
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

        const cancelBtn = el("button", "dc-btn", "Ləğv et");
        cancelBtn.type = "button";
        const saveBtn = el("button", "dc-btn dc-btn--primary");
        saveBtn.type = "button";
        saveBtn.append(icon("fa-solid fa-check"), document.createTextNode(" Yadda saxla"));
        buttons.append(cancelBtn, saveBtn);

        editor.appendChild(buttons);
        editor.appendChild(errorBox);

        async function save() {
            const text = textarea.value.trim();
            const willHaveFile = !!newFile || (!!dto.attachmentUrl && !removeAttachment);
            if (!text && !willHaveFile) {
                errorBox.textContent = "Mesaj boş qala bilməz. Mətn yazın və ya fayl əlavə edin.";
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
            saveBtn.lastChild.textContent = newFile ? " Yüklənir… 0%" : " Saxlanılır…";

            const res = await post("/api/chat/edit", fd, newFile ? (p) => (saveBtn.lastChild.textContent = " Yüklənir… " + Math.round(p * 100) + "%") : null);

            if (res.ok && res.data) {
                if (newFileUrl) URL.revokeObjectURL(newFileUrl);
                node.classList.remove("is-editing");
                upsert(res.data);
                refreshLayout();
            } else {
                saveBtn.disabled = false;
                cancelBtn.disabled = false;
                saveBtn.lastChild.textContent = " Yadda saxla";
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
        const question = ownMessage
            ? "Bu mesaj silinsin?"
            : "Bu mesaj (" + dto.userName + ") admin olaraq silinsin?";
        if (!confirm(question)) return;

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
    // START
    // =====================================================

    // Mobil: yazma sahəsi ekrandadırsa saytın üzən düymələri onu örtməsin (paneldə lazım deyil)
    if (form && !cfg.drawer && "IntersectionObserver" in window) {
        new IntersectionObserver((entries) => {
            document.body.classList.toggle("dc-composer-visible", entries[0].isIntersecting);
        }).observe(form);
    }

    updateSendState();

    if (cfg.drawer) {
        // Panel rejimi: mesajlar panel ilk dəfə açılanda yüklənir, bağlantı isə dərhal (bildirişlər üçün)
        document.addEventListener("dc:drawer-open", () => {
            drawerOpen = true;
            ensureLoaded().then(() => {
                scrollToBottom(false);
                markRead();
                if (input) input.focus({ preventScroll: true });
            });
        });
        document.addEventListener("dc:drawer-close", () => {
            drawerOpen = false;
            if (rec) cancelRecording();
            if (currentVoice) currentVoice.pause();
        });
        startConnection();
    } else {
        ensureLoaded().then(() => {
            if (isChatVisible()) markRead();
            startConnection();
        });
    }

    // İlk bildiriş: launcher skripti yükləndikdən sonra göndərilir
    const emitInitial = () => setUnread(Number(cfg.unreadCount) || 0, null, true);
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", emitInitial);
    else emitInitial();
})();
