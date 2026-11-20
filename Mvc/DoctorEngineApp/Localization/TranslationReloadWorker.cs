using DoctorEngineApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DoctorEngineApp.Localization
{
    /// <summary>
    /// API üçün: AI tərcümələrini (ContentTranslations) bazadan yaddaşa yükləyir və hər 5 dəqiqədən bir yeniləyir.
    /// Tərcümə etmir — bunu saytdakı <see cref="ContentTranslationWorker"/> edir (AI-a təkrar sorğu getməsin).
    /// </summary>
    public class TranslationReloadWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        private readonly TranslationStore _store;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<TranslationReloadWorker> _logger;

        public TranslationReloadWorker(TranslationStore store, IServiceScopeFactory scopes, ILogger<TranslationReloadWorker> logger)
        {
            _store = store;
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var rows = await db.ContentTranslations.AsNoTracking()
                        .Select(t => new { t.Lang, t.Source, t.Value })
                        .ToListAsync(ct);
                    foreach (var r in rows) _store.Set(r.Lang, r.Source, r.Value);
                    _store.MarkLoaded();
                    _store.TakePending(int.MaxValue);   // növbə burada işlənmir — yaddaşda yığılmasın
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tərcümələr bazadan yüklənmədi");
                }

                try { await Task.Delay(Interval, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
