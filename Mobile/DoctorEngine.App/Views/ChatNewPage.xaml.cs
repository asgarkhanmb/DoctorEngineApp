using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ChatNewPage : ContentPage
{
    private readonly ChatNewViewModel _vm;

    public ChatNewPage(ChatNewViewModel vm)
    {
        InitializeComponent();
        this.UseBottomNav("chat", floating: false);   // alt menyu bütün səhifələrdə (aid olduğu bölmə seçilmiş görünür)
        this.CenterOnWideScreens();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "ChatNewPage"); }
    }

    private async void OnBack(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
