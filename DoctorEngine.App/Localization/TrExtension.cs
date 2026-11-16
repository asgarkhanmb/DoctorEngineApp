using System.Diagnostics.CodeAnalysis;

namespace DoctorEngine.App.Localization;

/// <summary>
/// XAML-da tərcümə: <c>Text="{l:Tr home_latest_services}"</c>.
/// Lang.Current-ə bağlama qaytarır — dil dəyişəndə mətn özü yenilənir.
/// </summary>
[ContentProperty(nameof(Key))]
public sealed class TrExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = "";

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Lang indeksatoru kodda birbaşa istifadə olunur — trimmer onu silmir.")]
    public BindingBase ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]", BindingMode.OneWay, source: Lang.Current);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
