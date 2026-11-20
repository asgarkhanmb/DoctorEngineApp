using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class EditProfilePage : ContentPage
{
    public EditProfilePage(EditProfileViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = vm;
        // Səhifə hər açılışda yenidir (Transient). OnAppearing-də yükləmirik: şəkil seçicidən
        // qayıdanda (Android) səhifə yenidən "görünür" və yazılmış mətnlər silinərdi.
        vm.Load();
        Motion.RevealSections(this);
    }
}
