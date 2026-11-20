/* =====================================================
   DoctorEngine — FRONT AYARLARI
   Front sırf statik fayllardır (HTML + CSS + JS). Bütün məlumat API-dən fetch ilə gəlir.
   Serverə qoyanda yalnız API-nin ünvanını yoxlayın.
   ===================================================== */
window.DE_CONFIG = {
    // API ünvanı. Lokal: http://localhost:5020 (Server/Api/DoctorEngine.Api)
    apiUrl: (location.hostname === "localhost" || location.hostname === "127.0.0.1")
        ? "http://localhost:5020"
        : "https://api.doctorengine.az",

    // Video / səsli zənglər üçün STUN/TURN serverləri (mobil internetdə TURN tövsiyə olunur)
    iceServers: [
        { urls: ["stun:stun.l.google.com:19302", "stun:stun1.l.google.com:19302"] }
        // { urls: ["turn:turn.doctorengine.az:3478"], username: "...", credential: "..." }
    ]
};
