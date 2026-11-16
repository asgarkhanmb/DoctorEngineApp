using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace DoctorEngine.App.Platforms.Android;

/// <summary>
/// Android alt menyusu: 4-dən çox tab olanda Material standart olaraq yalnız seçilmiş tabın yazısını
/// göstərir (digərləri yalnız ikon). Burada bütün tabların yazısı həmişə görünür.
/// </summary>
public class DeShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem) =>
        new LabeledBottomNavTracker(this, shellItem);

    private sealed class LabeledBottomNavTracker : ShellBottomNavViewAppearanceTracker
    {
        public LabeledBottomNavTracker(IShellContext context, ShellItem item) : base(context, item) { }

        public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
        {
            base.SetAppearance(bottomView, appearance);
            bottomView.LabelVisibilityMode = NavigationBarView.LabelVisibilityLabeled;
        }

        public override void ResetAppearance(BottomNavigationView bottomView)
        {
            base.ResetAppearance(bottomView);
            bottomView.LabelVisibilityMode = NavigationBarView.LabelVisibilityLabeled;
        }
    }
}
