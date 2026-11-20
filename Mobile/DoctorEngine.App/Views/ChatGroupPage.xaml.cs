using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ChatGroupPage : ContentPage
{
    private readonly ChatGroupViewModel _vm;

    public ChatGroupPage(ChatGroupViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "ChatGroupPage"); }
    }

    private async void OnBack(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
