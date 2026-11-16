using System.Globalization;

namespace DoctorEngineApp.Localization
{
    /// <summary>
    /// Saytın çoxdilli mətnləri (az / en / ru / tr).
    /// Seçilmiş dil "de_lang" kukisində saxlanılır; middleware (Program.cs) hər sorğuda
    /// CurrentUICulture-u təyin edir. Yalnız UI dili dəyişir — tarix/rəqəm formatı (CurrentCulture) toxunulmur,
    /// ona görə formalarda onluq ədədlərin model binding-i pozulmur.
    ///
    /// İstifadə — bir funksiya, üç mənbə (bu ardıcıllıqla axtarılır):
    ///   @L.T("nav.home")              — açar: aşağıdakı Strings lüğəti (4 dil əllə)
    ///   @L.T("Rezervasiya et")         — azərbaycanca mətn: Localization/ui.{en,ru,tr}.json (əllə tərcümə)
    ///   @L.T(Model.Name)               — bazadan gələn mətn: AI tərcüməsi (TranslationStore); yoxdursa
    ///                                    azərbaycanca göstərilir və arxa planda tərcüməyə göndərilir
    ///   @L.T("{0} dəqiqə", n)          — {0}, {1} ... yer tutucuları
    /// JS üçün mətnlər: Localization/js.{en,ru,tr}.json → window.DE_I18N, JS-də deT("...").
    /// Admin panel (/Admin) həmişə azərbaycancadır.
    /// </summary>
    public static class L
    {
        /// <summary>AI tərcümələri (Program.cs-də təyin olunur).</summary>
        public static TranslationStore? Store { get; set; }
        public const string CookieName = "de_lang";
        public const string Default = "az";

        /// <summary>Dəstəklənən dillər (kod, öz dilində adı) — seçici bu sıra ilə göstərir.</summary>
        public static readonly (string Code, string Name)[] Languages =
        {
            ("az", "Azərbaycanca"),
            ("en", "English"),
            ("ru", "Русский"),
            ("tr", "Türkçe"),
        };

        public static bool IsSupported(string? code) =>
            code != null && Array.Exists(Languages, l => l.Code == code);

        /// <summary>Cari dil kodu (az/en/ru/tr).</summary>
        public static string Lang
        {
            get
            {
                var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return IsSupported(code) ? code : Default;
            }
        }

        private static int Index => Lang switch { "en" => 1, "ru" => 2, "tr" => 3, _ => 0 };

        /// <summary>
        /// Cari sorğunun dilini təyin edir:
        ///  - /Admin → həmişə az;
        ///  - "de_lang" kukisi (saytdakı seçici);
        ///  - /api (mobil tətbiq) → Accept-Language başlığı;
        ///  - əks halda az.
        /// </summary>
        public static void Apply(HttpContext ctx)
        {
            var path = ctx.Request.Path;
            string? code = null;

            if (!path.StartsWithSegments("/Admin"))
            {
                code = ctx.Request.Cookies[CookieName];
                if (!IsSupported(code) && path.StartsWithSegments("/api"))
                {
                    code = ctx.Request.GetTypedHeaders().AcceptLanguage
                        .OrderByDescending(h => h.Quality ?? 1)
                        .Select(h => h.Value.Value?.Split('-')[0].ToLowerInvariant())
                        .FirstOrDefault(IsSupported);
                }
            }

            if (!IsSupported(code)) code = Default;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(code!);
        }

        /// <summary>Açar, azərbaycanca UI mətni və ya bazadan gələn mətn → cari dildə.</summary>
        public static string T(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

            if (Strings.TryGetValue(text, out var v))
            {
                var s = v[Index];
                return string.IsNullOrEmpty(s) ? v[0] : s;
            }

            var lang = Lang;
            if (lang == Default) return text;

            if (Ui.Value.TryGetValue(lang, out var ui) && ui.TryGetValue(text, out var u)) return u;

            var store = Store;
            if (store != null)
            {
                if (store.TryGet(lang, text, out var t)) return t;
                store.Request(text);   // arxa planda AI ilə tərcümə olunacaq
            }
            return text;
        }

        public static string T(string? text, params object?[] args)
        {
            var format = T(text);
            try { return string.Format(format, args); }
            catch (FormatException) { return format; }
        }

        /// <summary>
        /// İçində link / qalın mətn olan cümlələr: tərcümə HTML-encode olunur, {0}, {1} ... yerinə
        /// verilən HTML parçaları (etibarlı, kodda yaradılmış) qoyulur.
        /// Məs.: @L.Html("Mesaj yazmaq üçün {0} və ya {1}.", loginLink, registerLink)
        /// </summary>
        public static Microsoft.AspNetCore.Html.HtmlString Html(string text, params object?[] htmlArgs)
        {
            var encoded = System.Net.WebUtility.HtmlEncode(T(text));
            try { return new Microsoft.AspNetCore.Html.HtmlString(string.Format(encoded, htmlArgs)); }
            catch (FormatException) { return new Microsoft.AspNetCore.Html.HtmlString(encoded); }
        }

        /// <summary>Tarix/ay adları üçün cari dilin mədəniyyəti (məs.: "15 noyabr" / "15 November").</summary>
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(Lang switch
        {
            "en" => "en-GB",
            "ru" => "ru-RU",
            "tr" => "tr-TR",
            _ => "az-Latn-AZ"
        });

        // ---------- Əllə tərcümə olunmuş JSON lüğətlər (embedded resource) ----------

        // ui.*.json — saytın sabit mətnləri; content.*.json — bazadakı mövcud kontentin əllə tərcüməsi
        // (AI açarı olmadan da işləsin; admin sonradan əlavə etdiyi yeni mətnləri AI tərcümə edir)
        private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> Ui = new(() =>
        {
            var ui = LoadJson("ui");
            foreach (var (lang, dict) in LoadJson("content"))
                foreach (var (k, v) in dict)
                    ui[lang].TryAdd(k, v);
            return ui;
        });
        private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> Js = new(() => LoadJson("js"));

        private static Dictionary<string, Dictionary<string, string>> LoadJson(string prefix)
        {
            var asm = typeof(L).Assembly;
            var result = new Dictionary<string, Dictionary<string, string>>();
            foreach (var (code, _) in Languages)
            {
                if (code == Default) continue;
                var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith($".{prefix}.{code}.json", StringComparison.OrdinalIgnoreCase));
                if (name == null) { result[code] = new(); continue; }
                using var stream = asm.GetManifestResourceStream(name)!;
                result[code] = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
            }
            return result;
        }

        /// <summary>JS lüğətinin versiyası (URL-də) — mətnlər dəyişəndə brauzer keşi yenilənir.</summary>
        public static string JsVersion => JsVersionLazy.Value;

        private static readonly Lazy<string> JsVersionLazy = new(() =>
            TranslationStore.Hash(System.Text.Json.JsonSerializer.Serialize(Js.Value))[..12]);

        /// <summary>JS mətnləri cari dildə (az üçün boş — JS azərbaycanca mətni özü göstərir).</summary>
        public static IReadOnlyDictionary<string, string> JsStrings(string lang) =>
            Js.Value.TryGetValue(lang, out var d) ? d : new Dictionary<string, string>();

        //                                   az                                  en                                   ru                                       tr
        private static readonly Dictionary<string, string[]> Strings = new()
        {
            // ---------- Dil seçici ----------
            ["lang.label"] = new[] { "Dil", "Language", "Язык", "Dil" },

            // ---------- Topbar ----------
            ["topbar.weekdays"] = new[] { "B.e–Cümə", "Mon–Fri", "Пн–Пт", "Pzt–Cum" },
            ["topbar.weekend"] = new[] { "Şənbə–Bazar", "Sat–Sun", "Сб–Вс", "Cmt–Paz" },
            ["topbar.askAi"] = new[] { "AI ilə sual ver", "Ask AI", "Спросить ИИ", "Yapay zekâya sor" },

            // Canlı iş statusu (motion.js) — {0} vaxt və ya dəqiqə
            ["status.openSoon"] = new[] { "İndi açığıq · {0} dəq sonra bağlanırıq", "Open now · closes in {0} min", "Сейчас открыто · закроемся через {0} мин", "Şu an açığız · {0} dk sonra kapanıyoruz" },
            ["status.openUntil"] = new[] { "İndi açığıq · {0}-dək", "Open now · until {0}", "Сейчас открыто · до {0}", "Şu an açığız · kapanış {0}" },
            ["status.opensToday"] = new[] { "Bağlıyıq · bu gün {0}-da açılırıq", "Closed · opens today at {0}", "Закрыто · откроемся сегодня в {0}", "Kapalıyız · açılış bugün {0}" },
            ["status.opensTomorrow"] = new[] { "Bağlıyıq · sabah {0}-da açılırıq", "Closed · opens tomorrow at {0}", "Закрыто · откроемся завтра в {0}", "Kapalıyız · açılış yarın {0}" },
            ["status.opensNext"] = new[] { "Bağlıyıq · yaxın gün {0}-da açılırıq", "Closed · opens next working day at {0}", "Закрыто · откроемся в ближайший рабочий день в {0}", "Kapalıyız · açılış ilk iş günü {0}" },
            ["status.closed"] = new[] { "Bağlıyıq", "Closed", "Закрыто", "Kapalıyız" },

            // ---------- Menyu ----------
            ["nav.main"] = new[] { "Əsas menyu", "Main menu", "Главное меню", "Ana menü" },
            ["nav.toggle"] = new[] { "Menyunu aç", "Open menu", "Открыть меню", "Menüyü aç" },
            ["nav.home"] = new[] { "Ana Səhifə", "Home", "Главная", "Ana Sayfa" },
            ["nav.about"] = new[] { "Haqqımızda", "About us", "О нас", "Hakkımızda" },
            ["nav.services"] = new[] { "Xidmətlər", "Services", "Услуги", "Hizmetler" },
            ["nav.reservation"] = new[] { "Rezervasiya", "Booking", "Запись", "Rezervasyon" },
            ["nav.contact"] = new[] { "Əlaqə", "Contact", "Контакты", "İletişim" },
            ["nav.ai"] = new[] { "AI Köməkçi", "AI Assistant", "ИИ-помощник", "AI Asistan" },

            // ---------- İstifadəçi menyusu ----------
            ["user.admin"] = new[] { "Administrator", "Administrator", "Администратор", "Yönetici" },
            ["user.user"] = new[] { "İstifadəçi", "User", "Пользователь", "Kullanıcı" },
            ["user.profile"] = new[] { "Profilim", "My profile", "Мой профиль", "Profilim" },
            ["user.chat"] = new[] { "Canlı çat", "Live chat", "Онлайн-чат", "Canlı sohbet" },
            ["user.book"] = new[] { "Rezervasiya et", "Book now", "Записаться", "Rezervasyon yap" },
            ["user.adminPanel"] = new[] { "Admin Panel", "Admin panel", "Админ-панель", "Yönetim Paneli" },
            ["user.logout"] = new[] { "Çıxış", "Log out", "Выйти", "Çıkış" },
            ["user.login"] = new[] { "Daxil ol", "Sign in", "Войти", "Giriş yap" },
            ["wa.aria"] = new[] { "WhatsApp ilə yazın", "Message us on WhatsApp", "Напишите нам в WhatsApp", "WhatsApp'tan yazın" },

            // ---------- Footer ----------
            ["footer.about"] = new[]
            {
                "Yeni model dizel və benzin mühərriklərinin peşəkar diaqnostikası və təmiri. Müasir avadanlıq, təcrübəli ustalar, etibarlı nəticə.",
                "Professional diagnostics and repair of modern diesel and petrol engines. Modern equipment, experienced technicians, reliable results.",
                "Профессиональная диагностика и ремонт современных дизельных и бензиновых двигателей. Современное оборудование, опытные мастера, надёжный результат.",
                "Yeni model dizel ve benzinli motorların profesyonel arıza tespiti ve onarımı. Modern ekipman, deneyimli ustalar, güvenilir sonuç."
            },
            ["footer.hours"] = new[] { "İş Saatları", "Working Hours", "Часы работы", "Çalışma Saatleri" },
            ["footer.allServices"] = new[] { "Bütün xidmətlər", "All services", "Все услуги", "Tüm hizmetler" },
            ["footer.noServices"] = new[] { "Xidmətlər tezliklə əlavə olunacaq.", "Services will be added soon.", "Услуги скоро появятся.", "Hizmetler yakında eklenecek." },
            ["footer.rights"] = new[] { "Bütün hüquqlar qorunur.", "All rights reserved.", "Все права защищены.", "Tüm hakları saklıdır." },

            // ---------- Layout ----------
            ["layout.metaDesc"] = new[]
            {
                "{0} — yeni model dizel və benzin mühərriklərinin diaqnostikası və təmiri. Onlayn rezervasiya və usta ilə canlı çat.",
                "{0} — diagnostics and repair of modern diesel and petrol engines. Online booking and live chat with a technician.",
                "{0} — диагностика и ремонт современных дизельных и бензиновых двигателей. Онлайн-запись и чат с мастером.",
                "{0} — yeni model dizel ve benzinli motorların arıza tespiti ve onarımı. Online rezervasyon ve ustayla canlı sohbet."
            },
            ["layout.toTop"] = new[] { "Yuxarı qayıt", "Back to top", "Наверх", "Yukarı çık" },
            ["layout.chatTitle"] = new[] { "Usta ilə canlı çat", "Live chat with a technician", "Чат с мастером", "Ustayla canlı sohbet" },
            ["layout.chatOpen"] = new[] { "Canlı çatı aç", "Open live chat", "Открыть чат", "Canlı sohbeti aç" },
        };
    }
}
