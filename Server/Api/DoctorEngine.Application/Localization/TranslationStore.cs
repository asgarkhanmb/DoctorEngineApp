using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DoctorEngineApp.Localization
{
    /// <summary>
    /// AI tərcümələrinin yaddaşdakı nüsxəsi (singleton). Səhifə göstəriləndə bazaya getmir —
    /// tərcümə yoxdursa mətn növbəyə düşür, <see cref="ContentTranslationWorker"/> onu arxa planda tərcümə edir,
    /// növbəti açılışda artıq tərcümə olunmuş göstərilir.
    /// </summary>
    public class TranslationStore
    {
        public static readonly string[] TargetLangs = { "en", "ru", "tr" };

        // (dil, mənbə mətn) → tərcümə
        private readonly ConcurrentDictionary<(string Lang, string Source), string> _items = new();
        // tərcümə gözləyən mətnlər
        private readonly ConcurrentDictionary<string, byte> _pending = new();
        private readonly SemaphoreSlim _signal = new(0);

        public bool IsLoaded { get; private set; }

        public static string Hash(string source) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

        public bool TryGet(string lang, string source, out string value) =>
            _items.TryGetValue((lang, source), out value!);

        public void Set(string lang, string source, string value) => _items[(lang, source)] = value;

        public void MarkLoaded() => IsLoaded = true;

        /// <summary>Bir dilin bütün tərcümələri (mənbə → tərcümə) — saytın (front) yaddaşı üçün.</summary>
        public Dictionary<string, string> Snapshot(string lang) =>
            _items.Where(kv => kv.Key.Lang == lang).ToDictionary(kv => kv.Key.Source, kv => kv.Value);

        /// <summary>Tərcüməsi olmayan mətni növbəyə əlavə edir (bütün hədəf dillər üçün).</summary>
        public void Request(string source)
        {
            if (!NeedsTranslation(source)) return;
            if (TargetLangs.All(l => _items.ContainsKey((l, source)))) return;
            if (_pending.TryAdd(source, 0))
                _signal.Release();
        }

        public List<string> TakePending(int maxChars)
        {
            var batch = new List<string>();
            var total = 0;
            foreach (var s in _pending.Keys)
            {
                if (batch.Count > 0 && total + s.Length > maxChars) break;
                if (_pending.TryRemove(s, out _))
                {
                    batch.Add(s);
                    total += s.Length;
                }
            }
            return batch;
        }

        public bool HasPending => !_pending.IsEmpty;

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);

        /// <summary>Yığılmış siqnalları sıfırlayır (növbə onsuz da bir dəfəyə boşaldılır).</summary>
        public void DrainSignals()
        {
            while (_signal.Wait(0)) { }
        }

        /// <summary>Hərf olmayan mətnləri (rəqəm, telefon, vaxt) tərcümə etməyə ehtiyac yoxdur.</summary>
        public static bool NeedsTranslation(string? s) =>
            !string.IsNullOrWhiteSpace(s) && s.Length <= 8000 && s.Any(char.IsLetter);
    }
}
