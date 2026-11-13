using DoctorEngine.App.ViewModels;
using Plugin.Maui.Audio;

namespace DoctorEngine.App.Services;

/// <summary>
/// Səs mesajı yazma — AAC (M4A konteyneri). Server yalnız webm / ogg / m4a / mp3 qəbul edir;
/// telefonların standart WAV formatı qəbul olunmaz (həm də çox böyükdür), ona görə AAC seçilib.
/// </summary>
public class VoiceRecorder : IVoiceRecorder
{
    private readonly IAudioManager _audio;
    private IAudioRecorder? _recorder;
    private string? _path;

    public VoiceRecorder(IAudioManager audio) => _audio = audio;

    public async Task StartAsync()
    {
#if ANDROID
        // Plugin.Maui.Audio AAC yazmanı Android 12 (API 31)+ üzərində dəstəkləyir
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            throw new InvalidOperationException("Səs mesajı Android 12 və daha yeni versiyalarda dəstəklənir.");
#endif
        _recorder ??= _audio.CreateRecorder();
        if (!_recorder.CanRecordAudio)
            throw new InvalidOperationException("Bu cihazda mikrofon tapılmadı.");

        _path = Path.Combine(FileSystem.CacheDirectory, $"voice_{DateTime.UtcNow:yyyyMMddHHmmss}.m4a");
        await _recorder.StartAsync(_path, new AudioRecorderOptions
        {
            Encoding = Encoding.Aac,
            ThrowIfNotSupported = true   // AAC alınmasa — sükutla WAV yazmasın (server qəbul etməz)
        });
    }

    public async Task<VoiceRecording?> StopAsync()
    {
        if (_recorder == null || !_recorder.IsRecording) return null;
        var source = await _recorder.StopAsync();
        var path = (source as FileAudioSource)?.GetFilePath() ?? _path;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        return new VoiceRecording(path, "audio/mp4");
    }
}

/// <summary>
/// Səs mesajını tətbiqin içində oxutma. Fayl yüklənir və cihazın pleyerində çalınır.
/// Format dəstəklənmirsə (məs. iOS saytdan gələn webm-i oxumur) — xəta atılır və
/// ViewModel onu sistem pleyerində (brauzerdə) açır.
/// </summary>
public class AudioPlayback : IAudioPlayback
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly IAudioManager _audio;
    private IAudioPlayer? _player;
    private Stream? _stream;

    public AudioPlayback(IAudioManager audio) => _audio = audio;

    public async Task PlayAsync(string url, Action onEnded)
    {
        await StopAsync();

        var bytes = await Http.GetByteArrayAsync(url);
        _stream = new MemoryStream(bytes);
        _player = _audio.CreatePlayer(_stream);
        _player.PlaybackEnded += (_, _) =>
        {
            onEnded();
            _ = StopAsync();
        };
        _player.Play();
    }

    public Task StopAsync()
    {
        try { _player?.Stop(); } catch { }
        try { _player?.Dispose(); } catch { }
        _player = null;
        _stream?.Dispose();
        _stream = null;
        return Task.CompletedTask;
    }
}
