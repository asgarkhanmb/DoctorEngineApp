/* =====================================================
   Admin: canlı rezervasiya bildirişləri (/reservationHub)
   - Yeni rezervasiya: bildiriş + səs + menyudakı say + başlıqda say
   - Rezervasiyalar səhifəsində: cədvələ sətir əlavə olunur / status yenilənir / silinir
   ===================================================== */
(function () {
    "use strict";

    if (!window.signalR) return;

    const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const reservationsUrl = document.querySelector("[data-res-link]")?.getAttribute("href") || "/Admin/Reservation";

    // ---------- Menyudakı say ----------
    const badge = document.querySelector("[data-res-badge]");
    function bumpBadge(delta) {
        if (!badge) return;
        const n = Math.max(0, (parseInt(badge.textContent, 10) || 0) + delta);
        badge.textContent = n;
        badge.hidden = n === 0;
        if (delta > 0) { badge.classList.remove("is-bump"); void badge.offsetWidth; badge.classList.add("is-bump"); }
    }

    // ---------- Başlıqda oxunmamış say ----------
    const baseTitle = document.title;
    let unseen = 0;
    function setUnseen(n) {
        unseen = n;
        document.title = n > 0 ? `(${n}) Yeni rezervasiya — ${baseTitle}` : baseTitle;
    }
    document.addEventListener("visibilitychange", () => { if (!document.hidden) setUnseen(0); });

    // ---------- Qısa səs siqnalı ----------
    function ding() {
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            [880, 1320].forEach((f, i) => {
                const o = ctx.createOscillator(), g = ctx.createGain();
                o.type = "sine"; o.frequency.value = f;
                g.gain.setValueAtTime(0.0001, ctx.currentTime + i * 0.15);
                g.gain.exponentialRampToValueAtTime(0.15, ctx.currentTime + i * 0.15 + 0.02);
                g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + i * 0.15 + 0.25);
                o.connect(g).connect(ctx.destination);
                o.start(ctx.currentTime + i * 0.15); o.stop(ctx.currentTime + i * 0.15 + 0.3);
            });
        } catch { /* brauzer səsə icazə vermir — problem deyil */ }
    }

    // ---------- Bildiriş ----------
    function toast(html, kind = "success", ms = 9000) {
        let box = document.querySelector(".adm-toasts");
        if (!box) { box = document.createElement("div"); box.className = "adm-toasts"; document.body.appendChild(box); }
        const el = document.createElement("div");
        el.className = `adm-toast is-${kind} adm-toast--live`;
        el.setAttribute("role", "status");
        el.innerHTML = `${html}<button type="button" aria-label="Bağla">&times;</button>`;
        const close = () => { el.classList.add("is-leaving"); setTimeout(() => el.remove(), 300); };
        el.querySelector("button").addEventListener("click", close);
        box.appendChild(el);
        setTimeout(close, ms);
    }

    // ---------- Cədvəl (yalnız Rezervasiyalar səhifəsində) ----------
    const table = document.getElementById("resTable");
    const live = document.getElementById("resLive");
    const tbody = table?.querySelector("tbody");

    const STATUS_BADGE = {
        Pending: '<span class="adm-badge is-pending"><i class="fa-regular fa-hourglass-half"></i> Gözləyir</span>',
        Confirmed: '<span class="adm-badge is-confirmed"><i class="fa-solid fa-check"></i> Təsdiqləndi</span>',
        Rejected: '<span class="adm-badge is-rejected"><i class="fa-solid fa-xmark"></i> Rədd edildi</span>'
    };

    function updateCounts() {
        if (!tbody) return;
        const rows = [...tbody.querySelectorAll("tr[data-row]")];
        const set = (k, n) => { const el = document.querySelector(`[data-count="${k}"]`); if (el) el.textContent = n; };
        set("all", rows.length);
        ["Pending", "Confirmed", "Rejected"].forEach((s) => set(s, rows.filter((r) => r.dataset.status === s).length));
    }

    // Axtarış + status filtri + səhifələmə yenidən tətbiq olunsun (admin.js)
    function reapplyFilter() {
        if (window.admTables && table) window.admTables.refresh(table);
    }

    function tokenInput() {
        return live?.querySelector("input[name=__RequestVerificationToken]")?.outerHTML || "";
    }

    function rowHtml(r) {
        const wa = (r.whatsApp || "").replace(/[+\s-]/g, "");
        const waText = encodeURIComponent(`Salam, ${r.fullName}! ${r.serviceName} üzrə rezervasiyanızla bağlı yazırıq.`);
        const tel = (r.phone || "").replace(/\s/g, "");
        return `<tr data-row data-status="Pending" data-id="${r.id}" class="is-new">
            <td>
                <div class="adm-cell-title">${esc(r.fullName)} <span class="adm-new-tag">Yeni</span></div>
                <div class="adm-cell-sub"><a href="tel:${esc(tel)}" class="text-reset">${esc(r.phone)}</a></div>
            </td>
            <td>${esc(r.serviceName)}</td>
            <td class="text-nowrap"><div class="adm-cell-title">${esc(r.date)}</div><div class="adm-cell-sub">${esc(r.time)}</div></td>
            <td><div class="adm-clamp" title="${esc(r.note)}">${r.note ? esc(r.note) : "—"}</div></td>
            <td data-status-cell>${STATUS_BADGE.Pending}</td>
            <td class="adm-actions-col">
                <div class="adm-actions">
                    ${wa ? `<a class="adm-act is-success" href="https://wa.me/${esc(wa)}?text=${waText}" target="_blank" rel="noopener" title="WhatsApp ilə yaz" aria-label="WhatsApp ilə yaz"><i class="fa-brands fa-whatsapp"></i></a>` : ""}
                    <form action="${esc(live.dataset.confirmUrl)}/${r.id}" method="post" data-pending-action>${tokenInput()}
                        <button type="submit" class="adm-act is-success" title="Təsdiqlə" aria-label="Təsdiqlə"><i class="fa-solid fa-check"></i></button>
                    </form>
                    <a href="${esc(live.dataset.rejectUrl)}/${r.id}" class="adm-act" title="Rədd et" aria-label="Rədd et" data-pending-action><i class="fa-solid fa-ban"></i></a>
                    <form action="${esc(live.dataset.deleteUrl)}/${r.id}" method="post">${tokenInput()}
                        <button type="submit" class="adm-act is-danger" title="Sil" aria-label="Sil" data-confirm="${esc(r.fullName)} — rezervasiya silinsin?"><i class="fa-regular fa-trash-can"></i></button>
                    </form>
                </div>
            </td>
        </tr>`;
    }

    // Statusu dəyişən sətir: badge yenilənir, "təsdiqlə / rədd et" düymələri götürülür
    function applyStatus(row, status, reason) {
        row.dataset.status = status;
        const cells = row.querySelectorAll("td");
        const cell = row.querySelector("[data-status-cell]") || cells[4];
        if (cell) {
            cell.innerHTML = (STATUS_BADGE[status] || esc(status)) +
                (status === "Rejected" && reason ? `<div class="adm-cell-sub mt-1 adm-clamp" title="${esc(reason)}">${esc(reason)}</div>` : "");
        }
        if (status !== "Pending") {
            row.querySelectorAll("form[action*='/Confirm'], a[href*='/Reject'], [data-pending-action]").forEach((el) => el.remove());
        }
        row.classList.remove("is-flash"); void row.offsetWidth; row.classList.add("is-flash");
    }

    // ---------- Bağlantı ----------
    const conn = new signalR.HubConnectionBuilder()
        .withUrl("/reservationHub")
        .withAutomaticReconnect()
        .build();

    conn.on("ReservationCreated", (r) => {
        bumpBadge(1);
        if (document.hidden) setUnseen(unseen + 1);
        ding();

        toast(`<i class="fa-regular fa-calendar-plus"></i>
               <span><b>Yeni rezervasiya:</b> ${esc(r.fullName)} — ${esc(r.serviceName)}, ${esc(r.date)} ${esc(r.time)}
               ${tbody ? "" : `<br><a href="${esc(reservationsUrl)}">Rezervasiyalara bax →</a>`}</span>`);

        if (tbody && live) {
            tbody.insertAdjacentHTML("afterbegin", rowHtml(r));
            updateCounts();
            reapplyFilter();
        } else if (document.querySelector(".adm-empty") && location.pathname.toLowerCase().includes("/reservation")) {
            // Boş siyahı idi — cədvəl hələ yoxdur, səhifəni yeniləyirik
            setTimeout(() => location.reload(), 1500);
        }
    });

    conn.on("ReservationUpdated", (r) => {
        const row = tbody?.querySelector(`tr[data-id="${r.id}"]`);
        const wasPending = row ? row.dataset.status === "Pending" : true;
        if (row) { applyStatus(row, r.status, r.reason); updateCounts(); reapplyFilter(); }
        if (wasPending && r.status !== "Pending") bumpBadge(-1);
    });

    conn.on("ReservationDeleted", (r) => {
        if (r.status === "Pending") bumpBadge(-1);
        const row = tbody?.querySelector(`tr[data-id="${r.id}"]`);
        if (!row) return;
        row.remove();
        updateCounts();
        reapplyFilter();
    });

    conn.start().catch((err) => console.warn("Canlı bildirişlər qoşulmadı:", err));
})();
