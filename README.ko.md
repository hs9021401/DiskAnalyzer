# DiskAnalyzer ⚡

> **빠른 Windows 디스크 공간 분석 및 시각화 도구**

DiskAnalyzer는 WPF와 .NET 10으로 개발된 Windows x64 디스크 공간 분석 도구입니다. 파일과 폴더 계층을 만들고 Tree View, File View, File Types 및 인터랙티브 Treemap으로 큰 용량을 차지하는 항목을 찾을 수 있습니다.

## 🌐 언어

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ 주요 기능

- **고속 스캔**: 필요한 권한이 있으면 전체 NTFS 볼륨 스캔에 `$MFT`를 사용합니다. 폴더, 비 NTFS 볼륨, 접근이 제한된 환경 또는 `$MFT` 읽기 실패 시에는 병렬 Win32 스캔을 사용합니다.
- **다양한 보기**: 계층형 Tree View, 대용량 파일을 찾는 File View, 확장자별 통계를 제공하는 File Types, 크기를 시각적으로 비교하는 Treemap.
- **트리 작업**: Ctrl/Shift 다중 선택, 우클릭 일괄 작업, 스캔 후 첫 번째 계층 자동 펼치기, 파일 더블클릭 열기.
- **정확한 용량 계산**: NTFS 하드 링크 중복 제거와 Free Space 및 Allocated/System Space 가상 항목을 지원합니다.
- **Windows 통합**: 파일 열기, 탐색기에서 표시, 경로와 상세 정보 복사, CMD/PowerShell 열기, 휴지통 이동, 영구 삭제, Windows 속성 표시.
- **내보내기 및 다국어**: 표준 CSV 내보내기와 영어, 번체 중국어, 간체 중국어, 일본어, 한국어, 스페인어, 프랑스어의 실행 중 언어 전환.

## 🖱️ 기본 사용법

1. 드라이브 또는 폴더를 선택하고 **Scan**을 클릭합니다.
2. Tree View에서 계층을 확인하거나 File View와 File Types에서 대상 항목을 찾습니다.
3. Ctrl/Shift로 여러 항목을 선택한 다음 우클릭 메뉴에서 일괄 작업을 실행합니다.
4. 파일을 더블클릭하면 Windows 기본 프로그램으로 열립니다. 추가 작업은 우클릭 메뉴에서 선택합니다.
5. Treemap의 도구 설명, 확대/축소 및 계층 탐색으로 큰 항목을 빠르게 찾습니다.

파일을 이동하거나 삭제하기 전에 선택한 경로를 확인하십시오. 영구 삭제한 항목은 휴지통에서 복원할 수 없습니다.

## 📦 다운로드 및 설치

### Portable 버전

[GitHub Releases](https://github.com/hs9021401/DiskAnalyzer/releases)에서 `DiskAnalyzer_Portable_win-x64.zip`을 다운로드하고 압축을 푼 후 `DiskAnalyzer.exe`를 실행합니다. Portable 버전은 설치가 필요 없으며 .NET 런타임이 포함되어 있습니다.

### Inno Setup 설치 프로그램

Release의 설치 프로그램을 실행하고 설치 언어, 바탕 화면 바로 가기, Windows 탐색기 컨텍스트 메뉴 통합을 선택합니다.

## 💻 요구 사항

- Windows 10, Windows 11 또는 호환되는 Windows Server x64.
- Portable 및 설치 버전은 별도의 .NET 설치가 필요하지 않습니다.
- 소스 빌드에는 .NET 10 SDK가 필요합니다.
- 관리자 권한은 필수는 아니지만 NTFS 스캔 접근 범위와 성능을 개선할 수 있습니다.

## 🔧 소스에서 빌드

Windows, PowerShell 및 .NET 10 SDK에서 실행합니다.

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

self-contained single-file Portable 버전 만들기:

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## ⚠️ 주의 사항

- DiskAnalyzer는 현재 Windows x64만 지원하며 Linux와 macOS는 지원하지 않습니다.
- 보호되었거나 오프라인 상태이거나 접근할 수 없는 폴더는 건너뛸 수 있습니다.
- 스캔, 휴지통 이동 및 삭제 작업은 선택한 파일에 적용됩니다. 중요한 데이터는 백업하십시오.
- 공개 API는 아직 안정적이지 않으므로 라이브러리로 사용할 때 버전 변경에 유의하십시오.

## 📄 라이선스 및 제3자 고지

이 프로젝트는 [MIT License](LICENSE)로 배포됩니다. 배포 가능한 Portable 및 설치 버전에는 `LICENSE.txt`와 `THIRD-PARTY-NOTICES.txt`가 포함되어 있으며, self-contained .NET 런타임과 관련 종속성의 고지 및 라이선스 링크를 제공합니다.

Copyright © 2026 Alex Lin.
