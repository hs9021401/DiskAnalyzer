# DiskAnalyzer ⚡

> **A fast Windows disk space analyzer and visualizer**

DiskAnalyzer is a Windows x64 disk space analyzer built with WPF and .NET 10. It builds a filesystem hierarchy using NTFS `$MFT`, the USN Change Journal, or a parallel Win32 directory walker, then presents the results through Tree View, File View, File Types, and an interactive Treemap.

## 🌐 Languages

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ Features

- **Fast scanning**: direct NTFS MFT/Data Runs when available, USN Journal enumeration, and a multithreaded Win32 fallback for folders, non-NTFS volumes, or non-admin sessions.
- **Multiple views**: hierarchical Tree View, virtualized large-file File View, extension-based File Types summaries, and an interactive Treemap.
- **Tree selection**: Ctrl/Shift multi-selection, context-menu batch actions, automatic first-level expansion after scanning, and double-click file opening.
- **Accurate accounting**: NTFS hard-link deduplication plus optional Free Space and Allocated/System Space virtual items.
- **Windows Shell integration**: open files, reveal items in Explorer, copy paths/details, open CMD or PowerShell, move items to the Recycle Bin, permanently delete, and show Windows Properties.
- **Export and localization**: standard CSV export and runtime language switching for English, Traditional Chinese, Simplified Chinese, Japanese, Korean, Spanish, and French.

## 💻 Requirements

- Windows 10, Windows 11, or a compatible Windows Server x64 system.
- .NET 10 SDK to build from source. The self-contained portable build includes the runtime.
- Administrator rights are optional, but may improve NTFS MFT access and scan coverage.

## 📦 Installation and use

### Portable build

Download `DiskAnalyzer_Portable_win-x64.zip` from GitHub Releases, extract it, and run `DiskAnalyzer.exe`. The portable build does not require installation.

### Inno Setup installer

The installer provides language selection, an optional desktop shortcut, and optional Windows Explorer context-menu integration. It is generated from `DiskAnalyzer.iss` using the portable publish directory.

### License and third-party notices

Portable and installed builds include `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt`. The first covers DiskAnalyzer's own code under the MIT License; the second lists attribution and license links for the self-contained .NET runtime and test dependencies.

## 🔧 Build from source

Run these commands on Windows with PowerShell and the .NET 10 SDK:

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

Create a self-contained single-file portable build:

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## 🏗️ Project structure

```text
src/DiskAnalyzer.Core/       Models, scanners, MFT/USN readers, export, and search
src/DiskAnalyzer.UI/         WPF application, ViewModels, controls, themes, and resources
tests/DiskAnalyzer.Tests/    Core and UI component tests
DiskAnalyzer.iss             Inno Setup installer script
```

## ⚠️ Notes and limitations

- This project targets Windows x64; Linux and macOS are not currently supported.
- Protected or inaccessible directories may be skipped. Raw disk access can require administrator rights.
- Scanning, deletion, and permanent deletion operate on user-selected files. Keep backups of important data.
- The public API is not yet stable; library consumers should expect breaking changes during development.

## 🤝 Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening an issue or pull request. Include the Windows version, scan mode, reproduction steps, and relevant logs, while removing personal paths and sensitive data.
For suspected vulnerabilities, please follow [SECURITY.md](SECURITY.md) instead of posting complete details publicly.

## 📄 License

DiskAnalyzer is released under the [MIT License](LICENSE).

Copyright © 2026 Alex Lin.
