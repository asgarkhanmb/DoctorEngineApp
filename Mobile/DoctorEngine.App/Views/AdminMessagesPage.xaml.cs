using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class AdminMessagesPage : ContentPage
{
    private readonly AdminMessagesViewModel _vm;

    public AdminMessagesPage(AdminMessagesViewModel vm)
    {
        InitializeComponent();
        this.UseBottomNav("profile", floating: false);   // alt menyu bütün səhifələrdə (aid olduğu bölmə seçilmiş görünür)
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;
    }

    /// <summary>Hər göstərilişdə yenilənir (məs. formadan qayıdanda siyahı təzələnsin).</summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "AdminMessagesPage"); }
    }
}
