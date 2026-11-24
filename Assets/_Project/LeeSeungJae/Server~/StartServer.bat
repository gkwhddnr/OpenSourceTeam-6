@echo off
setlocal
pushd "%~dp0" || exit /b 1
echo Starting the employee AI server. Press Ctrl+C to stop.
dotnet run --project "Gateway\Gateway.csproj" --no-launch-profile
set "serverExit=%errorlevel%"
popd
if not "%serverExit%"=="0" pause
exit /b %serverExit%
