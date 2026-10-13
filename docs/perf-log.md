# Performance log

One entry per build that changes runtime behaviour. Measure with ModProfiler (deep capture, 30 s) on the scenarios in `verification/README.md`. Keep raw captures out of git (`verification/perf/raw/`); copy the summary numbers here.

Budget (initial targets, adjust in docs/decisions.md):
- WW managed self time: average <= 0.5 ms/frame, worst frame <= 2 ms, in every scenario.
- No GC allocations per frame in steady state (any allocation in a per-frame path is a bug).
- Load: WW work after a save load spread over >= 10 frames, no single frame > 8 ms.

## Template

### x.y.z (build letter) - date
| Scenario | Avg ms | p99 ms | Max ms | GC alloc/frame | Top 3 methods | vs. standalone mods |
|---|---|---|---|---|---|---|
| S1 idle open world | | | | | | |
| S2 camp fight, 4+ NPCs | | | | | | |
| S3 driving through a tile boundary + origin shift | | | | | | |
| S4 load save | | | | | | |
| S5 convoy / many vehicles | | | | | | |
