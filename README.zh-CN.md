# DiskAnalyzer ⚡

> **快速的 Windows 磁盘空间分析与可视化工具**

DiskAnalyzer 是一款使用 WPF 和 .NET 10 构建的 Windows x64 磁盘空间分析工具。它会建立文件和文件夹层级，并通过 Tree View、File View、File Types 和交互式 Treemap 帮助你快速找到占用空间较大的内容。

## 🌐 语言

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ 主要功能

- **高速扫描**：扫描完整 NTFS 分区且权限足够时使用 `$MFT`；指定文件夹、非 NTFS 分区、权限受限或 `$MFT` 读取失败时，改用多线程 Win32 扫描。
- **多种视图**：使用层级式 Tree View 浏览文件夹，在 File View 中查找大文件，在 File Types 中按扩展名统计，或使用 Treemap 直观比较文件大小。
- **树状操作**：支持 Ctrl/Shift 多选、右键批量操作、扫描完成后自动展开第一层，以及双击打开文件。
- **准确统计**：支持 NTFS 硬链接去重，并可显示 Free Space 和 Allocated/System Space 虚拟项目。
- **Windows 集成**：打开文件、在资源管理器中显示、复制路径和详细信息、打开 CMD/PowerShell、移至回收站、永久删除，以及显示 Windows 属性窗口。
- **导出与本地化**：支持标准 CSV 导出，并可在程序中切换英文、繁体中文、简体中文、日文、韩文、西班牙文和法文。

## 🖱️ 基本使用

1. 选择磁盘或文件夹，点击 **Scan** 开始扫描。
2. 在 Tree View 中浏览层级，或切换到 File View 和 File Types 查找目标内容。
3. 使用 Ctrl/Shift 选择多个项目，再通过右键菜单执行批量操作。
4. 双击文件即可使用 Windows 默认程序打开；右键菜单提供更多文件操作。
5. 使用 Treemap 的提示、缩放和层级导航快速定位大项目。

移动或删除文件前，请确认所选路径。永久删除的项目无法从回收站恢复。

## 📦 下载与安装

### Portable 版本

从 [GitHub Releases](https://github.com/hs9021401/DiskAnalyzer/releases) 下载 `DiskAnalyzer_Portable_win-x64.zip`，解压后运行 `DiskAnalyzer.exe`。Portable 版本无需安装，并已包含 .NET 运行时。

### Inno Setup 安装程序

运行 Release 中的安装程序，并选择安装语言、桌面快捷方式以及 Windows 资源管理器右键菜单集成。

## 💻 系统要求

- Windows 10、Windows 11 或兼容的 Windows Server x64。
- Portable 和安装版本无需另外安装 .NET。
- 从源代码构建需要 .NET 10 SDK。
- 管理员权限不是必需条件，但可以改善 NTFS 扫描的访问范围和速度。

## 🔧 从源代码构建

在 Windows、PowerShell 和 .NET 10 SDK 环境中运行：

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

创建 self-contained single-file Portable 版本：

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## ⚠️ 注意事项

- DiskAnalyzer 目前只支持 Windows x64，不支持 Linux 或 macOS。
- 受保护、离线或无法访问的文件夹可能会被跳过。
- 扫描、移至回收站和删除操作会作用于所选文件，请先备份重要数据。
- 公开 API 尚未稳定，使用核心代码作为库时请留意版本变化。

## 📄 授权与第三方通知

本项目采用 [MIT License](LICENSE)。可分发的 Portable 和安装版本会随附 `LICENSE.txt` 与 `THIRD-PARTY-NOTICES.txt`，其中包含 self-contained .NET 运行时及相关依赖的通知和授权链接。

Copyright © 2026 Alex Lin.
