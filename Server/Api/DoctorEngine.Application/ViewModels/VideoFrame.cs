namespace DoctorEngineApp.ViewModels
{
    /// <summary>
    /// Vahid video çərçivəsi (Views/Shared/_VideoFrame.cshtml).
    /// Variant: "" (sayt), "compact" (admin redaktə), "thumb" (admin siyahı önizləməsi).
    /// </summary>
    public record VideoFrame(string Src, string Variant = "", string? Id = null);
}
