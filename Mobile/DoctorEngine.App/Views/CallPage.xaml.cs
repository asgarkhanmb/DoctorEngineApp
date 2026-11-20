namespace DoctorEngine.App.Views;

/// <summary>Zəng ekranı (WebView). Səhifə "doctorengine://call-ended" açanda bağlanır.</summary>
public partial class CallPage : ContentPage
{
    private bool _closing;

    public CallPage(string url)
    {
        InitializeComponent();
        Web.Source = new UrlWebViewSource { Url = url };
        Web.Navigated += (_, _) => Loading.IsVisible = false;
        DeviceDisplay.Current.KeepScreenOn = true;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("doctorengine://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            _ = CloseAsync();
        }
    }

    private async Task CloseAsync()
    {
        if (_closing) return;
        _closing = true;
        DeviceDisplay.Current.KeepScreenOn = false;
        // Səhifə bağlananda WebRTC dayansın
        Web.Source = new HtmlWebViewSource { Html = "<html><body style=\"background:#0b141a\"></body></html>" };
        if (Navigation.ModalStack.Contains(this)) await Navigation.PopModalAsync();
    }

    /// <summary>Android "geri": zəngi səhifənin özü bitirsin (Leave), sonra bağlanır.</summary>
    protected override bool OnBackButtonPressed()
    {
        _ = Web.EvaluateJavaScriptAsync("document.querySelector('[data-act=end]') && document.querySelector('[data-act=end]').click()");
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DeviceDisplay.Current.KeepScreenOn = false;
    }
}
