#!/bin/bash

# Get the directory path of this script.
# https://stackoverflow.com/questions/59895/get-the-source-directory-of-a-bash-script-from-within-the-script-itself
CMD_HOME="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

cd "${CMD_HOME}" || exit 1

SLN=${CMD_HOME}/src/dotnet-nuget-gc.csproj
BIN=${CMD_HOME}/bin/
CONFIG=release

dotnet build "${SLN}" -c ${CONFIG} -o="${BIN}" -nologo
