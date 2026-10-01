# Gacha UI — Version 3

Pearl white, mint and sky blue with peach pink accents. The existing Gacha Nox identity, character roles and four-page flow are retained.

## Installed assets

- 45 replacement PNGs in `Assets/Scenes/ui`.
- 13 replacement illustrations in `Assets/Scenes/ui/Onboarding`.
- Matching card, navigation, text and progress colors in `BaseScene.prefab`, the runtime controller and the editor builder.
- Exact original PNG dimensions, sprite metadata, references and animation assets are preserved. The five tintable onboarding shape textures are unchanged.
- Dressing remains the available activity; Coloring Book and Jigsaw Puzzle remain locked.

## Review and sources

- `UI-preview.png`: all 58 updated images.
- `masters/`: 26 source illustrations edited with built-in ImageGen.
- `prompts.json`: the full image prompts and source references.
- `vectors/`: the 25 existing vector UI graphics updated for the new palette and button corners.
- `ready/`: the installed replacement PNGs before Unity import.
- `backups/`: the previous images, scene, prefab and relevant scripts.
- `validation.json`: installed asset checks.
- `layout-validation.json`: confirms the prefab edits changed colors only.

The image pipeline only resizes and pads generated artwork. Animation frames retain their original occupied bounds. Unity previews and flow checks use an isolated temporary project under `.utmp/Version3Preview`.
