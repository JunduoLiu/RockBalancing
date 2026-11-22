# Zen Triptych — playable comparison and UI readability review

2026-11-22. No commit/push. Original scenes, other people's changes, physics, input and current effective tower-height calculation remain unchanged.

## Play
Herlilan > Zen Triptych > Open Wind / Open Paper / Open Arcade, then Play.
Scenes/Zen-Wind.unity, Scenes/Zen-Paper.unity, Scenes/Zen-Arcade.unity.
F6 / F7 / F8 switch the three looks. No experiment menu in normal HUD.

## Final UI-only refinement
Only Runtime/ZenPresentation.cs presentation changed: Wind control ink is warm white at .84 idle alpha, with a .75px dark edge and a .95-alpha center dot. Held controls reach full alpha. Paper/Arcade styling is unchanged. No panel, new raycast target, hit-area resize or input changes.
The captured idle Wind joystick and vertical rail/ticks are now visibly readable over grass.
Sand-disc proportions and arcade side-face styling were deliberately left for a later aesthetic pass.

## Real Unity evidence
Review/Zen-Triptych-Comparison.png: 4320x980 montage with title strip and three unmodified 1440x900 game captures.
Review/wind.png, paper.png, arcade.png: one GPU Play Mode run, same camera and same stone group, each showing 115 mm.
Review/wind-later.png: wind observation; Review/settling-wave.png: cyan ring ~0.22 sec after trigger.
Review/ui-validation.md: complete automation result, retaining Errors:1.

Executed in D:/Unity/ZenTriptych-Validation-20261122 using Unity 6000.3.20f1, batchmode + D3D11, RTX 4080. No nographics or original-editor activation. Runtime source in the test copy matches this experiment.
Final log: D:/Unity/ZenTriptych-Validation-20261122/validation-ui.log.

Automated PASS: three raise/move/lower/release sequences; positive effective height; wave trigger/capture; 36 repeated switches preserve colliders and height; wind pixel delta 2,834,613; restore Wind; move hit area >=114; restart.
Visual inspection PASS for the requested Wind idle control readability and distinct environments; montage checked.
Native keyboard/mouse/physical touch have NOT been tested. Scripted session API tests do not certify native input.

## Separate reproducible Unity error
ArgumentOutOfRangeException in UnityEditor.Search.SearchDatabase.EnumerateAll -> GetDefaultSearchDatabase -> SearchInit.IndexationOnStartup -> EditorApplication.Internal_CallDelayFunctions.
It recurred during isolated batch startup, did not prevent Play Mode/screenshots, and is retained as FAIL / Errors:1. No Search setting/index change or error suppression.
All isolated test processes exited.

## Library delivery
Only the comparison PNG was uploaded (977848 bytes). No source, project or logs uploaded.
Library path: /Zen-Triptych-Comparison.png
Library ID: libfile_fde3_f-7607d84d6e4481919fc91aaa4392fc25_228107bef1148191bacb02aafd8efe4c
File ID: file_00000000d94881f5a3abbe50f5c7ac20
Version: 0
Local path: D:/Unity/RockBalancing/Assets/Herlilan/ZenTriptych/Review/Zen-Triptych-Comparison.png
Library creation confirmed; local identity persisted using current Library helper.
No direct private-chat send tool was available in this Work, so this is a confirmed Library save, not a claimed chat-attachment send.

