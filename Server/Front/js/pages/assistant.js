/* AI köməkçi: söhbət + yan panel. Suallar js/assistant.js ilə /api/ai/chat-a gedir (api.js API-yə yönləndirir). */
DE.page({
    title: "AI Köməkçi",
    nav: "Assistant",
    scripts: ["/js/assistant.js"],
    async render() {
        let st = null;
        try { st = await DE.api.get("/api/ai/status"); } catch (e) { /* API əlçatan deyil — "tezliklə" */ }
        const configured = !!(st && st.configured);
        const remaining = st ? st.remaining : 0, limit = st ? st.limit : 0, maxLength = st ? st.maxMessageLength : 1000;
        const ui = DE.ui, esc = DE.esc, t = (s, ...a) => esc(DE.t(s, ...a));
        const chatLink = `<a href="/Contact#chat">${t("usta ilə canlı çatda")}</a>`;
        const html = (s, ...parts) => esc(DE.t(s)).replace(/\{(\d+)\}/g, (m, i) => parts[+i]);
        const suggestions = [
            ["fa-solid fa-gauge-high", "Mühərrik soyuqda çətin işə düşür, səbəbi nə ola bilər?"],
            ["fa-solid fa-oil-can", "Yağ təzyiqi lampası yanır, sürmək olarmı?"],
            ["fa-regular fa-clock", "İş saatlarınız və ünvanınız nədir?"],
            ["fa-regular fa-calendar-check", "Necə rezervasiya edə bilərəm?"],
            ["fa-solid fa-screwdriver-wrench", "Hansı xidmətləri göstərirsiniz?"],
            ["fa-solid fa-smog", "İşlənmiş qazdan qara tüstü çıxır, nədir?"]
        ];
        const disabled = !configured || remaining <= 0 ? "disabled" : "";

        document.getElementById("de-main").innerHTML = `
            ${ui.pageHeader("AI Köməkçi", "Mühərrik problemləri, xidmətlər, iş saatları və rezervasiya barədə suallarınıza dərhal cavab — günün istənilən vaxtı.")}
            <div class="container-xxl py-5"><div class="container"><div class="row g-4">
                <div class="col-lg-8">
                    <section class="ai" id="ai" aria-label="${t("AI köməkçi ilə söhbət")}">
                        <header class="ai__head">
                            <span class="ai__avatar" aria-hidden="true"><i class="fa-solid fa-wand-magic-sparkles"></i></span>
                            <div class="ai__title">
                                <h2>${t("DoctorEngine Köməkçi")}</h2>
                                <span class="ai__status ${configured ? "is-on" : "is-off"}"><span class="ai__dot"></span>
                                    ${configured ? t("Süni intellekt · adətən bir neçə saniyəyə cavab verir") : t("Tezliklə aktiv olacaq")}</span>
                            </div>
                            <button type="button" class="ai__icon-btn" data-ai-reset title="${t("Yeni söhbət")}" aria-label="${t("Yeni söhbət")}" hidden><i class="fa-solid fa-rotate-right"></i></button>
                        </header>
                        <div class="ai__body" role="log" aria-live="polite">
                            <div class="ai__welcome">
                                <div class="ai__welcome-icon" aria-hidden="true"><i class="fa-solid fa-robot"></i></div>
                                <h3>${t("Salam! Mən DoctorEngine-in AI köməkçisiyəm 👋")}</h3>
                                <p>${t("Mühərrikinizdə nə problem var? Əlaməti yazın — mümkün səbəbləri izah edim, xidmətlərimiz və rezervasiya barədə məlumat verim.")}</p>
                                <div class="ai__suggestions">
                                    ${suggestions.map(([icon, text]) => `<button type="button" class="ai__chip" data-ai-suggest="${t(text)}" ${disabled}><i class="${icon}" aria-hidden="true"></i><span>${t(text)}</span></button>`).join("")}
                                </div>
                            </div>
                            <div class="ai__list"></div>
                        </div>
                        ${configured ? `
                        <div class="ai__limit" data-ai-limit-box ${remaining > 0 ? "hidden" : ""}>
                            <i class="fa-regular fa-hourglass-half"></i>
                            <span>${html("Bugünkü {0} pulsuz sual limitiniz doldu — sabah yenidən yaza bilərsiniz. Təcili sualınız varsa, {1} yazın.", `<b>${limit}</b>`, chatLink)}</span>
                        </div>
                        <form class="ai__composer" novalidate ${remaining <= 0 ? "hidden" : ""}>
                            <div class="ai__input-wrap">
                                <textarea class="ai__input" rows="1" maxlength="${maxLength}" placeholder="${t("Sualınızı yazın… (məs: mühərrikdən tıqqıltı səsi gəlir)")}" aria-label="${t("Sual")}"></textarea>
                                <button type="submit" class="ai__send" title="${t("Göndər")}" aria-label="${t("Göndər")}" disabled><i class="fa-solid fa-arrow-up"></i></button>
                                <button type="button" class="ai__stop" title="${t("Dayandır")}" aria-label="${t("Dayandır")}" hidden><i class="fa-solid fa-stop"></i></button>
                            </div>
                            <div class="ai__hint">
                                <span class="ai__quota" title="${t("Gündəlik pulsuz limit. Bakı vaxtı ilə gecə yarısı yenilənir.")}">
                                    <i class="fa-solid fa-bolt"></i> ${html("Bu gün {0} / {1} sual qalıb", `<b data-ai-remaining>${remaining}</b>`, String(limit))}
                                </span>
                                <span class="ai__counter" aria-hidden="true"></span>
                                <span class="ai__privacy"><i class="fa-solid fa-shield-halved"></i> ${t("Şəxsi məlumat yazmayın")}</span>
                            </div>
                        </form>` : `
                        <div class="ai__offline"><i class="fa-solid fa-circle-info"></i><span>${html("AI köməkçi tezliklə aktiv olacaq. Hələlik {0} yazın.", chatLink)}</span></div>`}
                    </section>
                </div>
                <div class="col-lg-4">
                    <aside class="ai-side">
                        <div class="ai-card ai-card--accent">
                            <h3><i class="fa-solid fa-user-gear"></i> ${t("Usta ilə danışın")}</h3>
                            <p>${t("Dəqiq diaqnoz və qiymət üçün ustalarımızla birbaşa əlaqə saxlayın.")}</p>
                            <a href="/Contact#chat" class="btn btn-light w-100 mb-2"><i class="fa-regular fa-comments me-2"></i> ${t("Canlı çat")}</a>
                            <a href="/Reservation" class="btn btn-primary w-100"><i class="fa-regular fa-calendar-check me-2"></i> ${t("Rezervasiya et")}</a>
                        </div>
                        <div class="ai-card">
                            <h3><i class="fa-solid fa-route"></i> ${t("Necə işləyir?")}</h3>
                            <ol class="ai-steps">
                                <li><b>${t("Problemi təsvir edin")}</b><span>${t("Əlaməti, maşının modelini və ilini yazın.")}</span></li>
                                <li><b>${t("AI izah edir")}</b><span>${t("Mümkün səbəblər və nə etməli olduğunuz.")}</span></li>
                                <li><b>${t("Ustaya gəlin")}</b><span>${t("Rezervasiya edin — dəqiq diaqnozu ustalar qoyur.")}</span></li>
                            </ol>
                        </div>
                        <div class="ai-card ai-card--note">
                            <h3><i class="fa-solid fa-circle-exclamation"></i> ${t("Bilməli olduğunuz")}</h3>
                            <ul>
                                <li>${t("AI səhv edə bilər — cavablar məlumat xarakterlidir, diaqnoz deyil.")}</li>
                                <li>${t("Qiymət diaqnostikadan sonra müəyyən olunur.")}</li>
                                <li>${html("Hər istifadəçi gündə {0} sual verə bilər — qeydiyyat lazım deyil.", `<b>${limit}</b>`)}</li>
                                <li>${t("Şəxsi məlumat (FİN, kart, parol) yazmayın.")}</li>
                                <li>${t("Söhbət yalnız bu brauzer pəncərəsində saxlanılır.")}</li>
                            </ul>
                        </div>
                    </aside>
                </div>
            </div></div></div>`;

        const cfg = document.createElement("script");
        cfg.id = "aiConfig";
        cfg.type = "application/json";
        cfg.textContent = JSON.stringify({ token: "", maxLength, configured, remaining, limit });
        document.body.appendChild(cfg);
    }
});
