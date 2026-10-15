#!/bin/sh
# Builds WildWasteland.dll with mono mcs against the game's own libraries.
# Usage: MANAGED=<Apocalypter_Data/Managed> BEPCORE=<BepInEx/core> sh build.sh [outdir]
# The assembly is named after the -out file, so the file is always <outdir>/WildWasteland.dll.
# Sources are every .cs under src/ (same set as the csproj glob), so no file list to keep in sync.
# Embedded resources: every file under src/Modules/<Module>/Embedded/ is embedded with the logical name
#   <Module>.<path below Embedded with / replaced by .>   e.g. Embedded/DefaultTemplates/Bike.json -> Patrols.DefaultTemplates.Bike.json
set -e
cd "$(dirname "$0")"
M=${MANAGED:-/e/SteamLibrary/steamapps/common/Apocalypter/Apocalypter_Data/Managed}
B=${BEPCORE:-/e/SteamLibrary/steamapps/common/Apocalypter/BepInEx/core}
OUTDIR=${1:-bin}
mkdir -p "$OUTDIR"
SRC=$(find src -name '*.cs' | sort)
[ -n "$SRC" ] || { echo "build.sh: no .cs files under src/" >&2; exit 1; }
RES=""
for f in $(find src/Modules -path '*/Embedded/*' -type f | sort); do
  mod=$(echo "$f" | cut -d/ -f3)
  rel=$(echo "$f" | sed -e "s#^src/Modules/$mod/Embedded/##" -e 's#/#.#g')
  RES="$RES -resource:$f,$mod.$rel"
done
REFS=""
for d in mscorlib System System.Core netstandard \
  UnityEngine UnityEngine.CoreModule UnityEngine.PhysicsModule UnityEngine.IMGUIModule UnityEngine.TextRenderingModule \
  UnityEngine.InputLegacyModule UnityEngine.AnimationModule UnityEngine.AudioModule UnityEngine.ParticleSystemModule \
  UnityEngine.ImageConversionModule UnityEngine.AssetBundleModule UnityEngine.UI UnityEngine.UIModule \
  UnityEngine.TerrainModule UnityEngine.TerrainPhysicsModule UnityEngine.JSONSerializeModule UnityEngine.DirectorModule \
  UnityEngine.UnityWebRequestModule UnityEngine.UnityWebRequestAudioModule \
  Unity.TextMeshPro Unity.InputSystem PlayMaker Micosmo.SensorToolkit \
  NWH.Common NWH.VehiclePhysics2 NWH.WheelController MapMagic Den.Tools \
  Assembly-CSharp Assembly-CSharp-firstpass; do REFS="$REFS -r:$M/$d.dll"; done
mcs -nostdlib -noconfig -target:library -langversion:latest -optimize+ -nowarn:649,414,169,219,162,618,108,114,67,168,1717,429,665 \
  -out:"$OUTDIR/WildWasteland.dll" $REFS -r:$B/BepInEx.dll -r:$B/0Harmony.dll -r:$B/Mono.Cecil.dll -r:$B/MonoMod.Utils.dll $RES $SRC
echo "built $OUTDIR/WildWasteland.dll"
