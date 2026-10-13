#!/bin/sh
# Builds WildWasteland.dll with mono mcs against the game's own libraries.
# Usage: MANAGED=<Apocalypter_Data/Managed> BEPCORE=<BepInEx/core> sh build.sh [outdir]
# The assembly is named after the -out file, so the file is always <outdir>/WildWasteland.dll.
# Sources are every .cs under src/ (same set as the csproj glob), so no file list to keep in sync.
set -e
cd "$(dirname "$0")"
M=${MANAGED:-/e/SteamLibrary/steamapps/common/Apocalypter/Apocalypter_Data/Managed}
B=${BEPCORE:-/e/SteamLibrary/steamapps/common/Apocalypter/BepInEx/core}
OUTDIR=${1:-bin}
mkdir -p "$OUTDIR"
SRC=$(find src -name '*.cs' | sort)
[ -n "$SRC" ] || { echo "build.sh: no .cs files under src/" >&2; exit 1; }
mcs -nostdlib -noconfig -target:library -langversion:7 -optimize+ -out:"$OUTDIR/WildWasteland.dll" \
  -r:$M/mscorlib.dll -r:$M/System.dll -r:$M/System.Core.dll -r:$M/netstandard.dll \
  -r:$B/BepInEx.dll -r:$B/0Harmony.dll \
  -r:$M/UnityEngine.dll -r:$M/UnityEngine.CoreModule.dll -r:$M/UnityEngine.PhysicsModule.dll \
  -r:$M/UnityEngine.IMGUIModule.dll -r:$M/UnityEngine.TextRenderingModule.dll -r:$M/UnityEngine.InputLegacyModule.dll \
  -r:$M/UnityEngine.AnimationModule.dll -r:$M/UnityEngine.AudioModule.dll -r:$M/UnityEngine.ParticleSystemModule.dll \
  -r:$M/UnityEngine.ImageConversionModule.dll -r:$M/UnityEngine.AssetBundleModule.dll \
  -r:$M/UnityEngine.UI.dll -r:$M/UnityEngine.UIModule.dll -r:$M/Unity.TextMeshPro.dll -r:$M/Unity.InputSystem.dll \
  -r:$M/PlayMaker.dll -r:$M/Assembly-CSharp.dll -r:$M/Assembly-CSharp-firstpass.dll \
  $SRC
echo "built $OUTDIR/WildWasteland.dll"
