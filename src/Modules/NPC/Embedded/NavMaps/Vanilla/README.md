# Vanilla navigation pack

Put accepted `<prefab>-<signature>.wwnav` files here. The existing embedded-resource glob includes them in WildWasteland.dll as
`NPC.NavMaps.Vanilla.<filename>`; no project-file or release-installer change is required. They are read-only at runtime and survive the
custom pathfinding reset. An optional external pack can use `plugins/WildWasteland/NPC/NavMaps/Vanilla/` with the same file names.

There are no accepted map binaries yet. Capture the 39 native Camp_1-16 / Building_1-7 / Cave_1-7 / Wreck_1-8 / Starter_Area locations
in game with the supported game build, default navigation settings and custom POI mods disabled. Use NPC Debug > Export vanilla
pathfinding maps; it logs missing locations and writes candidates into `config/WildWasteland/NPC/NavMaps/VanillaExport/`.
Test the candidates on other rotations/elevations and terrain joins before placing them here. Export filters native names, so a custom
mod that replaces a native prefab must be disabled while capturing. This folder's README is a placeholder, not a navigation map.

See `../../../NavMaps/README.md` for file format, fallback, custom POI discovery and reset behavior.
