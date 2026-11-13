# DoctorEngine — mobil və desktop tətbiqi (.NET MAUI, .NET 10)

Bir kod bazası: **Android, iOS, Windows, macOS**. Sayt ilə eyni server və eyni hesablar
(`/api/v1/*` JWT API, `/chatHub` SignalR, `/api/ai/chat`).

Visual Studio-da açmaq üçün: `DoctorEngine.Mobile.slnx` (repo kökündə).
Server ayrıca solution-dadır (`DoctorEngineApp/DoctorEngineApp.sln`).

## Server ünvanı
- **Debug**: lokal server `http://localhost:5019` (Android emulyatorunda `http://10.0.2.2:5019`).
  Profil ekranının altında dəyişmək olar (yalnız Debug-da görünür).
- **Release**: `Services/AppConfig.cs` → `ProductionUrl` (hazırda `https://doctorengine.az`).
  Sayt başqa domendədirsə — buranı dəyişin.

## İşə salmaq (Windows)
```
dotnet build DoctorEngine.App -f net10.0-windows10.0.19041.0
DoctorEngine.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\DoctorEngine.App.exe
```

## Android
> Diqqət: Windows istifadəçi adında qeyri-ASCII hərf (`ə`) olduğu üçün Android alətləri (aapt2)
> həmin yolda işləmir. Layihəni ASCII yoldan yığın — məsələn junction:
> `New-Item -ItemType Junction -Path C:\DEsrc -Target <repo qovluğu>`

```
cd C:\DEsrc
dotnet publish DoctorEngine.App\DoctorEngine.App.csproj -f net10.0-android -c Release ^
  -p:AndroidPackageFormat=apk -o publish\android
```
APK hazırda **debug açarı** ilə imzalanır (test üçün). Google Play üçün öz keystore-unuz lazımdır
(`AndroidKeyStore`, `AndroidSigningKeyStore` və s. xassələri) və Play üçün `.aab` formatı.

## iOS / macOS
Mac (Xcode) və Apple Developer hesabı tələb edir. Kod hazırdır; Mac-da
`dotnet build -f net10.0-ios` və ya GitHub Actions `macos` runner ilə yığmaq olar.

## Qeydlər
- Səs mesajı AAC/M4A (`audio/mp4`) yazılır — server qəbul edir. Android 12+ tələb edir.
- Tokenlər SecureStorage-dadır (Android Keystore / iOS Keychain / Windows DPAPI).
- Hələ yoxdur: tətbiq bağlı olanda push bildirişi (FCM / APNs).
