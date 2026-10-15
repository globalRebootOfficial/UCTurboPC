# UC Turbo PC

Windows 10/11 x64 WinForms browser prototype based on Microsoft WebView2.

## Build

Requires .NET 10 SDK to build. On Windows run:

```powershell
dotnet restore
dotnet run
```

For release use GitHub Actions `Build Windows EXE`, then download the `UCTurboPC-win-x64` artifact.

WebView2 Runtime must be installed separately on the PC. This prototype is **not yet security-hardened**; phishing protection, VirusTotal checks, antivirus scanning, full theme editor and installer are not implemented.
