/* Admin: istifadəçilər və rollar (API: admin/users, admin/roles) */
(function () {
    "use strict";
    const esc = DE.esc, A = DE.admin;
    let users = [], roles = [];

    A.page({
        title: "İstifadəçilər",
        active: "User",
        async render() {
            [users, roles] = await Promise.all([DE.api.get("/api/v1/admin/users?take=10000"), DE.api.get("/api/v1/admin/roles")]);
            const me = (DE.auth.user() || {}).id;
            return `
${A.pageHead({ title: "İstifadəçilər", subtitle: "Qeydiyyatdan keçmiş istifadəçilər və onların rolları. Admin rolu olan istifadəçi idarəetmə panelinə daxil ola bilər.",
                actionsHtml: `<button type="button" class="btn btn-primary" data-add-role><i class="fa-solid fa-user-shield"></i> Rol ver</button>` })}
<div class="adm-card">
    <div class="adm-card__head flex-wrap"><div class="adm-search"><i class="fa-solid fa-magnifying-glass"></i><input type="search" class="form-control form-control-sm" data-table-search="#userTable" placeholder="Ad, istifadəçi adı, email…" /></div></div>
    <div class="adm-table-wrap">
        <table class="adm-table" id="userTable">
            <thead><tr><th>İstifadəçi</th><th>Email</th><th>Rollar</th><th>Vəziyyət</th><th class="adm-actions-col"></th></tr></thead>
            <tbody>
                ${users.map((u) => `
                <tr>
                    <td><div class="adm-cell-title">${esc(u.fullName)}</div><div class="adm-cell-sub">@${esc(u.userName)}</div></td>
                    <td>${esc(u.email)}</td>
                    <td>${(u.roles || []).map((r) => `
                        <form action="/Admin/User/RemoveRole" method="post" class="d-inline-flex align-items-center me-1">
                            <input type="hidden" name="userId" value="${esc(u.id)}" /><input type="hidden" name="role" value="${esc(r)}" />
                            <span class="adm-badge ${r === "Admin" ? "is-admin" : "is-neutral"}">${esc(r)}
                                ${r === "Admin" && u.id === me ? "" : `<button type="submit" class="btn btn-link btn-sm p-0 ms-1 text-reset" title="Rolu sil" aria-label="Rolu sil" data-confirm="${esc(u.userName)} — “${esc(r)}” rolu silinsin?" data-confirm-ok="Bəli, sil"><i class="fa-solid fa-xmark"></i></button>`}
                            </span>
                        </form>`).join("") || "—"}</td>
                    <td>${u.isLockedOut ? `<span class="adm-badge is-rejected"><i class="fa-solid fa-lock"></i> Bloklanıb</span>`
                        : u.emailConfirmed ? `<span class="adm-badge is-confirmed"><i class="fa-solid fa-check"></i> Təsdiqlənib</span>`
                            : `<span class="adm-badge is-pending"><i class="fa-regular fa-envelope"></i> Email təsdiqlənməyib</span>`}</td>
                    <td class="adm-actions-col"><div class="adm-actions">
                        <button type="button" class="adm-act" title="Rol ver" aria-label="Rol ver" data-add-role="${esc(u.id)}"><i class="fa-solid fa-user-plus"></i></button>
                    </div></td>
                </tr>`).join("")}
                <tr data-search-empty hidden><td colspan="5"><div class="adm-empty"><i class="fa-solid fa-magnifying-glass"></i>Nəticə tapılmadı</div></td></tr>
            </tbody>
        </table>
    </div>
</div>

<div class="modal fade" id="roleModal" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered">
        <form class="modal-content" data-js novalidate id="roleForm">
            <div class="modal-header"><h5 class="modal-title">Rol ver</h5><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Bağla"></button></div>
            <div class="modal-body">
                <div class="alert alert-danger py-2" role="alert" data-error hidden></div>
                <div class="mb-3">
                    <label class="form-label" for="UsernameId">İstifadəçi</label>
                    <select id="UsernameId" name="UsernameId" class="form-select" required data-val-required="İstifadəçini seçin">
                        <option value="">—</option>${users.map((u) => `<option value="${esc(u.id)}">${esc(u.userName)}</option>`).join("")}
                    </select>
                    <span class="text-danger small" data-valmsg-for="UsernameId"></span>
                </div>
                <div class="mb-1">
                    <label class="form-label" for="RoleId">Rol</label>
                    <select id="RoleId" name="RoleId" class="form-select" required data-val-required="Rolu seçin">
                        <option value="">—</option>${roles.map((r) => `<option value="${esc(r)}">${esc(r)}</option>`).join("")}
                    </select>
                    <span class="text-danger small" data-valmsg-for="RoleId"></span>
                </div>
            </div>
            <div class="modal-footer"><button type="button" class="btn btn-light" data-bs-dismiss="modal">Ləğv et</button><button type="submit" class="btn btn-primary"><i class="fa-solid fa-check"></i> Rol ver</button></div>
        </form>
    </div>
</div>`;
        },
        after() {
            const modal = bootstrap.Modal.getOrCreateInstance(document.getElementById("roleModal"));
            const form = document.getElementById("roleForm");
            document.addEventListener("click", (e) => {
                const b = e.target.closest("[data-add-role]");
                if (!b) return;
                form.reset();
                DE.forms.clearErrors(form);
                if (b.dataset.addRole) form.UsernameId.value = b.dataset.addRole;
                modal.show();
            });
            DE.forms.bind(form, async (d) => {
                await DE.api.post(`/api/v1/admin/users/${encodeURIComponent(d.UsernameId)}/roles`, { role: d.RoleId });
                const u = users.find((x) => x.id === d.UsernameId);
                DE.flash(`${u ? u.userName : ""} istifadəçisinə "${d.RoleId}" rolu verildi.`, "success");
                location.reload();
            });
        }
    });
})();
