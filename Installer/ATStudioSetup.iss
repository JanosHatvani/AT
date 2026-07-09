; ============================================================================
; AT Studio - Inno Setup telepítő szkript
; ============================================================================
; ELŐFELTÉTEL: az AT.App projektet előbb self-contained módban publikálni
; kell (lásd SelfContainedWinX64.pubxml és a README_Installer.md-t), MIELŐTT
; ezt a szkriptet lefordítod. Ez a szkript a publish kimeneti mappájából olvas.
;
; FORDÍTÁS: nyisd meg ezt a fájlt az Inno Setup Compiler-ben (ISCC.exe / a
; grafikus Inno Setup IDE-ben), és nyomj Compile-t (Ctrl+F9). A kész telepítő
; az "Output" mappában jön létre: AT-Studio-Setup-1.0.0.exe
; ============================================================================

#define MyAppName "AT Studio"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AT Studio"
#define MyAppExeName "AT.App.exe"

; Ez az elérési út a dotnet publish kimenetére mutat — ha a solution-öd más
; mappaszerkezetű, vagy a projekt neve/útja eltér, ezt az egy sort kell
; módosítanod (lásd README_Installer.md 2. lépés).
#define PublishDir "..\AT.App\bin\Release\net9.0-windows\win-x64\publish"

[Setup]
; Egyedi azonosító az alkalmazáshoz — FONTOS: ezt az egy GUID-ot generáld le
; magad (Inno Setup IDE-ben: Tools > Generate GUID), és NE változtasd meg
; többé jövőbeli verzióknál — ez teszi lehetővé, hogy a frissítő telepítő
; felismerje és lecserélje a korábbi verziót, ne kettőt telepítsen egymás mellé.
AppId={{B5D9F1A2-4C3E-4A8B-9F1D-2E6C8A0D5F31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; A kimeneti (kész) installer .exe ide kerül, a szkript mellé.
OutputDir=Output
OutputBaseFilename=AT-Studio-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; 64 bites Windows szükséges (win-x64 self-contained publish miatt).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Ne kérjen admin-jogot feltétlenül, ha a felhasználó saját mappájába
; (pl. AppData\Local) telepít — de mivel Program Files-ba telepítünk
; alapértelmezetten, ez admin-jogot igényel majd a UAC-on keresztül.
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "hungarian"; MessagesFile: "compiler:Languages\Hungarian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; A teljes publish-kimenetet becsomagolja, rekurzívan (az .exe, a self-contained
; runtime fájljai, és minden natív .dll, ami a WebView2/WPF/Automation drivereknek kell).
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; A telepítés végén felajánlja az azonnali indítást.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Az App.xaml.cs SettingsService a %AppData%\AT\settings.json fájlba ír — ezt az
; eltávolító SZÁNDÉKOSAN NEM törli, hogy a felhasználó beállításai/scheduled-tasks.json/
; history-fájljai megmaradjanak egy esetleges újratelepítéskor. Ha a vevő cég szeretné,
; hogy az eltávolító ezeket is törölje, egy [Code] szekció addModal InputOption-nal
; ("Beállítások és adatok törlése is?") bővíthető — szólj, ha ezt szeretnéd, és megírom.
