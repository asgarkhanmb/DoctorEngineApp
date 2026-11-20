/* =====================================================
   Canlı rezervasiya (Views/Shared/_BookingForm.cshtml, Views/Reservation/Status.cshtml)
   - Boş saatlar: /Reservation/Slots + SignalR (SlotTaken / SlotFreed)
   - Status: izləmə kodu + SignalR (StatusChanged)
   - "Rezervasiyalarım": kodlar brauzerdə (localStorage) saxlanılır
   ===================================================== */
(function () {
    "use strict";

    const SIGNALR_SRC = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js";
    const STORE_KEY = "de.reservations";
    const MAX_CODES = 10;

    const STATUS = {
        Pending:   { text: deT("Gözləyir"),     icon: "fa-regular fa-hourglass-half", cls: "is-pending" },
        Confirmed: { text: deT("Təsdiqləndi"),  icon: "fa-solid fa-check",            cls: "is-confirmed" },
        Rejected:  { text: deT("Rədd edildi"),  icon: "fa-solid fa-xmark",            cls: "is-rejected" },
        Deleted:   { text: deT("Ləğv edildi"),  icon: "fa-solid fa-ban",              cls: "is-rejected" }
    };

    // ---------------- Yardımçılar ----------------

    const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

    function readCodes() {
        try { return JSON.parse(localStorage.getItem(STORE_KEY) || "[]").filter((c) => /^[0-9a-f]{32}$/i.test(c)); }
        catch { return []; }
    }

    function saveCode(code) {
        try {
            const list = [code, ...readCodes().filter((c) => c !== code)].slice(0, MAX_CODES);
            localStorage.setItem(STORE_KEY, JSON.stringify(list));
        } catch { /* gizli rejim — sadəcə yadda saxlanmır */ }
    }

    function loadSignalR() {
        if (window.signalR) return Promise.resolve(window.signalR);
        return new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${SIGNALR_SRC}"]`);
            const s = existing || document.createElement("script");
            s.addEventListener("load", () => resolve(window.signalR));
            s.addEventListener("error", reject);
            if (!existing) { s.src = SIGNALR_SRC; s.async = true; document.head.appendChild(s); }
        });
    }

    let connPromise = null;
    const handlers = {};
    let trackedCodes = new Set();

    function on(event, fn) { (handlers[event] = handlers[event] || []).push(fn); }

    function connect() {
        if (connPromise) return connPromise;
        connPromise = loadSignalR().then(async (signalR) => {
            const conn = new signalR.HubConnectionBuilder()
                .withUrl("/reservationHub")
                .withAutomaticReconnect()
                .build();

            ["SlotTaken", "SlotFreed", "StatusChanged"].forEach((ev) =>
                conn.on(ev, (data) => (handlers[ev] || []).forEach((fn) => fn(data))));

            conn.onreconnecting(() => setLive(false));
            conn.onreconnected(() => {
                setLive(true);
                if (trackedCodes.size) conn.invoke("Track", [...trackedCodes]).catch(() => { });
                (handlers.reconnected || []).forEach((fn) => fn());
            });

            await conn.start();
            setLive(true);
            return conn;
        }).catch((err) => {
            console.warn("Canlı bağlantı qurulmadı:", err);
            setLive(false);
            return null;
        });
        return connPromise;
    }

    async function track(codes) {
        codes.forEach((c) => trackedCodes.add(c));
        const conn = await connect();
        if (conn && codes.length) conn.invoke("Track", codes).catch(() => { });
    }

    function setLive(ok) {
        document.querySelectorAll(".bk-live").forEach((el) => {
            el.classList.toggle("is-off", !ok);
            const t = el.querySelector("[data-bk-live-text]");
            if (t) t.textContent = ok ? (el.dataset.onText || deT("Canlı: boş saatlar real vaxtda yenilənir")) : deT("Bağlantı bərpa olunur…");
        });
    }

    // Status izləyicisi: Göndərildi → Baxılır → Təsdiqləndi / Rədd edildi
    function trackerHtml(status, reason) {
        const final = status === "Confirmed" ? { t: deT("Təsdiqləndi"), i: "fa-solid fa-check", c: "is-done is-ok" }
                    : status === "Rejected" ? { t: deT("Rədd edildi"), i: "fa-solid fa-xmark", c: "is-done is-bad" }
                    : status === "Deleted" ? { t: deT("Ləğv edildi"), i: "fa-solid fa-ban", c: "is-done is-bad" }
                    : { t: deT("Təsdiq"), i: "fa-regular fa-circle-check", c: "" };
        const reviewing = status === "Pending";

        let msg = "";
        if (status === "Pending") msg = '<p class="bk-tracker__msg"><span class="bk-live__dot"></span> ' + deT('Usta rezervasiyanıza baxır — status dəyişən kimi burada görünəcək.') + '</p>';
        else if (status === "Confirmed") msg = '<p class="bk-tracker__msg is-ok"><i class="fa-solid fa-circle-check"></i> ' + deT('Rezervasiyanız təsdiqləndi! Sizi gözləyirik.') + '</p>';
        else if (status === "Rejected") msg = `<p class="bk-tracker__msg is-bad"><i class="fa-solid fa-circle-info"></i> ${deT("Təəssüf ki, rezervasiya təsdiqlənmədi.")}${reason ? " " + deT("Səbəb:") + " " + esc(reason) : ""}</p>`;
        else if (status === "Deleted") msg = '<p class="bk-tracker__msg is-bad"><i class="fa-solid fa-circle-info"></i> ' + deT('Rezervasiya ləğv edilib.') + '</p>';

        return `<ol class="bk-tracker" data-status="${esc(status)}">
                    <li class="is-done"><span><i class="fa-solid fa-paper-plane"></i></span><b>${deT("Göndərildi")}</b></li>
                    <li class="${reviewing ? "is-active" : "is-done"}"><span><i class="fa-solid fa-magnifying-glass"></i></span><b>${deT("Baxılır")}</b></li>
                    <li class="${final.c}"><span><i class="${final.i}"></i></span><b>${final.t}</b></li>
                </ol>${msg}`;
    }

    function pill(status) {
        const s = STATUS[status] || STATUS.Pending;
        return `<span class="bk-pill ${s.cls}"><i class="${s.icon}"></i> ${s.text}</span>`;
    }

    // ---------------- Forma ----------------

    const root = document.querySelector("[data-bk]");
    if (root) initForm(root);

    function initForm(root) {
        const form = root.querySelector("[data-bk-form]");
        const slotsEl = root.querySelector("[data-bk-slots]");
        const notice = root.querySelector("[data-bk-notice]");
        const hidden = root.querySelector("[data-bk-datetime]");
        const summary = root.querySelector("[data-bk-summary] span");
        const msgBox = root.querySelector("#reservationMessage");
        const submitBtn = form.querySelector("button[type=submit]");
        const done = root.querySelector("[data-bk-done]");
        const slotsUrl = root.dataset.slotsUrl;
        const lookupUrl = root.dataset.lookupUrl;
        const statusUrl = root.dataset.statusUrl;

        let selectedTime = null;
        let lastCode = null;
        let reqId = 0;

        const serviceId = () => form.querySelector("[data-bk-services] input:checked")?.value || "";
        const serviceName = () => form.querySelector("[data-bk-services] input:checked")?.parentElement.textContent.trim() || "";
        const day = () => form.querySelector("[data-bk-days] input:checked")?.value || "";
        const dayLabel = () => {
            const s = form.querySelector("[data-bk-days] input:checked + span");
            return s ? [...s.children].map((c) => c.textContent).join(" ") : "";
        };

        function showNotice(text) {
            notice.textContent = text;
            notice.hidden = !text;
        }

        function updateSummary() {
            hidden.value = day() && selectedTime ? `${day()}T${selectedTime}` : "";
            summary.textContent = serviceId() && day() && selectedTime
                ? `${serviceName()} · ${dayLabel()} · ${selectedTime}`
                : deT("Xidmət, gün və saat seçin");
            root.querySelector("[data-bk-summary]").classList.toggle("is-ready", !!hidden.value);
        }

        async function loadSlots() {
            showNotice("");
            if (!serviceId()) {
                slotsEl.innerHTML = '<p class="bk-slots__hint"><i class="fa-regular fa-hand-pointer"></i> ' + deT('Boş saatları görmək üçün xidmət seçin.') + '</p>';
                selectedTime = null; updateSummary();
                return;
            }

            const id = ++reqId;
            slotsEl.classList.add("is-loading");
            if (!slotsEl.querySelector(".bk-slot")) slotsEl.innerHTML = '<div class="bk-slots__skeleton">' + '<span></span>'.repeat(12) + "</div>";

            try {
                const res = await fetch(`${slotsUrl}?serviceId=${encodeURIComponent(serviceId())}&date=${encodeURIComponent(day())}`, { headers: { Accept: "application/json" } });
                // API: [{ time, state }] (köhnə sayt: [{ t, s }])
                const slots = (await res.json()).map((x) => ({ t: x.t ?? x.time, s: x.s ?? x.state }));
                if (id !== reqId) return;   // köhnə cavab

                const free = slots.filter((s) => s.s === "free");
                if (selectedTime && !free.some((s) => s.t === selectedTime)) selectedTime = null;

                if (!free.length) {
                    slotsEl.innerHTML = '<p class="bk-slots__hint"><i class="fa-regular fa-calendar-xmark"></i> ' + deT('Bu gün üçün boş saat qalmayıb — başqa gün seçin.') + '</p>';
                } else {
                    slotsEl.innerHTML = slots.map((s) =>
                        `<button type="button" class="bk-slot ${s.s === "free" ? "" : "is-" + s.s} ${s.t === selectedTime ? "is-selected" : ""}"
                                 data-time="${s.t}" ${s.s === "free" ? "" : "disabled"}
                                 aria-pressed="${s.t === selectedTime}" aria-label="${s.t}${s.s === "busy" ? " — " + deT("tutulub") : s.s === "past" ? " — " + deT("keçib") : ""}">${s.t}</button>`).join("");
                }
            } catch {
                slotsEl.innerHTML = '<p class="bk-slots__hint is-error"><i class="fa-solid fa-triangle-exclamation"></i> ' + deT('Saatlar yüklənmədi. Səhifəni yeniləyin.') + '</p>';
            } finally {
                if (id === reqId) slotsEl.classList.remove("is-loading");
                updateSummary();
            }
        }

        slotsEl.addEventListener("click", (e) => {
            const btn = e.target.closest(".bk-slot");
            if (!btn || btn.disabled) return;
            selectedTime = btn.dataset.time;
            slotsEl.querySelectorAll(".bk-slot").forEach((b) => {
                const on = b === btn;
                b.classList.toggle("is-selected", on);
                b.setAttribute("aria-pressed", on);
            });
            showNotice("");
            updateSummary();
        });

        form.querySelector("[data-bk-services]").addEventListener("change", loadSlots);
        form.querySelector("[data-bk-days]").addEventListener("change", () => { selectedTime = null; loadSlots(); });

        // Seçilmiş gün görünən yerdə olsun (yalnız gün zolağını üfüqi sürüşdür).
        // scrollIntoView istifadə etmirik — o, bütün səhifəni rezervasiya bölməsinə endirirdi.
        const daysEl = form.querySelector("[data-bk-days]");
        const checkedDay = daysEl?.querySelector("input:checked")?.parentElement;
        if (daysEl && checkedDay) {
            const cr = daysEl.getBoundingClientRect();
            const lr = checkedDay.getBoundingClientRect();
            daysEl.scrollLeft += (lr.left - cr.left) - (cr.width - lr.width) / 2;
        }

        // ---- Canlı: başqası saat tutanda / saat boşalanda ----
        const sameSlot = (d) => String(d.serviceId) === serviceId() && d.date === day();

        on("SlotTaken", (d) => {
            if (!sameSlot(d)) return;
            const btn = slotsEl.querySelector(`.bk-slot[data-time="${d.time}"]`);
            if (!btn || btn.classList.contains("is-busy")) return;
            btn.classList.remove("is-selected");
            btn.classList.add("is-busy", "is-flash");
            btn.disabled = true;
            // Öz göndərişimiz deyilsə (forma açıqdır və göndərilmir) — xəbərdar et
            if (selectedTime === d.time && !submitting && !form.hidden) {
                selectedTime = null;
                updateSummary();
                showNotice(deT("Seçdiyiniz {0} saatı indicə başqa müştəri tərəfindən tutuldu. Zəhmət olmasa başqa saat seçin.", d.time));
            }
        });

        on("SlotFreed", (d) => {
            if (!sameSlot(d)) return;
            const btn = slotsEl.querySelector(`.bk-slot[data-time="${d.time}"]`);
            if (!btn || !btn.classList.contains("is-busy")) { loadSlots(); return; }
            btn.classList.remove("is-busy");
            btn.classList.add("is-flash");
            btn.disabled = false;
        });

        on("reconnected", loadSlots);

        // ---- Göndər ----
        let submitting = false;

        function showMessage(ok, text) {
            msgBox.innerHTML = `<div class="bk-alert ${ok ? "is-ok" : "is-error"}"><i class="fa-solid ${ok ? "fa-circle-check" : "fa-circle-exclamation"}"></i><span>${esc(text)}</span></div>`;
        }

        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            if (submitting) return;
            msgBox.innerHTML = "";

            if (!serviceId()) return showMessage(false, deT("Xidmət seçin."));
            if (!selectedTime) return showMessage(false, deT("Boş saatlardan birini seçin."));
            if (!form.checkValidity()) { form.reportValidity(); return; }

            submitting = true;
            const label = submitBtn.innerHTML;
            submitBtn.disabled = true;
            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> ' + deT('Göndərilir…');

            const chosen = { service: serviceName(), day: dayLabel(), time: selectedTime };

            try {
                const res = await fetch(form.action, { method: "POST", body: new FormData(form) });
                const data = await res.json();

                if (!data.success) {
                    showMessage(false, data.message || data.error || deT("Xəta baş verdi. Yenidən cəhd edin."));
                    loadSlots();   // saat tutulubsa siyahı yenilənsin
                    return;
                }

                if (data.trackingCode) {
                    lastCode = data.trackingCode;
                    saveCode(lastCode);
                    track([lastCode]);
                }

                showDone(chosen);
                refreshMine();
            } catch (err) {
                console.error(err);
                showMessage(false, deT("Bağlantı xətası. İnternetinizi yoxlayıb yenidən cəhd edin."));
            } finally {
                submitting = false;
                submitBtn.disabled = false;
                submitBtn.innerHTML = label;
            }
        });

        function showDone(chosen) {
            done.querySelector("[data-bk-done-summary]").textContent = `${chosen.service} · ${chosen.day} · ${chosen.time}`;
            done.querySelector("[data-bk-done-tracker]").innerHTML = trackerHtml("Pending");
            const link = done.querySelector("[data-bk-done-link]");
            if (lastCode) { link.href = statusUrl.replace("__code__", lastCode); link.hidden = false; }
            else link.hidden = true;

            form.hidden = true;
            done.hidden = false;
            done.scrollIntoView({ behavior: "smooth", block: "center" });
        }

        done.querySelector("[data-bk-again]").addEventListener("click", () => {
            form.reset();
            const same = document.getElementById("sameAsPhone");
            const wa = document.getElementById("whatsappInput");
            if (same) same.checked = false;
            if (wa) wa.readOnly = false;
            selectedTime = null;
            lastCode = null;
            msgBox.innerHTML = "";
            done.hidden = true;
            form.hidden = false;
            loadSlots();
        });

        on("StatusChanged", (d) => {
            if (d.code === lastCode && !done.hidden)
                done.querySelector("[data-bk-done-tracker]").innerHTML = trackerHtml(d.status, d.reason);
        });

        // ---- Rezervasiyalarım ----
        const mine = root.querySelector("[data-bk-mine]");
        const mineList = root.querySelector("[data-bk-mine-list]");

        async function refreshMine() {
            const codes = readCodes();
            if (!codes.length) { mine.hidden = true; return; }

            try {
                const qs = codes.map((c) => "codes=" + encodeURIComponent(c)).join("&");
                const items = await (await fetch(`${lookupUrl}?${qs}`, { headers: { Accept: "application/json" } })).json();
                if (!items.length) { mine.hidden = true; return; }

                mineList.innerHTML = items.slice(0, 5).map((r) => `
                    <li data-code="${esc(r.code)}">
                        <a href="${esc(statusUrl.replace("__code__", r.code))}">
                            <span class="bk-mine__main"><b>${esc(r.service)}</b><small>${esc(r.date)} · ${esc(r.time)}</small></span>
                            <span data-pill>${pill(r.status)}</span>
                        </a>
                    </li>`).join("");
                mine.hidden = false;
                track(items.map((r) => r.code));
            } catch { /* sakit */ }
        }

        on("StatusChanged", (d) => {
            const li = mineList.querySelector(`li[data-code="${d.code}"]`);
            if (!li) return;
            li.querySelector("[data-pill]").innerHTML = pill(d.status);
            li.classList.add("is-flash");
            setTimeout(() => li.classList.remove("is-flash"), 1600);
        });

        // Başlanğıc
        loadSlots();
        refreshMine();
        connect();
    }

    // ---------------- İzləmə səhifəsi ----------------

    const statusRoot = document.querySelector("[data-bk-status]");
    if (statusRoot) {
        const code = statusRoot.dataset.code;
        const box = statusRoot.querySelector("[data-bk-status-tracker]");
        const pillBox = statusRoot.querySelector("[data-bk-status-pill]");
        box.innerHTML = trackerHtml(statusRoot.dataset.status, statusRoot.dataset.reason);
        saveCode(code);
        track([code]);

        on("StatusChanged", (d) => {
            if (d.code !== code) return;
            box.innerHTML = trackerHtml(d.status, d.reason);
            if (pillBox) pillBox.innerHTML = pill(d.status);
            statusRoot.classList.add("is-flash");
            setTimeout(() => statusRoot.classList.remove("is-flash"), 1600);
        });
    }
})();
