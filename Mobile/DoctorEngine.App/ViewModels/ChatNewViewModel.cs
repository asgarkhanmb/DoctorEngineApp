using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;

namespace DoctorEngine.App.ViewModels;

/// <summary>
/// Yeni çat ("+"): istifadəçinin adına toxun — yazışma açılır.
/// Müştəri ↔ müştəri və usta ↔ usta — şəxsi yazışma; müştəri ↔ usta — "ustalarla yazışma".
/// </summary>
public partial class ChatNewViewModel : BaseViewModel
{
    private readonly ChatClient _chat;
    private readonly ChatViewModel _chatVm;
    private readonly AuthService _auth;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<PersonItem> People { get; } = new();

    [ObservableProperty] private string? search;
    [ObservableProperty] private bool isEmpty;

    public bool IsAdmin => _auth.User?.IsAdmin == true;

    public ChatNewViewModel(ChatClient chat, ChatViewModel chatVm, AuthService auth)
    {
        _chat = chat;
        _chatVm = chatVm;
        _auth = auth;
    }

    public Task LoadAsync()
    {
        OnPropertyChanged(nameof(IsAdmin));
        return LoadPeopleAsync();
    }

    partial void OnSearchChanged(string? value)
    {
        // Yazdıqca hər hərfdə sorğu getməsin
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = Task.Delay(300, cts.Token).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(async () => await LoadPeopleAsync()),
            cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    private async Task LoadPeopleAsync()
    {
        await RunAsync(async () =>
        {
            var list = await _chat.GetPeopleAsync(Search);
            People.Clear();
            foreach (var u in list) People.Add(new PersonItem(u));
            IsEmpty = People.Count == 0;
        });
    }

    [RelayCommand]
    private async Task Open(PersonItem person)
    {
        if (person == null || IsBusy) return;
        string? conversation = null;
        await RunAsync(async () => conversation = await _chat.StartDirectAsync(person.UserName));
        if (conversation == null) return;
        await Shell.Current.GoToAsync("..");
        await _chatVm.OpenDirectAsync(conversation, person.FullName, person.AvatarUrlRaw, person.Online);
    }

    [RelayCommand]
    private async Task NewGroup()
    {
        await Shell.Current.GoToAsync("..");
        await Shell.Current.GoToAsync("chat-group");
    }

    [RelayCommand]
    private async Task Broadcast()
    {
        await Shell.Current.GoToAsync("..");
        await _chatVm.BroadcastCommand.ExecuteAsync(null);
    }
}

public class PersonItem
{
    public string UserName { get; }
    public string FullName { get; }
    public string? AvatarUrl { get; }
    public string? AvatarUrlRaw { get; }
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);
    public string Initial => string.IsNullOrWhiteSpace(FullName) ? "?" : FullName.Trim()[..1].ToUpperInvariant();
    public bool IsAdmin { get; }
    public bool Online { get; }
    public string Subtitle => Online ? Lang.T("chat_peer_online") : "@" + UserName;

    public PersonItem(Contracts.ChatUserDto u)
    {
        UserName = u.UserName;
        FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName : u.FullName;
        AvatarUrlRaw = u.AvatarUrl;
        AvatarUrl = AppConfig.Absolute(u.AvatarUrl);
        IsAdmin = u.IsAdmin;
        Online = u.Online;
    }
}
