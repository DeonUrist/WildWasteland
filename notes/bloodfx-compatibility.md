# BloodFX compatibility — Testing, 2026-10-21

Denis installed BloodFX and reports it does not work with WW, then requested a compatibility patch inside WW. Installed plugin logs BloodFX0.2.0 startup; General Enabled/NpcsBleed/PlayerBleeds are true. SHA256: 13AB800FBF77760A45341AE8F6F218206478151437DF3D2CA990A417B343BD3B. No source was found in the game-source dump; read-only ILSpy inspection of the installed assembly established the relevant contracts.

BloodFX.Plugin.Update owns input, scanning, health polling and effect maintenance. Awake provides no persistent update relay or scene-load recovery. Its two DontDestroyOnLoad calls protect particle objects only. BloodFX.BloodRelay.OnParticleCollision tests Owner with Unity null semantics, so a destroyed plugin component cannot handle spray collisions despite retaining a usable managed instance. The Weapons Pack repository's earlier TESTING.md additionally records a destroyed native BloodFX component in its historical live diagnostic; that is historical evidence, not a new current-session probe.

## Patch

Guns/BloodFxCompat.cs discovers com.apoca.bloodfx after plugin loading through the existing Bosses interval. It validates the initialized config, exact Plugin/relay type, method signatures and Owner field, then retains the managed instance and closed delegates (no hard assembly dependency).

One Guns-owned WW scheduler callback delegates to original Update only when Unity considers the original component destroyed and WW is in active gameplay. While the component remains alive (including deliberately disabled), WW never calls its updater. No additional health/world scan or modified scan/poll cadence is introduced. Once per recovered owner it logs the takeover. A signature-checked Guns-owned prefix on BloodRelay.OnParticleCollision forwards only relays whose managed Owner is exactly that retained destroyed instance; all other relays keep their native behavior. Module shutdown removes the callback and releases retained state; the normal module patch lifecycle removes the prefix. Existing Weapons Pack optional BloodFX integration can still observe that same managed owner and its methods.

No BloodFX DLL/config replacement, plugin-object cloning, new MonoBehaviour, native damage interception, settings or save schema changes. The compatibility belongs to Guns and is inactive when Guns is disabled or BloodFX is absent. BloodFX's own polling/effect overhead is restored, not reduced or certified hitch-free.

## Build/deployment

Windows Release/Deploy=false: zero errors and two existing NPC PoiNav CS0649 warnings. Game closed, exact disabled manifests absent in both cache roots, normalized runtime indexes equal and all361 runtime assets checked. DLL-only installed; live assets/index/settings/layouts/saves retained.

- Stage: V:/Repos/Apocalypter/_releases/WildWasteland_bloodfx-compat_20261021_223031_313.dll
- Previous DLL: same stem .before.dll
- Installed MD5: BAA0316FA5E6CA8DBDB10EF7F612FDDB

No game launch/harness/commit/push/release. The existing native IK removal, Weapons Pack behavior and disabled dismemberment are included.

## Denis's game checks

1. Start/load and check `[Guns] BloodFX 0.2.0: scene-load lifecycle recovery connected.`
2. If the native plugin is destroyed, expect `[Guns] BloodFX: native plugin object destroyed; continuing its managed updater through WW.` Its absence alone does not prove failure if native BloodFX remains alive.
3. Press BloodFX's configured F8 test key. Then shoot/strike a living enemy and existing corpse; verify spray, floor impacts/puddles and configured bleed-out. Confirm ordinary damage/WW tracers remain intact. Check chainsaw contacts separately with Weapons Pack.
4. Save, return to title, reload and repeat. No duplicate sprays/update effects should appear while native BloodFX survives. Check logs for Scan/Poll/Update errors and WW scheduler errors.
5. Disable BloodFX's master switch: normal effects must stop. With Guns disabled, this adapter is inactive and standalone BloodFX's own lifecycle applies.

Current actual component destruction, automatic lifecycle callbacks, rendered effects and performance require Denis's game test. No blanket compatibility/Verified claim.
