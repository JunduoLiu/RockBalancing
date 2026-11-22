# Scene 2 — Hilltop

A playable visual study for the existing Rock Balancing game: a rounded grassy
summit, a natural stone base, softly lit low-poly pebbles, a pale blue / sage
horizon and gently moving grass. This follows the updated Hilltop brief.

## Open and play

Open `Scenes/Hilltop.unity`, or use **Herlilan → Hilltop → Open playable scene**,
then press Play. Click the Game view to give it input focus.

| Control | Action |
| --- | --- |
| Left round joystick / WASD | Move the pending stone horizontally |
| Adjacent vertical control / up and down arrows | Raise or lower the stone |
| Drag the pending stone | Rotate it |
| Drag the scenery | Orbit the camera |
| Mouse wheel / two-finger pinch | Zoom |
| Space | Release the current stone |
| Backspace | Discard the current stone |
| R | Open the existing restart confirmation |
| Tab | Show or hide the original release, discard, restart and share controls |
| 1 / 2 / 3 / 4 | Blend to morning / noon / golden hour / overcast lighting |
| L | Toggle automatic lighting cycling (10 seconds per look by default) |

## Live lighting study

While playing Hilltop, open **Herlilan → Hilltop → Live lighting**. The editor
panel selects the four looks, controls cycling and blend duration, and adjusts
sun elevation, azimuth, intensity and exposure. Selecting a look blends from
the currently visible lighting over 2.5 seconds, including when interrupting
another transition. Fine tuning stops cycling; select the same preset again to
reset its values. These changes are temporary and reset when Play Mode ends.

The presets coordinate direct light, shadow strength, ambient light, gradient
sky, fog, white balance, saturation and exposure. Morning is gently warm;
noon has a higher, brighter neutral sun; golden hour uses a low amber sun and
long shadows; overcast reduces direct light and shadow contrast.

Lighting uses runtime copies of the sky material and all volume overrides.
Disabling the component restores the original sun, atmosphere and asset
references. It does not edit the shared gameplay, project settings or authored
materials. The game keeps its minimal two-control interface; the lighting
panel is an editor tool, not part of the mobile UI.

**Herlilan → Hilltop → Capture and validate lighting** starts a dedicated
Play Mode review, captures the four looks from the same camera, checks
transitions, cycling, manual adjustments and cleanup, then stops Play Mode.
Results are written to `Review/lighting-validation.md`, with actual renders
in `Review/lighting-0.png` through `lighting-3.png`.

The default minimal view is a **desktop visual prototype** with two permanent
movement controls. On touch-only devices, disable `Minimal Controls` on the
scene root to expose the existing action buttons. A two-control-only mobile
release gesture has not been defined by this study. Device builds and native
multi-touch have not been validated here.

## Art and rendering

- The existing `StoneFactory`, `PebbleMesh`, `GameSession`, `GameInput`,
  `GameView` and `ShareController` supply gameplay, physics and measurement.
- Stones retain the shared low-poly contact geometry. The new stone shader
  adds object-space mineral variation, fine grain, sparse veins and a matte
  PBR surface; no downloaded textures or paid assets are needed.
- A beveled stone platform and a smooth radial hill replace the test ground.
  Both have matching mesh colliders. Grass is visual only.
- 26,400 grass blades are divided into twelve meshes. Root-to-tip bending and
  two slow waves animate the grass in the shader. It receives sunlight and
  shadows; grass shadow casting is disabled.
- A gradient sky, fog and nine muted distant ridges provide depth without
  detailed structures. A soft directional light and scene-local SSAO anchor
  the stones.
- `Settings/HilltopVolume.asset` supplies neutral tonemapping, a small exposure
  and contrast adjustment, gentle bloom and vignette. The camera uses FXAA.
- `Settings/HilltopPipeline.asset` and `HilltopRenderer.asset` are isolated
  copies. The scene applies its pipeline only while playing, and restores the
  previous pipeline, target frame rate and background setting on teardown.
- The camera is framed slightly below the pending stone to include the tower.
  The platform stays visible from above despite the shared conservative bounds
  occlusion test; normal fading is retained from below. Grass follows hill
  occlusion without changing any collider or stone physics.

## Rebuild and review

**Herlilan → Hilltop → Rebuild generated scene and art** regenerates this scene,
its meshes, materials and settings. It preserves asset GUIDs. It overwrites
manual changes to those generated Hilltop assets; keep custom variants in
separate scene/material copies. Source is in `Runtime`, `Shaders` and `Editor`.

**Herlilan → Hilltop → Capture and validate preview** enters Play Mode, places
five stones through the existing GameSession placement and release APIs, checks physics, current
height, shaders, wind, occlusion and restart, and exits Play Mode. Leave the
Game view open until it finishes. Results are saved in `Review/validation.md`.
This automated replay is independent of OS input focus and does not certify
native mouse, keyboard or multi-touch event delivery.
In this session, native UI automation did not reliably drive the Game view's
input. Physical-device control feel therefore remains a manual acceptance item;
the successful replay should not be interpreted as an input acceptance test.

`Review/hilltop-landscape.png` and `hilltop-portrait.png` are actual Unity renders
at 1440 × 900 and 720 × 1200. `hilltop-without-post.png` shows the same scene
without camera post-processing. `hilltop-breeze.png` is a later frame for wind
comparison. These are editor captures, not device performance certification.

## Workspace scope

All additions live under `Assets/Herlilan/Hilltop`. The original scenes,
materials, gameplay source, build scenes and project render settings are not
edited by this study. Existing local changes in Balance, SampleScene and the
shared Stone material are left intact.

Before implementation, the current branch `codex/issue-31-desktop-keyboard`
at `0ac0dcd1abb29d14d40d82514468a24fca6dfe4e` was verified against GitHub and
matched its remote head. `main` was already an ancestor of this branch. Fetch
could not write the sandbox's read-only Git metadata, so remote equality was
checked through GitHub instead. The separate arcade branch was not merged.
No commit or push is performed by this visual study.
