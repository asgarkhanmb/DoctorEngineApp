using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>Rəy və ya cavab (siyahıda cavablar öz rəyinin altında, içəri çəkilmiş göstərilir).</summary>
public partial class ReelCommentItem : ObservableObject
{
    public ReelCommentItem(ReelCommentDto dto)
    {
        Id = dto.Id;
        ParentId = dto.ParentId;
        UserName = dto.UserName;
        FullName = dto.FullName;
        AvatarUrl = AppConfig.Absolute(dto.AvatarUrl);
        Sticker = ReelStickers.Emoji(dto.Sticker);
        ReplyToName = dto.ReplyToName;
        CanEdit = dto.CanEdit;
        CanDelete = dto.CanDelete;
        CreatedAt = DateTime.TryParse(dto.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Now;
        text = dto.Text;
        edited = dto.Edited;
        likes = dto.Likes;
        liked = dto.Liked;
    }

    public int Id { get; }
    public int? ParentId { get; }
    public bool IsReply => ParentId != null;
    public string UserName { get; }
    public string FullName { get; }
    public string? AvatarUrl { get; }
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);
    public string Initial => string.IsNullOrEmpty(FullName) ? "?" : FullName[..1].ToUpperInvariant();
    public string? Sticker { get; }
    public bool HasSticker => Sticker != null;
    public string? ReplyToName { get; }
    public string? ReplyToText => ReplyToName == null ? null : "@" + ReplyToName + " ";
    public bool CanEdit { get; }
    public bool CanDelete { get; }
    public DateTime CreatedAt { get; }
    public double AvatarSize => IsReply ? 26 : 34;
    public Thickness Indent => IsReply ? new Thickness(58, 6, 10, 6) : new Thickness(14, 10, 10, 6);

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasText))] private string text;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Meta))] private bool edited;
    [ObservableProperty] private bool isHighlighted;   // cavab verilən / redaktə olunan
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LikesText), nameof(HasLikes))] private int likes;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HeartIcon))] private bool liked;

    public string HeartIcon => Liked ? "ic_heart_r.png" : "ic_heart_g.png";
    public bool HasLikes => Likes > 0;
    public string LikesText => ReelItem.Compact(Likes);

    public bool HasText => !string.IsNullOrWhiteSpace(Text);
    public string Meta => Edited ? $"{ReelItem.Ago(CreatedAt)} · {Lang.T("reels_edited")}" : ReelItem.Ago(CreatedAt);

    public void Refresh() => OnPropertyChanged(nameof(Meta));
}

public record StickerItem(string Code, string Emoji);

/// <summary>Videonun rəyləri — aşağıdan açılan pəncərə (ReelCommentsPage): cavab, stiker, redaktə, silmə.</summary>
public partial class ReelCommentsViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ReelsService _reels;
    private readonly AuthService _auth;

    public ObservableCollection<ReelCommentItem> Comments { get; } = [];
    public IReadOnlyList<StickerItem> Stickers { get; } = ReelStickers.All.Select(s => new StickerItem(s.Code, s.Emoji)).ToList();

    [ObservableProperty] private ReelItem? reel;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SendCommand))] private string? text;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SendCommand))] private bool isSending;
    [ObservableProperty] private bool hasMore;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private int total;
    [ObservableProperty] private bool showStickers;

    /// <summary>Cavab yazılır (yazı sahəsinin üstündə zolaq).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasMode), nameof(ModeText), nameof(ModeIcon))] private ReelCommentItem? replyingTo;
    /// <summary>Öz rəyini redaktə edir.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasMode), nameof(ModeText), nameof(ModeIcon), nameof(IsEditing))] private ReelCommentItem? editing;

    public bool HasMode => ReplyingTo != null || Editing != null;
    public bool IsEditing => Editing != null;
    public string ModeText => Editing != null ? Lang.T("reels_editing") : ReplyingTo != null ? Lang.T("reels_replying_to", ReplyingTo.FullName) : "";
    public string ModeIcon => Editing != null ? "ic_edit_g.png" : "ic_reply2_g.png";

    public bool IsLoggedIn => _auth.IsLoggedIn;
    public string Title => Total > 0 ? $"{Lang.T("reels_comments")} · {ReelItem.Compact(Total)}" : Lang.T("reels_comments");
    public string MyInitial => string.IsNullOrEmpty(_auth.User?.FullName) ? "?" : _auth.User!.FullName[..1].ToUpperInvariant();
    public string? MyAvatar => AppConfig.Absolute(_auth.User?.AvatarUrl);
    public bool HasMyAvatar => !string.IsNullOrEmpty(MyAvatar);

    /// <summary>Yazı sahəsinə fokus (cavab / redaktə başlayanda) — səhifə dinləyir.</summary>
    public event EventHandler? FocusInput;

    public ReelCommentsViewModel(ReelsService reels, AuthService auth)
    {
        _reels = reels;
        _auth = auth;
    }

    partial void OnTotalChanged(int value)
    {
        OnPropertyChanged(nameof(Title));
        if (Reel != null) Reel.Comments = value;
    }

    partial void OnReplyingToChanged(ReelCommentItem? oldValue, ReelCommentItem? newValue)
    {
        if (oldValue != null) oldValue.IsHighlighted = false;
        if (newValue != null) newValue.IsHighlighted = true;
    }

    partial void OnEditingChanged(ReelCommentItem? oldValue, ReelCommentItem? newValue)
    {
        if (oldValue != null) oldValue.IsHighlighted = false;
        if (newValue != null) newValue.IsHighlighted = true;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("reel", out var r) && r is ReelItem item)
        {
            Reel = item;
            Total = item.Comments;
        }
    }

    public async Task LoadAsync()
    {
        if (Reel == null) return;
        await RunAsync(async () =>
        {
            var page = await _reels.CommentsAsync(Reel.Id);
            Comments.SyncWith(Flatten(page.Items));
            HasMore = page.HasMore;
            Total = page.Total;
            IsEmpty = Comments.Count == 0;
        });
    }

    private static IEnumerable<ReelCommentItem> Flatten(IEnumerable<ReelCommentDto> items)
    {
        foreach (var c in items)
        {
            yield return new ReelCommentItem(c);
            foreach (var r in c.Replies) yield return new ReelCommentItem(r);
        }
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (Reel == null || !HasMore || Comments.Count == 0) return;
        var lastTop = Comments.LastOrDefault(c => !c.IsReply);
        if (lastTop == null) return;
        try
        {
            var page = await _reels.CommentsAsync(Reel.Id, lastTop.Id);
            foreach (var c in Flatten(page.Items)) Comments.Add(c);
            HasMore = page.HasMore;
        }
        catch (ApiException ex) { ErrorMessage = ex.Message; }
    }

    // ================= YAZ / CAVAB / REDAKTƏ =================

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(Text);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task Send()
    {
        if (Reel == null) return;
        if (!_auth.IsLoggedIn) { await Login(); return; }
        IsSending = true;
        try
        {
            if (Editing is { } edit)
            {
                var dto = await _reels.EditCommentAsync(edit.Id, Text!.Trim());
                edit.Text = dto.Text;
                edit.Edited = dto.Edited;
                Editing = null;
            }
            else
            {
                var dto = await _reels.AddCommentAsync(Reel.Id, Text!.Trim(), ReplyingTo?.Id);
                Added(dto);
            }
            Text = null;
        }
        catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
        finally { IsSending = false; }
    }

    [RelayCommand]
    private async Task SendSticker(StickerItem? sticker)
    {
        if (sticker == null || Reel == null || IsSending) return;
        if (!_auth.IsLoggedIn) { await Login(); return; }
        IsSending = true;
        try
        {
            Added(await _reels.AddCommentAsync(Reel.Id, null, ReplyingTo?.Id, sticker.Code));
            ShowStickers = false;
        }
        catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
        finally { IsSending = false; }
    }

    /// <summary>Yeni rəy əvvələ; cavab — öz rəyinin cavablarının sonuna.</summary>
    private void Added(ReelCommentDto dto)
    {
        var item = new ReelCommentItem(dto);
        if (dto.ParentId is int parentId && Comments.FirstOrDefault(c => c.Id == parentId) is { } parent)
        {
            var idx = Comments.IndexOf(parent) + 1;
            while (idx < Comments.Count && Comments[idx].ParentId == parentId) idx++;
            Comments.Insert(idx, item);
        }
        else Comments.Insert(0, item);
        ReplyingTo = null;
        IsEmpty = false;
        Total++;
    }

    /// <summary>Rəyi bəyən / geri al — dərhal göstərilir, server cavabı ilə düzəldilir.</summary>
    [RelayCommand]
    private async Task LikeComment(ReelCommentItem? comment)
    {
        if (comment == null) return;
        if (!_auth.IsLoggedIn) { await Login(); return; }
        var like = !comment.Liked;
        comment.Liked = like;
        comment.Likes = Math.Max(0, comment.Likes + (like ? 1 : -1));
        try
        {
            var res = await _reels.LikeCommentAsync(comment.Id, like);
            comment.Liked = res.Liked;
            comment.Likes = res.Likes;
        }
        catch (ApiException ex)
        {
            comment.Liked = !like;
            comment.Likes = Math.Max(0, comment.Likes + (like ? -1 : 1));
            await Alert(Lang.T("common_error"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task Reply(ReelCommentItem? comment)
    {
        if (comment == null) return;
        if (!_auth.IsLoggedIn) { await Login(); return; }
        Editing = null;
        ReplyingTo = comment;
        FocusInput?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Edit(ReelCommentItem? comment)
    {
        if (comment == null || !comment.CanEdit) return;
        ReplyingTo = null;
        Editing = comment;
        Text = comment.Text;
        ShowStickers = false;
        FocusInput?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CancelMode()
    {
        if (Editing != null) Text = null;
        Editing = null;
        ReplyingTo = null;
    }

    [RelayCommand]
    private void ToggleStickers()
    {
        if (Editing != null) return;   // stikeri redaktə etmək olmur
        ShowStickers = !ShowStickers;
    }

    [RelayCommand]
    private async Task Delete(ReelCommentItem? comment)
    {
        if (comment == null || !comment.CanDelete) return;
        if (!await Confirm(Lang.T("reels_comment_delete"),
                Lang.T(comment.IsReply ? "reels_comment_delete_confirm" : "reels_comment_delete_thread"), Lang.T("reels_comment_delete"))) return;
        try
        {
            await _reels.DeleteCommentAsync(comment.Id);
            // Yuxarı səviyyəli rəy silinəndə cavabları da gedir
            var removed = Comments.Where(c => c == comment || c.ParentId == comment.Id).ToList();
            foreach (var c in removed) Comments.Remove(c);
            if (Editing != null && removed.Contains(Editing)) { Editing = null; Text = null; }
            if (ReplyingTo != null && removed.Contains(ReplyingTo)) ReplyingTo = null;
            IsEmpty = Comments.Count == 0;
            Total = Math.Max(0, Total - removed.Count);
        }
        catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
    }

    [RelayCommand]
    private async Task Login()
    {
        await Shell.Current.GoToAsync("..");
        await Shell.Current.GoToAsync("//profile");
    }

    [RelayCommand]
    private Task Close() => Shell.Current.GoToAsync("..");
}
