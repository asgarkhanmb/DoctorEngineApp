using System.Text.Json;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.Services;

/// <summary>
/// Sayt məzmunu. Son uğurlu cavab cihazda saxlanılır — internet olmayanda tətbiq
/// boş ekran yox, son yüklənmiş məlumatı göstərir.
/// </summary>
public class ContentService
{
    private readonly ApiClient _api;
    private HomeDto? _home;
    private static string CacheFile => Path.Combine(FileSystem.CacheDirectory, "home.json");

    public ContentService(ApiClient api) => _api = api;

    /// <summary>true: məlumat serverdən gəldi; false: oflayn keşdən.</summary>
    public bool IsFresh { get; private set; }

    public async Task<HomeDto?> GetHomeAsync(bool force = false)
    {
        if (_home != null && !force) return _home;
        try
        {
            _home = await _api.GetAsync<HomeDto>("api/v1/content/home");
            IsFresh = true;
            try { await File.WriteAllTextAsync(CacheFile, JsonSerializer.Serialize(_home, Json.Options)); } catch { }
            return _home;
        }
        catch (ApiException)
        {
            IsFresh = false;
            if (_home != null) return _home;
            if (File.Exists(CacheFile))
            {
                try { return _home = JsonSerializer.Deserialize<HomeDto>(await File.ReadAllTextAsync(CacheFile), Json.Options); }
                catch { }
            }
            throw;
        }
    }

    public Task<MessageResponse> SendContactAsync(ContactMessageRequest request) =>
        _api.PostAsync<MessageResponse>("api/v1/content/contact", request);
}
