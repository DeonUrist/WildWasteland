# Decisions

Short records of choices that later sessions must not silently undo. Newest first.

Format:
## YYYY-MM-DD - title
Decision. Why. What it rules out.

## 2026-10-13 - Repo scaffold
- One plugin, one GUID `com.denis.apocalypter.wildwasteland`, one Harmony id, one runner GameObject. Mechanics are modules inside it.
- Sources compile from `src/**` in both build.sh and the csproj (no hand-kept file list).
- csproj deploy is opt-in (`-p:Deploy=true`) because the game locks loaded DLLs.
- No `ModDev\WildWasteland` working copy: this repo is the only source. (Confirm with Denis.)
- Performance budget: see docs/perf-log.md.
