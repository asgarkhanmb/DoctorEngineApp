using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class AdminFormPage : ContentPage
{
    private readonly AdminFormViewModel _vm;
    private bool _loaded;

    public AdminFormPage(AdminFormViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;
    }

    /// <summary>
    /// Yalnız ilk göstərilişdə yüklənir: şəkil / video seçicidən qayıdanda (Android) səhifə yenidən
    /// "görünür" — o zaman yükləsək, doldurulmuş sahələr itərdi.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        try { await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "AdminFormPage"); }
    }
}
