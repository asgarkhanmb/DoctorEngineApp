using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>
/// Admin məzmun forması. Sahələr serverdən gəlir (AdminFormDto) — hər bölmə üçün ayrıca ekran yoxdur.
/// Yeni qeyd: id yoxdur; redaktə: id var; "single" bölmə (ayarlar): həmişə bir qeyd.
/// </summary>
public partial class AdminFormViewModel : BaseViewModel, IQueryAttributable
{
    private readonly AdminClient _admin;
    private int? _id;

    [ObservableProperty] private AdminSectionDto? section;
    [ObservableProperty] private bool isReadOnly;
    [ObservableProperty] private bool isLoaded;

    public ObservableCollection<AdminFieldVm> Fields { get; } = new();

    public bool IsNew => _id == null && Section?.Kind != "single";
    public bool CanSave => IsLoaded && !IsReadOnly && (IsNew ? Section?.CanCreate == true || Section?.Kind == "single" : Section?.CanEdit == true);
    public bool CanDelete => IsLoaded && _id != null && Section?.Kind == "list" && Section.CanDelete;
    public string Heading => Section == null ? "" : IsNew ? Lang.T("adm_new") : IsReadOnly ? Lang.T("adm_view") : Lang.T("adm_edit");

    public AdminFormViewModel(AdminClient admin) => _admin = admin;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("section", out var s) && s is AdminSectionDto dto) Section = dto;
        _id = query.TryGetValue("id", out var id) && id is int i ? i : null;
        NotifyState();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(Heading));
    }

    protected override void OnLanguageChanged() => NotifyState();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (Section == null) return;
        await RunAsync(async () =>
        {
            var form = await _admin.FormAsync(Section.Key, _id);
            if (Section.Kind == "single") _id = form.Id;
            IsReadOnly = form.ReadOnly;
            var isNew = form.Id == null;

            var list = new List<AdminFieldVm>();
            foreach (var f in form.Fields)
            {
                if (f.Mode == "create" && !isNew) continue;
                if (f.Mode == "edit" && isNew) continue;
                form.Values.TryGetValue(f.Name, out var value);
                form.Files.TryGetValue(f.Name, out var file);
                list.Add(AdminFieldVm.Create(f, value, file, form.ReadOnly));
            }
            Fields.SyncWith(list);   // Clear() yox — Windows-da BindableLayout Reset alanda çökə bilir
            IsLoaded = true;
        });
        NotifyState();
    }

    [RelayCommand]
    private async Task Save()
    {
        if (Section == null || !CanSave) return;

        // Tətbiqdə sadə yoxlama — əsas yoxlama serverdədir (saytla eyni qaydalar)
        var missing = Fields.FirstOrDefault(f => f.IsMissing);
        if (missing != null) { await Alert(Lang.T("common_error"), Lang.T("adm_required", missing.Label)); return; }

        var values = new Dictionary<string, string>();
        var files = new Dictionary<string, FileResult>();
        foreach (var f in Fields) f.Collect(values, files);

        await RunAsync(async () =>
        {
            await _admin.SaveAsync(Section.Key, _id, values, files);
            await Alert(Section.Title, Lang.T("adm_saved"));
            if (Section.Kind == "single") await LoadAsync();
            else await Shell.Current.GoToAsync("..");
        }, showAlert: true);
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Section == null || _id == null) return;
        if (!await Confirm(Lang.T("adm_delete_title"), Lang.T("adm_delete_text"), Lang.T("chat_delete_btn"))) return;
        await RunAsync(async () =>
        {
            await _admin.DeleteAsync(Section.Key, _id.Value);
            await Shell.Current.GoToAsync("..");
        }, showAlert: true);
    }
}

// =====================================================================
// Sahələr — hər tip öz şablonu ilə göstərilir (AdminFieldTemplateSelector)
// =====================================================================

public abstract partial class AdminFieldVm : ObservableObject
{
    protected AdminFieldVm(AdminFieldDto field, bool readOnly)
    {
        Field = field;
        IsReadOnly = readOnly;
    }

    public AdminFieldDto Field { get; }
    public string Name => Field.Name;
    public string Label => Field.Label + (Field.Required && !IsReadOnly ? " *" : "");
    public string? Hint => IsReadOnly ? null : Field.Hint;
    public bool HasHint => !string.IsNullOrEmpty(Hint);
    public bool IsReadOnly { get; }
    public bool IsEditable => !IsReadOnly;

    /// <summary>Məcburi sahə boşdursa true.</summary>
    public virtual bool IsMissing => false;

    public abstract void Collect(Dictionary<string, string> values, Dictionary<string, FileResult> files);

    public static AdminFieldVm Create(AdminFieldDto f, string? value, string? fileUrl, bool readOnly) => f.Type switch
    {
        "bool" => new BoolFieldVm(f, value, readOnly),
        "select" => new SelectFieldVm(f, value, readOnly),
        "image" or "video" => new FileFieldVm(f, fileUrl, readOnly),
        "items" => new ItemsFieldVm(f, value, readOnly),
        _ => new TextFieldVm(f, value, readOnly)
    };
}

public partial class TextFieldVm : AdminFieldVm
{
    [ObservableProperty] private string text;

    public TextFieldVm(AdminFieldDto f, string? value, bool readOnly) : base(f, readOnly) => text = value ?? "";

    public bool IsMultiline => Field.Type == "multiline";
    public bool IsSingleLine => !IsMultiline;
    public int MaxLength => Field.MaxLength ?? int.MaxValue;

    public Keyboard Keyboard => Field.Type switch
    {
        "email" => Keyboard.Email,
        "url" => Keyboard.Url,
        "phone" => Keyboard.Telephone,
        "number" => Keyboard.Numeric,
        _ => Keyboard.Text
    };

    public override bool IsMissing => Field.Required && string.IsNullOrWhiteSpace(Text);

    public override void Collect(Dictionary<string, string> values, Dictionary<string, FileResult> files) =>
        values[Name] = Text?.Trim() ?? "";
}

public partial class BoolFieldVm : AdminFieldVm
{
    [ObservableProperty] private bool value;

    public BoolFieldVm(AdminFieldDto f, string? v, bool readOnly) : base(f, readOnly) =>
        value = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

    public override void Collect(Dictionary<string, string> values, Dictionary<string, FileResult> files) =>
        values[Name] = Value ? "true" : "false";
}

public partial class SelectFieldVm : AdminFieldVm
{
    [ObservableProperty] private AdminOptionDto? selected;

    public SelectFieldVm(AdminFieldDto f, string? value, bool readOnly) : base(f, readOnly)
    {
        Options = f.Options ?? Array.Empty<AdminOptionDto>();
        selected = Options.FirstOrDefault(o => o.Value == value);
    }

    public IReadOnlyList<AdminOptionDto> Options { get; }
    public string Placeholder => Lang.T("adm_choose");

    public override bool IsMissing => Field.Required && Selected == null;

    public override void Collect(Dictionary<string, string> values, Dictionary<string, FileResult> files) =>
        values[Name] = Selected?.Value ?? "";
}

/// <summary>Şəkil / video: hazırkı fayl (önizləmə) + yenisini seç.</summary>
public partial class FileFieldVm : AdminFieldVm
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSource), nameof(HasPreview), nameof(FileText), nameof(HasPicked), nameof(IsMissing))]
    private FileResult? picked;

    public FileFieldVm(AdminFieldDto f, string? url, bool readOnly) : base(f, readOnly) =>
        CurrentUrl = AppConfig.Absolute(url);

    public string? CurrentUrl { get; }
    public bool IsVideo => Field.Type == "video";
    public bool IsImage => !IsVideo;
    public bool HasPicked => Picked != null;
    public bool HasCurrent => !string.IsNullOrEmpty(CurrentUrl);

    public ImageSource? PreviewSource => IsVideo ? null
        : Picked != null ? ImageSource.FromFile(Picked.FullPath)
        : HasCurrent ? ImageSource.FromUri(new Uri(CurrentUrl!)) : null;
    public bool HasPreview => PreviewSource != null;

    public string FileText => Picked != null ? Picked.FileName
        : HasCurrent ? Lang.T(IsVideo ? "adm_file_video_current" : "adm_file_image_current")
        : Lang.T("adm_file_none");

    public string PickText => Lang.T(IsVideo ? "adm_pick_video" : "adm_pick_image");

    public override bool IsMissing => Field.Required && Picked == null && !HasCurrent;

    [RelayCommand]
    private async Task Pick()
    {
        try
        {
            var options = new MediaPickerOptions { SelectionLimit = 1 };
            var list = IsVideo ? await MediaPicker.Default.PickVideosAsync(options) : await MediaPicker.Default.PickPhotosAsync(options);
            var file = list?.FirstOrDefault();
            if (file != null) Picked = file;
        }
        catch (Exception ex) { CrashLog.Write(ex, "AdminForm.Pick"); }
    }

    [RelayCommand] private void ClearPicked() => Picked = null;

    [RelayCommand]
    private async Task OpenCurrent()
    {
        if (!HasCurrent) return;
        try { await Launcher.Default.OpenAsync(new Uri(CurrentUrl!)); } catch { }
    }

    public override void Collect(Dictionary<string, string> values, Dictionary<string, FileResult> files)
    {
        if (Picked != null) files[Name] = Picked;
    }
}

/// <summary>Zəmanət detalları: sətir-sətir (detal adı + ay). Form-a Items[0].PartName kimi göndərilir.</summary>
public partial class ItemsFieldVm : AdminFieldVm
{
    public ItemsFieldVm(AdminFieldDto f, string? json, bool readOnly) : base(f, readOnly)
    {
        List<AdminWarrantyItemDto>? items = null;
        try { if (!string.IsNullOrEmpty(json)) items = JsonSerializer.Deserialize<List<AdminWarrantyItemDto>>(json, Json.Options); }
        catch (JsonException) { }
        foreach (var i in items ?? new()) Rows.Add(new ItemRowVm(this, i.PartName, i.WarrantyMonth));
        if (Rows.Count == 0 && !readOnly) Rows.Add(new ItemRowVm(this, "", 6));
    }

    public ObservableCollection<ItemRowVm> Rows { get; } = new();
    public string PartLabel => Lang.T("adm_part");
    public string MonthsLabel => Lang.T("adm_months");
    public string AddText => Lang.T("adm_add_row");

    public override bool IsMissing => Field.Required && !Rows.Any(r => !string.IsNullOrWhiteSpace(r.PartName));

    [RelayCommand] private void AddRow() => Rows.Add(new ItemRowVm(this, "", 6));

    public void RemoveRow(ItemRowVm row)
    {
        if (Rows.Count > 1) Rows.Remove(row);
    }

    public override void Collect(Dictionary<string, string> values, Dictionary<string, FileResult> files)
    {
        var i = 0;
        foreach (var r in Rows.Where(r => !string.IsNullOrWhiteSpace(r.PartName)))
        {
            values[$"{Name}[{i}].PartName"] = r.PartName.Trim();
            values[$"{Name}[{i}].WarrantyMonth"] = int.TryParse(r.Months, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)
                ? m.ToString(CultureInfo.InvariantCulture) : "0";
            i++;
        }
    }
}

public partial class ItemRowVm : ObservableObject
{
    private readonly ItemsFieldVm _owner;

    [ObservableProperty] private string partName;
    [ObservableProperty] private string months;

    public ItemRowVm(ItemsFieldVm owner, string part, int months)
    {
        _owner = owner;
        partName = part;
        this.months = months.ToString(CultureInfo.InvariantCulture);
    }

    public bool IsEditable => _owner.IsEditable;

    [RelayCommand] private void Remove() => _owner.RemoveRow(this);
}
