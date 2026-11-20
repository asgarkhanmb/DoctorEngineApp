/* Admin: rezervasiyalar — təsdiqlə, rədd et (səbəb), sil, WhatsApp. Yeni rezervasiyalar canlı gəlir (admin-live.js). */
(function () {
    "use strict";
    const esc = DE.esc, A = DE.admin;
    const wa = (n) => String(n || "").replace(/[+\s-]/g, "");

    A.page({
        title: "Rezervasiyalar",
        active: "Reservation",
        async render() {
            const list = await DE.api.get("/api/v1/admin/reservations");
            const items = list.slice().sort((a, b) => (b.status === "Pending") - (a.status === "Pending") || A.date(b.dateTime) - A.date(a.dateTime));
            const count = (s) => items.filter((r) => r.status === s).length;
            const waText = (r) => {
                const when = A.fmt.date(r.dateTime) + " " + A.fmt.time(r.dateTime);
                return r.status === "Confirmed" ? `Salam, ${r.fullName}! ${r.serviceName} üzrə ${when} tarixli rezervasiyanız təsdiqləndi. DoctorEngine.Az`
                    : r.status === "Rejected" ? `Salam, ${r.fullName}! Təəssüf ki, ${when} tarixli rezervasiyanız təsdiqlənmədi. Səbəb: ${r.rejectionReason || ""}`
                        : `Salam, ${r.fullName}! ${r.serviceName} üzrə rezervasiyanızla bağlı yazırıq.`;
            };

            return `
${A.pageHead({ title: "Rezervasiyalar", subtitle: "Müştərilərin onlayn rezervasiyaları — təsdiqləyin, rədd edin və ya WhatsApp ilə yazın. Yeni rezervasiyalar səhifə yenilənmədən burada görünür." })}
<div id="resLive" hidden data-confirm-url="/Admin/Reservation/Confirm" data-reject-url="/Admin/Reservation/Reject" data-delete-url="/Admin/Reservation/Delete"></div>
<div class="adm-card">
    <div class="adm-card__head flex-wrap">
        <div class="btn-group btn-group-sm" role="group" data-status-filter="#resTable" aria-label="Statusa görə filtr">
            <button type="button" class="btn btn-light active" data-status="all">Hamısı <span class="text-muted" data-count="all">${items.length}</span></button>
            <button type="button" class="btn btn-light" data-status="Pending">Gözləyir <span class="text-muted" data-count="Pending">${count("Pending")}</span></button>
            <button type="button" class="btn btn-light" data-status="Confirmed">Təsdiqləndi <span class="text-muted" data-count="Confirmed">${count("Confirmed")}</span></button>
            <button type="button" class="btn btn-light" data-status="Rejected">Rədd edildi <span class="text-muted" data-count="Rejected">${count("Rejected")}</span></button>
        </div>
        <div class="adm-search"><i class="fa-solid fa-magnifying-glass"></i><input type="search" class="form-control form-control-sm" data-table-search="#resTable" placeholder="Ad, telefon, xidmət…" /></div>
    </div>
    ${!items.length ? `<div class="adm-empty"><i class="fa-regular fa-calendar"></i><strong>Hələ rezervasiya yoxdur</strong>Saytdan gələn rezervasiyalar burada görünəcək.</div>` : `
    <div class="adm-table-wrap">
        <table class="adm-table" id="resTable">
            <thead><tr><th>Müştəri</th><th>Xidmət</th><th>Tarix</th><th>Qeyd</th><th>Status</th><th class="adm-actions-col"></th></tr></thead>
            <tbody>
                ${items.map((r) => `
                <tr data-row data-status="${esc(r.status)}" data-id="${r.id}">
                    <td><div class="adm-cell-title">${esc(r.fullName)}</div><div class="adm-cell-sub"><a href="tel:${esc((r.phone || "").replace(/ /g, ""))}" class="text-reset">${esc(r.phone)}</a></div></td>
                    <td>${esc(r.serviceName)}</td>
                    <td class="text-nowrap"><div class="adm-cell-title">${A.fmt.date(r.dateTime)}</div><div class="adm-cell-sub">${A.fmt.time(r.dateTime)}</div></td>
                    <td><div class="adm-clamp" title="${esc(r.note || "")}">${r.note ? esc(r.note) : "—"}</div></td>
                    <td>${A.statusBadge(r.status)}${r.status === "Rejected" && r.rejectionReason ? `<div class="adm-cell-sub mt-1 adm-clamp" title="${esc(r.rejectionReason)}">${esc(r.rejectionReason)}</div>` : ""}</td>
                    <td class="adm-actions-col"><div class="adm-actions">
                        ${r.whatsApp ? `<a class="adm-act is-success" href="https://wa.me/${esc(wa(r.whatsApp))}?text=${encodeURIComponent(waText(r))}" target="_blank" rel="noopener" title="WhatsApp ilə yaz" aria-label="WhatsApp ilə yaz"><i class="fa-brands fa-whatsapp"></i></a>` : ""}
                        ${r.status === "Pending" ? `
                        <form action="/Admin/Reservation/Confirm/${r.id}" method="post"><button type="submit" class="adm-act is-success" title="Təsdiqlə" aria-label="Təsdiqlə"><i class="fa-solid fa-check"></i></button></form>
                        <a href="/Admin/Reservation/Reject/${r.id}" class="adm-act" title="Rədd et" aria-label="Rədd et"><i class="fa-solid fa-ban"></i></a>` : ""}
                        ${A.deleteButton(`/Admin/Reservation/Delete/${r.id}`, `${r.fullName} — rezervasiya silinsin?`)}
                    </div></td>
                </tr>`).join("")}
                <tr data-search-empty hidden><td colspan="6"><div class="adm-empty"><i class="fa-solid fa-magnifying-glass"></i>Nəticə tapılmadı</div></td></tr>
            </tbody>
        </table>
    </div>`}
</div>

<!-- Rədd etmə səbəbi -->
<div class="modal fade" id="rejectModal" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered">
        <form class="modal-content" data-js novalidate id="rejectForm">
            <div class="modal-header"><h5 class="modal-title">Rezervasiyanı rədd et</h5><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Bağla"></button></div>
            <div class="modal-body">
                <div class="alert alert-danger py-2" role="alert" data-error hidden></div>
                <label class="form-label" for="reason">İmtina səbəbi *</label>
                <textarea id="reason" name="reason" class="form-control" rows="3" maxlength="500" required data-val-required="İmtina səbəbini yazın."></textarea>
                <span class="text-danger small" data-valmsg-for="reason"></span>
                <div class="form-text">Səbəb müştəriyə göstərilir (status səhifəsində və WhatsApp mesajında).</div>
            </div>
            <div class="modal-footer"><button type="button" class="btn btn-light" data-bs-dismiss="modal">Ləğv et</button><button type="submit" class="btn btn-danger"><i class="fa-solid fa-ban"></i> Rədd et</button></div>
        </form>
    </div>
</div>`;
        },
        after() {
            const modalEl = document.getElementById("rejectModal");
            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            const form = document.getElementById("rejectForm");
            let rejectId = null;

            // "Rədd et" (canlı əlavə olunan sətirlərdə də): /Admin/Reservation/Reject/{id}
            document.addEventListener("click", (e) => {
                const a = e.target.closest("a[href*='/Admin/Reservation/Reject/']");
                if (!a) return;
                e.preventDefault();
                rejectId = a.getAttribute("href").split("/").pop();
                form.reset();
                DE.forms.clearErrors(form);
                modal.show();
                setTimeout(() => form.reason.focus(), 300);
            });

            DE.forms.bind(form, async (d) => {
                await DE.api.post(`/api/v1/admin/reservations/${rejectId}/reject`, { reason: d.reason.trim() });
                DE.flash("Rezervasiya rədd edildi.", "success");
                location.reload();
            });
        }
    });
})();
