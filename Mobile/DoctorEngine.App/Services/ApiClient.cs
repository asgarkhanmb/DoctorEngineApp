using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DoctorEngine.App.Localization;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.Services;

/// <summary>API xətası — istifadəçiyə göstərilən mesajla (server Azərbaycan dilində qaytarır).</summary>
public class ApiException : Exception
{
    public HttpStatusCode Status { get; }
    public ApiException(string message, HttpStatusCode status) : base(message) => Status = status;
}

/// <summary>
/// Server ilə bütün əlaqə. Access token avtomatik əlavə olunur; vaxtı bitəndə (və ya 401 gələndə)
/// refresh token ilə bir dəfə yenilənir və sorğu təkrarlanır. Yenilənmə alınmasa — sessiya bitir.
/// </summary>
public class ApiClient
{
    private readonly TokenStore _tokens;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private HttpClient _http;

    /// <summary>Sessiya bitdi (refresh token etibarsızdır) — tətbiq giriş ekranına qayıdır.</summary>
    public event EventHandler? SessionExpired;

    public ApiClient(TokenStore tokens)
    {
        _tokens = tokens;
        _http = Create();
    }

    public TokenStore Tokens => _tokens;

    /// <summary>Server ünvanı ayarlarda dəyişəndə.</summary>
    public void Reset() => _http = Create();

    private static HttpClient Create() => new()
    {
        BaseAddress = new Uri(AppConfig.BaseUrl.TrimEnd('/') + "/"),
        Timeout = Timeout.InfiniteTimeSpan   // hər sorğunun öz vaxt limiti var (SendRawAsync) — böyük video yükləmələri üçün
    };

    // ================= SADƏ ÇAĞIRIŞLAR =================

    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Get, path), ct);

    public Task<T> PostAsync<T>(string path, object? body, CancellationToken ct = default) =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Post, path) { Content = body == null ? null : JsonContent.Create(body, options: Json.Options) }, ct);

    public async Task PostAsync(string path, object? body, CancellationToken ct = default)
    {
        using var res = await SendRawAsync(() => new HttpRequestMessage(HttpMethod.Post, path)
        { Content = body == null ? null : JsonContent.Create(body, options: Json.Options) }, ct);
        await EnsureSuccessAsync(res, ct);
    }

    public Task<T> PutAsync<T>(string path, object? body, CancellationToken ct = default) =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Put, path) { Content = body == null ? null : JsonContent.Create(body, options: Json.Options) }, ct);

    public Task<T> DeleteAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Delete, path), ct);

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var res = await SendRawAsync(() => new HttpRequestMessage(HttpMethod.Delete, path), ct);
        await EnsureSuccessAsync(res, ct);
    }

    /// <summary>
    /// Access tokeni dərhal yeniləyir — profil (istifadəçi adı) və ya şifrə dəyişəndən sonra
    /// tokendəki məlumat (claim-lər, security stamp) yenilənsin.
    /// </summary>
    public Task<bool> ForceRefreshAsync() => RefreshAsync(_tokens.AccessToken, default);

    /// <summary>Fayl / form göndərmək (çat) — content hər cəhddə yenidən yaradılır.</summary>
    public async Task<T> PostFormAsync<T>(string path, Func<HttpContent> content, CancellationToken ct = default) =>
        await SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Post, path) { Content = content() }, ct);

    public async Task PostFormAsync(string path, Func<HttpContent> content, CancellationToken ct = default)
    {
        using var res = await SendRawAsync(() => new HttpRequestMessage(HttpMethod.Post, path) { Content = content() }, ct);
        await EnsureSuccessAsync(res, ct);
    }

    /// <summary>Uzun yükləmə (Reels videosu) — standart 100 saniyəlik limit əvəzinə <paramref name="timeout"/>.</summary>
    public async Task<T> UploadAsync<T>(string path, Func<HttpContent> content, TimeSpan timeout, CancellationToken ct = default)
    {
        using var res = await SendRawAsync(() => new HttpRequestMessage(HttpMethod.Post, path) { Content = content() }, ct, timeout: timeout);
        await EnsureSuccessAsync(res, ct);
        return (await res.Content.ReadFromJsonAsync<T>(Json.Options, ct))!;
    }

    /// <summary>Axın kimi cavab (AI köməkçi) — çağıran tərəf bağlamalıdır.</summary>
    public async Task<HttpResponseMessage> PostStreamAsync(string path, object body, CancellationToken ct)
    {
        var res = await SendRawAsync(() => new HttpRequestMessage(HttpMethod.Post, path)
        { Content = JsonContent.Create(body, options: Json.Options) }, ct, HttpCompletionOption.ResponseHeadersRead);
        if (!res.IsSuccessStatusCode)
        {
            try { await EnsureSuccessAsync(res, ct); }
            finally { res.Dispose(); }
        }
        return res;
    }

    // ================= GİRİŞ (tokensiz) =================

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        using var res = await _http.PostAsJsonAsync("api/v1/auth/login", request, Json.Options, ct);
        await EnsureSuccessAsync(res, ct);
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>(Json.Options, ct))!;
        await _tokens.SaveAsync(auth);
        return auth;
    }

    public async Task LogoutAsync()
    {
        var refresh = _tokens.RefreshToken;
        _tokens.Clear();
        if (string.IsNullOrEmpty(refresh)) return;
        try
        {
            using var res = await _http.PostAsJsonAsync("api/v1/auth/logout", new LogoutRequest(refresh), Json.Options);
        }
        catch { /* oflayn — server tərəfdə token vaxtı keçəndə özü bitir */ }
    }

    /// <summary>Lazım olanda tokeni yeniləyir (SignalR üçün də istifadə olunur).</summary>
    public async Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        if (!_tokens.HasSession) return null;
        if (!string.IsNullOrEmpty(_tokens.AccessToken) && _tokens.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60))
            return _tokens.AccessToken;
        return await RefreshAsync(_tokens.AccessToken, ct) ? _tokens.AccessToken : null;
    }

    // ================= DAXİLİ =================

    private async Task<T> SendAsync<T>(Func<HttpRequestMessage> build, CancellationToken ct)
    {
        using var res = await SendRawAsync(build, ct);
        await EnsureSuccessAsync(res, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return default!;
        return (await res.Content.ReadFromJsonAsync<T>(Json.Options, ct))!;
    }

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);

    private async Task<HttpResponseMessage> SendRawAsync(Func<HttpRequestMessage> build, CancellationToken ct,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead, TimeSpan? timeout = null)
    {
        var token = await GetValidAccessTokenAsync(ct);
        HttpResponseMessage res;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout ?? DefaultTimeout);
        try
        {
            res = await _http.SendAsync(WithAuth(build(), token), completion, limit.Token);
        }
        catch (HttpRequestException)
        {
            throw new ApiException(Lang.T("api_no_connection"), HttpStatusCode.ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(Lang.T("api_timeout"), HttpStatusCode.RequestTimeout);
        }

        // Token serverdə etibarsız sayılıbsa (şifrə dəyişib və s.) — bir dəfə yenilə və təkrarla
        if (res.StatusCode == HttpStatusCode.Unauthorized && _tokens.HasSession)
        {
            res.Dispose();
            if (await RefreshAsync(token, ct))
                return await _http.SendAsync(WithAuth(build(), _tokens.AccessToken), completion, limit.Token);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }
        return res;
    }

    private static HttpRequestMessage WithAuth(HttpRequestMessage req, string? token)
    {
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Seçilmiş dil — server gələcəkdə mətnləri bu dildə qaytara bilsin (AI cavabı sualın dilindədir)
        req.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(Lang.Current.Code));
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    /// <summary>Eyni anda bir neçə sorğu 401 alsa — yalnız bir yeniləmə (refresh token bir dəfəlikdir).</summary>
    private async Task<bool> RefreshAsync(string? staleToken, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            // Başqa sorğu artıq yeniləyibsə
            if (_tokens.AccessToken != staleToken && _tokens.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60))
                return true;
            if (string.IsNullOrEmpty(_tokens.RefreshToken)) return false;

            using var res = await _http.PostAsJsonAsync("api/v1/auth/refresh",
                new RefreshRequest(_tokens.RefreshToken, AppConfig.DeviceName), Json.Options, ct);
            if (res.IsSuccessStatusCode)
            {
                await _tokens.SaveAsync((await res.Content.ReadFromJsonAsync<AuthResponse>(Json.Options, ct))!);
                return true;
            }
            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                _tokens.Clear();
                MainThread.BeginInvokeOnMainThread(() => SessionExpired?.Invoke(this, EventArgs.Empty));
            }
            return false;
        }
        catch (HttpRequestException)
        {
            return false;   // oflayn — sessiyanı silmirik, internet qayıdanda yenilənəcək
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        string message;
        try
        {
            var err = await res.Content.ReadFromJsonAsync<ApiErrorBody>(Json.Options, ct);
            message = !string.IsNullOrWhiteSpace(err?.Error) ? err!.Error!
                    : !string.IsNullOrWhiteSpace(err?.Message) ? err!.Message! : Default(res.StatusCode);
        }
        catch
        {
            message = Default(res.StatusCode);
        }
        throw new ApiException(message, res.StatusCode);
    }

    private static string Default(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => Lang.T("api_unauthorized"),
        HttpStatusCode.Forbidden => Lang.T("api_forbidden"),
        HttpStatusCode.NotFound => Lang.T("api_not_found"),
        HttpStatusCode.TooManyRequests => Lang.T("api_too_many"),
        >= HttpStatusCode.InternalServerError => Lang.T("api_server_error"),
        _ => Lang.T("api_failed")
    };

    /// <summary>Server xətaları: { error } (ApiError) və ya rezervasiyada { message }.</summary>
    private sealed record ApiErrorBody(string? Error, string? Message);
}
