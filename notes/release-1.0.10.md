# Wild Wasteland 1.0.10 — Fog-of-war saving

Fixes saving being rejected with `Fog of war save failed: Win32 IO returned 1175`. The fog writer now uses the existing Windows-safe atomic replacement used by other save participants. A later successful callback does not erase a genuine earlier transaction failure.

Save formats and settings are unchanged. Reload the last good snapshot after an already failed transaction, then test saving twice, overwriting a named save, and save-and-exit/reload. Expect save completion, no error 1175 and retained explored map areas. Gameplay/save-integrity confirmation remains **Testing**.

Extract the complete ZIP into `BepInEx/plugins`, preserving customized files and backing up saves. No companion mod DLLs are bundled.

Build and package: Windows Release/Deploy=false: zero errors, two existing NPC CS0649 warnings. Exact stage `_releases/WildWasteland_1.0.10a.dll`, MD5 **6D0E4C66D41A87A7FBBA9267B71EC62B**. Runtime verification remains Denis's. ZIP **535 files**, **206203799 bytes**; SHA256 **4a2113aff5bf71cb30feca1c4bce7f1169b3e9cce9de86264f7a64756f05e6cf**. All **534 non-DLL entries** are byte-identical to the previous release. CRC/exact bytes/safe unique paths/single DLL/18 loading screens checked. No game/harness/live save/config edits.
