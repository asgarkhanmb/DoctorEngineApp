namespace DoctorEngineApp.ViewModels.Admin
{
    /// <summary>Admin səhifə başlığı: başlıq, izah, əsas düymə və "geri" linki.</summary>
    public record AdminPageHead(
        string Title,
        string? Subtitle = null,
        string? PrimaryText = null,
        string? PrimaryAction = null,
        string PrimaryIcon = "fa-solid fa-plus",
        string? BackAction = null,
        string BackText = "Siyahıya qayıt",
        object? PrimaryRoute = null);

    /// <summary>Silmə düyməsi (POST + təsdiq pəncərəsi).</summary>
    public record AdminDeleteButton(string Action, int Id, string Confirm, string? Text = null);
}
