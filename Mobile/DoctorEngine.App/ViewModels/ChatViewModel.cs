using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>
/// Şəxsi çat (WhatsApp kimi).
///  - Müştəri: öz yazışması (ustalarla).
///  - Usta (admin): "Çatlar" siyahısı → müştərinin yazışması.
/// Mətn, şəkil, video, səs; cavab, reaksiya, redaktə / silmə; ✓ / ✓✓ / mavi ✓✓; "yazır…", onlayn; axtarış; emoji.
/// </summary>
public partial class ChatViewModel : BaseViewModel
{
    public static readonly string[] EmojiSet =
    {
        "😀", "😂", "😊", "😍", "🥰", "😘", "😉", "😎", "🤔", "😅", "😢", "😭", "😡", "😱", "🥺", "😴",
        "👍", "👎", "👌", "🙏", "👏", "💪", "🤝", "✌️", "❤️", "💔", "🔥", "⭐", "✅", "❌", "❗", "❓",
        "🚗", "🏎️", "🚙", "🛻", "🔧", "🔩", "🛠️", "⚙️", "🔋", "⛽", "🛞", "🛢️", "📍", "⏰", "💰", "📞"
    };

    private readonly ChatClient _chat;
    private readonly AuthService _auth;
    private readonly CallCoordinator _calls;
    private CancellationTokenSource? _listReloadCts;
    private readonly IVoiceRecorder _recorder;
    private readonly IAudioPlayback _playback;
    private bool _hasMore;
    private bool _connected;
    private DateTime _recordStarted;
    private IDispatcherTimer? _recTimer;
    private IDispatcherTimer? _typingTimer;
    private DateTime _typingSentAt;
    private bool _typingOn;
    private CancellationTokenSource? _peerTypingCts;
    private CancellationTokenSource? _searchCts;
    private ChatPeerDto? _peer;
    private bool _visible;

    public ObservableCollection<ChatItem> Messages { get; } = new();
    public ObservableCollection<ConversationItem> Conversations { get; } = new();
    public ObservableCollection<ChatItem> SearchResults { get; } = new();
    public IReadOnlyList<string> Emojis => EmojiSet;

    /// <summary>Açıq yazışma: müştəri üçün null (server özü bilir), usta üçün müştərinin adı.</summary>
    [ObservableProperty] private string? conversation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private string? text;

    private string _state = "connecting";
    [ObservableProperty] private string status = Lang.T("chat_connecting");
    [ObservableProperty] private bool isOnline;
    [ObservableProperty] private bool isSending;
    [ObservableProperty] private bool isRecording;
    [ObservableProperty] private string recordTime = "0:00";
    [ObservableProperty] private bool canLoadMore;
    [ObservableProperty] private ChatItem? editing;

    // Qarşı tərəf (başlıq)
    [ObservableProperty] private string peerTitle = Lang.T("chat_header");
    [ObservableProperty] private string? peerAvatar;
    [ObservableProperty] private bool peerOnline;
    [ObservableProperty] private bool peerTyping;
    [ObservableProperty] private string? peerTypingName;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeerStatus), nameof(ShowComposer), nameof(ShowReadOnly))]
    private bool peerIsGroup;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowComposer), nameof(ShowReadOnly))]
    private bool peerCanWrite = true;
    /// <summary>İki istifadəçinin şəxsi yazışması — başlıqda adın baş hərfi, "ustalar" mətni yox.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeerStatus), nameof(PrivateNote), nameof(PeerShowsSupport), nameof(PeerShowsInitial))]
    private bool peerIsDirect;

    /// <summary>Başlıq ikonu: müştəri üçün ustalar (🔧), usta və şəxsi yazışmada — adın baş hərfi.</summary>
    public bool PeerShowsSupport => !IsAdmin && !PeerIsGroup && !PeerIsDirect && !HasPeerAvatar;
    public bool PeerShowsInitial => (IsAdmin || PeerIsDirect) && !PeerIsGroup && !HasPeerAvatar;

    /// <summary>"Yalnız ustalar yaza bilər" qrupunda müştəri yaza bilmir.</summary>
    public bool ShowComposer => ShowConversation && IsLoggedIn && PeerCanWrite;
    public bool ShowReadOnly => ShowConversation && IsLoggedIn && !PeerCanWrite;

    // Cavab
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReplying))]
    private ChatItem? replyTo;

    // Emoji / axtarış
    [ObservableProperty] private bool showEmoji;
    [ObservableProperty] private bool isSearching;
    [ObservableProperty] private string? searchText;
    [ObservableProperty] private bool searchEmpty;

    // Usta: siyahı
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowList), nameof(ShowConversation), nameof(ShowComposer), nameof(ShowReadOnly))]
    private bool isListMode;
    [ObservableProperty] private string? conversationFilter;
    [ObservableProperty] private bool unreadOnly;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTotalUnread))]
    private int totalUnread;
    public bool HasTotalUnread => TotalUnread > 0;

    public bool IsAdmin => _auth.User?.IsAdmin == true;
    public bool IsLoggedIn => _auth.IsLoggedIn;
    public bool IsGuest => !_auth.IsLoggedIn;
    public bool ShowList => IsLoggedIn && IsListMode;
    public bool ShowConversation => IsLoggedIn && !IsListMode;
    public bool CanSend => !string.IsNullOrWhiteSpace(Text) && !IsSending;
    public bool IsEmpty => IsLoggedIn && !IsBusy && Messages.Count == 0;
    public bool IsReplying => ReplyTo != null;
    public string PeerInitial => string.IsNullOrWhiteSpace(PeerTitle) ? "?" : PeerTitle.Trim()[..1].ToUpperInvariant();
    public bool HasPeerAvatar => !string.IsNullOrEmpty(PeerAvatar);
    public string PrivateNote => PeerIsDirect
        ? Lang.T("chat_private_note_direct", PeerTitle)
        : Lang.T(IsAdmin ? "chat_private_note_admin" : "chat_private_note");
    public string ReplyName => ReplyTo == null ? "" : ReplyTo.SenderLabel;
    public string ReplyText => ReplyTo?.PreviewText ?? "";

    /// <summary>Başlıq altındakı sətir: "yazır…", "onlayn", "son görülmə bu gün 14:20" və ya bağlantı vəziyyəti.</summary>
    public string PeerStatus
    {
        get
        {
            if (IsGuest) return Lang.T("chat_guest_status");
            if (_state != "online") return Status;
            if (PeerTyping) return PeerIsGroup && !string.IsNullOrEmpty(PeerTypingName)
                ? Lang.T("chat_typing_name", PeerTypingName) : Lang.T("chat_typing");
            if (PeerIsGroup) return _peer?.AllUsers == true ? Lang.T("group_all_users") : Lang.T("group_members", _peer?.MemberCount ?? 0);
            if (PeerOnline) return Lang.T("chat_peer_online");
            if (_peer?.LastSeen is string seen) return LastSeenLabel(seen);
            return IsAdmin || PeerIsDirect ? "" : Lang.T("chat_support_soon");
        }
    }

    /// <summary>Yeni mesaj gələndə siyahı aşağı sürüşsün (View dinləyir).</summary>
    public event Action<ChatItem>? ScrollToRequested;

    public ChatViewModel(ChatClient chat, AuthService auth, IVoiceRecorder recorder, IAudioPlayback playback, CallCoordinator calls)
    {
        _chat = chat;
        _auth = auth;
        _calls = calls;
        _chat.ConversationChanged += conv => ScheduleListReload(conv);
        _recorder = recorder;
        _playback = playback;

        _chat.MessageReceived += OnReceived;
        _chat.MessageEdited += OnEdited;
        _chat.MessageDeleted += id => Remove(id);
        _chat.ReceiptReceived += OnReceipt;
        _chat.TypingReceived += OnTyping;
        _chat.PresenceReceived += OnPresence;
        _chat.ConversationUpdated += OnConversationUpdated;
        _chat.ConversationRemoved += async c =>
        {
            var row = Conversations.FirstOrDefault(x => x.Conversation == c);
            if (row != null) Conversations.Remove(row);
            UpdateTotalUnread();
            if (c == Conversation)
            {
                await BackToList();
                await Alert(Lang.T("group_title"), Lang.T("group_removed"));
            }
        };
        _chat.ConversationCleared += async c =>
        {
            // Açıq yazışma silinibsə — mesajları yenidən yüklə (köhnələr artıq görünmür)
            if (c == Conversation && !IsListMode) { Messages.Clear(); await LoadConversationAsync(); }
            await LoadConversationsAsync(silent: true);
        };
        _chat.StateChanged += s =>
        {
            IsOnline = s == "online";
            _state = s;
            UpdateStatus();
        };
        _auth.Changed += async (_, _) =>
        {
            NotifyAuth();
            if (!_auth.IsLoggedIn)
            {
                Messages.Clear();
                Conversations.Clear();
                Conversation = null;
                await _chat.DisconnectAsync();
                _connected = false;
            }
        };
        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        IsListMode = IsAdmin;
    }

    /// <summary>Qrup / yazışma dəyişdi — siyahını bir az gözləyib yenilə (bir neçə hadisə bir dəfə).</summary>
    private void ScheduleListReload(string conversation)
    {
        _listReloadCts?.Cancel();
        var cts = _listReloadCts = new CancellationTokenSource();
        _ = Task.Delay(500, cts.Token).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadConversationsAsync(silent: true);
            // Açıq qrupun adı / üzvləri / "yalnız ustalar" dəyişibsə — başlıq yenilənsin
            if (conversation == Conversation && IsGroupKey(conversation))
            {
                try { ApplyPeer((await _chat.GetPageAsync(Conversation, take: 1)).Peer); } catch { }
            }
        }), cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    public static bool IsGroupKey(string? c) => c != null && c.StartsWith("g:", StringComparison.Ordinal);
    /// <summary>İki istifadəçinin şəxsi yazışması ("d:{id}").</summary>
    public static bool IsDirectKey(string? c) => c != null && c.StartsWith("d:", StringComparison.Ordinal);
    /// <summary>Üzvlərə əsaslanan yazışma (qrup və ya şəxsi): "mənim mesajım" — mənim adımla göndərilən.</summary>
    public static bool IsMemberKey(string? c) => IsGroupKey(c) || IsDirectKey(c);

    private void NotifyAuth()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsGuest));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(ShowConversation));
        OnPropertyChanged(nameof(ShowComposer));
        OnPropertyChanged(nameof(ShowReadOnly));
        OnPropertyChanged(nameof(PrivateNote));
        OnPropertyChanged(nameof(PeerStatus));
    }

    partial void OnPeerTitleChanged(string value) => OnPropertyChanged(nameof(PeerInitial));
    partial void OnPeerAvatarChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPeerAvatar));
        OnPropertyChanged(nameof(PeerShowsSupport));
        OnPropertyChanged(nameof(PeerShowsInitial));
    }
    partial void OnPeerIsGroupChanged(bool value)
    {
        OnPropertyChanged(nameof(PeerShowsSupport));
        OnPropertyChanged(nameof(PeerShowsInitial));
    }
    partial void OnPeerOnlineChanged(bool value) => OnPropertyChanged(nameof(PeerStatus));
    partial void OnPeerTypingChanged(bool value) => OnPropertyChanged(nameof(PeerStatus));
    partial void OnReplyToChanged(ChatItem? value)
    {
        OnPropertyChanged(nameof(ReplyName));
        OnPropertyChanged(nameof(ReplyText));
    }
    partial void OnConversationFilterChanged(string? value) => ApplyConversationFilter();
    partial void OnUnreadOnlyChanged(bool value) => ApplyConversationFilter();

    private void UpdateStatus()
    {
        Status = _state switch
        {
            "online" => Lang.T("chat_online"),
            "connecting" => Lang.T("chat_connecting"),
            _ => Lang.T("chat_offline")
        };
        OnPropertyChanged(nameof(PeerStatus));
    }

    protected override void OnLanguageChanged()
    {
        UpdateStatus();
        OnPropertyChanged(nameof(PrivateNote));
        foreach (var m in Messages) m.RefreshLabels();
        foreach (var c in Conversations) c.Refresh();
    }

    // ================= EKRAN =================

    /// <summary>Ekran açılanda: siyahını / mesajları yüklə, canlı bağlantını qur, oxunmuş kimi qeyd et.</summary>
    public async Task AppearingAsync()
    {
        _visible = true;
        NotifyAuth();
        if (!IsLoggedIn) return;

        if (!_connected)
        {
            _connected = true;
            _ = _chat.ConnectAsync();
        }

        if (IsAdmin)
        {
            if (Conversation == null) IsListMode = true;
            await LoadConversationsAsync();
            if (!IsListMode && Conversation != null) await MarkReadAsync();
            return;
        }

        // Müştəri: əvvəlcə ustalarla yazışması açılır; "geri" ilə qrupları da olan siyahı
        await LoadConversationsAsync(silent: true);
        if (IsListMode) return;
        if (Messages.Count == 0) await LoadConversationAsync();
        await MarkReadAsync();
    }

    public void Disappearing()
    {
        _visible = false;
        _ = StopTypingAsync();
    }

    private async Task LoadConversationAsync()
    {
        await RunAsync(async () =>
        {
            var page = await _chat.GetPageAsync(Conversation);
            Messages.Clear();
            ApplyPeer(page.Peer);
            foreach (var m in page.Messages) Messages.Add(ToItem(m));
            _hasMore = page.HasMore;
            CanLoadMore = _hasMore;
            Regroup();
        });
        if (Messages.Count > 0) ScrollToRequested?.Invoke(Messages[^1]);
        _ = MarkDeliveredAsync();
    }

    // ================= USTA: SİYAHI =================

    private async Task LoadConversationsAsync(bool silent = false)
    {
        try
        {
            var rows = await _chat.GetConversationsAsync();
            Conversations.Clear();
            foreach (var r in rows)
            {
                var item = new ConversationItem(r);
                // Açıq və görünən yazışma oxunmuş sayılır
                if (r.Conversation == (Conversation ?? _auth.User?.UserName) && !IsListMode && _visible) item.Unread = 0;
                Conversations.Add(item);
            }
            ApplyConversationFilter();
            UpdateTotalUnread();
        }
        catch (ApiException ex) { if (!silent) await Alert(Lang.T("common_error"), ex.Message); }
        catch { /* şəbəkə — növbəti dəfə */ }
    }

    [RelayCommand]
    private async Task OpenConversation(ConversationItem row)
    {
        await CancelRecord();
        CloseSearch();
        ReplyTo = null;
        Editing = null;
        ShowEmoji = false;
        Conversation = row.Conversation;
        PeerTitle = row.Title;
        PeerAvatar = row.AvatarUrl;
        PeerOnline = row.Online;
        PeerTyping = row.Typing;
        PeerIsGroup = row.IsGroup;
        PeerIsDirect = row.IsDirect;
        PeerCanWrite = true;
        Messages.Clear();
        IsListMode = false;
        row.Unread = 0;
        UpdateTotalUnread();
        await LoadConversationAsync();
        await MarkReadAsync();
    }

    [RelayCommand]
    private async Task BackToList()
    {
        await CancelRecord();
        await StopTypingAsync();
        CloseSearch();
        ShowEmoji = false;
        Conversation = null;
        Messages.Clear();
        _peer = null;
        PeerIsGroup = false;
        PeerIsDirect = false;
        PeerCanWrite = true;
        IsListMode = true;
        await LoadConversationsAsync();
    }

    /// <summary>Siyahıda sola sürüşdürüb "Sil" — WhatsApp kimi yazışmanı silmək.</summary>
    [RelayCommand]
    private async Task ClearConversation(ConversationItem row)
    {
        if (row == null) return;
        bool forEveryone;
        if (IsAdmin)
        {
            string forMe = Lang.T("chat_clear_for_me"), forAll = Lang.T("chat_clear_for_all");
            var choice = await Shell.Current.DisplayActionSheetAsync(Lang.T("chat_clear_title_named", row.Title),
                Lang.T("common_cancel"), forAll, forMe);
            if (choice == forMe) forEveryone = false;
            else if (choice == forAll)
            {
                if (!await Confirm(Lang.T("chat_clear_title"), Lang.T("chat_clear_all_text", row.Title), Lang.T("chat_delete_btn"))) return;
                forEveryone = true;
            }
            else return;
        }
        else
        {
            if (!await Confirm(Lang.T("chat_clear_title"), Lang.T("chat_clear_me_text", row.Title), Lang.T("chat_delete_btn"))) return;
            forEveryone = false;
        }

        try
        {
            await _chat.ClearConversationAsync(row.Conversation, forEveryone);
            if (!row.IsSupport && !(forEveryone && row.IsGroup)) Conversations.Remove(row);
            UpdateTotalUnread();
            if (row.Conversation == Conversation && !IsListMode) await BackToList();
            else await LoadConversationsAsync(silent: true);
        }
        catch (ApiException ex) { await Alert(Lang.T("chat_not_deleted"), ex.Message); }
    }

    /// <summary>Yeni çat ("+") — istifadəçi seçmə ekranı.</summary>
    [RelayCommand]
    private Task NewChat() => Shell.Current.GoToAsync("chat-new");

    /// <summary>"Yeni çat" ekranından: seçilən istifadəçi ilə yazışmanı aç.</summary>
    public async Task OpenDirectAsync(string conversation, string title, string? avatarUrl, bool online)
    {
        var row = Conversations.FirstOrDefault(c => c.Conversation == conversation)
                  ?? new ConversationItem(new ChatConversationDto(conversation, title, avatarUrl, online, null, 0, null, false, "", 0, false, false,
                         IsSupport: !IsAdmin && !IsDirectKey(conversation), IsDirect: IsDirectKey(conversation)));
        await OpenConversation(row);
    }

    [ObservableProperty] private bool groupsOnly;
    partial void OnGroupsOnlyChanged(bool value) => ApplyConversationFilter();

    [RelayCommand]
    private void SetFilter(string filter)
    {
        UnreadOnly = filter == "unread";
        GroupsOnly = filter == "groups";
    }

    private void ApplyConversationFilter()
    {
        var q = (ConversationFilter ?? "").Trim();
        foreach (var c in Conversations)
            c.IsVisible = (!UnreadOnly || c.Unread > 0) && (!GroupsOnly || c.IsGroup) &&
                          (q.Length == 0 || c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                           c.Conversation.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateTotalUnread() => TotalUnread = Conversations.Sum(c => c.Unread);

    private void OnConversationUpdated(ChatConversationDto dto)
    {
        if (!IsAdmin) return;
        var existing = Conversations.FirstOrDefault(c => c.Conversation == dto.Conversation);
        var item = new ConversationItem(dto) { Typing = existing?.Typing ?? false };
        // Açıq və görünən yazışma oxunmuş sayılır
        if (dto.Conversation == Conversation && !IsListMode && _visible) item.Unread = 0;
        if (existing != null) Conversations.Remove(existing);
        var index = 0;
        while (index < Conversations.Count && Conversations[index].LastMessageId > dto.LastMessageId) index++;
        Conversations.Insert(index, item);
        ApplyConversationFilter();
        UpdateTotalUnread();
    }

    // ================= YÜKLƏMƏ =================

    [RelayCommand]
    private async Task LoadMore()
    {
        if (!_hasMore || Messages.Count == 0 || IsBusy) return;
        var first = Messages[0];
        await RunAsync(async () =>
        {
            var page = await _chat.GetPageAsync(Conversation, first.Id);
            for (var i = page.Messages.Count - 1; i >= 0; i--) Messages.Insert(0, ToItem(page.Messages[i]));
            _hasMore = page.HasMore;
            CanLoadMore = _hasMore;
            Regroup();
        });
    }

    // ================= GÖNDƏR =================

    [RelayCommand]
    private async Task Send()
    {
        var msg = Text?.Trim();
        if (string.IsNullOrEmpty(msg) || IsSending) return;
        if (msg.Length > ChatLimits.MaxMessageLength) { await Alert(Lang.T("chat_too_long_title"), Lang.T("chat_too_long", ChatLimits.MaxMessageLength)); return; }

        IsSending = true;
        _ = StopTypingAsync();
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
                var sent = await _chat.SendAsync(Conversation, msg, ReplyTo?.Id);
                ReplyTo = null;
                OnReceived(sent);   // SignalR gecikərsə də dərhal görünsün (Id ilə təkrar olmur)
            }
            Text = null;
            ShowEmoji = false;
        }
        catch (ApiException ex) { await Alert(Lang.T("chat_not_sent"), ex.Message); }
        finally { IsSending = false; OnPropertyChanged(nameof(CanSend)); }
    }

    partial void OnTextChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { _ = StopTypingAsync(); return; }
        _ = NotifyTypingAsync();
    }

    private async Task NotifyTypingAsync()
    {
        if (!IsLoggedIn || (IsAdmin && Conversation == null)) return;
        if (!_typingOn || DateTime.UtcNow - _typingSentAt > TimeSpan.FromSeconds(3))
        {
            _typingOn = true;
            _typingSentAt = DateTime.UtcNow;
            await _chat.TypingAsync(IsAdmin || IsMemberKey(Conversation) ? Conversation : null, true);
        }
        _typingTimer ??= CreateTypingTimer();
        _typingTimer.Stop();
        _typingTimer.Start();
    }

    private IDispatcherTimer CreateTypingTimer()
    {
        var t = Application.Current!.Dispatcher.CreateTimer();
        t.Interval = TimeSpan.FromSeconds(3.5);
        t.IsRepeating = false;
        t.Tick += (_, _) => _ = StopTypingAsync();
        return t;
    }

    private async Task StopTypingAsync()
    {
        _typingTimer?.Stop();
        if (!_typingOn) return;
        _typingOn = false;
        await _chat.TypingAsync(IsAdmin || IsMemberKey(Conversation) ? Conversation : null, false);
    }

    [RelayCommand]
    private async Task Attach()
    {
        string pickPhoto = Lang.T("chat_pick_photo"), pickVideo = Lang.T("chat_pick_video"), takePhoto = Lang.T("chat_take_photo");
        var choice = await Shell.Current.DisplayActionSheetAsync(Lang.T("chat_send_file"), Lang.T("common_cancel"), null,
            pickPhoto, pickVideo, takePhoto);
        FileResult? file = null;
        if (choice == pickPhoto)
            file = (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault();
        else if (choice == pickVideo)
            file = (await MediaPicker.Default.PickVideosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault();
        else if (choice == takePhoto && MediaPicker.Default.IsCaptureSupported)
            file = await MediaPicker.Default.CapturePhotoAsync();
        if (file == null) return;

        var isVideo = file.ContentType?.StartsWith("video/") == true ||
                      new[] { ".mp4", ".mov", ".m4v", ".webm" }.Contains(Path.GetExtension(file.FileName).ToLowerInvariant());
        var size = new FileInfo(file.FullPath).Length;
        var max = isVideo ? ChatLimits.MaxVideoBytes : ChatLimits.MaxImageBytes;
        if (size > max)
        {
            await Alert(Lang.T("chat_file_too_big_title"), Lang.T(isVideo ? "chat_video_max" : "chat_photo_max", max / 1024 / 1024));
            return;
        }

        IsSending = true;
        try
        {
            var sent = await _chat.SendAsync(Conversation, Text, ReplyTo?.Id, file, ContentTypeFor(file.FileName, isVideo));
            Text = null;
            ReplyTo = null;
            OnReceived(sent);
        }
        catch (ApiException ex) { await Alert(Lang.T("chat_not_sent"), ex.Message); }
        finally { IsSending = false; }
    }

    // ================= EMOJİ =================

    [RelayCommand] private void ToggleEmoji() => ShowEmoji = !ShowEmoji;

    [RelayCommand] private void InsertEmoji(string emoji) => Text = (Text ?? "") + emoji;

    // ================= SƏS MESAJI =================

    [RelayCommand]
    private async Task ToggleRecord()
    {
        if (IsRecording) { await StopAndSendAsync(); return; }

        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            await Alert(Lang.T("mic_title"), Lang.T("mic_permission"));
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
            await Alert(Lang.T("mic_title"), Lang.T("mic_failed", ex.Message));
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
            if (seconds < 1) { await Alert(Lang.T("voice_too_short_title"), Lang.T("voice_too_short")); return; }
            if (new FileInfo(rec.Path).Length > ChatLimits.MaxAudioBytes) { await Alert(Lang.T("voice_too_big_title"), Lang.T("voice_too_big")); return; }
            IsSending = true;
            var sent = await _chat.SendAudioAsync(Conversation, rec.Path, rec.ContentType, ReplyTo?.Id);
            ReplyTo = null;
            OnReceived(sent);
        }
        catch (ApiException ex) { await Alert(Lang.T("chat_not_sent"), ex.Message); }
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

    // ================= MENYU: REAKSİYA / CAVAB / KOPYALA / REDAKTƏ / SİL =================

    [RelayCommand]
    private async Task Options(ChatItem item)
    {
        string reply = Lang.T("chat_reply"), edit = Lang.T("chat_edit"), delete = Lang.T("chat_delete"), copy = Lang.T("chat_copy");
        // WhatsApp kimi: yuxarıda tez reaksiyalar, sonra əməliyyatlar
        var mine = item.MyReaction;
        var actions = ChatReactions.Quick.Select(e => e == mine ? e + "  ✓" : e).ToList();
        actions.Add(reply);
        if (item.Text != null) actions.Add(copy);
        if (item.IsMine && item.Text != null) actions.Add(edit);
        if (item.IsMine || IsAdmin) actions.Add(delete);

        var choice = await Shell.Current.DisplayActionSheetAsync(null, Lang.T("common_cancel"), null, actions.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == Lang.T("common_cancel")) return;

        var emoji = ChatReactions.Quick.FirstOrDefault(e => choice == e || choice == e + "  ✓");
        if (emoji != null) { await React(item, emoji); return; }

        if (choice == reply) { Reply(item); return; }
        if (choice == edit)
        {
            Editing = item;
            ReplyTo = null;
            Text = item.Text;
        }
        else if (choice == delete)
        {
            if (!await Confirm(Lang.T("chat_delete_confirm_title"), item.IsMine
                    ? Lang.T("chat_delete_confirm_mine")
                    : Lang.T("chat_delete_confirm_admin", item.UserName), Lang.T("chat_delete_btn"))) return;
            try { await _chat.DeleteAsync(item.Id); Remove(item.Id); }
            catch (ApiException ex) { await Alert(Lang.T("chat_not_deleted"), ex.Message); }
        }
        else if (choice == copy)
        {
            await Clipboard.Default.SetTextAsync(item.Text);
        }
    }

    private async Task React(ChatItem item, string emoji)
    {
        try { OnEdited(await _chat.ReactAsync(item.Id, emoji)); }
        catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
    }

    /// <summary>Reaksiya çipinə toxunanda: öz reaksiyamı götür / qoy.</summary>
    [RelayCommand]
    private Task ToggleReaction(ChatItem item) =>
        React(item, item.MyReaction ?? item.Reactions.FirstOrDefault()?.Emoji ?? ChatReactions.Quick[0]);

    [RelayCommand]
    private void Reply(ChatItem item)
    {
        Editing = null;
        ReplyTo = item;
    }

    [RelayCommand] private void CancelReply() => ReplyTo = null;

    [RelayCommand]
    private void CancelEdit()
    {
        Editing = null;
        Text = null;
    }

    /// <summary>Cavab sitatına toxunanda — həmin mesaja keç.</summary>
    [RelayCommand]
    private async Task JumpTo(int id)
    {
        var guard = 0;
        while (Messages.All(m => m.Id != id) && _hasMore && guard++ < 30)
            await LoadMore();
        var target = Messages.FirstOrDefault(m => m.Id == id);
        if (target == null) { await Alert(Lang.T("chat_not_found"), Lang.T("chat_deleted_msg")); return; }
        target.Flash();
        ScrollToRequested?.Invoke(target);
    }

    // ================= AXTARIŞ =================

    [RelayCommand]
    private void ToggleSearch()
    {
        if (IsSearching) { CloseSearch(); return; }
        IsSearching = true;
        SearchText = null;
        SearchResults.Clear();
        SearchEmpty = false;
    }

    private void CloseSearch()
    {
        IsSearching = false;
        SearchText = null;
        SearchResults.Clear();
        SearchEmpty = false;
    }

    partial void OnSearchTextChanged(string? value)
    {
        _searchCts?.Cancel();
        var q = (value ?? "").Trim();
        if (q.Length < 2) { SearchResults.Clear(); SearchEmpty = false; return; }
        var cts = _searchCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            await Task.Delay(350, cts.Token);
            var items = await _chat.SearchAsync(Conversation, q);
            if (cts.IsCancellationRequested) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SearchResults.Clear();
                foreach (var m in items) SearchResults.Add(ToItem(m));
                SearchEmpty = SearchResults.Count == 0;
            });
        }, cts.Token);
    }

    [RelayCommand]
    private async Task OpenSearchResult(ChatItem item)
    {
        CloseSearch();
        await JumpTo(item.Id);
    }

    [RelayCommand] private Task GoToLogin() => Shell.Current.GoToAsync("//profile");

    // ================= ZƏNGLƏR =================

    [RelayCommand] private Task VideoCall() => _calls.StartCallAsync(Conversation, true, PeerTitle);
    [RelayCommand] private Task AudioCall() => _calls.StartCallAsync(Conversation, false, PeerTitle);

    // ================= QRUPLAR / YAYIM (ustalar) =================

    [RelayCommand]
    private Task NewGroup() => Shell.Current.GoToAsync("chat-group");

    [RelayCommand]
    private async Task Broadcast()
    {
        var text = await Shell.Current.DisplayPromptAsync(Lang.T("broadcast_title"), Lang.T("broadcast_text"),
            Lang.T("broadcast_send"), Lang.T("common_cancel"), maxLength: ChatLimits.MaxMessageLength, keyboard: Keyboard.Chat);
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var r = await _chat.BroadcastAsync(text.Trim());
            await Alert(Lang.T("broadcast_title"), Lang.T("broadcast_done", r.Count));
            await LoadConversationsAsync(silent: true);
        }
        catch (ApiException ex) { await Alert(Lang.T("chat_not_sent"), ex.Message); }
    }

    /// <summary>Başlığa toxunanda (qrupda): üzvlər, redaktə, silmə.</summary>
    [RelayCommand]
    private async Task GroupInfo()
    {
        if (!IsGroupKey(Conversation) || !int.TryParse(Conversation![2..], out var id)) return;
        ChatGroupInfoDto g;
        try { g = await _chat.GetGroupAsync(id); }
        catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); return; }

        string members = Lang.T("group_members_list", g.MemberCount), edit = Lang.T("group_edit"), delete = Lang.T("group_delete");
        var actions = new List<string> { members };
        if (IsAdmin) { actions.Add(edit); actions.Add(delete); }
        var choice = await Shell.Current.DisplayActionSheetAsync(g.Name + (string.IsNullOrEmpty(g.Description) ? "" : "\n" + g.Description),
            Lang.T("common_cancel"), null, actions.ToArray());

        if (choice == members)
        {
            var list = g.AllUsers ? Lang.T("group_all_users") : string.Join("\n", g.Members.Select(m => (m.IsAdmin ? "🔧 " : "• ") + m.FullName));
            await Alert(g.Name, list);
        }
        else if (choice == edit)
        {
            await Shell.Current.GoToAsync("chat-group?id=" + id);
        }
        else if (choice == delete)
        {
            if (!await Confirm(Lang.T("group_delete_title"), Lang.T("group_delete_text"), Lang.T("chat_delete_btn"))) return;
            try { await _chat.DeleteGroupAsync(id); await BackToList(); }
            catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
        }
    }

    // ================= CANLI HADİSƏLƏR =================

    private bool IsCurrent(string conversation) =>
        conversation == (Conversation ?? _auth.User?.UserName);

    private void OnReceived(ChatMessageDto dto)
    {
        if (!IsCurrent(dto.Conversation) || IsListMode)
        {
            // Başqa yazışmaya / siyahıda olanda gələn mesaj da "çatdı" (✓✓) sayılır
            if (dto.UserName != _auth.User?.UserName)
                _ = Task.Run(async () => { try { await _chat.MarkDeliveredAsync(dto.Conversation, dto.Id); } catch { } });
            return;
        }
        if (Messages.Any(m => m.Id == dto.Id)) return;
        var item = ToItem(dto);
        // Id sırası ilə düzgün yerə
        var index = Messages.Count;
        while (index > 0 && Messages[index - 1].Id > dto.Id) index--;
        Messages.Insert(index, item);
        Regroup();
        ScrollToRequested?.Invoke(item);
        if (!item.IsOutgoing)
        {
            PeerTyping = false;
            if (_visible) _ = MarkReadAsync(); else _ = MarkDeliveredAsync();
        }
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

    private void OnReceipt(ChatReceiptDto r)
    {
        if (_peer == null || !IsCurrent(r.Conversation)) return;
        var peerSide = IsMemberKey(r.Conversation) ? "group" : IsAdmin ? "user" : "admin";
        if (r.Side != peerSide) return;
        _peer = _peer with
        {
            ReadUpToId = Math.Max(_peer.ReadUpToId, r.ReadUpToId),
            DeliveredUpToId = Math.Max(_peer.DeliveredUpToId, r.DeliveredUpToId)
        };
        foreach (var m in Messages) m.UpdateTick(_peer);
    }

    private void OnTyping(ChatTypingDto t)
    {
        if (t.UserName == _auth.User?.UserName) return;
        var fromPeer = t.IsGroup || (IsAdmin ? !t.FromAdmin : t.FromAdmin);
        if (!fromPeer) return;

        var row = Conversations.FirstOrDefault(c => c.Conversation == t.Conversation);
        if (row != null) { row.TypingName = t.IsGroup ? t.UserName : null; row.Typing = t.IsTyping; }
        if (IsCurrent(t.Conversation)) { PeerTypingName = t.UserName; PeerTyping = t.IsTyping; }

        // 6 saniyə yeni siqnal gəlməsə — "yazır…" söndürülür
        _peerTypingCts?.Cancel();
        if (!t.IsTyping) return;
        var cts = _peerTypingCts = new CancellationTokenSource();
        _ = Task.Delay(6000, cts.Token).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (row != null) row.Typing = false;
            if (IsCurrent(t.Conversation)) PeerTyping = false;
        }), cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    private void OnPresence(ChatPresenceDto p)
    {
        if (p.IsSupport)
        {
            if (IsAdmin) return;
            foreach (var r in Conversations.Where(x => x.IsSupport)) { r.Online = p.Online; r.LastSeen = p.LastSeen; }
            if (_peer == null || PeerIsGroup) return;
            _peer = _peer with { Online = p.Online, LastSeen = p.LastSeen };
            PeerOnline = p.Online;
            OnPropertyChanged(nameof(PeerStatus));
            return;
        }
        if (!IsAdmin) return;
        var row = Conversations.FirstOrDefault(c => c.Conversation == p.Conversation);
        if (row != null) { row.Online = p.Online; row.LastSeen = p.LastSeen; }
        if (_peer != null && p.Conversation == Conversation)
        {
            _peer = _peer with { Online = p.Online, LastSeen = p.LastSeen };
            PeerOnline = p.Online;
            OnPropertyChanged(nameof(PeerStatus));
        }
    }

    private void ApplyPeer(ChatPeerDto? peer)
    {
        if (peer == null) return;
        _peer = peer;
        PeerTitle = peer.Title;
        PeerAvatar = AppConfig.Absolute(peer.AvatarUrl);
        PeerOnline = peer.Online;
        PeerIsGroup = peer.IsGroup;
        PeerIsDirect = peer.IsDirect || IsDirectKey(peer.Conversation);
        PeerCanWrite = peer.CanWrite;
        OnPropertyChanged(nameof(PeerStatus));
    }

    private async Task MarkReadAsync()
    {
        var last = Messages.LastOrDefault();
        if (last == null) return;
        try { await _chat.MarkReadAsync(Conversation, last.Id); } catch { }
    }

    private async Task MarkDeliveredAsync()
    {
        var last = Messages.LastOrDefault(m => !m.IsOutgoing);
        if (last == null) return;
        try { await _chat.MarkDeliveredAsync(Conversation, last.Id); } catch { }
    }

    /// <summary>Tarix ayırıcıları və ardıcıl mesaj qrupları (quyruq yalnız qrupun ilk mesajında).</summary>
    private void Regroup()
    {
        ChatItem? prev = null;
        foreach (var m in Messages)
        {
            m.ShowDate = prev == null || prev.Date != m.Date;
            var sameGroup = !m.ShowDate && prev!.UserName == m.UserName;
            m.ShowName = !m.IsMine && (m.IsAdmin || IsGroupKey(Conversation)) && !sameGroup;
            m.IsFirstInGroup = !sameGroup;
            prev = m;
        }
    }

    private ChatItem ToItem(ChatMessageDto m) => new(m, _auth.User?.UserName, IsAdmin, _peer);

    private static string LastSeenLabel(string iso)
    {
        if (!DateTime.TryParse(iso, out var d)) return "";
        var time = d.ToString("HH:mm");
        var day = d.Date == DateTime.Today ? Lang.T("chat_seen_today", time)
            : d.Date == DateTime.Today.AddDays(-1) ? Lang.T("chat_seen_yesterday", time)
            : d.ToString("d MMMM HH:mm", Lang.Current.Culture);
        return Lang.T("chat_last_seen", day);
    }

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

/// <summary>Ekranda bir mesaj (WhatsApp balonu).</summary>
public partial class ChatItem : ObservableObject
{
    private readonly string? _me;
    private readonly bool _viewerIsAdmin;

    public int Id { get; }
    public string UserName { get; }
    public string? Text { get; }
    public bool IsAdmin { get; }
    public bool IsMine { get; }
    /// <summary>Mənim tərəfimin mesajı (sağda, yaşıl): usta üçün bütün ustalar, müştəri üçün özü.</summary>
    public bool IsOutgoing { get; }
    public bool IsIncoming => !IsOutgoing;
    public bool IsEdited { get; }
    public string Date { get; }
    public string Time { get; }
    public string? ImageUrl { get; }
    public string? VideoUrl { get; }
    public string? AudioUrl { get; }
    public string? AttachmentType { get; }
    public string Initial => string.IsNullOrEmpty(UserName) ? "?" : UserName[..1].ToUpperInvariant();
    public string DateLabel => MakeDateLabel(Date);

    // Cavab sitatı
    public bool HasReply { get; }
    public int ReplyId { get; }
    public string ReplyName { get; private set; } = "";
    public string ReplyText { get; private set; } = "";
    public bool ReplyIsOutgoing { get; }
    private readonly ChatReplyDto? _reply;

    // Reaksiyalar
    public IReadOnlyList<ChatReactionDto> Reactions { get; }
    public bool HasReactions => Reactions.Count > 0;
    public string ReactionsText => string.Join(" ", Reactions.Select(r => r.Users.Count > 1 ? r.Emoji + " " + r.Users.Count : r.Emoji));
    public string? MyReaction => _me == null ? null : Reactions.FirstOrDefault(r => r.Users.Contains(_me))?.Emoji;

    /// <summary>Yalnız emoji (1–3) — WhatsApp kimi böyük.</summary>
    public bool IsJumbo { get; }

    [ObservableProperty] private bool showName;
    [ObservableProperty] private bool showDate;
    [ObservableProperty] private bool isFirstInGroup = true;
    [ObservableProperty] private bool isPlaying;
    [ObservableProperty] private bool isFlashing;
    /// <summary>"✓" | "✓✓" | "" (gələn mesaj)</summary>
    [ObservableProperty] private string tick = "";
    [ObservableProperty] private bool tickRead;

    public string SenderLabel => IsMine ? Lang.T("chat_you") : IsAdmin && !_viewerIsAdmin ? Lang.T("chat_mechanic") : UserName;

    public string PreviewText => Text ?? AttachmentType switch
    {
        "audio" => Lang.T("chat_voice"),
        "video" => Lang.T("chat_video"),
        "image" => Lang.T("chat_photo"),
        _ => ""
    };

    public ChatItem(ChatMessageDto m, string? me, bool viewerIsAdmin, ChatPeerDto? peer)
    {
        _me = me;
        _viewerIsAdmin = viewerIsAdmin;
        Id = m.Id;
        UserName = m.UserName;
        Text = string.IsNullOrWhiteSpace(m.Message) ? null : m.Message;
        IsAdmin = m.IsAdmin;
        IsMine = me != null && string.Equals(m.UserName, me, StringComparison.Ordinal);
        var isGroup = ChatViewModel.IsMemberKey(m.Conversation);
        // Qrupda — yalnız öz mesajlarım sağdadır
        IsOutgoing = isGroup ? IsMine : viewerIsAdmin ? m.IsAdmin : !m.IsAdmin;
        IsEdited = m.IsEdited;
        Date = m.Date;
        Time = m.Time;
        AttachmentType = m.AttachmentType;
        var url = AppConfig.Absolute(m.AttachmentUrl);
        ImageUrl = m.AttachmentType == "image" ? url : null;
        VideoUrl = m.AttachmentType == "video" ? url : null;
        AudioUrl = m.AttachmentType == "audio" ? url : null;
        Reactions = m.Reactions ?? Array.Empty<ChatReactionDto>();
        IsJumbo = Text != null && Text.Length <= 12 && !Text.Any(char.IsLetterOrDigit) &&
                  System.Globalization.StringInfo.ParseCombiningCharacters(Text.Replace(" ", "")).Length is > 0 and <= 3 &&
                  Text.Any(c => char.IsSurrogate(c) || c > 0x2000);

        _reply = m.ReplyTo;
        if (m.ReplyTo is { } r)
        {
            HasReply = true;
            ReplyId = r.Id;
            ReplyIsOutgoing = isGroup ? r.UserName == me : viewerIsAdmin ? r.IsAdmin : !r.IsAdmin;
            SetReplyLabels();
        }

        if (peer != null) UpdateTick(peer);
        else if (IsOutgoing) Tick = "✓";
    }

    private void SetReplyLabels()
    {
        if (_reply == null) return;
        if (_reply.Deleted)
        {
            ReplyName = "";
            ReplyText = Lang.T("chat_deleted_msg");
            return;
        }
        ReplyName = _reply.UserName == _me ? Lang.T("chat_you")
            : _reply.IsAdmin && !_viewerIsAdmin ? Lang.T("chat_mechanic") : _reply.UserName;
        ReplyText = _reply.Message ?? _reply.AttachmentType switch
        {
            "audio" => Lang.T("chat_voice"),
            "video" => Lang.T("chat_video"),
            "image" => Lang.T("chat_photo"),
            _ => ""
        };
    }

    public void UpdateTick(ChatPeerDto peer)
    {
        if (!IsOutgoing) { Tick = ""; return; }
        TickRead = peer.ReadUpToId >= Id;
        Tick = TickRead || peer.DeliveredUpToId >= Id ? "✓✓" : "✓";
    }

    /// <summary>Axtarışdan / sitatdan keçəndə qısa vurğulama.</summary>
    public async void Flash()
    {
        IsFlashing = true;
        await Task.Delay(1600);
        IsFlashing = false;
    }

    /// <summary>Dil dəyişəndə tarix ayırıcısı və sitat yenidən yazılır.</summary>
    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(DateLabel));
        OnPropertyChanged(nameof(SenderLabel));
        OnPropertyChanged(nameof(PreviewText));
        SetReplyLabels();
        OnPropertyChanged(nameof(ReplyName));
        OnPropertyChanged(nameof(ReplyText));
    }

    private static string MakeDateLabel(string iso)
    {
        if (!DateTime.TryParse(iso, out var d)) return iso;
        var today = DateTime.Today;
        if (d.Date == today) return Lang.T("day_today");
        if (d.Date == today.AddDays(-1)) return Lang.T("day_yesterday");
        return d.ToString(d.Year != today.Year ? "d MMMM yyyy" : "d MMMM", Lang.Current.Culture);
    }
}

/// <summary>Ustanın "Çatlar" siyahısında bir sətir.</summary>
public partial class ConversationItem : ObservableObject
{
    public string Conversation { get; }
    public string Title { get; }
    public string? AvatarUrl { get; }
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);
    public string Initial => string.IsNullOrWhiteSpace(Title) ? "?" : Title.Trim()[..1].ToUpperInvariant();
    public int LastMessageId { get; }
    public string? LastMessage { get; }
    public bool LastFromAdmin { get; }
    public string LastDate { get; }
    public string Tick { get; }
    public bool TickRead { get; }
    public bool HasTick => Tick.Length > 0;
    public bool IsGroup { get; }
    /// <summary>Müştəri üçün: ustalarla yazışma sətri.</summary>
    public bool IsSupport { get; }
    public string? LastSender { get; }
    public bool ShowInitial => !HasAvatar && !IsGroup && !IsSupport;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private string? typingName;

    [ObservableProperty] private bool online;
    [ObservableProperty] private string? lastSeen;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread), nameof(UnreadText))]
    private int unread;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private bool typing;
    [ObservableProperty] private bool isVisible = true;

    public bool HasUnread => Unread > 0;
    public string UnreadText => Unread > 99 ? "99+" : Unread.ToString();
    public string SubtitleText => Typing
        ? (string.IsNullOrEmpty(TypingName) ? Lang.T("chat_typing") : Lang.T("chat_typing_name", TypingName))
        : IsGroup && !string.IsNullOrEmpty(LastSender) ? LastSender + ": " + LastMessage : LastMessage ?? "";
    public string TimeLabel => MakeTime(LastDate);

    /// <summary>İki istifadəçinin şəxsi yazışması.</summary>
    public bool IsDirect { get; }

    public ConversationItem(ChatConversationDto c)
    {
        Conversation = c.Conversation;
        IsDirect = c.IsDirect;
        Title = string.IsNullOrWhiteSpace(c.Title) ? c.Conversation : c.Title;
        AvatarUrl = AppConfig.Absolute(c.AvatarUrl);
        Online = c.Online;
        LastSeen = c.LastSeen;
        LastMessageId = c.LastMessageId;
        LastMessage = c.LastMessage;
        LastFromAdmin = c.LastFromAdmin;
        LastDate = c.LastDate;
        Unread = c.Unread;
        IsGroup = c.IsGroup;
        IsSupport = c.IsSupport;
        LastSender = c.LastSender;
        // ✓✓ mənim son mesajımda: usta — ustaların mesajı; müştəri — öz mesajı (ustalar sətri); qrup — öz mesajım
        var mine = c.IsSupport ? !c.LastFromAdmin && !string.IsNullOrEmpty(c.LastDate) : c.LastFromAdmin;
        Tick = mine ? (c.LastRead || c.LastDelivered ? "✓✓" : "✓") : "";
        TickRead = c.LastRead;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(TimeLabel));
        OnPropertyChanged(nameof(SubtitleText));
    }

    private static string MakeTime(string iso)
    {
        if (!DateTime.TryParse(iso, out var d)) return "";
        if (d.Date == DateTime.Today) return d.ToString("HH:mm");
        if (d.Date == DateTime.Today.AddDays(-1)) return Lang.T("day_yesterday");
        return d.ToString("dd.MM.yyyy");
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
