/* Admin: əlaqə mesajları — siyahı (/Admin/ContactMessage) və detal (?id=5) (API: admin/messages) */
(function () {
    "use strict";
    const esc = DE.esc, A = DE.admin;
    const id = DE.qs("id");

    A.page({
        title: "Əlaqə mesajları",
        active: "ContactMessage",
        async render() {
            if (id) {
                const m = await DE.api.get(`/api/v1/admin/messages/${encodeURIComponent(id)}`);
                return `
${A.pageHead({ title: m.subject || "Mesaj", back: { href: "/Admin/ContactMessage", text: "Əlaqə mesajları" } })}
<div class="row g-4"><div class="col-xl-8">
    <div class="adm-card"><div class="adm-card__body">
        <dl class="row mb-0">
            <dt class="col-sm-3">Göndərən</dt><dd class="col-sm-9">${esc(m.fullName)}</dd>
            <dt class="col-sm-3">Email</dt><dd class="col-sm-9"><a href="mailto:${esc(m.email)}?subject=${encodeURIComponent("Re: " + (m.subject || ""))}">${esc(m.email)}</a></dd>
            <dt class="col-sm-3">Tarix</dt><dd class="col-sm-9">${A.fmt.date(m.createdDate)} ${A.fmt.time(m.createdDate)}</dd>
            <dt class="col-sm-3">Mövzu</dt><dd class="col-sm-9">${esc(m.subject)}</dd>
        </dl>
        <hr />
        <div style="white-space:pre-wrap">${esc(m.message)}</div>
    </div>
    <div class="adm-form-actions">
        <a href="mailto:${esc(m.email)}?subject=${encodeURIComponent("Re: " + (m.subject || ""))}" class="btn btn-primary"><i class="fa-regular fa-paper-plane"></i> Cavab yaz</a>
        <form action="/Admin/ContactMessage/Delete/${m.id}" method="post" class="d-inline"><button type="submit" class="btn btn-light text-danger" data-confirm="Mesaj silinsin?"><i class="fa-regular fa-trash-can"></i> Sil</button></form>
    </div></div>
</div></div>`;
            }

            const list = await DE.api.get("/api/v1/admin/messages");
            return `
${A.pageHead({ title: "Əlaqə mesajları", subtitle: "Saytdakı əlaqə formasından gələn mesajlar." })}
<div class="adm-card">
    <div class="adm-card__head flex-wrap"><div class="adm-search"><i class="fa-solid fa-magnifying-glass"></i><input type="search" class="form-control form-control-sm" data-table-search="#msgTable" placeholder="Ad, email, mövzu…" /></div></div>
    ${!list.length ? `<div class="adm-empty"><i class="fa-regular fa-envelope-open"></i><strong>Mesaj yoxdur</strong></div>` : `
    <div class="adm-table-wrap">
        <table class="adm-table" id="msgTable">
            <thead><tr><th>Göndərən</th><th>Mövzu</th><th>Tarix</th><th class="adm-actions-col"></th></tr></thead>
            <tbody>
                ${list.map((m) => `
                <tr>
                    <td><div class="adm-cell-title">${esc(m.fullName)}</div><div class="adm-cell-sub">${esc(m.email)}</div></td>
                    <td><a href="/Admin/ContactMessage?id=${m.id}" class="adm-cell-title text-reset">${esc(m.subject)}</a><div class="adm-cell-sub adm-clamp">${esc(m.message)}</div></td>
                    <td class="text-nowrap">${A.fmt.date(m.createdDate)}<div class="adm-cell-sub">${A.fmt.time(m.createdDate)}</div></td>
                    <td class="adm-actions-col"><div class="adm-actions">
                        <a href="/Admin/ContactMessage?id=${m.id}" class="adm-act" title="Bax" aria-label="Bax"><i class="fa-solid fa-eye"></i></a>
                        ${A.deleteButton(`/Admin/ContactMessage/Delete/${m.id}`, "Mesaj silinsin?")}
                    </div></td>
                </tr>`).join("")}
                <tr data-search-empty hidden><td colspan="4"><div class="adm-empty"><i class="fa-solid fa-magnifying-glass"></i>Nəticə tapılmadı</div></td></tr>
            </tbody>
        </table>
    </div>`}
</div>`;
        },
        after() {
            // Detaldan silindikdə siyahıya qayıt
            if (id) document.addEventListener("submit", () => sessionStorage.setItem("de_after_delete", "/Admin/ContactMessage"), true);
        }
    });
})();
