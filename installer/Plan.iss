; Instalátor aplikace Plan (Inno Setup 6).
;
; Sestavení:
;   ISCC.exe /DAppVersion=1.0.0 /DSourceExe=..\publish\Plan.exe installer\Plan.iss
;
; Instaluje se per-user do %LocalAppData%\Programs\Plan, takže není potřeba UAC ani
; práva správce. Aktualizace se instaluje přes stávající verzi do stejné složky pod
; stejným názvem souboru — díky tomu zůstane zástupce na ploše na svém místě.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef SourceExe
  #define SourceExe "..\publish\Plan.exe"
#endif

#define AppName "Plan"
#define AppPublisher "DarwinKhonus"
#define AppUrl "https://github.com/DarwinKhonus/Plan"
#define AppExeName "Plan.exe"

[Setup]
; AppId musí zůstat neměnné — podle něj Windows pozná, že jde o aktualizaci
; existující instalace, a ne o druhý souběžný produkt.
AppId={{8F3A6C21-5D47-4E9B-A0F2-6B1C93D8E4A7}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases

; Instalace jen pro přihlášeného uživatele: {autopf} se pak mapuje
; na %LocalAppData%\Programs, žádné UAC.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto

OutputDir=..\installer-output
OutputBaseFilename={#AppName}-{#AppVersion}-setup
SetupIconFile=..\Plan\Resources\plan.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Aplikace je 64bitová (win-x64), na 32bitovém systému nemá smysl ji instalovat.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Když při aktualizaci běží stará verze, Inno ji nabídne zavřít místo toho,
; aby instalace skončila chybou „soubor je používán".
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"

[Tasks]
Name: "desktopicon"; Description: "Vytvořit zástupce na ploše"; GroupDescription: "Zástupci:"

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Ruční instalace: zaškrtávátko na poslední stránce průvodce.
Filename: "{app}\{#AppExeName}"; Description: "Spustit aplikaci {#AppName}"; \
    Flags: nowait postinstall skipifsilent

; Tichá instalace (aktualizace z aplikace): průvodce se nezobrazí, takže se aplikace
; spustí bez ptaní. Uživatel ji před instalací měl otevřenou a instalátor mu ji zavřel,
; takže ji čeká zase otevřenou.
Filename: "{app}\{#AppExeName}"; Flags: nowait; Check: JeTichaInstalace

; Databáze v %AppData%\Plan se záměrně nemaže — odinstalace nesmí připravit
; uživatele o naplánované termíny. Kdo chce smazat i data, smaže složku ručně.
[UninstallDelete]
Type: dirifempty; Name: "{app}"

; Sekce [Code] musí zůstat poslední — vše za ní se čte jako Pascal, takže by se
; na komentářích začínajících středníkem překlad rozbil.
[Code]
function JeTichaInstalace(): Boolean;
begin
  Result := WizardSilent;
end;
