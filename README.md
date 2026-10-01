# DiskAnalyzer ⚡

> **極速 Windows 磁碟空間分析與視覺化工具**

DiskAnalyzer 是以 WPF 和 .NET 10 開發的 Windows x64 磁碟空間分析工具。它會建立檔案與資料夾階層，並透過 Tree View、File View、File Types 與互動式 Treemap，協助你快速找出佔用空間的內容。

## 🌐 語言 / Languages

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ 主要功能

- **高速掃描**：掃描完整 NTFS 磁區且權限足夠時使用 `$MFT`；指定資料夾、非 NTFS 磁區、權限受限或 `$MFT` 讀取失敗時，改用 Win32 平行掃描。
- **多種檢視方式**：以階層式 Tree View 瀏覽資料夾，使用 File View 尋找大型檔案，透過 File Types 依副檔名彙整容量，或使用 Treemap 以圖形方式比較檔案大小。
- **便利的樹狀操作**：支援 Ctrl/Shift 多選、右鍵批次操作、掃描完成後自動展開第一層，以及雙擊開啟檔案。
- **精準容量統計**：支援 NTFS Hard Link 去重，並可顯示 Free Space 與 Allocated/System Space 等虛擬項目。
- **Windows 整合**：開啟檔案、在檔案總管中顯示、複製路徑與檔案資訊、開啟 CMD/PowerShell、移至資源回收筒、永久刪除及顯示 Windows 內容視窗。
- **匯出與多國語言**：支援標準 CSV 匯出，並可在程式中切換 English、繁體中文、简体中文、日本語、한국어、Español 與 Français。

## 🖱️ 基本使用方式

1. 選擇磁碟或資料夾，按下 **Scan** 開始掃描。
2. 在 Tree View 中展開資料夾，或切換到 File View 與 File Types 找出目標內容。
3. 使用 Ctrl/Shift 選取多個項目，再以右鍵執行批次操作。
4. 雙擊檔案即可使用 Windows 預設程式開啟；右鍵可使用更多檔案管理功能。
5. 使用 Treemap 的提示、縮放與階層導覽快速定位大型項目。

刪除或永久刪除前，請確認選取的路徑與檔案；永久刪除的項目無法從資源回收筒還原。

## 📦 下載與安裝

### Portable

從 [GitHub Releases](https://github.com/hs9021401/DiskAnalyzer/releases) 下載 `DiskAnalyzer_Portable_win-x64.zip`，解壓縮後執行 `DiskAnalyzer.exe`。Portable 版本不需要安裝，並已包含 .NET 執行環境。

### Inno Setup 安裝程式

下載 Release 中的安裝程式並執行，依照精靈選擇安裝語言、桌面捷徑，以及 Windows 檔案總管右鍵選單整合。

## 💻 系統需求

- Windows 10、Windows 11 或相容的 Windows Server x64。
- 使用 Portable 或安裝版本不需要另外安裝 .NET。
- 從原始碼建置需要 .NET 10 SDK。
- 管理員權限不是必要條件，但可能改善 NTFS 掃描的存取範圍與速度。

## 🔧 從原始碼建置

在 Windows、PowerShell 與 .NET 10 SDK 環境執行：

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

建立 self-contained single-file Portable 版本：

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## ⚠️ 注意事項

- DiskAnalyzer 目前只支援 Windows x64，不支援 Linux 或 macOS。
- 受保護、離線或無法存取的資料夾可能會被略過。
- 掃描、移至資源回收筒與刪除功能都會作用於使用者選取的檔案，請先備份重要資料。
- 公開 API 尚未承諾穩定，使用核心程式碼作為函式庫時請留意版本變更。

## 📄 授權與第三方通知

本專案採用 [MIT License](LICENSE)。可散布的 Portable 與安裝版本會隨附 `LICENSE.txt` 及 `THIRD-PARTY-NOTICES.txt`，其中包含 self-contained .NET 執行環境與相關相依套件的通知及授權連結。

Copyright © 2026 Alex Lin.
