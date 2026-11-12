namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// Faylın həqiqi tipini ilk baytlarına ("magic bytes") görə yoxlayır.
    /// Uzantı və Content-Type brauzerdən gəlir və saxtalaşdırıla bilər — bu yoxlama yox.
    /// </summary>
    public static class FileSignature
    {
        public static bool Matches(IFormFile file, string extension)
        {
            var header = new byte[16];
            int read;
            using (var stream = file.OpenReadStream())
            {
                read = stream.Read(header, 0, header.Length);
            }

            if (read < 12)
                return false;

            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
                ".webp" => Ascii(header, 0, "RIFF") && Ascii(header, 8, "WEBP"),
                ".mp4" or ".mov" or ".m4v" => IsIsoMedia(header),
                ".webm" => header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3,
                ".ogg" => Ascii(header, 0, "OggS"),
                _ => false
            };
        }

        // MP4/MOV: 4-cü baytdan sonra atom adı gəlir
        private static bool IsIsoMedia(byte[] h) =>
            Ascii(h, 4, "ftyp") || Ascii(h, 4, "moov") || Ascii(h, 4, "mdat") ||
            Ascii(h, 4, "wide") || Ascii(h, 4, "free") || Ascii(h, 4, "skip");

        private static bool Ascii(byte[] data, int offset, string text)
        {
            for (int i = 0; i < text.Length; i++)
                if (data[offset + i] != (byte)text[i])
                    return false;
            return true;
        }
    }
}
