# DiskAnalyzer ⚡

> **高速な Windows ディスク容量分析・可視化ツール**

DiskAnalyzer は WPF と .NET 10 で作られた Windows x64 向けのディスク容量分析ツールです。NTFS の `$MFT`、USN Change Journal、または Win32 のマルチスレッド走査を使ってファイル階層を作成し、Tree View、File View、File Types、インタラクティブな Treemap で表示します。

## 🌐 言語

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ 主な機能

- **高速スキャン**：条件に応じて NTFS MFT/Data Runs や USN Journal を利用し、フォルダー・非 NTFS ボリューム・非管理者環境には Win32 の並列走査を使用します。
- **複数の表示**：階層型 Tree View、大容量ファイルの File View、拡張子別の File Types、インタラクティブな Treemap。
- **ツリー選択**：Ctrl/Shift 複数選択、右クリックの一括操作、スキャン後の第 1 階層自動展開、ファイルのダブルクリック起動。
- **正確な集計**：NTFS ハードリンクの重複排除、Free Space と Allocated/System Space の仮想項目。
- **Windows Shell 連携**：ファイルを開く、Explorer で表示、パスや詳細のコピー、CMD/PowerShell、ゴミ箱、完全削除、Windows のプロパティ。
- **エクスポートと多言語**：標準 CSV 出力と、英語・繁体字中国語・簡体字中国語・日本語・韓国語・スペイン語・フランス語の実行時切り替え。

## 💻 必要環境

- Windows 10、Windows 11、または互換性のある Windows Server x64。
- ソースからのビルドには .NET 10 SDK が必要です。self-contained portable 版にはランタイムが含まれます。
- 管理者権限は必須ではありませんが、NTFS MFT へのアクセスとスキャン範囲を改善できます。

## 📦 インストールと実行

GitHub Releases から `DiskAnalyzer_Portable_win-x64.zip` をダウンロードして展開し、`DiskAnalyzer.exe` を実行します。Portable 版はインストール不要です。Inno Setup 版では言語、デスクトップショートカット、Explorer のコンテキストメニュー統合を選択できます。

## 🔧 ソースからのビルド

Windows、PowerShell、.NET 10 SDK で実行します。

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

Portable 版を作成するには次を実行します。

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## 🏗️ プロジェクト構成

```text
src/DiskAnalyzer.Core/       モデル、スキャナー、MFT/USN、エクスポート、検索
src/DiskAnalyzer.UI/         WPF アプリ、ViewModel、コントロール、テーマ、リソース
tests/DiskAnalyzer.Tests/    Core と UI のテスト
DiskAnalyzer.iss             Inno Setup スクリプト
```

## ⚠️ 注意事項

- 対象は Windows x64 です。Linux と macOS は現在サポートしていません。
- 保護されたフォルダーやアクセスできない項目はスキップされる場合があります。
- スキャン、削除、完全削除は選択したファイルに作用します。重要なデータはバックアップしてください。

## 🤝 貢献とライセンス

Issue や Pull Request の前に [CONTRIBUTING.md](CONTRIBUTING.md) を確認してください。ライセンスは [MIT License](LICENSE) です。

Copyright © 2026 Alex Lin.
