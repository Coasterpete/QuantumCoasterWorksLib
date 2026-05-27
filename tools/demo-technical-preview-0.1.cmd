@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"

if not exist "%REPO_ROOT%\QuantumCoasterWorks.sln" (
    echo Could not resolve repository root. Expected solution file at "%REPO_ROOT%\QuantumCoasterWorks.sln".
    exit /b 1
)

pushd "%REPO_ROOT%" >nul
if errorlevel 1 (
    echo Failed to enter repository root "%REPO_ROOT%".
    exit /b 1
)

powershell -ExecutionPolicy Bypass -File .\tools\demo-technical-preview-0.1.ps1
set "EXIT_CODE=%ERRORLEVEL%"

popd >nul
exit /b %EXIT_CODE%
