/* =====================================================
   DoctorEngine.Az — Admin panel UX
   ===================================================== */
(function () {
    "use strict";

    // ---------- Mobil menyu ----------
    document.querySelectorAll("[data-adm-toggle]").forEach((btn) =>
        btn.addEventListener("click", () => document.body.classList.toggle("adm-nav-open")));
    document.querySelectorAll("[data-adm-close]").forEach((el) =>
        el.addEventListener("click", () => document.body.classList.remove("adm-nav-open")));
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape") document.body.classList.remove("adm-nav-open");
    });

    // ---------- Bildirişlər: 5 saniyədən sonra bağlansın ----------
    document.querySelectorAll(".adm-toast").forEach((toast) => {
        const close = () => {
            toast.classList.add("is-leaving");
            setTimeout(() => toast.remove(), 300);
        };
        toast.querySelector("[data-toast-close]")?.addEventListener("click", close);
        setTimeout(close, 5000);
    });

    // ---------- Təsdiq pəncərəsi ----------
    // İstifadə: <button type="submit" data-confirm="Bu slayd silinsin?">  (form daxilində)
    const modalEl = document.getElementById("admConfirm");
    if (modalEl && window.bootstrap) {
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        const okBtn = modalEl.querySelector("[data-confirm-ok]");
        let pendingForm = null;

        document.addEventListener("click", (e) => {
            const trigger = e.target.closest("[data-confirm]");
            if (!trigger) return;
            const form = trigger.closest("form");
            if (!form) return;
            e.preventDefault();
            pendingForm = form;
            modalEl.querySelector(".adm-confirm__title").textContent = trigger.dataset.confirm || "Əminsiniz?";
            modalEl.querySelector(".adm-confirm__text").textContent = trigger.dataset.confirmText || "Bu əməliyyat geri qaytarıla bilməz.";
            okBtn.textContent = trigger.dataset.confirmOk || "Bəli, sil";
            modal.show();
        });

        okBtn.addEventListener("click", () => {
            if (!pendingForm) return;
            okBtn.disabled = true;
            pendingForm.submit();
        });

        modalEl.addEventListener("hidden.bs.modal", () => {
            pendingForm = null;
            okBtn.disabled = false;
        });
    }

    // ---------- Fayl önizləməsi ----------
    // <input type="file" data-preview="#elementId">  — şəkil və ya video
    document.querySelectorAll("input[type=file][data-preview]").forEach((input) => {
        const target = document.querySelector(input.dataset.preview);
        if (!target) return;
        input.addEventListener("change", () => {
            const file = input.files[0];
            if (!file) return;
            const url = URL.createObjectURL(file);
            target.replaceChildren();
            if (file.type.startsWith("video/")) {
                target.style.backgroundImage = "";
                const v = document.createElement("video");
                v.src = url;
                v.muted = true;
                v.preload = "metadata";
                target.appendChild(v);
            } else {
                target.style.backgroundImage = "url('" + url + "')";
            }
        });
    });

    // ---------- Cədvəldə axtarış ----------
    // <input data-table-search="#tableId">
    document.querySelectorAll("[data-table-search]").forEach((input) => {
        const table = document.querySelector(input.dataset.tableSearch);
        if (!table) return;
        const rows = Array.from(table.querySelectorAll("tbody tr[data-row]"));
        const empty = table.querySelector("tbody tr[data-search-empty]");
        input.addEventListener("input", () => {
            const q = input.value.trim().toLocaleLowerCase("az");
            let visible = 0;
            rows.forEach((row) => {
                const match = !q || row.textContent.toLocaleLowerCase("az").includes(q);
                row.hidden = !match;
                if (match) visible++;
            });
            if (empty) empty.hidden = visible > 0;
        });
    });

    // ---------- Status filtri (rezervasiyalar) ----------
    document.querySelectorAll("[data-status-filter]").forEach((group) => {
        const table = document.querySelector(group.dataset.statusFilter);
        if (!table) return;
        group.addEventListener("click", (e) => {
            const btn = e.target.closest("[data-status]");
            if (!btn) return;
            group.querySelectorAll("[data-status]").forEach((b) => b.classList.toggle("active", b === btn));
            const status = btn.dataset.status;
            table.querySelectorAll("tbody tr[data-row]").forEach((row) => {
                row.hidden = status !== "all" && row.dataset.status !== status;
            });
        });
    });

    // ---------- Göndərərkən düyməni kilidlə (təkrar göndərmənin qarşısı) ----------
    document.querySelectorAll("form.adm-form").forEach((form) => {
        form.addEventListener("submit", () => {
            if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;
            const btn = form.querySelector("button[type=submit]");
            if (!btn) return;
            setTimeout(() => {
                btn.disabled = true;
                btn.insertAdjacentHTML("afterbegin", '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>');
            }, 0);
        });
    });
})();
