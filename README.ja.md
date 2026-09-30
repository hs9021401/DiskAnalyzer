# DiskAnalyzer ⚡

> **高速な Windows ディスク容量分析・可視化ツール**

DiskAnalyzer は WPF と .NET 10 で開発された Windows x64 向けのディスク容量分析ツールです。ファイルとフォルダーの階層を作成し、Tree View、File View、File Types、インタラクティブな Treemap で容量の大きな項目を見つけられます。

## 🌐 言語

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ 主な機能

- **高速スキャン**：条件に応じて NTFS の `$MFT` と USN Change Journal を使用し、フォルダー、非 NTFS ボリューム、アクセスが制限された環境では並列 Win32 スキャンを使用します。
- **複数の表示**：階層型 Tree View、大容量ファイルを探す File View、拡張子別の File Types、サイズを視覚的に比較できる Treemap。
- **ツリー操作**：Ctrl/Shift 複数選択、右クリックの一括操作、スキャン後の第 1 階層自動展開、ファイルのダブルクリック起動。
- **正確な集計**：NTFS ハードリンクの重複排除、Free Space と Allocated/System Space の仮想項目。
- **Windows 連携**：ファイルを開く、Explorer で表示、パスや詳細のコピー、CMD/PowerShell、ゴミ箱、完全削除、Windows のプロパティ表示。
- **エクスポートと多言語**：標準 CSV 出力と、英語・繁体字中国語・簡体字中国語・日本語・韓国語・スペイン語・フランス語の実行時切り替え。

## 🖱️ 基本的な使い方

1. ドライブまたはフォルダーを選択し、**Scan** をクリックします。
2. Tree View で階層を確認するか、File View と File Types で対象を探します。
3. Ctrl/Shift で複数項目を選択し、右クリックメニューから一括操作を実行します。
4. ファイルをダブルクリックすると Windows の既定のアプリで開きます。その他の操作は右クリックメニューから行えます。
5. Treemap のツールチップ、ズーム、階層ナビゲーションで大きな項目をすばやく確認できます。

移動または削除する前に、選択したパスを確認してください。完全削除した項目はごみ箱から復元できません。

## 📦 ダウンロードとインストール

### Portable 版

[GitHub Releases](https://github.com/hs9021401/DiskAnalyzer/releases) から `DiskAnalyzer_Portable_win-x64.zip` をダウンロードして展開し、`DiskAnalyzer.exe` を実行します。Portable 版はインストール不要で、.NET ランタイムを含みます。

### Inno Setup インストーラー

Release のインストーラーを実行し、インストール言語、デスクトップショートカット、Windows Explorer のコンテキストメニュー統合を選択します。

## 💻 必要環境

- Windows 10、Windows 11、または互換性のある Windows Server x64。
- Portable 版とインストール版では、別途 .NET をインストールする必要はありません。
- ソースからのビルドには .NET 10 SDK が必要です。
- 管理者権限は必須ではありませんが、NTFS スキャンのアクセス範囲と速度を改善できます。

## 🔧 ソースからのビルド

Windows、PowerShell、.NET 10 SDK で実行します。

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

self-contained single-file Portable 版を作成するには、次を実行します。

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## ⚠️ 注意事項

- DiskAnalyzer は現在 Windows x64 のみをサポートしており、Linux と macOS には対応していません。
- 保護されたフォルダー、オフラインの項目、アクセスできないフォルダーはスキップされる場合があります。
- スキャン、ゴミ箱への移動、削除は選択したファイルに作用します。重要なデータはバックアップしてください。
- 公開 API はまだ安定していないため、ライブラリとして利用する場合はバージョン間の変更に注意してください。

## 📄 ライセンスと第三者通知

本プロジェクトは [MIT License](LICENSE) で提供されます。配布用の Portable 版とインストール版には `LICENSE.txt` と `THIRD-PARTY-NOTICES.txt` が含まれ、self-contained .NET ランタイムと関連依存関係の通知およびライセンスリンクを記載しています。

Copyright © 2026 Alex Lin.