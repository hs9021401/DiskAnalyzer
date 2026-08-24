; =====================================================================
; DiskAnalyzer - Inno Setup Script
; Ultra-Fast NTFS Disk Space Visualizer and Analyzer
; Copyright © 2026 Alex Lin. All rights reserved.
; =====================================================================

#define MyAppName "DiskAnalyzer"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Alex Lin"
#define MyAppExeName "DiskAnalyzer.exe"
#define MyAppCopyright "Copyright © 2026 Alex Lin. All rights reserved."

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
Name: "chinesetraditional"; MessagesFile: "installer_languages\ChineseTraditional.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "contextmenu"; Description: "新增 Windows 檔案總管右鍵選單整合 (Analyze with DiskAnalyzer)"; GroupDescription: "系統整合功能:"

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
Root: HKA; Subkey: "Software\Classes\Drive\shell\{#MyAppName}"; ValueType: string; ValueData: "使用 DiskAnalyzer 分析磁碟空間"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Drive\shell\{#MyAppName}"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Drive\shell\{#MyAppName}\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu

; Windows Explorer Context Menu for Folders/Directories
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#MyAppName}"; ValueType: string; ValueData: "使用 DiskAnalyzer 分析此資料夾"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#MyAppName}"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#MyAppName}\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu

[Run]
; Option to launch the application immediately after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up any temp or runtime files generated during use
Type: filesandordirs; Name: "{app}"
