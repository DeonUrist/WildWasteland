# Wiki format (V:\Repos\Apocalypter\WildWastelandWiki)

The wiki is plain Markdown (GitHub-wiki compatible) built from small, independent pages so one feature can be updated
without touching the rest. `tools/wiki-index.py` regenerates `Home.md`, `_Sidebar.md`, `Feature-Status.md` and `features.json`
from the page headers; never edit those four by hand. The folder is a separate repo (exact name `WildWastelandWiki`) pushed as the
GitHub wiki of the WW repo.

```
WildWastelandWiki/
  Home.md                     generated: intro + module table + feature index by module / framework
  _Sidebar.md                 generated: guides, architecture, modules -> features
  Feature-Status.md           generated: every feature with status, toggle and tools (mirrors the project board)
  features.json               generated: board export [{title, page, module, framework, status, f6, f7, since}]
  Backlog-Candidates.md       hand-written: the remaining candidate mods, one feature entry each (Status Backlog, module "Future")
  Guides/                     hand-written how-tos (Installation, F6-Settings, F7-Tool-Window, Migration, Troubleshooting, Performance, Saves)
  Architecture/               Overview, Core-Services, Scheduler-And-Frame-Budget, Harmony-Hooks, Config-And-Tools, Adding-A-Module, Release-Process
  Modules/<ModuleId>/<ModuleId>-Module.md   module overview (what it replaces, F6 section, F7 pages, services, files, migration)
  Modules/<ModuleId>/<Feature-Slug>.md      one page per player-visible feature or tool
```

**GitHub wikis flatten folders**: every file name must be unique across the whole wiki (that is why the module overview is
`Guns-Module.md`, not `README.md`, and why pages are prefixed with the module when the name is generic: `Patrols-Cleanup.md`,
`NPC-Dogs.md`). Links are by file name without folder and without `.md`: `[[Human-Turrets]]` or `[Human Turrets](Human-Turrets)`.
The index script fails on a duplicate file name or a link to a page that does not exist.

## Feature page template

File name: `Title-Case-With-Dashes.md`. The metadata table MUST be the first table after the title (the index script parses it;
fields in this order: Module, Framework, Status, F6 toggle, F7 tools, Source, Since):

```markdown
# Human Turrets

| Field | Value |
|---|---|
| Module | Patrols |
| Framework | Crews |
| Status | Testing |
| F6 toggle | `[Patrols] Human turrets` |
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
For 0.1.0 ported features use `Testing` (ported, compiles, awaiting in-game verification) unless stated otherwise; `Verified` after
Denis saw it work in the game; `Done` when it shipped in a release. `-` in F6 toggle / F7 tools means none.
Framework = the logical sub-system inside the module (folder name), e.g. NPC: Detection / Navigation / Brain / Animals / Hazards / Debug;
`-` on module overview pages.

Module overview page (`<Id>-Module.md`): same metadata table (Framework `-`, F6 toggle = `[<Id>] Enabled`, F7 tools = all its pages),
then What it does / Features (table: page link, framework, F6 toggle) / F6 section / F7 / Services / Files / Saves / Migration.

Backlog entries (`Backlog-Candidates.md`): one `## <Title>` heading per candidate with a short line list `Module: Future`,
`Framework: <folder it would get>`, `Status: Backlog`, `Source: <repo> <version> / <commit>`; the index script reads those headings
instead of a metadata table.
