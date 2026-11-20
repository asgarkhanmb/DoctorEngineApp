using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>Usta: qrup yarat / redaktə et (ad, təsvir, "bütün istifadəçilər", "yalnız ustalar yazır", üzvlər).</summary>
[QueryProperty(nameof(GroupId), "id")]
public partial class ChatGroupViewModel : BaseViewModel
{
    private readonly ChatClient _chat;
    private List<GroupMemberItem> _all = new();
    private HashSet<string> _existing = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<GroupMemberItem> Users { get; } = new();

    [ObservableProperty] private string? groupId;
    [ObservableProperty] private string? name;
    [ObservableProperty] private string? description;
    [ObservableProperty] private bool allUsers;
    [ObservableProperty] private bool adminsOnly;
    [ObservableProperty] private string? search;
    [ObservableProperty] private string pageTitle = Lang.T("group_new");

    public bool IsEdit => int.TryParse(GroupId, out _);
    public bool ShowMembers => !AllUsers;

    public ChatGroupViewModel(ChatClient chat) => _chat = chat;

    partial void OnAllUsersChanged(bool value) => OnPropertyChanged(nameof(ShowMembers));
    partial void OnSearchChanged(string? value) => Filter();

    public async Task LoadAsync()
    {
        PageTitle = Lang.T(IsEdit ? "group_edit" : "group_new");
        OnPropertyChanged(nameof(IsEdit));
        await RunAsync(async () =>
        {
            if (int.TryParse(GroupId, out var id))
            {
                var g = await _chat.GetGroupAsync(id);
                Name = g.Name;
                Description = g.Description;
                AllUsers = g.AllUsers;
                AdminsOnly = g.AdminsOnly;
                _existing = new HashSet<string>(g.Members.Select(m => m.UserName), StringComparer.OrdinalIgnoreCase);
            }
            var users = await _chat.GetUsersAsync();
            _all = users.Select(u => new GroupMemberItem(u, _existing.Contains(u.UserName))).ToList();
            Filter();
        });
    }

    private void Filter()
    {
        var q = (Search ?? "").Trim();
        Users.Clear();
        foreach (var u in _all.Where(u => q.Length == 0 ||
                     u.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) || u.UserName.Contains(q, StringComparison.OrdinalIgnoreCase)))
            Users.Add(u);
    }

    [RelayCommand]
    private void Toggle(GroupMemberItem item)
    {
        if (item.IsExisting) return;   // mövcud üzvü buradan çıxarmaq olmur (qrup məlumatında)
        item.IsSelected = !item.IsSelected;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name)) { await Alert(Lang.T("group_title"), Lang.T("group_name_required")); return; }
        var members = _all.Where(u => u.IsSelected && !u.IsExisting).Select(u => u.UserName).ToList();
        if (!IsEdit && !AllUsers && members.Count == 0) { await Alert(Lang.T("group_title"), Lang.T("group_members_required")); return; }

        await RunAsync(async () =>
        {
            int? id = int.TryParse(GroupId, out var gid) ? gid : null;
            await _chat.SaveGroupAsync(id, Name!.Trim(), Description, AllUsers, AdminsOnly, members);
            await Shell.Current.GoToAsync("..");
        });
    }
}

public partial class GroupMemberItem : ObservableObject
{
    public string UserName { get; }
    public string FullName { get; }
    public string? AvatarUrl { get; }
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);
    public string Initial => string.IsNullOrWhiteSpace(FullName) ? "?" : FullName.Trim()[..1].ToUpperInvariant();
    public bool IsAdmin { get; }
    public bool IsExisting { get; }
    public string Handle => "@" + UserName;

    [ObservableProperty] private bool isSelected;

    public GroupMemberItem(ChatUserDto u, bool existing)
    {
        UserName = u.UserName;
        FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName : u.FullName;
        AvatarUrl = AppConfig.Absolute(u.AvatarUrl);
        IsAdmin = u.IsAdmin;
        IsExisting = existing;
        IsSelected = existing;
    }
}
