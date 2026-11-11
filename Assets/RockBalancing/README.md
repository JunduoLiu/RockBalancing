# Rock Balance

Open `Scenes/Balance.unity` and enter Play Mode. The scene creates its camera, base, surrounding ground, UI and session at runtime. It uses the new Input System and URP.

## Controls

- Hold the bottom joystick away from its centre to move horizontally; a larger offset moves faster. Return to centre or let go to stop. The separate lift control works the same way vertically, and both controls can be held together.
- Drag the selected stone to rotate it without changing its position or release height.
- Press **释放 / Release** to drop the selected stone and receive the next one. Empty-space taps and ending any drag never release. Rapid repeat clicks are ignored; release and discard bind to the stone selected when the button was pressed.
- Drag empty world space to orbit; pinch to zoom. The mouse wheel also zooms in the Editor.
- Discard throws the selected stone beside the base. A stone disappears at its first contact with the surrounding ground.
- The four main actions use Lucide icons: circular arrow = restart, connected nodes = share, down arrow onto a line = release, trash = discard. Desktop hover shows the action name. Touch targets stay at least 48 screen pixels in small windows. Pressed and unavailable states change contrast; disabled actions also dim the icon. Restart still requires the textual confirmation. Share captures the current height and complete scene before displaying a preview.

The rules are defined in [specification #9](https://github.com/JunduoLiu/RockBalancing/issues/9), with the joystick and release-button interaction superseded by [incremental specification #27](https://github.com/JunduoLiu/RockBalancing/issues/27). Runtime code does not store a high score or serialized session.

`GameInput` exposes joystick dead zone (default 12%), maximum horizontal speed (0.35 m/s), maximum lift speed (0.45 m/s), and repeat-action delay (0.4 s). Diagonal input is capped to the same speed. UI touches are assigned at touch-down and never participate in scene pinching. Focus loss, cancellation, dialogs, release, discard and restart clear held input; all old touches must end before fresh input controls a new stone. Adjustments and release use the session's collision checks; rejected release keeps the current stone available for correction.

## Editor and browser

The camera holds its observation centre while adjusting a stone or holding a scene gesture. Once both end, it smoothly recentres on the pending stone (`FollowSpeed=2 / s`). Release/discard hold the view during supply, then smoothly acquire the next pending stone. Joystick directions use the current horizontal orbit heading, including at high pitch. A pending-stone direction cue appears when the stone leaves the clear play area; it does not capture touches. The landing footprint and vertical guide remain in the scene, but there is no landing-point edge label. Issue #28 removes the gameplay panorama, title/subtitle and permanent instruction bar. Height (or stablizing) stays at the top, beside restart and share; release/discard sit directly above the adjustment controls. Collision rejection appears as a non-blocking two-second message, and supply failures retain the retry button. Share still uses its own full-scene camera. See the [continuous operation demo and regression record](../../docs/experiments/issue-25-transitions.md).

Use `Rock Balancing > Update scene assets` after changing fonts or material templates. The referenced material assets keep the runtime shaders available in builds. `Portrait preview` sets a 430 × 900 Game View.

Install the matching Unity Web Build Support module and restart the Editor, then invoke `RockBalancing.Editor.BuildGame.BuildWeb` through the Unity Pipeline `script eval` command, or use that method as a batch-mode execute method. Output is `Builds/Web`. The build uses the included `RockBalance` Web template, Gzip compression and Unity's decompression fallback so static hosts do not need custom compression headers. Upload the complete output directory to an HTTPS static host; do not open `index.html` as a local file. The builder checks that the page, loader, data, framework and WebAssembly output are present before reporting success.

The Web template composes a portrait card with the captured millimetre value, full scene and a QR code pointing to the current game URL. Supported browsers offer a second, explicit system-share button so the browser has a fresh user gesture. Save-image and copy-link controls remain available. Failed generation retries the original captured image; restarting invalidates previous callbacks.

Run Play Mode tests filtered to `RockBalancing` through Unity Test Runner or Pipeline. Run the browser bridge contract tests with `node --test tools/share.test.cjs` from the repository root. These checks do not replace the iPhone + Chrome acceptance in #26: stacking experience, the continuous control loop, and height/share/restart regression, as scoped by #27.

## Stone distribution

`GameBootstrap > Stone Shapes` controls the flat-stone probability (default 85%) and thickness/length ratio (default 0.18–0.28). The remaining stones keep the original irregular generator. Flat stones have actual planar support faces, the same render/collision mesh, volume-based mass, and an upright initial pose; generated stones do not change when the settings change. See the [fixed-seed comparison and physics validation](../../docs/experiments/issue-22-flat-stones.md) for the parameter choice and reproducible experiment.

## Third-party assets

`Fonts/BalanceSans.ttf` is a static, reduced character subset of Google Fonts Noto Sans SC, under the included OFL licence. `tools/subset_font.py` regenerates it from `.scratch/NotoSansSC.ttf`; download the upstream variable font from `https://github.com/google/fonts/tree/main/ofl/notosanssc` and run with Python/fonttools. Include all UI strings before generating the subset.

The Web template includes Kazuhiko Arase's `qrcode-generator` JavaScript distribution, under the adjacent MIT licence: `https://github.com/kazuhikoarase/qrcode-generator`.

The four action sprites use Lucide 0.468.0 (ISC, with Feather MIT portions). Original SVGs are in `tools/lucide/`; source/license and regeneration details are in [Icons/Lucide/README.md](Icons/Lucide/README.md). Web builds include `lucide-licenses.txt`.
