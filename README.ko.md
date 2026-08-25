# DiskAnalyzer ⚡

> **빠른 Windows 디스크 공간 분석 및 시각화 도구**

DiskAnalyzer는 WPF와 .NET 10으로 만든 Windows x64 디스크 공간 분석 도구입니다. NTFS `$MFT`, USN Change Journal 또는 Win32 멀티스레드 폴더 스캐너로 파일 계층을 만들고 Tree View, File View, File Types 및 인터랙티브 Treemap으로 결과를 보여 줍니다.

## 🌐 언어

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ 주요 기능

- **고속 스캔**: 가능한 경우 NTFS MFT/Data Runs와 USN Journal을 사용하고, 폴더·비 NTFS 볼륨·비관리자 환경에는 병렬 Win32 스캐너를 사용합니다.
- **다양한 보기**: 계층형 Tree View, 대용량 파일 File View, 확장자별 File Types, 인터랙티브 Treemap을 제공합니다.
- **트리 선택**: Ctrl/Shift 다중 선택, 우클릭 일괄 작업, 스캔 후 첫 번째 계층 자동 펼치기, 파일 더블클릭 열기를 지원합니다.
- **정확한 용량 계산**: NTFS 하드 링크 중복 제거와 Free Space 및 Allocated/System Space 가상 항목을 지원합니다.
- **Windows Shell 통합**: 파일 열기, 탐색기에서 표시, 경로/상세 정보 복사, CMD/PowerShell 열기, 휴지통 이동, 영구 삭제, Windows 속성 창을 지원합니다.
- **내보내기 및 다국어**: 표준 CSV 내보내기와 영어, 번체 중국어, 간체 중국어, 일본어, 한국어, 스페인어, 프랑스어의 실행 중 언어 전환을 지원합니다.

## 💻 요구 사항

- Windows 10, Windows 11 또는 호환되는 Windows Server x64.
- 소스 빌드에는 .NET 10 SDK가 필요하며 self-contained portable 버전에는 런타임이 포함됩니다.
- 관리자 권한은 필수는 아니지만 NTFS MFT 접근 범위와 스캔 성능을 개선할 수 있습니다.

## 📦 설치 및 실행

GitHub Releases에서 `DiskAnalyzer_Portable_win-x64.zip`을 다운로드하여 압축을 풀고 `DiskAnalyzer.exe`를 실행합니다. Portable 버전은 설치가 필요하지 않습니다. Inno Setup 설치 프로그램은 언어, 바탕 화면 바로가기, Windows 탐색기 컨텍스트 메뉴 통합을 제공합니다.

## 🔧 소스에서 빌드

Windows, PowerShell, .NET 10 SDK에서 실행합니다.

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

Portable 버전 만들기:

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## 🏗️ 프로젝트 구조

```text
src/DiskAnalyzer.Core/       모델, 스캐너, MFT/USN, 내보내기, 검색
src/DiskAnalyzer.UI/         WPF 앱, ViewModel, 컨트롤, 테마, 리소스
tests/DiskAnalyzer.Tests/    Core 및 UI 테스트
DiskAnalyzer.iss             Inno Setup 스크립트
```

## ⚠️ 주의 사항

- 현재 Windows x64를 대상으로 하며 Linux와 macOS는 지원하지 않습니다.
- 보호되었거나 접근할 수 없는 폴더는 건너뛸 수 있습니다.
- 스캔, 삭제, 영구 삭제는 선택한 파일에 적용됩니다. 중요한 데이터는 백업해 두십시오.

## 🤝 기여 및 라이선스

Issue 또는 Pull Request를 제출하기 전에 [CONTRIBUTING.md](CONTRIBUTING.md)를 확인하십시오. 라이선스는 [MIT License](LICENSE)입니다.

Copyright © 2026 Alex Lin.
