using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoctorEngineApp.Localization
{
    /// <summary>
    /// Bazadan gələn mətnləri (slider, xidmətlər, haqqımızda, ustalar ...) AI ilə en/ru/tr dillərinə avtomatik tərcümə edir.
    ///  - Başlanğıcda: bütün tərcümələri yaddaşa yükləyir və bazadakı bütün mətnləri yoxlayır (yeni/dəyişmiş olanlar növbəyə).
    ///  - Səhifədə tərcüməsi olmayan mətn göstəriləndə (L.T) növbəyə düşür — bir neçə saniyəyə tərcümə olunur.
    ///  - Hər 30 dəqiqədən bir baza yenidən yoxlanılır (admin dəyişiklikləri ziyarətçi gözləmədən tərcümə olunsun).
    /// AI açarı (Ai:ApiKey) yoxdursa heç nə etmir — sayt azərbaycanca mətnləri göstərir.
    /// </summary>
    public class ContentTranslationWorker : BackgroundService
    {
        private const int MaxBatchChars = 5000;
        private const int MaxAttempts = 3;

        private static readonly Dictionary<string, string> LangNames = new()
        {
            ["en"] = "English",
            ["ru"] = "Russian",
            ["tr"] = "Turkish"
        };

        private readonly TranslationStore _store;
        private readonly IServiceScopeFactory _scopes;
        private readonly IHttpClientFactory _httpFactory;
        private readonly AiOptions _ai;
        private readonly ILogger<ContentTranslationWorker> _logger;
        private readonly Dictionary<string, int> _attempts = new();
        private bool _warnedNoKey;

        public ContentTranslationWorker(TranslationStore store, IServiceScopeFactory scopes, IHttpClientFactory httpFactory,
                                        IOptions<AiOptions> ai, ILogger<ContentTranslationWorker> logger)
        {
            _store = store;
            _scopes = scopes;
            _httpFactory = httpFactory;
            _ai = ai.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            try
            {
                await LoadAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Məs.: migration hələ tətbiq olunmayıb — sayt işləməyə davam edir (azərbaycanca)
                _logger.LogError(ex, "Tərcümələr yüklənmədi (ContentTranslations cədvəli var? dotnet ef database update)");
                return;
            }

            var prewarm = true;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (prewarm) await PrewarmAsync(ct);

                    // Növbəyə mətn düşənə qədər (və ya 30 dəqiqə) gözlə; 30 dəq keçibsə — bazanı yenidən yoxla
                    var signalled = await _store.WaitAsync(TimeSpan.FromMinutes(30), ct);
                    prewarm = !signalled;
                    if (signalled) await Task.Delay(1500, ct);   // bir səhifənin bütün mətnləri yığılsın
                    _store.DrainSignals();

                    while (_store.HasPending && !ct.IsCancellationRequested)
                    {
                        var batch = _store.TakePending(MaxBatchChars);
                        if (!_ai.IsConfigured)
                        {
                            if (!_warnedNoKey)
                            {
                                _logger.LogWarning("Ai:ApiKey təyin olunmayıb — bazadakı kontent tərcümə olunmur (azərbaycanca göstərilir).");
                                _warnedNoKey = true;
                            }
                            continue;   // növbəni boşalt
                        }
                        await TranslateBatchAsync(batch, ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tərcümə dövründə xəta");
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                }
            }
        }

        private async Task LoadAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.ContentTranslations.AsNoTracking()
                .Select(t => new { t.Lang, t.Source, t.Value })
                .ToListAsync(ct);
            foreach (var r in rows) _store.Set(r.Lang, r.Source, r.Value);
            _store.MarkLoaded();
            _logger.LogInformation("{Count} tərcümə yükləndi", rows.Count);
        }

        /// <summary>Bazadakı bütün tərcümə olunan mətnləri növbəyə verir (tərcüməsi olanlar atlanır).</summary>
        private async Task PrewarmAsync(CancellationToken ct)
        {
            if (!_ai.IsConfigured) return;

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var texts = new List<string?>();

            texts.AddRange((await db.Sliders.AsNoTracking().Select(x => new { x.Title, x.Subtitle }).ToListAsync(ct)).SelectMany(x => new[] { x.Title, x.Subtitle }));
            texts.AddRange((await db.Informs.AsNoTracking().Select(x => new { x.Title, x.Description }).ToListAsync(ct)).SelectMany(x => new[] { x.Title, x.Description }));
            texts.AddRange((await db.Abouts.AsNoTracking().Select(x => new { x.Title, x.Description }).ToListAsync(ct)).SelectMany(x => new[] { x.Title, x.Description }));
            texts.AddRange(await db.ServiceCategories.AsNoTracking().Select(x => x.Name).ToListAsync(ct));
            texts.AddRange(await db.Services.AsNoTracking().Select(x => x.Name).ToListAsync(ct));
            texts.AddRange((await db.SubServices.AsNoTracking().Select(x => new { x.Title, x.Description }).ToListAsync(ct)).SelectMany(x => new[] { x.Title, x.Description }));
            texts.AddRange((await db.Technicians.AsNoTracking().Select(x => new { x.Position, x.Bio }).ToListAsync(ct)).SelectMany(x => new[] { x.Position, x.Bio }));
            texts.AddRange((await db.ContactPhones.AsNoTracking().Select(x => new { x.Subtitle, x.Title, x.Description }).ToListAsync(ct)).SelectMany(x => new[] { x.Subtitle, x.Title, x.Description }));
            texts.AddRange(await db.ContactInfos.AsNoTracking().Select(x => x.Address).ToListAsync(ct));
            texts.AddRange(await db.Maps.AsNoTracking().Select(x => x.Address).ToListAsync(ct));
            texts.AddRange(await db.WhatsappSettings.AsNoTracking().Select(x => x.Message).ToListAsync(ct));

            foreach (var t in texts.Distinct())
                if (t != null) _store.Request(t);
        }

        private async Task TranslateBatchAsync(List<string> batch, CancellationToken ct)
        {
            foreach (var lang in TranslationStore.TargetLangs)
            {
                var todo = batch.Where(s => !_store.TryGet(lang, s, out _)).ToList();
                if (todo.Count == 0) continue;

                List<string> result;
                try
                {
                    result = await CallAiAsync(todo, lang, ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "AI tərcüməsi alınmadı ({Lang}, {Count} mətn)", lang, todo.Count);
                    // Bir neçə dəfə yenidən cəhd et, sonra (yenə görünənə qədər) burax
                    _ = RetryLaterAsync(todo, ct);
                    return;
                }

                await SaveAsync(lang, todo, result, ct);
            }
        }

        private async Task RetryLaterAsync(List<string> items, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromMinutes(2), ct);
            lock (_attempts)
            {
                foreach (var s in items)
                {
                    var n = _attempts.GetValueOrDefault(s) + 1;
                    _attempts[s] = n;
                    if (n < MaxAttempts) _store.Request(s);
                }
            }
        }

        private async Task SaveAsync(string lang, List<string> sources, List<string> values, CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var hashes = sources.Select(TranslationStore.Hash).ToList();
            var existing = (await db.ContentTranslations
                .Where(t => t.Lang == lang && hashes.Contains(t.SourceHash))
                .Select(t => t.SourceHash)
                .ToListAsync(ct)).ToHashSet();

            for (var i = 0; i < sources.Count; i++)
            {
                var value = values[i].Trim();
                if (string.IsNullOrEmpty(value)) continue;
                if (!existing.Contains(hashes[i]))
                {
                    db.ContentTranslations.Add(new ContentTranslation
                    {
                        SourceHash = hashes[i],
                        Lang = lang,
                        Source = sources[i],
                        Value = value
                    });
                }
                _store.Set(lang, sources[i], value);
            }
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("{Count} mətn {Lang} dilinə tərcümə olundu", sources.Count, lang);
        }

        private async Task<List<string>> CallAiAsync(List<string> texts, string lang, CancellationToken ct)
        {
            var system =
                "You are a professional translator for the website of an engine diagnostics and repair service in Baku, Azerbaijan. " +
                $"Translate each Azerbaijani string in the JSON array the user sends into {LangNames[lang]}. " +
                "Rules: keep the meaning and tone (friendly, professional); keep HTML tags, line breaks, emojis, numbers, phone numbers, " +
                "prices, times, URLs, placeholders like {0}, and brand/model names unchanged; translate Azerbaijani place names into their usual form in the target language. " +
                "Return ONLY a JSON array of strings with exactly the same number of items in the same order. No comments, no code fences.";

            var body = new
            {
                model = _ai.Model,
                temperature = 0.2,
                max_tokens = 16000,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = JsonSerializer.Serialize(texts) }
                }
            };

            var http = _httpFactory.CreateClient("ai-translate");
            using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_ai.BaseUrl), "chat/completions"))
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _ai.ApiKey);

            using var res = await http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"AI {(int)res.StatusCode}: {Truncate(json, 300)}");

            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

            var start = content.IndexOf('[');
            var end = content.LastIndexOf(']');
            if (start < 0 || end <= start) throw new FormatException("AI cavabında JSON massivi yoxdur: " + Truncate(content, 200));

            var list = JsonSerializer.Deserialize<List<string>>(content[start..(end + 1)]) ?? new();
            if (list.Count != texts.Count)
                throw new FormatException($"AI {list.Count} tərcümə qaytardı, {texts.Count} gözlənilirdi");
            return list;
        }

        private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
    }
}
