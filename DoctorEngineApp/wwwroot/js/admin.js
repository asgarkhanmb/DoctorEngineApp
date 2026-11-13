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

    // ---------- Cədvəllər: axtarış + status filtri + səhifələmə ----------
    // Bütün .adm-table cədvəllərinə avtomatik tətbiq olunur.
    //   <input data-table-search="#tableId">           — axtarış
    //   <div data-status-filter="#tableId"> [data-status] düymələri — status filtri
    // Axtarış və filtr BİRLİKDƏ işləyir və bütün sətirlər üzrə aparılır (yalnız cari səhifə yox).
    // Səhifə / axtarış / filtr sessiya ərzində yadda qalır — təsdiqlə/sil sonrası eyni səhifəyə qayıdırsınız.
    // admin-live.js yeni sətir əlavə edəndə window.admTables.refresh(table) çağırır.
    const PAGE_SIZES = [10, 25, 50, 100];
    const SIZE_KEY = "adm-page-size";
    const controllers = new Map();

    const readJson = (storage, key) => { try { return JSON.parse(storage.getItem(key) || "null"); } catch (e) { return null; } };
    const writeJson = (storage, key, value) => { try { storage.setItem(key, JSON.stringify(value)); } catch (e) { /* yaddaş bağlıdır */ } };

    function setupTable(table) {
        const tbody = table.tBodies[0];
        if (!tbody || controllers.has(table)) return;

        const id = table.id;
        const search = id ? document.querySelector(`[data-table-search="#${id}"]`) : null;
        const filter = id ? document.querySelector(`[data-status-filter="#${id}"]`) : null;
        const empty = tbody.querySelector("tr[data-search-empty]");
        const headers = Array.from(table.tHead ? table.tHead.rows[0].cells : []).map((th) => th.textContent.trim());
        const storeKey = "adm-table:" + location.pathname.toLowerCase() + "#" + (id || "");

        const saved = readJson(sessionStorage, storeKey) || {};
        const savedSize = Number(readJson(localStorage, SIZE_KEY));
        const state = {
            q: typeof saved.q === "string" ? saved.q : "",
            status: typeof saved.status === "string" ? saved.status : "all",
            page: Number(saved.page) || 1,
            size: PAGE_SIZES.includes(savedSize) ? savedSize : PAGE_SIZES[0]
        };

        // Bərpa olunan status üçün düymə yoxdursa — "hamısı"
        if (filter && !filter.querySelector(`[data-status="${CSS.escape(state.status)}"]`)) state.status = "all";
        if (!filter) state.status = "all";
        if (!search) state.q = "";

        const rowsOf = () => Array.from(tbody.rows).filter((r) =>
            !r.hasAttribute("data-search-empty") && !(r.cells.length === 1 && r.cells[0].colSpan > 1));

        // Mobil kart görünüşü üçün hər xanaya sütun adı (CSS: td[data-label]::before)
        function labelCells(rows) {
            rows.forEach((r) => {
                if (r.dataset.labeled) return;
                Array.from(r.cells).forEach((td, i) => {
                    if (i > 0 && headers[i] && !td.hasAttribute("data-label")) td.setAttribute("data-label", headers[i]);
                });
                r.dataset.labeled = "1";
            });
        }

        // ---- Səhifələyici ----
        const pager = document.createElement("nav");
        pager.className = "adm-pager";
        pager.setAttribute("aria-label", "Səhifələr");
        (table.closest(".adm-table-wrap") || table).after(pager);

        const btn = (label, page, opts = {}) => {
            const b = document.createElement("button");
            b.type = "button";
            b.className = "adm-pager__btn" + (opts.cls ? " " + opts.cls : "");
            b.dataset.page = String(page);
            if (opts.icon) {
                const i = document.createElement("i");
                i.className = opts.icon;
                i.setAttribute("aria-hidden", "true");
                b.appendChild(i);
            } else {
                b.textContent = label;
            }
            if (opts.aria) b.setAttribute("aria-label", opts.aria);
            if (opts.current) { b.classList.add("is-current"); b.setAttribute("aria-current", "page"); }
            if (opts.disabled) b.disabled = true;
            return b;
        };

        // 1 … 4 5 [6] 7 8 … 20 — hər zaman ilk, son və cari ətrafı
        function pageWindow(page, pages) {
            const set = new Set([1, pages, page - 1, page, page + 1]);
            if (page <= 3) [2, 3, 4].forEach((p) => set.add(p));
            if (page >= pages - 2) [pages - 1, pages - 2, pages - 3].forEach((p) => set.add(p));
            const list = [...set].filter((p) => p >= 1 && p <= pages).sort((a, b) => a - b);
            const out = [];
            list.forEach((p, i) => {
                if (i > 0 && p - list[i - 1] > 1) out.push(p - list[i - 1] === 2 ? p - 1 : null);
                out.push(p);
            });
            return out;   // null = "…"
        }

        function renderPager(total, pages, start, end, all) {
            pager.replaceChildren();
            // Az sətir varsa səhifələyici lazım deyil
            pager.hidden = all <= PAGE_SIZES[0];
            if (pager.hidden) return;

            const info = document.createElement("div");
            info.className = "adm-pager__info";
            info.textContent = total
                ? `${start + 1}–${Math.min(end, total)} / ${total}` + (total !== all ? ` (cəmi ${all})` : "")
                : `0 / ${all}`;

            const list = document.createElement("div");
            list.className = "adm-pager__list";
            list.appendChild(btn("", "prev", { icon: "fa-solid fa-chevron-left", aria: "Əvvəlki səhifə", disabled: state.page <= 1 }));
            pageWindow(state.page, pages).forEach((p) => {
                if (p === null) {
                    const gap = document.createElement("span");
                    gap.className = "adm-pager__gap";
                    gap.textContent = "…";
                    gap.setAttribute("aria-hidden", "true");
                    list.appendChild(gap);
                } else {
                    list.appendChild(btn(String(p), p, { cls: "is-num", current: p === state.page, aria: p + "-ci səhifə" }));
                }
            });
            const compact = document.createElement("span");
            compact.className = "adm-pager__compact";
            compact.textContent = `${state.page} / ${pages}`;
            list.appendChild(compact);
            list.appendChild(btn("", "next", { icon: "fa-solid fa-chevron-right", aria: "Növbəti səhifə", disabled: state.page >= pages }));

            const sizeLabel = document.createElement("label");
            sizeLabel.className = "adm-pager__size";
            const sizeText = document.createElement("span");
            sizeText.textContent = "Səhifədə";
            const select = document.createElement("select");
            select.className = "form-select form-select-sm";
            select.setAttribute("aria-label", "Səhifədə neçə sətir göstərilsin");
            PAGE_SIZES.forEach((n) => {
                const o = document.createElement("option");
                o.value = String(n);
                o.textContent = String(n);
                o.selected = n === state.size;
                select.appendChild(o);
            });
            sizeLabel.append(sizeText, select);

            pager.append(info, list, sizeLabel);
        }

        function render() {
            const all = rowsOf();
            labelCells(all);

            const q = state.q.trim().toLocaleLowerCase("az");
            const matched = all.filter((r) =>
                (state.status === "all" || r.dataset.status === state.status) &&
                (!q || r.textContent.toLocaleLowerCase("az").includes(q)));

            const total = matched.length;
            const pages = Math.max(1, Math.ceil(total / state.size));
            state.page = Math.min(Math.max(1, state.page), pages);
            const start = (state.page - 1) * state.size;
            const end = start + state.size;

            const shown = new Set(matched.slice(start, end));
            all.forEach((r) => { r.hidden = !shown.has(r); });
            if (empty) empty.hidden = total > 0 || all.length === 0;

            renderPager(total, pages, start, end, all.length);
            writeJson(sessionStorage, storeKey, { q: state.q, status: state.status, page: state.page });
        }

        function goTo(page) {
            state.page = page;
            render();
            // Cədvəlin başı ekrandan çıxıbsa, ora qayıt
            const card = table.closest(".adm-card") || table;
            if (card.getBoundingClientRect().top < 0) card.scrollIntoView({ behavior: "smooth", block: "start" });
        }

        pager.addEventListener("click", (e) => {
            const b = e.target.closest(".adm-pager__btn");
            if (!b || b.disabled) return;
            const p = b.dataset.page;
            goTo(p === "prev" ? state.page - 1 : p === "next" ? state.page + 1 : Number(p));
        });

        pager.addEventListener("change", (e) => {
            if (!e.target.matches("select")) return;
            const firstRow = (state.page - 1) * state.size;   // eyni sətirlər görünsün
            state.size = Number(e.target.value);
            writeJson(localStorage, SIZE_KEY, state.size);
            state.page = Math.floor(firstRow / state.size) + 1;
            render();
        });

        if (search) {
            search.value = state.q;
            let t = 0;
            search.addEventListener("input", () => {
                clearTimeout(t);
                t = setTimeout(() => { state.q = search.value; state.page = 1; render(); }, 120);
            });
        }

        if (filter) {
            const mark = () => filter.querySelectorAll("[data-status]").forEach((b) => {
                const on = b.dataset.status === state.status;
                b.classList.toggle("active", on);
                b.setAttribute("aria-pressed", String(on));
            });
            filter.addEventListener("click", (e) => {
                const b = e.target.closest("[data-status]");
                if (!b) return;
                state.status = b.dataset.status;
                state.page = 1;
                mark();
                render();
            });
            mark();
        }

        controllers.set(table, { render });
        render();
    }

    document.querySelectorAll("table.adm-table").forEach(setupTable);

    window.admTables = {
        refresh(table) {
            const el = typeof table === "string" ? document.querySelector(table) : table;
            const c = el && controllers.get(el);
            if (c) c.render();
        }
    };

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
