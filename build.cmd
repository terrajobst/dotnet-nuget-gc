@echo off
setlocal

set SLN=%~dp0src\dotnet-nuget-gc.csproj
set BIN=%~dp0bin\
set CONFIG=release

dotnet build %SLN% -c %CONFIG% -o=%BIN% /nologo
