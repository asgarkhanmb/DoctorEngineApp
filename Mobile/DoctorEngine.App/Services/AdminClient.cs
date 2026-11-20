using System.Net.Http.Headers;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.Services;

/// <summary>Mobil admin panel (api/v1/admin) — yalnız Admin rolu olan istifadəçilər üçün.</summary>
public class AdminClient
{
    private readonly ApiClient _api;

    public AdminClient(ApiClient api) => _api = api;

    // ---------- statistika, rezervasiya, mesaj, istifadəçi ----------

    public Task<AdminDashboardDto> DashboardAsync() => _api.GetAsync<AdminDashboardDto>("api/v1/admin/dashboard");

    public Task<List<AdminReservationDto>> ReservationsAsync(string? status) =>
        _api.GetAsync<List<AdminReservationDto>>("api/v1/admin/reservations" + (string.IsNullOrEmpty(status) ? "" : "?status=" + status));

    public Task ConfirmAsync(int id) => _api.PostAsync($"api/v1/admin/reservations/{id}/confirm", null);
    public Task RejectAsync(int id, string reason) => _api.PostAsync($"api/v1/admin/reservations/{id}/reject", new RejectReservationRequest(reason));
    public Task DeleteReservationAsync(int id) => _api.DeleteAsync($"api/v1/admin/reservations/{id}");

    public Task<List<AdminMessageDto>> MessagesAsync() => _api.GetAsync<List<AdminMessageDto>>("api/v1/admin/messages");
    public Task DeleteMessageAsync(int id) => _api.DeleteAsync($"api/v1/admin/messages/{id}");

    public Task<List<AdminUserDto>> UsersAsync(string? q) =>
        _api.GetAsync<List<AdminUserDto>>("api/v1/admin/users" + (string.IsNullOrWhiteSpace(q) ? "" : "?q=" + Uri.EscapeDataString(q.Trim())));
    public Task<List<string>> RolesAsync() => _api.GetAsync<List<string>>("api/v1/admin/roles");
    public Task AddRoleAsync(string userId, string role) => _api.PostAsync($"api/v1/admin/users/{userId}/roles", new RoleRequest(role));
    public Task RemoveRoleAsync(string userId, string role) =>
        _api.DeleteAsync($"api/v1/admin/users/{userId}/roles/{Uri.EscapeDataString(role)}");

    // ---------- məzmun bölmələri ----------

    public Task<List<AdminSectionDto>> SectionsAsync() => _api.GetAsync<List<AdminSectionDto>>("api/v1/admin/sections");
    public Task<AdminListDto> ListAsync(string key) => _api.GetAsync<AdminListDto>($"api/v1/admin/sections/{key}");
    public Task<AdminFormDto> FormAsync(string key, int? id) =>
        _api.GetAsync<AdminFormDto>($"api/v1/admin/sections/{key}/form" + (id is int i ? "?id=" + i : ""));
    public Task DeleteAsync(string key, int id) => _api.DeleteAsync($"api/v1/admin/sections/{key}/{id}");

    /// <summary>Formanı göndərir (multipart): mətn sahələri + seçilmiş fayllar. id null — yeni qeyd / tək qeyd.</summary>
    public Task SaveAsync(string key, int? id, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, FileResult> files) =>
        _api.PostFormAsync($"api/v1/admin/sections/{key}" + (id is int i ? "/" + i : ""), () =>
        {
            var form = new MultipartFormDataContent();
            foreach (var (name, value) in values) form.Add(new StringContent(value), name);
            foreach (var (name, file) in files)
            {
                var content = new StreamContent(File.OpenRead(file.FullPath));
                content.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrEmpty(file.ContentType) ? GuessType(file.FileName) : file.ContentType);
                form.Add(content, name, file.FileName);
            }
            return form;
        });

    private static string GuessType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".mp4" or ".m4v" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        _ => "image/jpeg"
    };
}
