; NSIS Installation Script for OllamaAgent
; Build with: makensis.exe setup.nsi

!define APP_NAME "OllamaAgent"
!define APP_VERSION "1.1.0"
!define APP_PUBLISHER "OllamaAgent Team"
!define APP_URL "https://github.com/adrobotov/OllamaAgent"

OutFile "OllamaAgent_Setup.exe"
InstallDir "$LOCALAPPDATA\OllamaAgent"
RequestExecutionLevel user

; Use modern interface
!include "MUI2.nsh"

; Modern UI settings
!define MUI_ABORTWARNING
!define MUI_ICON="..\src\OllamaAgent\Assets\icons\icon-256.ico"

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Russian"

Section "Install"
    SetOutPath "$INSTDIR"
    
    ; Copy all application files
    File /r "publish\*"
    
    ; Create shortcuts
    CreateShortCut "$DESKTOP\OllamaAgent.lnk" "$INSTDIR\OllamaAgent.exe"
    CreateShortCut "$STARTMENU\OllamaAgent.lnk" "$INSTDIR\OllamaAgent.exe"
    
    ; Register uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    
    ; Write registry entry for add/remove programs
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${APP_PUBLISHER}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon" "$INSTDIR\OllamaAgent.exe"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
SectionEnd

Section "Uninstall"
    ; Remove registry entry
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    
    ; Remove shortcuts
    Delete "$DESKTOP\OllamaAgent.lnk"
    Delete "$STARTMENU\OllamaAgent.lnk"
    
    ; Remove all files
    RMDir /r "$INSTDIR\*"
    
    ; Remove uninstaller
    Delete "$INSTDIR\Uninstall.exe"
    
    ; Remove directory
    RMDir "$INSTDIR"
SectionEnd
