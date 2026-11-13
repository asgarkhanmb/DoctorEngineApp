using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>AI köməkçi — saytla eyni: cavab hissə-hissə yazılır, "Dayandır" ilə kəsmək olar.</summary>
public partial class AiViewModel : BaseViewModel
{
    private readonly AiClient _ai;
    private CancellationTokenSource? _cts;

    public ObservableCollection<AiItem> Messages { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAsk))]
    private string? question;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAsk))]
    private bool isAnswering;

    public bool CanAsk => !IsAnswering && !string.IsNullOrWhiteSpace(Question);
    public bool IsEmpty => Messages.Count == 0;

    public IReadOnlyList<string> Suggestions { get; } = new[]
    {
        "Mühərrikdən tıqqıltı səsi gəlir, səbəbi nə ola bilər?",
        "Yağ dəyişməyi nə vaxt etməliyəm?",
        "Mühərrik çox qızır, nə etməliyəm?"
    };

    public event Action<AiItem>? ScrollToRequested;

    public AiViewModel(AiClient ai)
    {
        _ai = ai;
        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand] private Task UseSuggestion(string text) { Question = text; return Ask(); }

    [RelayCommand]
    private async Task Ask()
    {
        var q = Question?.Trim();
        if (string.IsNullOrEmpty(q) || IsAnswering) return;
        Question = null;

        Messages.Add(new AiItem("user", q));
        var answer = new AiItem("assistant", "");
        Messages.Add(answer);
        ScrollToRequested?.Invoke(answer);

        // Son 10 mesaj kontekst kimi göndərilir (serverdəki limitlərlə uyğun)
        var history = Messages.Where(m => m != answer && !m.IsError).TakeLast(10)
            .Select(m => new AiMessageDto(m.Role, m.Content)).ToList();

        IsAnswering = true;
        _cts = new CancellationTokenSource();
        try
        {
            await foreach (var chunk in _ai.AskAsync(history, _cts.Token))
            {
                answer.Content += chunk;
                ScrollToRequested?.Invoke(answer);
            }
            if (string.IsNullOrWhiteSpace(answer.Content))
                answer.Content = "Bağışlayın, cavab hazırlaya bilmədim. Sualı bir az fərqli yazmağa cəhd edin.";
        }
        catch (OperationCanceledException)
        {
            if (string.IsNullOrEmpty(answer.Content)) Messages.Remove(answer);
        }
        catch (ApiException ex)
        {
            answer.Content = ex.Message;
            answer.IsError = true;
        }
        finally
        {
            IsAnswering = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand] private void Stop() => _cts?.Cancel();

    [RelayCommand]
    private void Clear()
    {
        _cts?.Cancel();
        Messages.Clear();
    }
}

public partial class AiItem : ObservableObject
{
    public string Role { get; }
    public bool IsUser => Role == "user";
    [ObservableProperty] private string content;
    [ObservableProperty] private bool isError;

    public AiItem(string role, string content)
    {
        Role = role;
        this.content = content;
    }
}
