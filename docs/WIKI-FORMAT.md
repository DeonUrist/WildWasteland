# Wiki format (V:\Repos\Apocalypter\WildWastelandwWiki)

The wiki is plain Markdown (GitHub-wiki compatible) built from small, independent pages so one feature can be updated
without touching the rest. `tools/wiki-index.py` regenerates `Home.md`, `_Sidebar.md` and `Feature-Status.md` from the page headers;
never edit those three by hand.

```
WildWastelandwWiki/
  Home.md                     generated: intro + module table + feature index
  _Sidebar.md                 generated
  Feature-Status.md           generated: every feature with status, toggle and tools
  Guides/                     hand-written how-tos (Install, F6 settings, F7 tools, Troubleshooting, Performance)
  Architecture/               Core, scheduler, hooks, saves, adding a module
  Modules/<ModuleId>/README.md        module overview (what it replaces, F6 section, F7 pages, services)
  Modules/<ModuleId>/<Feature-Slug>.md   one page per player-visible feature or tool
```

## Feature page template

File name: `Title-Case-With-Dashes.md`, unique across the wiki (prefix with the module if ambiguous, e.g. `Patrols-Cleanup.md`).
The metadata table MUST be the first thing after the title (the index script parses it):

```markdown
# Human Turrets

| Field | Value |
|---|---|
| Module | Patrols |
| Framework | Crews |
| Status | Done |
| F6 toggle | `[Patrols / Combat] HumanTurrets` |
| F7 tools | Seat & turret poses → Turret spot editor |
| Source | Apocapatrol 2.14.4 (Rider.cs, RiderGuard, RiderAnchorGuard) |
| Since | 0.1.0 |

## What it does
2-6 sentences, player view.

## How to use
Steps or "automatic".

## Settings
| Where | Setting | Default | Meaning |
|---|---|---|---|
| F6 | `[Patrols / Combat] WitnessMeChance` | 10 | ... |
| F7 | `[Patrols / Combat] RiderRange` | 35 | ... |

## Notes
Compatibility, performance, known issues. Optional.
```

Status values (same as the GitHub project columns): `Backlog`, `Working on`, `Testing`, `Verified`, `Done`.
For 0.1.0 ported features use `Testing` (ported, compiles, awaiting in-game verification) unless stated otherwise.
Framework = the logical sub-system inside the module (folder name), e.g. NPC: Detection / Navigation / Brain / Animals / Hazards.
