; NSIS Installation Script for OllamaAgent
; Build with: makensis.exe setup.nsi

!define APP_NAME "OllamaAgent"
!define APP_VERSION "1.0.0"
!define APP_PUBLISHER "OllamaAgent Team"
!define APP_URL "https://github.com/adrobotov/OllamaAgent"

OutFile "OllamaAgent_Setup.exe"
InstallDir "$PROGRAMFILES64\OllamaAgent"
RequestExecutionLevel admin

Page directory
Page instfiles

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
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${APP_PUBLISHER}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon" "$INSTDIR\OllamaAgent.exe"
SectionEnd

Section "Uninstall"
    ; Remove registry entry
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    
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
