using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

/// <summary>Admin formasında sahənin tipinə görə şablon (mətn, çoxsətirli, seçim, bəli/xeyr, fayl, detallar).</summary>
public class AdminFieldTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Text { get; set; }
    public DataTemplate? Multiline { get; set; }
    public DataTemplate? Bool { get; set; }
    public DataTemplate? Select { get; set; }
    public DataTemplate? File { get; set; }
    public DataTemplate? Items { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => item switch
    {
        TextFieldVm { IsMultiline: true } => Multiline!,
        TextFieldVm => Text!,
        BoolFieldVm => Bool!,
        SelectFieldVm => Select!,
        FileFieldVm => File!,
        ItemsFieldVm => Items!,
        _ => Text!
    };
}
