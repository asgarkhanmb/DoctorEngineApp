/* Canlı çat — tam səhifə (/Chat, /Chat?c=yazışma). Daxil olmayıbsa vidjet "daxil olun" göstərir. */
DE.page({
    title: "Canlı Çat",
    nav: "Contact",
    inlineChat: true,
    async render() {
        document.getElementById("de-main").innerHTML = `
            ${DE.ui.pageHeader("Canlı Çat", "Usta ilə birbaşa yazışın — şəkil və video da göndərə bilərsiniz.")}
            <div class="container-xxl pb-4"><div class="container">${DE.chatWidget.html({ fullPage: true })}</div></div>`;
    },
    after() { DE.chatWidget.start({ fullPage: true }); }
});
