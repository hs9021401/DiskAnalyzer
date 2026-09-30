# DiskAnalyzer ⚡

> **A fast Windows disk space analyzer and visualizer**

DiskAnalyzer is a Windows x64 disk space analyzer built with WPF and .NET 10. It builds a file and folder hierarchy, then helps you find large items through Tree View, File View, File Types, and an interactive Treemap.

## 🌐 Languages

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ Features

- **Fast scanning**: uses the NTFS `$MFT` and USN Change Journal when applicable, with a parallel Win32 scanner for folders, non-NTFS volumes, or restricted access.
- **Multiple views**: browse folders in the hierarchical Tree View, find large files in File View, group usage by extension in File Types, or compare sizes visually with the Treemap.
- **Tree operations**: Ctrl/Shift multi-selection, context-menu batch actions, automatic first-level expansion after scanning, and double-click file opening.
- **Accurate accounting**: NTFS hard-link deduplication plus optional Free Space and Allocated/System Space virtual items.
- **Windows integration**: open files, reveal them in Explorer, copy paths and details, open CMD/PowerShell, move items to the Recycle Bin, permanently delete them, and show Windows Properties.
- **Export and localization**: standard CSV export and in-app switching between English, Traditional Chinese, Simplified Chinese, Japanese, Korean, Spanish, and French.

## 🖱️ Basic usage

1. Select a drive or folder and click **Scan**.
2. Browse the hierarchy in Tree View, or switch to File View and File Types to locate targets.
3. Use Ctrl/Shift to select multiple items, then use the context menu for batch actions.
4. Double-click a file to open it with the Windows default application; use the context menu for additional file actions.
5. Use the Treemap tooltips, zoom, and hierarchy navigation to locate large items quickly.

Review the selected paths before moving or deleting files. Items permanently deleted cannot be restored from the Recycle Bin.

## 📦 Download and installation

### Portable build

Download `DiskAnalyzer_Portable_win-x64.zip` from [GitHub Releases](https://github.com/hs9021401/DiskAnalyzer/releases), extract it, and run `DiskAnalyzer.exe`. The portable build requires no installation and includes the .NET runtime.

### Inno Setup installer

Run the installer from a Release and choose the installer language, an optional desktop shortcut, and optional Windows Explorer context-menu integration.

## 💻 Requirements

- Windows 10, Windows 11, or a compatible Windows Server x64 system.
- The Portable and installed builds do not require a separate .NET installation.
- The .NET 10 SDK is required to build from source.
- Administrator rights are optional, but may improve NTFS scan access and coverage.

## 🔧 Build from source

Run these commands on Windows with PowerShell and the .NET 10 SDK:

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

Create a self-contained single-file Portable build:

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## ⚠️ Notes and limitations

- DiskAnalyzer currently supports Windows x64 only; Linux and macOS are not supported.
- Protected, offline, or inaccessible folders may be skipped.
- Scanning, Recycle Bin, and deletion actions operate on the selected files. Keep backups of important data.
- The public API is not yet stable; library consumers should expect changes between versions.

## 📄 License and third-party notices

DiskAnalyzer is released under the [MIT License](LICENSE). Distributable Portable and installed builds include `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt`, which contain notices and license links for the self-contained .NET runtime and related dependencies.

Copyright © 2026 Alex Lin.