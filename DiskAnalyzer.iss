; =====================================================================
; DiskAnalyzer - Inno Setup Script
; Ultra-Fast NTFS Disk Space Visualizer and Analyzer
; Copyright © 2026 Alex Lin
; =====================================================================

#define MyAppName "DiskAnalyzer"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Alex Lin"
#define MyAppExeName "DiskAnalyzer.exe"
#define MyAppCopyright "Copyright © 2026 Alex Lin"

[Setup]
; App basic identity
AppId={{D3A5F7B8-9A2E-4C78-8E9B-527D6E9A8C10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright={#MyAppCopyright}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright={#MyAppCopyright}
VersionInfoProductName={#MyAppName}

; Installation target directory (Standard 64-bit Program Files)
DefaultDirName={autopf64}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Architecture: 64-bit only (Windows 10 / 11 x64)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Privileges & Execution
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline

; Output settings
OutputDir=SetupOutput
OutputBaseFilename=DiskAnalyzer_Setup_v{#MyAppVersion}
SetupIconFile=src\DiskAnalyzer.UI\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compression settings (Maximum compression for fastest download & setup)
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Modern UI Style
WizardStyle=modern
WizardSizePercent=110
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesetraditional"; MessagesFile: "installer_languages\ChineseTraditional.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
ContextMenuTask=Add Windows File Explorer context menu integration (Analyze with DiskAnalyzer)
SystemIntegration=System integration:
ContextMenuDrive=Analyze disk space with DiskAnalyzer
ContextMenuFolder=Analyze this folder with DiskAnalyzer

chinesetraditional.ContextMenuTask=新增 Windows 檔案總管右鍵選單整合（使用 DiskAnalyzer 分析）
chinesetraditional.SystemIntegration=系統整合：
chinesetraditional.ContextMenuDrive=使用 DiskAnalyzer 分析磁碟空間
chinesetraditional.ContextMenuFolder=使用 DiskAnalyzer 分析此資料夾

chinesesimplified.ContextMenuTask=添加 Windows 文件资源管理器右键菜单集成（使用 DiskAnalyzer 分析）
chinesesimplified.SystemIntegration=系统集成：
chinesesimplified.ContextMenuDrive=使用 DiskAnalyzer 分析磁盘空间
chinesesimplified.ContextMenuFolder=使用 DiskAnalyzer 分析此文件夹

japanese.ContextMenuTask=Windows ファイル エクスプローラーのコンテキスト メニュー統合を追加（DiskAnalyzer で分析）
japanese.SystemIntegration=システム統合:
japanese.ContextMenuDrive=DiskAnalyzer でディスク容量を分析
japanese.ContextMenuFolder=DiskAnalyzer でこのフォルダーを分析

korean.ContextMenuTask=Windows 파일 탐색기 컨텍스트 메뉴 통합 추가(DiskAnalyzer로 분석)
korean.SystemIntegration=시스템 통합:
korean.ContextMenuDrive=DiskAnalyzer로 디스크 공간 분석
korean.ContextMenuFolder=DiskAnalyzer로 이 폴더 분석

spanish.ContextMenuTask=Agregar integración con el menú contextual del Explorador de archivos de Windows (Analizar con DiskAnalyzer)
spanish.SystemIntegration=Integración del sistema:
spanish.ContextMenuDrive=Analizar el espacio del disco con DiskAnalyzer
spanish.ContextMenuFolder=Analizar esta carpeta con DiskAnalyzer

french.ContextMenuTask=Ajouter l'intégration au menu contextuel de l'Explorateur de fichiers Windows (Analyser avec DiskAnalyzer)
french.SystemIntegration=Intégration système :
french.ContextMenuDrive=Analyser l'espace disque avec DiskAnalyzer
french.ContextMenuFolder=Analyser ce dossier avec DiskAnalyzer

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "contextmenu"; Description: "{cm:ContextMenuTask}"; GroupDescription: "{cm:SystemIntegration}"

[Files]
; Source from the self-contained portable win-x64 build
Source: "DiskAnalyzer_Portable_win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu Shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; Desktop Shortcut
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"

[Registry]
; Windows Explorer Context Menu for Drives (e.g. C:\, D:\)
Root: HKA; Subkey: "Software\Classes\Drive\shell\{#MyAppName}"; ValueType: string; ValueData: "{cm:ContextMenuDrive}"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Drive\shell\{#MyAppName}"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Drive\shell\{#MyAppName}\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu

; Windows Explorer Context Menu for Folders/Directories
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#MyAppName}"; ValueType: string; ValueData: "{cm:ContextMenuFolder}"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#MyAppName}"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#MyAppName}\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu

[Run]
; Option to launch the application immediately after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up any temp or runtime files generated during use
Type: filesandordirs; Name: "{app}"
