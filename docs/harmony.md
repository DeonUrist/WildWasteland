# Harmony patch inventory

Every patched method appears here exactly once. A target may have only ONE patch method per kind (prefix/postfix/finalizer) in Wild Wasteland; modules subscribe to the dispatcher instead of patching the same target again.

| Target (Type.Method) | Kind | Dispatcher | Subscribed modules | Hot? (calls/frame) | Early-out filter | Known other patchers |
|---|---|---|---|---|---|---|
| | | | | | | |

"Known other patchers" = other mods (ours or third-party) that patch the same target. Known shared targets: `SendEvent.OnEnter`, `CreateObject.OnEnter`, `Raycast.OnEnter/DoRaycast`, `SetVelocity.DoSetVelocity`, `Rotate.DoRotate`, `SmoothLookAt.DoSmoothLookAt`, `AddForce.DoAddForce`, `SensorGetDetections.DoAction`, `SensorGetDetectionRayHit.*`, `SetFsmFloat.OnEnter`, `GetButtonDown.OnUpdate` (ApocalypterAI, PartAdjustment/Ultimate Tools, Apocasetter InputBlocker, standalone Gunplay/NPCAI).
