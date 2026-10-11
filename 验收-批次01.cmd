@echo off
rem ASCII-only runner (safe under any console codepage). Chinese guidance is printed
rem by the tools themselves: dotnet build/test (localized) and TodoWpf.DataCheck.
rem 936 = default Chinese Windows console codepage; set explicitly so dotnet output
rem renders correctly, then restore after the probe (probe switches console to UTF-8).
chcp 936 >nul
cd /d "%~dp0"

echo ============================================================
echo  Desktop Todo (WPF branch) - Batch 01 acceptance
echo  Steps: [1] build  [2] tests  [3] read-only data check
echo  Safe: this script never modifies your data files.
echo ============================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] "dotnet" not found - .NET SDK missing or not in PATH.
    echo Please send a screenshot of this window to the dev session.
    goto END
)

echo [1/3] build ...
echo ------------------------------------------------------------
dotnet build
if errorlevel 1 goto FAIL

echo.
echo [2/3] tests ...
echo ------------------------------------------------------------
dotnet test
if errorlevel 1 goto FAIL

echo.
echo [3/3] read-only data check ...
echo ------------------------------------------------------------
dotnet run --project src/TodoWpf.DataCheck

chcp 936 >nul
echo.
echo [DONE] Pass criteria are printed in Chinese above.
echo        If anything looks wrong, send a screenshot to the dev session.
goto END

:FAIL
echo.
echo [FAILED] A step failed - see the messages above.
echo          Please send a screenshot of this window to the dev session.

:END
echo.
pause
