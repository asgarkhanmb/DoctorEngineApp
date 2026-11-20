using System.ComponentModel;
using CommunityToolkit.Maui.Views;
using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ReelNewPage : ContentPage
{
    private readonly ReelNewViewModel _vm;

    public ReelNewPage(ReelNewViewModel vm)
    {
        InitializeComponent();
        this.UseBottomNav("reels", floating: false);   // alt menyu bütün səhifələrdə (aid olduğu bölmə seçilmiş görünür)
        this.CenterOnWideScreens();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnVmChanged;
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ReelNewViewModel.PreviewPath)) return;
        Preview.Source = string.IsNullOrEmpty(_vm.PreviewPath) ? null : MediaSource.FromFile(_vm.PreviewPath);
        Preview.Play();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Leaving();
        Preview.Stop();
        Preview.Handler?.DisconnectHandler();   // oynadıcı resursları buraxılsın
    }
}
