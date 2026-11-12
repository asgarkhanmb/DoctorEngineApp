/* =====================================================
   Brauzerin "əvvəl yazdıqlarım" siyahısını söndürür.
   Bütün formalara və sahələrə autocomplete="off" verilir;
   sonradan JS ilə əlavə olunan sahələr də (çat, zəmanət detalları) əhatə olunur.
   Şifrə sahələrinə toxunulmur — brauzerin şifrə meneceri işləməyə davam edir.
   ===================================================== */
(function () {
    "use strict";

    var SELECTOR = "input:not([type=password]):not([type=hidden]):not([type=checkbox]):not([type=radio])" +
                   ":not([type=submit]):not([type=button]):not([type=file]), textarea";

    function apply(root) {
        if (!root || !root.querySelectorAll) return;

        if (root.matches && root.matches("form")) root.setAttribute("autocomplete", "off");
        root.querySelectorAll("form").forEach(function (f) { f.setAttribute("autocomplete", "off"); });

        var fields = [];
        if (root.matches && root.matches(SELECTOR)) fields.push(root);
        root.querySelectorAll(SELECTOR).forEach(function (el) { fields.push(el); });

        fields.forEach(function (el) {
            if (el.getAttribute("autocomplete") !== "off") el.setAttribute("autocomplete", "off");
        });
    }

    function init() {
        apply(document);
        new MutationObserver(function (records) {
            records.forEach(function (r) {
                r.addedNodes.forEach(function (n) { if (n.nodeType === 1) apply(n); });
            });
        }).observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
