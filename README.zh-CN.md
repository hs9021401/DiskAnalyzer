# DiskAnalyzer ⚡

> **快速的 Windows 磁盘空间分析与可视化工具**

DiskAnalyzer 是一款使用 WPF 和 .NET 10 构建的 Windows x64 磁盘空间分析工具。它可以通过 NTFS `$MFT`、USN Change Journal 或 Win32 多线程目录遍历器建立文件层级，并通过 Tree View、File View、File Types 和交互式 Treemap 展示结果。

## 🌐 语言

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ 主要功能

- **高速扫描**：在满足条件时直接读取 NTFS MFT/Data Runs，支持 USN Journal，并为指定文件夹、非 NTFS 分区或非管理员环境提供多线程 Win32 备援。
- **多种视图**：目录层级 Tree View、大文件 File View、按扩展名统计的 File Types，以及交互式 Treemap。
- **树状选择**：支持 Ctrl/Shift 多选、右键批量操作、扫描完成后自动展开第一层，以及双击打开文件。
- **准确统计**：支持 NTFS 硬链接去重，并可显示 Free Space 和 Allocated/System Space 虚拟项目。
- **Windows Shell 集成**：打开文件、在资源管理器中显示、复制路径/详细信息、打开 CMD 或 PowerShell、移至回收站、永久删除和显示 Windows 属性。
- **导出与本地化**：支持标准 CSV 导出，并可在运行时切换英文、繁体中文、简体中文、日文、韩文、西班牙文和法文。

## 💻 系统要求

- Windows 10、Windows 11 或兼容的 Windows Server x64。
- 从源代码构建需要 .NET 10 SDK；self-contained portable 版本包含运行时。
- 管理员权限不是必需条件，但可以改善 NTFS MFT 的访问权限和扫描完整度。

## 📦 安装与使用

从 GitHub Releases 下载 `DiskAnalyzer_Portable_win-x64.zip`，解压后运行 `DiskAnalyzer.exe`。Portable 版本无需安装。Inno Setup 安装程序支持语言选择、桌面快捷方式和 Windows 资源管理器右键菜单集成。

## 🔧 从源代码构建

在 Windows、PowerShell 和 .NET 10 SDK 环境中运行：

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

创建 self-contained single-file portable 版本：

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## 🏗️ 项目结构

```text
src/DiskAnalyzer.Core/       核心模型、扫描器、MFT/USN、导出与搜索
src/DiskAnalyzer.UI/         WPF 应用程序、ViewModel、控件、主题与资源
tests/DiskAnalyzer.Tests/    核心和 UI 组件测试
DiskAnalyzer.iss             Inno Setup 安装程序脚本
```

## ⚠️ 注意事项

- 当前目标平台为 Windows x64，不支持 Linux 或 macOS。
- 受保护或无法访问的目录可能会被跳过；原始磁盘访问可能需要管理员权限。
- 扫描、删除和永久删除操作会影响用户选择的文件，请先备份重要数据。
- 项目仍在开发中，公开 API 可能发生不兼容变更。

## 🤝 贡献与授权

提交 Issue 或 Pull Request 前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)，并删除个人路径和敏感信息。项目采用 [MIT License](LICENSE)。

Copyright © 2026 Alex Lin.
