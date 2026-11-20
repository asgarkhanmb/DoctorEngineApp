/* =====================================================
   Admin: məzmun bölməsi — siyahı + forma, hamısı API-nin sxemindən (api/v1/admin/sections/{key})
   <body data-section="sliders">   /Admin/Slider            — siyahı
                                   /Admin/Slider?new=1      — yeni qeyd
                                   /Admin/Slider?id=5       — redaktə (zəmanət — baxış)
   Tək qeydli bölmələr (əlaqə, xəritə, WhatsApp, sayt ayarları) birbaşa forma açır.
   ===================================================== */
(function () {
    "use strict";
    const esc = DE.esc, A = DE.admin;
    const key = document.body.dataset.section;
    const id = DE.qs("id");
    const isNew = DE.qs("new") === "1";
    const base = location.pathname;

    let meta = null, form = null;

    const ICON = { sliders: "fa-regular fa-images", informs: "fa-regular fa-star", about: "fa-regular fa-address-card", categories: "fa-solid fa-layer-group",
        videos: "fa-solid fa-film", services: "fa-solid fa-screwdriver-wrench", technicians: "fa-solid fa-user-gear", warranties: "fa-solid fa-shield-halved",
        phones: "fa-solid fa-phone" };

    A.page({
        title: "…",
        active: key,
        async render() {
            const sections = await DE.api.get("/api/v1/admin/sections");
            meta = sections.find((s) => s.key === key);
            if (!meta) throw new DE.ApiError(404, "Bölmə tapılmadı.");
            this.title = meta.title;

            if (meta.kind === "single" || isNew || id) {
                form = await DE.api.get(`/api/v1/admin/sections/${key}/form` + (id ? `?id=${encodeURIComponent(id)}` : ""));
                return renderForm();
            }
            const list = await DE.api.get(`/api/v1/admin/sections/${key}`);
            return renderList(list);
        },
        after() { if (form) wireForm(); }
    });

    // ---------------- SİYAHI ----------------
    function renderList(list) {
        const items = list.items || [];
        const blocked = list.createBlockedReason;
        const head = A.pageHead({
            title: meta.title,
            subtitle: meta.subtitle,
            primary: meta.canCreate && !blocked ? { href: base + "?new=1", text: key === "warranties" ? "Yeni zəmanət" : "Yeni əlavə et" } : null
        });
        const warn = blocked ? `<div class="alert alert-warning d-flex align-items-center gap-2"><i class="fa-solid fa-circle-info"></i> ${esc(blocked)}</div>` : "";
        if (!items.length)
            return head + warn + `<div class="adm-card"><div class="adm-empty"><i class="${ICON[key] || "fa-regular fa-folder-open"}"></i><strong>Hələ qeyd yoxdur</strong></div></div>`;

        const hasImage = items.some((i) => i.imageUrl);
        return head + warn + `
<div class="adm-card">
    <div class="adm-card__head flex-wrap">
        <div class="adm-search">
            <i class="fa-solid fa-magnifying-glass"></i>
            <input type="search" class="form-control form-control-sm" data-table-search="#secTable" placeholder="Axtar…" />
        </div>
    </div>
    <div class="adm-table-wrap">
        <table class="adm-table" id="secTable">
            <thead><tr>
                <th style="width:60px">#</th>
                ${hasImage ? `<th style="width:170px">Şəkil</th>` : ""}
                <th>Ad</th>
                <th></th>
                <th class="adm-actions-col"></th>
            </tr></thead>
            <tbody>
                ${items.map((it, i) => `
                <tr>
                    <td>${i + 1}</td>
                    ${hasImage ? `<td>${it.imageUrl ? (/\.(mp4|webm|mov|m4v)$/i.test(it.imageUrl)
                        ? `<video src="${esc(DE.upload(it.imageUrl))}#t=0.5" muted preload="metadata" style="width:150px;height:84px;object-fit:cover;border-radius:6px"></video>`
                        : `<img src="${esc(DE.upload(it.imageUrl))}" alt="" style="width:150px;height:70px;object-fit:cover;border-radius:6px" />`) : ""}</td>` : ""}
                    <td>
                        <div class="adm-cell-title">${esc(it.title)}</div>
                        ${it.subtitle ? `<div class="adm-cell-sub">${esc(it.subtitle)}</div>` : ""}
                    </td>
                    <td>${it.badge ? `<span class="adm-badge is-neutral">${esc(it.badge)}</span>` : ""}</td>
                    <td class="adm-actions-col">
                        <div class="adm-actions">
                            ${key === "warranties" ? `<button type="button" class="adm-act" title="PDF" aria-label="PDF" data-pdf="${it.id}"><i class="fa-regular fa-file-pdf"></i></button>` : ""}
                            <a href="${base}?id=${it.id}" class="adm-act" title="${meta.canEdit ? "Redaktə et" : "Bax"}" aria-label="${meta.canEdit ? "Redaktə et" : "Bax"}"><i class="fa-solid ${meta.canEdit ? "fa-pen" : "fa-eye"}"></i></a>
                            ${meta.canDelete ? A.deleteButton(`/Admin/Section/${key}/Delete/${it.id}`, `“${it.title}” silinsin?`) : ""}
                        </div>
                    </td>
                </tr>`).join("")}
                <tr data-search-empty hidden><td colspan="${hasImage ? 5 : 4}"><div class="adm-empty"><i class="fa-solid fa-magnifying-glass"></i>Nəticə tapılmadı</div></td></tr>
            </tbody>
        </table>
    </div>
</div>`;
    }

    // Zəmanət PDF-i (API hazırlayır) — yeni tabda açılır
    document.addEventListener("click", async (e) => {
        const btn = e.target.closest("[data-pdf]");
        if (!btn) return;
        btn.disabled = true;
        try {
            const file = await DE.api.file(`/api/v1/warranties/${btn.dataset.pdf}/pdf`);
            const url = URL.createObjectURL(file.blob);
            const a = document.createElement("a");
            a.href = url; a.download = file.name || `zemanet_${btn.dataset.pdf}.pdf`;
            document.body.appendChild(a); a.click(); a.remove();
            setTimeout(() => URL.revokeObjectURL(url), 60000);
        } catch (err) { A.toast(err.message, "error"); }
        finally { btn.disabled = false; }
    });

    // ---------------- FORMA ----------------
    function fieldHtml(f, values, files, readOnly) {
        const v = values[f.name] == null ? "" : values[f.name];
        const req = f.required ? " *" : "";
        const dis = readOnly ? "disabled" : "";
        const label = `<label class="form-label" for="f_${f.name}">${esc(f.label)}${req}${f.hint ? ` <small class="text-muted">(${esc(f.hint)})</small>` : ""}</label>`;
        const err = `<span class="text-danger small" data-valmsg-for="${f.name}"></span>`;
        const max = f.maxLength ? `maxlength="${f.maxLength}"` : "";
        const required = f.required && !readOnly ? "required" : "";
        const type = { email: "email", url: "url", phone: "tel", number: "number" }[f.type] || "text";

        switch (f.type) {
            case "multiline":
                return `<div class="mb-3">${label}<textarea id="f_${f.name}" name="${f.name}" class="form-control" rows="4" ${max} ${required} ${dis}>${esc(v)}</textarea>${err}</div>`;
            case "bool":
                return `<div class="mb-3 form-check"><input type="checkbox" class="form-check-input" id="f_${f.name}" name="${f.name}" value="true" ${v === "true" || v === "True" ? "checked" : ""} ${dis} />
                    <label class="form-check-label" for="f_${f.name}">${esc(f.label)}</label>${f.hint ? `<div class="form-text">${esc(f.hint)}</div>` : ""}</div>`;
            case "select":
                return `<div class="mb-3">${label}<select id="f_${f.name}" name="${f.name}" class="form-select" ${required} ${dis}>
                    ${f.required ? "" : `<option value="">—</option>`}
                    ${(f.options || []).map((o) => `<option value="${esc(o.value)}" ${String(o.value) === String(v) ? "selected" : ""}>${esc(o.label)}</option>`).join("")}
                    </select>${err}</div>`;
            case "image":
            case "video": {
                const current = files[f.name];
                const isVideo = f.type === "video";
                const preview = current ? (isVideo
                    ? `<div class="mb-2"><video src="${esc(DE.upload(current))}#t=0.5" controls preload="metadata" style="max-width:100%;max-height:240px;border-radius:8px"></video><small class="text-muted d-block">Mövcud video — yenisini seçsəniz əvəz olunacaq</small></div>`
                    : `<div class="mb-2"><img src="${esc(DE.upload(current))}" alt="" style="max-width:220px;max-height:140px;object-fit:cover;border-radius:8px;border:1px solid #ddd" /> <small class="text-muted d-block">Mövcud şəkil — yenisini seçsəniz əvəz olunacaq</small></div>`) : "";
                const accept = isVideo ? "video/mp4,video/webm,video/quicktime,.mov,.m4v" : "image/jpeg,image/png,image/webp,image/svg+xml";
                if (readOnly) return `<div class="mb-3">${label}${preview || "<div>—</div>"}</div>`;
                return `<div class="mb-3">${label}${preview}<input type="file" id="f_${f.name}" name="${f.name}" class="form-control" accept="${accept}" ${f.required && !current ? "required" : ""} data-preview="#p_${f.name}" />
                    <div id="p_${f.name}" class="mt-2"></div>${err}</div>`;
            }
            case "items": {
                let rows = [];
                try { rows = JSON.parse(v || "[]"); } catch (e) { rows = []; }
                if (!rows.length) rows = [{ partName: "", warrantyMonth: 6 }];
                return `<div class="mb-3">${label}
                    <div data-items="${f.name}">${rows.map((r, i) => itemRow(f.name, i, r, readOnly)).join("")}</div>
                    ${readOnly ? "" : `<button type="button" class="btn btn-light btn-sm mt-1" data-item-add="${f.name}"><i class="fa-solid fa-plus"></i> Detal əlavə et</button>`}
                    ${err}</div>`;
            }
            default:
                return `<div class="mb-3">${label}<input type="${type}" id="f_${f.name}" name="${f.name}" class="form-control" value="${esc(v)}" ${max} ${required} ${dis} ${type === "number" ? `step="1"` : ""} />${err}</div>`;
        }
    }

    function itemRow(name, i, r, readOnly) {
        const dis = readOnly ? "disabled" : "";
        return `<div class="row g-2 mb-2 align-items-center" data-item-row>
            <div class="col"><input class="form-control" placeholder="Detal (məs. Turbo)" data-item-field="PartName" value="${esc(r.partName || "")}" maxlength="100" ${readOnly ? "" : "required"} ${dis} /></div>
            <div class="col-4 col-md-3"><div class="input-group"><input type="number" class="form-control" min="1" max="120" data-item-field="WarrantyMonth" value="${esc(r.warrantyMonth || 6)}" ${readOnly ? "" : "required"} ${dis} /><span class="input-group-text">ay</span></div></div>
            ${readOnly ? "" : `<div class="col-auto"><button type="button" class="adm-act is-danger" data-item-remove title="Sil" aria-label="Sil"><i class="fa-regular fa-trash-can"></i></button></div>`}
        </div>`;
    }

    function renderForm() {
        const editing = form.id != null && meta.kind === "list";
        const readOnly = !!form.readOnly;
        const fields = (form.fields || []).filter((f) => f.mode === "all" || !f.mode || (f.mode === "create" ? !editing : editing));
        const title = meta.kind === "single" ? meta.title : readOnly ? meta.title + " — baxış" : editing ? meta.title + " — redaktə" : meta.title + " — yeni";
        const head = A.pageHead({
            title,
            subtitle: meta.kind === "single" ? meta.subtitle : null,
            back: meta.kind === "list" ? { href: base, text: meta.title } : null,
            actionsHtml: key === "warranties" && form.id != null ? `<button type="button" class="btn btn-primary" data-pdf="${form.id}"><i class="fa-regular fa-file-pdf"></i> PDF (çap)</button>` : ""
        });
        return head + `
<div class="row g-4"><div class="col-xl-8">
    <div class="adm-card">
        <form class="adm-form" data-js novalidate>
            <div class="adm-card__body">
                <div class="alert alert-danger py-2" role="alert" data-error hidden></div>
                ${fields.map((f) => fieldHtml(f, form.values || {}, form.files || {}, readOnly)).join("")}
            </div>
            ${readOnly ? `<div class="adm-form-actions"><a href="${base}" class="btn btn-light">Geri</a></div>` : `
            <div class="adm-form-actions">
                <button type="submit" class="btn btn-primary"><i class="fa-solid fa-check"></i> ${editing || meta.kind === "single" ? "Yadda saxla" : "Yarat"}</button>
                ${meta.kind === "list" ? `<a href="${base}" class="btn btn-light">Ləğv et</a>` : ""}
            </div>`}
        </form>
    </div>
</div></div>`;
    }

    function wireForm() {
        const el = document.querySelector("form.adm-form[data-js]");
        if (!el || form.readOnly) return;

        // Detallar (zəmanət): əlavə et / sil
        el.addEventListener("click", (e) => {
            const add = e.target.closest("[data-item-add]");
            if (add) {
                const box = el.querySelector(`[data-items="${add.dataset.itemAdd}"]`);
                box.insertAdjacentHTML("beforeend", itemRow(add.dataset.itemAdd, box.children.length, { partName: "", warrantyMonth: 6 }, false));
            }
            const rm = e.target.closest("[data-item-remove]");
            if (rm) {
                const box = rm.closest("[data-items]");
                if (box.children.length > 1) rm.closest("[data-item-row]").remove();
            }
        });

        // Seçilən şəkil / video önizləməsi — admin.js (input[type=file][data-preview])

        DE.forms.bind(el, async (fd) => {
            // Detalları ASP.NET formatına çevir: Items[0].PartName, Items[0].WarrantyMonth
            el.querySelectorAll("[data-items]").forEach((box) => {
                const name = box.dataset.items;
                box.querySelectorAll("[data-item-row]").forEach((row, i) =>
                    row.querySelectorAll("[data-item-field]").forEach((inp) => fd.append(`${name}[${i}].${inp.dataset.itemField}`, inp.value)));
            });
            // Boş fayl sahələri göndərilməsin
            for (const [k, v] of Array.from(fd.entries())) if (v instanceof File && !v.size) fd.delete(k);

            const editing = form.id != null && meta.kind === "list";
            await DE.api.form(`/api/v1/admin/sections/${key}` + (editing ? `/${form.id}` : ""), fd);
            DE.api.clearCache();
            DE.flash(meta.kind === "single" || editing ? "Yadda saxlanıldı." : "Əlavə olundu.", "success");
            location.href = meta.kind === "single" ? base : base;
        }, { files: true });
    }
})();
