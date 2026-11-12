/* =====================================================
   DoctorEngine.Az — AI Köməkçi
   Təhlükəsizlik: AI cavabı HEÇ VAXT innerHTML ilə yazılmır — sadə markdown
   (qalın, kursiv, siyahı, link) createElement + textContent ilə qurulur.
   ===================================================== */
(function () {
    "use strict";

    const root = document.getElementById("ai");
    const cfgEl = document.getElementById("aiConfig");
    if (!root || !cfgEl) return;

    const cfg = JSON.parse(cfgEl.textContent);
    const STORAGE_KEY = "de-ai-chat-v1";

    const body = root.querySelector(".ai__body");
    const list = root.querySelector(".ai__list");
    const welcome = root.querySelector(".ai__welcome");
    const resetBtn = root.querySelector("[data-ai-reset]");
    const form = root.querySelector(".ai__composer");
    const input = root.querySelector(".ai__input");
    const sendBtn = root.querySelector(".ai__send");
    const stopBtn = root.querySelector(".ai__stop");
    const counter = root.querySelector(".ai__counter");

    const remainingEl = root.querySelector("[data-ai-remaining]");
    const quotaEl = root.querySelector(".ai__quota");
    const limitBox = root.querySelector("[data-ai-limit-box]");

    let messages = loadHistory();   // [{ role: "user"|"assistant", content }]
    let busy = false;
    let controller = null;
    let remaining = typeof cfg.remaining === "number" ? cfg.remaining : 0;

    // =====================================================
    // GÜNDƏLİK LİMİT
    // =====================================================

    function setRemaining(value) {
        if (typeof value !== "number" || isNaN(value)) return;
        remaining = Math.max(0, value);
        if (remainingEl) remainingEl.textContent = String(remaining);
        if (quotaEl) quotaEl.classList.toggle("is-low", remaining <= 3);

        const exhausted = remaining <= 0;
        if (form) form.hidden = exhausted;
        if (limitBox) limitBox.hidden = !exhausted;
        root.querySelectorAll("[data-ai-suggest]").forEach((b) => (b.disabled = busy || exhausted || !cfg.configured));
    }

    function readRemaining(response) {
        const header = response.headers.get("X-AI-Remaining");
        if (header !== null) setRemaining(parseInt(header, 10));
    }

    // =====================================================
    // YADDAŞ (yalnız bu brauzer pəncərəsi; əlçatan olmasa da səhifə işləyir)
    // =====================================================

    function loadHistory() {
        try {
            const raw = sessionStorage.getItem(STORAGE_KEY);
            const data = raw ? JSON.parse(raw) : [];
            return Array.isArray(data) ? data.filter((m) => m && (m.role === "user" || m.role === "assistant") && typeof m.content === "string") : [];
        } catch {
            return [];
        }
    }

    function saveHistory() {
        try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(messages.slice(-30))); } catch { /* yaddaş əlçatan deyil */ }
    }

    // =====================================================
    // TƏHLÜKƏSİZ MARKDOWN → DOM
    // =====================================================

    function safeHref(url) {
        url = (url || "").trim();
        if (/^\/(?!\/)[\w\-./#?=&%]*$/.test(url)) return url;                 // saytın öz səhifəsi
        if (/^tel:\+?[0-9\s\-()]+$/i.test(url)) return url;
        if (/^https:\/\/wa\.me\/[0-9]+(\?[^\s"'<>]*)?$/i.test(url)) return url;
        return null;
    }

    /** Sətir daxili formatlama: **qalın**, *kursiv*, `kod`, [mətn](link) */
    function appendInline(parent, text) {
        const re = /(\*\*([^*]+)\*\*)|(\[([^\]]+)\]\(([^)\s]+)\))|(`([^`]+)`)|(\*([^*\s][^*]*)\*)|(_([^_\s][^_]*)_)/g;
        let last = 0, m;
        while ((m = re.exec(text)) !== null) {
            if (m.index > last) parent.appendChild(document.createTextNode(text.slice(last, m.index)));
            if (m[1]) {
                const b = document.createElement("strong"); b.textContent = m[2]; parent.appendChild(b);
            } else if (m[3]) {
                const href = safeHref(m[5]);
                if (href) {
                    const a = document.createElement("a");
                    a.textContent = m[4];
                    a.href = href;
                    if (href.startsWith("https://")) { a.target = "_blank"; a.rel = "noopener noreferrer"; }
                    parent.appendChild(a);
                } else {
                    parent.appendChild(document.createTextNode(m[4]));
                }
            } else if (m[6]) {
                const c = document.createElement("code"); c.textContent = m[7]; parent.appendChild(c);
            } else if (m[8]) {
                const i = document.createElement("em"); i.textContent = m[9]; parent.appendChild(i);
            } else if (m[10]) {
                const i = document.createElement("em"); i.textContent = m[11]; parent.appendChild(i);
            }
            last = re.lastIndex;
        }
        if (last < text.length) parent.appendChild(document.createTextNode(text.slice(last)));
    }

    /** Blok formatlama: paraqraf, "-"/"*" siyahı, "1." siyahı, "#" başlıq */
    function renderMarkdown(container, text) {
        container.replaceChildren();
        const lines = text.replace(/\r/g, "").split("\n");
        let listEl = null, listType = null, para = null;

        const closeList = () => { listEl = null; listType = null; };
        const closePara = () => { para = null; };

        for (const raw of lines) {
            const line = raw.trimEnd();

            if (!line.trim()) { closeList(); closePara(); continue; }

            const ul = line.match(/^\s*[-*•]\s+(.*)$/);
            const ol = line.match(/^\s*(\d+)[.)]\s+(.*)$/);
            const h = line.match(/^\s*#{1,6}\s+(.*)$/);

            if (ul || ol) {
                closePara();
                const type = ul ? "ul" : "ol";
                if (!listEl || listType !== type) {
                    listEl = document.createElement(type);
                    listType = type;
                    container.appendChild(listEl);
                }
                const li = document.createElement("li");
                appendInline(li, ul ? ul[1] : ol[2]);
                listEl.appendChild(li);
            } else if (h) {
                closeList(); closePara();
                const h4 = document.createElement("h4");
                appendInline(h4, h[1]);
                container.appendChild(h4);
            } else {
                closeList();
                if (!para) {
                    para = document.createElement("p");
                    container.appendChild(para);
                } else {
                    para.appendChild(document.createElement("br"));
                }
                appendInline(para, line.trim());
            }
        }

        if (!container.childNodes.length) container.appendChild(document.createElement("p"));
    }

    // =====================================================
    // GÖRÜNÜŞ
    // =====================================================

    function isNearBottom() {
        return body.scrollHeight - body.scrollTop - body.clientHeight < 140;
    }

    function scrollToBottom(force) {
        if (force || isNearBottom()) body.scrollTop = body.scrollHeight;
    }

    function updateChrome() {
        const has = messages.length > 0;
        welcome.hidden = has;
        if (resetBtn) resetBtn.hidden = !has || busy;
    }

    function buildMessage(role, content) {
        const wrap = document.createElement("div");
        wrap.className = "ai__msg " + (role === "user" ? "is-user" : "is-ai");

        const avatar = document.createElement("span");
        avatar.className = "ai__msg-avatar";
        avatar.setAttribute("aria-hidden", "true");
        const icon = document.createElement("i");
        icon.className = "fa-solid fa-wand-magic-sparkles";
        avatar.appendChild(icon);
        wrap.appendChild(avatar);

        const col = document.createElement("div");
        col.style.minWidth = "0";
        const bubble = document.createElement("div");
        bubble.className = "ai__bubble";

        if (role === "user") {
            bubble.textContent = content;
        } else {
            renderMarkdown(bubble, content);
        }

        col.appendChild(bubble);
        wrap.appendChild(col);
        return { wrap, bubble, col };
    }

    function addCopyButton(col, getText) {
        const meta = document.createElement("div");
        meta.className = "ai__meta";
        const copy = document.createElement("button");
        copy.type = "button";
        copy.innerHTML = '<i class="fa-regular fa-copy" aria-hidden="true"></i>';
        copy.appendChild(document.createTextNode(" Kopyala"));
        copy.addEventListener("click", async () => {
            try {
                await navigator.clipboard.writeText(getText());
                copy.lastChild.textContent = " Kopyalandı";
                setTimeout(() => (copy.lastChild.textContent = " Kopyala"), 1500);
            } catch { /* clipboard icazəsi yoxdur */ }
        });
        meta.appendChild(copy);
        col.appendChild(meta);
    }

    function renderAll() {
        list.replaceChildren();
        messages.forEach((m) => {
            const { wrap, col } = buildMessage(m.role, m.content);
            if (m.role === "assistant") addCopyButton(col, () => m.content);
            list.appendChild(wrap);
        });
        updateChrome();
        scrollToBottom(true);
    }

    function showError(text, retry) {
        const box = document.createElement("div");
        box.className = "ai__error";
        box.setAttribute("role", "alert");
        const icon = document.createElement("i");
        icon.className = "fa-solid fa-circle-exclamation";
        const span = document.createElement("span");
        span.textContent = text;
        box.append(icon, span);
        if (retry) {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.textContent = "Yenidən cəhd et";
            btn.addEventListener("click", () => { box.remove(); retry(); });
            box.appendChild(btn);
        }
        list.appendChild(box);
        scrollToBottom(true);
    }

    function setBusy(value) {
        busy = value;
        if (sendBtn) sendBtn.hidden = value;
        if (stopBtn) stopBtn.hidden = !value;
        if (input) input.readOnly = value;
        root.querySelectorAll("[data-ai-suggest]").forEach((b) => (b.disabled = value || !cfg.configured || remaining <= 0));
        updateSend();
        updateChrome();
    }

    function updateSend() {
        if (!input || !sendBtn) return;
        const len = input.value.trim().length;
        sendBtn.disabled = busy || len === 0;
        if (counter) {
            const used = input.value.length;
            counter.textContent = used > cfg.maxLength * 0.7 ? used + " / " + cfg.maxLength : "";
            counter.classList.toggle("is-warn", used >= cfg.maxLength);
        }
    }

    function autosize() {
        input.style.height = "auto";
        input.style.height = Math.min(input.scrollHeight, 160) + "px";
    }

    // =====================================================
    // GÖNDƏRMƏ (axın şəklində cavab)
    // =====================================================

    async function ask(question) {
        if (busy || !cfg.configured || remaining <= 0) return;
        question = (question || "").trim();
        if (!question) return;
        if (question.length > cfg.maxLength) question = question.slice(0, cfg.maxLength);

        messages.push({ role: "user", content: question });
        saveHistory();
        list.appendChild(buildMessage("user", question).wrap);
        updateChrome();
        scrollToBottom(true);

        await requestAnswer();
    }

    async function requestAnswer() {
        const { wrap, bubble, col } = buildMessage("assistant", "");
        bubble.replaceChildren();
        const typing = document.createElement("span");
        typing.className = "ai__typing";
        typing.setAttribute("aria-label", "Yazılır");
        typing.append(document.createElement("span"), document.createElement("span"), document.createElement("span"));
        bubble.appendChild(typing);
        list.appendChild(wrap);
        scrollToBottom(true);

        setBusy(true);
        controller = new AbortController();
        let text = "";
        let stopped = false;

        try {
            const response = await fetch("/api/ai/chat", {
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": cfg.token },
                body: JSON.stringify({ messages }),
                signal: controller.signal
            });

            readRemaining(response);

            if (!response.ok) {
                let msg = "Xəta baş verdi. Yenidən cəhd edin.";
                let data = null;
                try { data = await response.json(); if (data && data.error) msg = data.error; } catch { }
                if (data && typeof data.remaining === "number") setRemaining(data.remaining);
                wrap.remove();
                // Limit dolubsa və ya sorğu səhvdirsə "yenidən cəhd" mənasızdır
                const retryable = response.status !== 400 && response.status !== 503 && remaining > 0;
                showError(msg, retryable ? () => requestAnswer() : null);
                return;
            }

            bubble.classList.add("is-streaming");
            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let pending = false;

            const paint = () => {
                pending = false;
                const stick = isNearBottom();
                renderMarkdown(bubble, text);
                if (stick) scrollToBottom(true);
            };

            while (true) {
                const { done, value } = await reader.read();
                if (done) break;
                text += decoder.decode(value, { stream: true });
                if (!pending) { pending = true; requestAnimationFrame(paint); }
            }
            text += decoder.decode();
        } catch (err) {
            if (err && err.name === "AbortError") {
                stopped = true;
            } else {
                console.error(err);
                if (!text) {
                    wrap.remove();
                    showError("Bağlantı xətası. İnternetinizi yoxlayıb yenidən cəhd edin.", () => requestAnswer());
                    return;
                }
                text += "\n\n_(Bağlantı kəsildi.)_";
            }
        } finally {
            controller = null;
            bubble.classList.remove("is-streaming");
            setBusy(false);
            if (input) input.focus();
        }

        if (!text.trim()) {
            wrap.remove();
            if (!stopped) showError("Cavab alınmadı. Yenidən cəhd edin.", () => requestAnswer());
            return;
        }

        if (stopped) text += "\n\n_(dayandırıldı)_";
        renderMarkdown(bubble, text);
        const saved = { role: "assistant", content: text };
        messages.push(saved);
        saveHistory();
        addCopyButton(col, () => saved.content);
        scrollToBottom();
    }

    // =====================================================
    // HADİSƏLƏR
    // =====================================================

    if (form) {
        form.addEventListener("submit", (e) => {
            e.preventDefault();
            const q = input.value;
            input.value = "";
            autosize();
            updateSend();
            ask(q);
        });

        input.addEventListener("input", () => { autosize(); updateSend(); });
        input.addEventListener("keydown", (e) => {
            if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
                e.preventDefault();
                if (!sendBtn.disabled) form.requestSubmit();
            }
        });
    }

    if (stopBtn) stopBtn.addEventListener("click", () => controller && controller.abort());

    root.querySelectorAll("[data-ai-suggest]").forEach((btn) =>
        btn.addEventListener("click", () => ask(btn.dataset.aiSuggest)));

    if (resetBtn) {
        resetBtn.addEventListener("click", () => {
            if (busy) return;
            messages = [];
            saveHistory();
            renderAll();
            if (input) input.focus();
        });
    }

    renderAll();
    setRemaining(remaining);
    updateSend();
})();
