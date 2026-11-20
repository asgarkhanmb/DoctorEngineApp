# DoctorEngine — Deploy

Repo-da üç server hissəsi var, hər biri ayrıca deploy olunur:

| Hissə | Qovluq | Nə edir | Lokal ünvan |
|---|---|---|---|
| **API** | `Server/Api/` | Bütün biznes məntiqi, baza, email, AI, push, SignalR hub-ları (çat, rezervasiya, zəng), yüklənmiş fayllar (`/uploads`), REST API (JWT) | `http://localhost:5020` |
| **JS front** | `Server/Front/` | Sırf statik fayllar: **HTML + CSS + JavaScript**. Heç bir .NET layihəsi deyil. Bütün məlumatı brauzerdən `fetch` ilə API-dən alır | `http://localhost:5021` |
| **Köhnə MVC sayt** | `Mvc/DoctorEngineApp/` | Tam müstəqil: öz Models/Data/Services kodu, bazaya birbaşa qoşulur (API ilə eyni baza) | `http://localhost:5019` |

API-nin həll faylı: `Server/Api/DoctorEngine.Api.slnx`. Köhnə MVC: `Mvc/DoctorEngineApp.sln`. Mobil: `Mobile/DoctorEngine.Mobile.slnx`. Front build tələb etmir.

Mobil tətbiq yalnız API ilə işləyir. Ortaq DTO layihəsi yoxdur:
- API-nin cavab formaları: `Server/Api/DoctorEngine.Application/Contracts`
- Mobil tətbiqin öz surəti: `Mobile/DoctorEngine.App/Contracts`

> Köhnə MVC və API eyni bazanı işlədirsə: AI tərcümə işçisi yalnız birində işləsin — API-də `"Translations:RunWorker": false`.
> Yüklənmiş fayllar: köhnə MVC öz `wwwroot/uploads`-una, API öz `wwwroot/uploads`-una yazır. Hər ikisi istifadədədirsə,
> eyni qovluğu göstərin (API: `Storage:WebRoot` = köhnə saytın `wwwroot` qovluğu), yoxsa birində yüklənən şəkil o birində görünməz.

## Arxitektura

```
Server/
├── Api/                               DoctorEngine.Api.slnx — API-nin həll faylı
│   ├── DoctorEngine.Domain/           Entitilər (Models), Roles — heç nədən asılı deyil
│   ├── DoctorEngine.Application/      ViewModel/DTO-lar (Contracts), servis interfeysləri, ayarlar, tərcümə (L.T)
│   ├── DoctorEngine.Infrastructure/   EF Core (AppDbContext, Migrations), servislər, email, push, AI,
│   │                                  SignalR hub-ları, DependencyInjection.cs (AddInfrastructure)
│   └── DoctorEngine.Api/              Controllers, Program.cs, Swagger, wwwroot (uploads, Rotativa — zəmanət PDF-i)
│       ├── Controllers/V1/            auth, profile, content, reservations, admin (+sections), languages, translations
│       ├── Controllers/Resources/     REST resursları: sliders, informs, abouts, categories, subservices, services,
│       │                              technicians, warranties (+pdf), contact-info, contact-phones, map, whatsapp,
│       │                              site-settings, contact-messages, reservations/form|lookup|all, admin/overview
│       └── Controllers/               api/chat (çat), api/ai (AI köməkçi)
└── Front/                             ← JS FRONT (statik)
    ├── config.js                      API ünvanı (apiUrl), zəng serverləri (iceServers)
    ├── index.html, About/, Service/, Reservation/, Contact/, Assistant/, Chat/, Call/, Account/..., Admin/...
    │                                  ünvanlar köhnə MVC saytı ilə eynidir: /About → About/index.html
    ├── js/core/api.js                 fetch köməkçiləri, giriş (access + refresh token), "/api/...", "/uploads/...",
    │                                  "/chatHub" kimi nisbi ünvanları avtomatik API-yə yönləndirir (token ilə)
    ├── js/core/i18n.js                az / en / ru / tr (mətnlər API-dən: api/v1/languages/{dil}/ui)
    ├── js/core/layout.js              header, footer, çat düyməsi, səhifənin açılışı
    ├── js/core/partials.js            slayder, xidmət kartları, rezervasiya forması, ustalar ...
    ├── js/core/forms.js, auth-layout.js, chat-widget.js
    ├── js/pages/*.js                  ictimai səhifələr + hesab (giriş, qeydiyyat, profil ...)
    ├── js/admin/*.js                  admin paneli: idarə paneli, rezervasiyalar, mesajlar, istifadəçilər,
    │                                  məzmun bölmələri (formalar API-nin sxemindən qurulur: api/v1/admin/sections)
    ├── js/chat.js, booking.js, assistant.js, call.js ...   köhnə saytın skriptləri (demək olar dəyişməyib)
    ├── css/, assets/, lib/            dizayn (köhnə saytla eyni)
    ├── web.config                     IIS: /About → /About/index.html, 404 səhifəsi
    └── (lokalda API işə düşəndə front avtomatik http://localhost:5021-də açılır)

Mvc/
├── DoctorEngineApp.sln
└── DoctorEngineApp/                   Köhnə MVC sayt (müstəqil, Server/Api-yə istinad yoxdur)

Mobile/
├── DoctorEngine.Mobile.slnx
└── DoctorEngine.App/                  MAUI tətbiqi (Android / iOS / Windows)
```

### JS front API ilə necə danışır

- **Məlumat:** hər səhifə açılanda `fetch` ilə API-dən alır (`api/v1/content/home`, `content/services`, `reservations/options` ...).
  Hamıya açıq məlumat brauzerdə 60 saniyə keşlənir. Bazadakı mətnlər API-dən seçilmiş dildə gəlir (`Accept-Language`).
- **Giriş:** `api/v1/auth/login` → access + refresh token brauzerin `localStorage`-ında. Hər sorğuda `Authorization: Bearer`;
  token bitməyə yaxın avtomatik yenilənir. Şifrə / rol dəyişəndə və ya hesab bloklananda istifadəçi çıxarılır.
- **Çat, AI, zənglər, canlı rezervasiya:** SignalR birbaşa API-yə qoşulur (`/chatHub`, `/callHub`, `/reservationHub`, token ilə).
- **Şəkil və videolar:** `/uploads/...` API-dən gəlir.
- **CORS:** API-də `Cors:Origins` frontun ünvanını icazə verməlidir (aşağıya bax).
- **Email linkləri** (təsdiq, şifrə bərpası, email dəyişikliyi) API-dən göndərilir və frontun səhifələrinə aparır
  (`Site:BaseUrl` + `/Account/ConfirmEmail`, `/Account/ResetPassword`, `/Account/ConfirmEmailChange`).

## 1. Dərc etmək

```powershell
pwsh ./publish-server.ps1              # hamısı
pwsh ./publish-server.ps1 -Only api    # API
pwsh ./publish-server.ps1 -Only front  # JS front (statik fayllar köçürülür)
pwsh ./publish-server.ps1 -Only web    # köhnə MVC
```

Nəticə: `publish/server/api`, `publish/server/front`, `publish/server/web`.

> API-nin `wwwroot/uploads` qovluğu publish-ə **düşmür** — serverdəki şəkillərin/videoların üzərinə yazılmasın.
> İlk dəfə: lokal `Server/Api/DoctorEngine.Api/wwwroot/uploads` qovluğunu serverə əllə köçürün.

## 2. Serverdə yerləşdirmə

```
C:\sites\DoctorEngine.Api\       ← publish/server/api     (api.doctorengine.az — IIS / Kestrel)
C:\sites\DoctorEngine.Front\     ← publish/server/front   (doctorengine.az — IIS statik sayt, nginx, istənilən statik hostinq)
C:\sites\DoctorEngineApp\        ← publish/server/web     (köhnə MVC, lazımdırsa)
C:\sites\uploads-root\uploads\   ← yüklənmiş fayllar (deploy-dan asılı olmasın deyə ayrıca)
```

**Front:**
- `config.js` → `apiUrl` — API-nin ünvanı (standart: `https://api.doctorengine.az`, lokalda `http://localhost:5020`).
- IIS: `web.config` hazırdır — **URL Rewrite** modulu lazımdır (`/About` → `/About/index.html`).
- nginx: `try_files $uri $uri/index.html =404; error_page 404 /Error/404/index.html;`
- HTTPS mütləqdir (kamera / mikrofon — zənglər, səs mesajı; push bildirişləri).

**API:** faylları `Storage:WebRoot` qovluğunun `uploads` alt qovluğuna yazır (standart: API-nin öz `wwwroot`-u).
Ayrıca qovluq istifadə edirsinizsə, `Rotativa\wkhtmltopdf.exe` və `assets\img\el.png` (zəmanət PDF-i) də orada olmalıdır.
IIS: Application Pool istifadəçisinə `uploads` qovluğuna **yazma** icazəsi verin.

## 3. Ayarlar (API)

| Açar | Nümunə | Qeyd |
|---|---|---|
| `ConnectionStrings:Default` | SQL Server | Bazaya yalnız API qoşulur |
| `Jwt:Key` | ≥ 32 simvol | Tokenlər (front da, mobil tətbiq də bununla daxil olur) |
| `Cors:Origins` | `["https://doctorengine.az", "https://www.doctorengine.az"]` | **Frontun ünvanı(ları)** — brauzer API-yə birbaşa müraciət edir. Lokal: `http://localhost:5021` (appsettings.Development.json) |
| `Site:BaseUrl` | `https://doctorengine.az` | Emaildəki linklər frontun səhifələrinə aparır (lokal: `http://localhost:5021`) |
| `EmailSettings:*` | Server, Port, From, UserName, Password | |
| `Ai:ApiKey` | | AI köməkçi və məzmunun avtomatik tərcüməsi |
| `Push:PublicKey`, `Push:PrivateKey`, `Push:Subject` | | Brauzer bildirişləri |
| `Storage:WebRoot` | `wwwroot` və ya `C:\sites\uploads-root` | Yüklənmiş fayllar |
| `DataProtection:KeysPath` | `C:\sites\keys\api` | Email təsdiq / şifrə bərpası tokenləri (server yenidən başlayanda etibarlı qalsın) |
| `Translations:RunWorker` | `false` | AI tərcümə işçisini söndürür (köhnə MVC də eyni bazada işləyirsə, işçi yalnız birində olsun) |
| `ForwardedHeaders:KnownProxies` | `["10.0.0.5"]` | Yalnız API reverse proxy (nginx, köhnə MVC proxy) arxasındadırsa — onun IP-si |
| `OpenApi:Enabled` | `true` | Production-da `/swagger`-i açır (standart: bağlı) |

Mühit dəyişəni kimi: `ConnectionStrings__Default`, `Jwt__Key`, `Cors__Origins__0` ... (`:` əvəzinə `__`).

Frontda heç bir gizli açar **yoxdur** — yalnız `config.js`-dəki API ünvanı.

### Redis (istəyə bağlı)

Bütün SignalR hub-ları API-dədir, bir API nüsxəsi ilə Redis **lazım deyil**.
API-ni bir neçə nüsxədə işlədəcəksinizsə: `ConnectionStrings:Redis` (məs. `localhost:6379`).

### Video / səsli zənglər (WebRTC)

- Zəng siqnalları API-dədir (`/callHub`); zəng səhifəsi frontdadır (`/Call`). Mobil tətbiq hub-a API ilə,
  zəng səhifəsinə (WebView) isə frontun ünvanı ilə qoşulur (`AppConfig.SiteUrl` = `https://doctorengine.az`, lokal: 5021).
- Görüntü / səs serverdən keçmir — iştirakçılar birbaşa bağlanır. Mobil internetdə **TURN server** tövsiyə olunur:
  frontun `config.js` → `iceServers`.
- Qrup zəngi: hər iştirakçı hər biri ilə birbaşa (mesh) — 6 nəfərə qədər rahat işləyir (maksimum 8).

## 4. Baza (migration)

Migration-lar `Server/Api/DoctorEngine.Infrastructure/Migrations`-dadır (başlanğıc layihə — API):

```powershell
dotnet ef database update  -p Server/Api/DoctorEngine.Infrastructure -s Server/Api/DoctorEngine.Api
dotnet ef migrations add X -p Server/Api/DoctorEngine.Infrastructure -s Server/Api/DoctorEngine.Api
```

> **ChatGroups**, **ChatProfileAbout**, **ChatClears**, **ChatDirect** və **PrivateChat** migration-ları serverdə də
> tətbiq olunmalıdır. PrivateChat köhnə ortaq otaqdakı mesajları avtomatik şəxsi yazışmalara bölür.

## 5. Lokal işə salmaq

1. **API:** Visual Studio-da `Server/Api/DoctorEngine.Api.slnx` → `DoctorEngine.Api` profili (port 5020) və ya `dotnet run --project Server/Api/DoctorEngine.Api`.
2. **Front:** ayrıca işə salmaq lazım deyil — API (Development) front-u özü `http://localhost:5021`-də açır və brauzer ora açılır (ayar: `Front:Serve`, `Front:Path`, `Front:Port`). Swagger: `http://localhost:5020/swagger`.
   (VS Code "Live Server" və ya başqa statik server də olar — lakin `/About` kimi ünvanlar `About/index.html`-ə yönəlməlidir.)
3. API işləmirsə, front "Sayt müvəqqəti əlçatan deyil" mesajı göstərir.

Yoxlama:
- `https://api.doctorengine.az/health` → `{"status":"ok"}`
- `https://api.doctorengine.az/api/v1/content/home` → JSON
- `https://doctorengine.az` → ana səhifə

## 6. Mobil tətbiq

`Mobile/DoctorEngine.App/Services/AppConfig.cs`:
- `ProductionUrl = "https://api.doctorengine.az"` — API (məlumat, çat, zəng siqnalları)
- `ProductionSiteUrl = "https://doctorengine.az"` — JS front (zəng səhifəsi `/Call`)
- `DevPort = 5020`, `DevSitePort = 5021` — lokal

Domen fərqlidirsə, burada dəyişib APK-nı yenidən yığın.
