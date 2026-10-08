@ECHO OFF
SETLOCAL EnableExtensions
ECHO #######################################################
ECHO ##            APK Shell Extension  2                 ##
ECHO ##                                                   ##
ECHO ##           http://apkshellext.com      s            ##
ECHO #######################################################

REM === check and get the UAC for administrator privilege ===
REM === code from https://sites.google.com/site/eneerge/scripts/batchgotadmin
REM === 
:: BatchGotAdmin
:-------------------------------------
REM  --> Check for permissions
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"

REM --> If error flag set, we do not have admin.
if '%errorlevel%' NEQ '0' (
    echo Requesting administrative privileges...
    goto UACPrompt
) else ( goto gotAdmin )

:UACPrompt
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "%~s0", "", "", "runas", 1 >> "%temp%\getadmin.vbs"

    "%temp%\getadmin.vbs"
    exit /B

:gotAdmin
    if exist "%temp%\getadmin.vbs" ( del "%temp%\getadmin.vbs" )
    pushd "%CD%"
    CD /D "%~dp0"
:--------------------------------------

REM Register both COM bitnesses; do not remove or overwrite unrelated Shell keys.
set "DLL=%~dp0apkshellext2.dll"
set "REGASM32=%windir%\Microsoft.NET\Framework\v4.0.30319\regasm.exe"
set "REGASM64=%windir%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
if not exist "%DLL%" (
    ECHO ERROR: ApkShellext2.dll was not found next to this script.
    exit /B 1
)
set "REGISTERED=0"
set "FAILED=0"
if exist "%REGASM64%" (
    ECHO Unregistering 64-bit Shell handlers...
    "%REGASM64%" /unregister "%DLL%"
    if errorlevel 1 set "FAILED=1"
    set "REGISTERED=1"
)
if exist "%REGASM32%" (
    ECHO Unregistering 32-bit Shell handlers...
    "%REGASM32%" /unregister "%DLL%"
    if errorlevel 1 set "FAILED=1"
    set "REGISTERED=1"
)
if "%REGISTERED%"=="0" (
    ECHO ERROR: The .NET Framework registration tool was not found.
    exit /B 1
)
if "%FAILED%"=="1" (
    ECHO ERROR: At least one registration command failed. See details above.
    exit /B 1
)

ECHO Unregistration completed. Restart Explorer manually before removing files.

ECHO.
ECHO /-------------------------------------------------------------------\
ECHO  apkshellext is an open-source project,
ECHO  Please visit http://apkshellext.com for more information
ECHO \-------------------------------------------------------------------/

PAUSE
@ECHO ON
