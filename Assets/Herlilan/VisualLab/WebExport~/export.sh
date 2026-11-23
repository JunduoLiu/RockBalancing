#!/bin/bash
set -eu
template=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$template/../../../.." && pwd)
version=$(awk '/^m_EditorVersion:/ {print $2}' "$project/ProjectSettings/ProjectVersion.txt")
unity=${UNITY_EDITOR:-"/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"}
destination=${1:?Pass a new directory for the exported website}
case "$destination" in /*) ;; *) destination="$PWD/$destination";; esac
if [ -e "$destination" ]; then printf 'Output already exists: %s\n' "$destination" >&2; exit 1; fi
stage=$(mktemp -d "${TMPDIR:-/tmp}/stillbalance-web.XXXXXX")
printf 'Temporary project and build.log: %s\n' "$stage"
cp -R "$project/Assets" "$project/Packages" "$project/ProjectSettings" "$stage/"
mkdir -p "$stage/Assets/Editor" "$stage/Assets/WebGLTemplates/StillBalance"
cp "$template/StillBalanceWebBuild.cs" "$stage/Assets/Editor/"
cp "$template/index.html" "$stage/Assets/WebGLTemplates/StillBalance/"
touch "$stage/.stillbalance-export"
"$unity" -batchmode -quit -projectPath "$stage" -buildTarget WebGL -executeMethod StillBalanceWebBuild.Run -logFile "$stage/build.log"
test -s "$stage/Export/index.html"
mkdir -p "$(dirname "$destination")"
cp -R "$stage/Export" "$destination"
printf 'Portrait site: %s\n' "$destination"
