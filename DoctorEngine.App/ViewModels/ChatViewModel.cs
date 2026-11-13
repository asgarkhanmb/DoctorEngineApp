using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>
/// Canlı usta dəstəyi — saytdakı çatla eyni söhbət (sayt və tətbiq istifadəçiləri bir yerdə).
/// Mətn, şəkil, video və səs mesajı; redaktə / silmə; canlı yeniləmə (SignalR).
/// </summary>
public partial class ChatViewModel : BaseViewModel
{
    private readonly ChatClient _chat;
    private readonly AuthService _auth;
    private readonly IVoiceRecorder _recorder;
    private readonly IAudioPlayback _playback;
    private bool _hasMore;
    private bool _connected;
    private DateTime _recordStarted;
    private IDispatcherTimer? _recTimer;

    public ObservableCollection<ChatItem> Messages { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private string? text;

    [ObservableProperty] private string status = "Qoşulur…";
    [ObservableProperty] private bool isOnline;
    [ObservableProperty] private bool isSending;
    [ObservableProperty] private bool isRecording;
    [ObservableProperty] private string recordTime = "0:00";
    [ObservableProperty] private bool canLoadMore;
    [ObservableProperty] private ChatItem? editing;

    public bool IsLoggedIn => _auth.IsLoggedIn;
    public bool IsGuest => !_auth.IsLoggedIn;
    public bool CanSend => !string.IsNullOrWhiteSpace(Text) && !IsSending;
    public bool IsEmpty => IsLoggedIn && !IsBusy && Messages.Count == 0;

    /// <summary>Yeni mesaj gələndə siyahı aşağı sürüşsün (View dinləyir).</summary>
    public event Action<ChatItem>? ScrollToRequested;

    public ChatViewModel(ChatClient chat, AuthService auth, IVoiceRecorder recorder, IAudioPlayback playback)
    {
        _chat = chat;
        _auth = auth;
        _recorder = recorder;
        _playback = playback;

        _chat.MessageReceived += OnReceived;
        _chat.MessageEdited += OnEdited;
        _chat.MessageDeleted += id => Remove(id);
        _chat.StateChanged += s =>
        {
            IsOnline = s == "online";
            Status = s switch { "online" => "Onlayn", "connecting" => "Qoşulur…", _ => "Bağlantı yoxdur" };
        };
        _auth.Changed += async (_, _) =>
        {
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsGuest));
            if (!_auth.IsLoggedIn) { Messages.Clear(); await _chat.DisconnectAsync(); _connected = false; }
        };
        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Ekran açılanda: mesajları yüklə, canlı bağlantını qur, oxunmuş kimi qeyd et.</summary>
    public async Task AppearingAsync()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsGuest));
        if (!IsLoggedIn) return;

        if (Messages.Count == 0)
        {
            await RunAsync(async () =>
            {
                var page = await _chat.GetPageAsync();
                Messages.Clear();
                foreach (var m in page.Messages) Messages.Add(ToItem(m));
                _hasMore = page.HasMore;
                CanLoadMore = _hasMore;
                Regroup();
            });
            if (Messages.Count > 0) ScrollToRequested?.Invoke(Messages[^1]);
        }

        if (!_connected)
        {
            _connected = true;
            await _chat.ConnectAsync();
        }
        await MarkReadAsync();
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (!_hasMore || Messages.Count == 0 || IsBusy) return;
        var first = Messages[0];
        await RunAsync(async () =>
        {
            var page = await _chat.GetPageAsync(first.Id);
            for (var i = page.Messages.Count - 1; i >= 0; i--) Messages.Insert(0, ToItem(page.Messages[i]));
            _hasMore = page.HasMore;
            CanLoadMore = _hasMore;
            Regroup();
        });
    }

    [RelayCommand]
    private async Task Send()
    {
        var msg = Text?.Trim();
        if (string.IsNullOrEmpty(msg) || IsSending) return;
        if (msg.Length > ChatLimits.MaxMessageLength) { await Alert("Çox uzundur", $"Mesaj maksimum {ChatLimits.MaxMessageLength} simvol ola bilər."); return; }

        IsSending = true;
        try
        {
            if (Editing != null)
            {
                var edited = await _chat.EditAsync(Editing.Id, msg, removeAttachment: false);
                OnEdited(edited);
                Editing = null;
            }
            else
            {
                var sent = await _chat.SendAsync(msg);
                OnReceived(sent);   // SignalR gecikərsə də dərhal görünsün (Id ilə təkrar olmur)
            }
            Text = null;
        }
        catch (ApiException ex) { await Alert("Göndərilmədi", ex.Message); }
        finally { IsSending = false; OnPropertyChanged(nameof(CanSend)); }
    }

    [RelayCommand]
    private async Task Attach()
    {
        var choice = await Shell.Current.DisplayActionSheetAsync("Fayl göndər", "Ləğv et", null,
            "📷 Şəkil seç", "🎬 Video seç", "📸 Şəkil çək");
        FileResult? file = choice switch
        {
            "📷 Şəkil seç" => (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault(),
            "🎬 Video seç" => (await MediaPicker.Default.PickVideosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault(),
            "📸 Şəkil çək" => MediaPicker.Default.IsCaptureSupported ? await MediaPicker.Default.CapturePhotoAsync() : null,
            _ => null
        };
        if (file == null) return;

        var isVideo = file.ContentType?.StartsWith("video/") == true ||
                      new[] { ".mp4", ".mov", ".m4v", ".webm" }.Contains(Path.GetExtension(file.FileName).ToLowerInvariant());
        var size = new FileInfo(file.FullPath).Length;
        var max = isVideo ? ChatLimits.MaxVideoBytes : ChatLimits.MaxImageBytes;
        if (size > max)
        {
            await Alert("Fayl çox böyükdür", $"{(isVideo ? "Video" : "Şəkil")} maksimum {max / 1024 / 1024} MB ola bilər.");
            return;
        }

        IsSending = true;
        try
        {
            var sent = await _chat.SendAsync(Text, file, ContentTypeFor(file.FileName, isVideo));
            Text = null;
            OnReceived(sent);
        }
        catch (ApiException ex) { await Alert("Göndərilmədi", ex.Message); }
        finally { IsSending = false; }
    }

    // ================= SƏS MESAJI =================

    [RelayCommand]
    private async Task ToggleRecord()
    {
        if (IsRecording) { await StopAndSendAsync(); return; }

        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            await Alert("Mikrofon", "Səs mesajı üçün mikrofona icazə verin (Parametrlər → Tətbiqlər → DoctorEngine).");
            return;
        }
        try
        {
            await _playback.StopAsync();
            await _recorder.StartAsync();
            IsRecording = true;
            _recordStarted = DateTime.UtcNow;
            RecordTime = "0:00";
            _recTimer = Application.Current!.Dispatcher.CreateTimer();
            _recTimer.Interval = TimeSpan.FromMilliseconds(250);
            _recTimer.Tick += (_, _) =>
            {
                var s = (DateTime.UtcNow - _recordStarted).TotalSeconds;
                RecordTime = $"{(int)s / 60}:{(int)s % 60:00}";
                if (s >= 15 * 60) _ = StopAndSendAsync();   // saytla eyni limit: 15 dəq
            };
            _recTimer.Start();
        }
        catch (Exception ex)
        {
            IsRecording = false;
            await Alert("Mikrofon", "Səs yazmaq mümkün olmadı: " + ex.Message);
        }
    }

    [RelayCommand]
    private async Task CancelRecord()
    {
        if (!IsRecording) return;
        _recTimer?.Stop();
        IsRecording = false;
        var rec = await _recorder.StopAsync();
        if (rec != null) TryDelete(rec.Path);
    }

    private async Task StopAndSendAsync()
    {
        if (!IsRecording) return;
        _recTimer?.Stop();
        IsRecording = false;
        var seconds = (DateTime.UtcNow - _recordStarted).TotalSeconds;
        var rec = await _recorder.StopAsync();
        if (rec == null) return;
        try
        {
            if (seconds < 1) { await Alert("Çox qısadır", "Səs mesajı ən azı 1 saniyə olmalıdır."); return; }
            if (new FileInfo(rec.Path).Length > ChatLimits.MaxAudioBytes) { await Alert("Çox böyükdür", "Səs mesajı maksimum 16 MB ola bilər."); return; }
            IsSending = true;
            var sent = await _chat.SendAudioAsync(rec.Path, rec.ContentType);
            OnReceived(sent);
        }
        catch (ApiException ex) { await Alert("Göndərilmədi", ex.Message); }
        finally { IsSending = false; TryDelete(rec.Path); }
    }

    [RelayCommand]
    private async Task PlayAudio(ChatItem item)
    {
        if (item.AudioUrl == null) return;
        if (item.IsPlaying) { await _playback.StopAsync(); return; }
        foreach (var m in Messages) m.IsPlaying = false;
        item.IsPlaying = true;
        try
        {
            await _playback.PlayAsync(item.AudioUrl, () => MainThread.BeginInvokeOnMainThread(() => item.IsPlaying = false));
        }
        catch
        {
            item.IsPlaying = false;
            await OpenAsync(item.AudioUrl);   // cihaz formatı oynada bilmirsə — sistem pleyerində
        }
    }

    [RelayCommand] private Task OpenMedia(string? url) => OpenAsync(url);

    // ================= REDAKTƏ / SİLMƏ =================

    [RelayCommand]
    private async Task Options(ChatItem item)
    {
        var actions = new List<string>();
        if (item.IsMine && item.Text != null) actions.Add("✏️ Redaktə et");
        if (item.IsMine || _auth.User?.IsAdmin == true) actions.Add("🗑 Sil");
        if (item.Text != null) actions.Add("📋 Kopyala");
        if (actions.Count == 0) return;

        var choice = await Shell.Current.DisplayActionSheetAsync(null, "Ləğv et", null, actions.ToArray());
        switch (choice)
        {
            case "✏️ Redaktə et":
                Editing = item;
                Text = item.Text;
                break;
            case "🗑 Sil":
                if (!await Confirm("Mesaj silinsin?", item.IsMine
                        ? "Mesaj hamı üçün silinəcək. Bu əməliyyat geri qaytarıla bilməz."
                        : $"{item.UserName} istifadəçisinin mesajı admin olaraq silinəcək.", "Sil")) return;
                try { await _chat.DeleteAsync(item.Id); Remove(item.Id); }
                catch (ApiException ex) { await Alert("Silinmədi", ex.Message); }
                break;
            case "📋 Kopyala":
                await Clipboard.Default.SetTextAsync(item.Text);
                break;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        Editing = null;
        Text = null;
    }

    [RelayCommand] private Task GoToLogin() => Shell.Current.GoToAsync("//profile");

    // ================= DAXİLİ =================

    private void OnReceived(ChatMessageDto dto)
    {
        if (Messages.Any(m => m.Id == dto.Id)) return;
        var item = ToItem(dto);
        // Id sırası ilə düzgün yerə
        var index = Messages.Count;
        while (index > 0 && Messages[index - 1].Id > dto.Id) index--;
        Messages.Insert(index, item);
        Regroup();
        ScrollToRequested?.Invoke(item);
        if (!item.IsMine) _ = MarkReadAsync();
    }

    private void OnEdited(ChatMessageDto dto)
    {
        var i = Messages.ToList().FindIndex(m => m.Id == dto.Id);
        if (i < 0) return;
        Messages[i] = ToItem(dto);
        Regroup();
    }

    private void Remove(int id)
    {
        var item = Messages.FirstOrDefault(m => m.Id == id);
        if (item != null) { Messages.Remove(item); Regroup(); }
    }

    private async Task MarkReadAsync()
    {
        var last = Messages.LastOrDefault();
        if (last == null) return;
        try { await _chat.MarkReadAsync(last.Id); } catch { }
    }

    /// <summary>Tarix ayırıcıları və ardıcıl mesajlarda adın təkrarlanmaması (saytdakı kimi).</summary>
    private void Regroup()
    {
        ChatItem? prev = null;
        foreach (var m in Messages)
        {
            m.ShowDate = prev == null || prev.Date != m.Date;
            m.ShowName = !m.IsMine && (m.ShowDate || prev!.UserName != m.UserName);
            prev = m;
        }
    }

    private ChatItem ToItem(ChatMessageDto m) => new(m, _auth.User?.UserName);

    private static string ContentTypeFor(string fileName, bool isVideo) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".mp4" or ".m4v" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        _ => isVideo ? "video/mp4" : "image/jpeg"
    };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

/// <summary>Ekranda bir mesaj.</summary>
public partial class ChatItem : ObservableObject
{
    public int Id { get; }
    public string UserName { get; }
    public string? Text { get; }
    public bool IsAdmin { get; }
    public bool IsMine { get; }
    public bool IsEdited { get; }
    public string Date { get; }
    public string Time { get; }
    public string? ImageUrl { get; }
    public string? VideoUrl { get; }
    public string? AudioUrl { get; }
    public string Initial => string.IsNullOrEmpty(UserName) ? "?" : UserName[..1].ToUpperInvariant();
    public string DateLabel { get; }

    [ObservableProperty] private bool showName;
    [ObservableProperty] private bool showDate;
    [ObservableProperty] private bool isPlaying;

    public ChatItem(ChatMessageDto m, string? me)
    {
        Id = m.Id;
        UserName = m.UserName;
        Text = string.IsNullOrWhiteSpace(m.Message) ? null : m.Message;
        IsAdmin = m.IsAdmin;
        IsMine = me != null && string.Equals(m.UserName, me, StringComparison.Ordinal);
        IsEdited = m.IsEdited;
        Date = m.Date;
        Time = m.Time;
        var url = AppConfig.Absolute(m.AttachmentUrl);
        ImageUrl = m.AttachmentType == "image" ? url : null;
        VideoUrl = m.AttachmentType == "video" ? url : null;
        AudioUrl = m.AttachmentType == "audio" ? url : null;
        DateLabel = MakeDateLabel(m.Date);
    }

    private static string MakeDateLabel(string iso)
    {
        if (!DateTime.TryParse(iso, out var d)) return iso;
        var today = DateTime.Today;
        if (d.Date == today) return "Bu gün";
        if (d.Date == today.AddDays(-1)) return "Dünən";
        string[] months = { "yanvar", "fevral", "mart", "aprel", "may", "iyun", "iyul", "avqust", "sentyabr", "oktyabr", "noyabr", "dekabr" };
        return $"{d.Day} {months[d.Month - 1]}" + (d.Year != today.Year ? $" {d.Year}" : "");
    }
}

/// <summary>Səs yazma (platformadan asılı reallaşdırma — Services/VoiceRecorder.cs).</summary>
public interface IVoiceRecorder
{
    Task StartAsync();
    /// <summary>Yazını dayandırır; fayl yolu və serverin qəbul etdiyi Content-Type (audio/mp4 və s.).</summary>
    Task<VoiceRecording?> StopAsync();
}

public record VoiceRecording(string Path, string ContentType);

public interface IAudioPlayback
{
    Task PlayAsync(string url, Action onEnded);
    Task StopAsync();
}
