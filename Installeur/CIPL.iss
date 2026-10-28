; ════════════════════════════════════════════════════════════════════════════
; CIPL — étape 15 : l'installeur Windows (Inno Setup 6, gratuit : https://jrsoftware.org).
; Construit depuis Unity, menu CIPL › Construire l'installeur (Assets/Editor/ConstruireInstalleur.cs),
; qui passe /DVersion, /DSource (le build Unity) et /O (le dossier de sortie).
; La version Mac (étape 15b) n'a pas d'installeur : un .zip (menu CIPL › Construire la version Mac).
; Pour la personne seulement, sans droits d'administrateur, dans %LOCALAPPDATA%\Programs\CIPL :
; la mise à jour lancée par CIPL (MiseAJour.cs, /SILENT) n'a besoin d'aucune autorisation.
; AppId : NE JAMAIS LE CHANGER, c'est lui qui fait remplacer une version déjà installée.
; Nom : le nom visible (raccourcis, liste des programmes) ; il pourra changer, pas AppId.
; ════════════════════════════════════════════════════════════════════════════
#define Nom "CIPL"
#define Exe "CIPL.exe"
#ifndef Version
  #define Version "0.0.0"
#endif
#ifndef Source
  #define Source "..\..\Build\Installation\CIPL"
#endif

[Setup]
AppId={{8F3C2A71-5D4E-4B9A-A6C1-2E7D9F04B3C8}
AppName={#Nom}
AppVersion={#Version}
AppVerName={#Nom} {#Version}
AppPublisher={#Nom}
VersionInfoVersion={#Version}
DefaultDirName={localappdata}\Programs\CIPL
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=CIPL-installation
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Lancé à la main pendant que CIPL est ouvert : propose de le fermer (jamais de force).
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#Exe}
UninstallDisplayName={#Nom}

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "bureau"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Une version précédente : ses fichiers Unity sont remplacés en entier, sans restes.
Type: filesandordirs; Name: "{app}\CIPL_Data"

[Files]
Source: "{#Source}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#Nom}"; Filename: "{app}\{#Exe}"
Name: "{autodesktop}\{#Nom}"; Filename: "{app}\{#Exe}"; Tasks: bureau

[Registry]
; Liens cipl:// retirés le 26/10 (inutilisés : tout passe par des codes) ; les versions
; précédentes les avaient déclarés, on les efface.
Root: HKCU; Subkey: "Software\Classes\cipl"; ValueType: none; Flags: deletekey

[Run]
; Aussi après une mise à jour lancée par CIPL (/SILENT) : CIPL se rouvre.
Filename: "{app}\{#Exe}"; Description: "{cm:LaunchProgram,{#Nom}}"; Flags: nowait postinstall
