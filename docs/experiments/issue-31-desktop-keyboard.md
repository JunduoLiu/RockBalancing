# #31 Desktop keyboard and mouse controls

Issue: https://github.com/JunduoLiu/RockBalancing/issues/31
Base: #30, commit `381c7096a004a1855658877921db35425759ee98`.
Branch: `codex/issue-31-desktop-keyboard`.

## Implementation

- WASD continuously moves the pending stone along the camera's yaw-only horizontal heading. Up/Down changes only release height. Both axes can operate together, opposite keys cancel, and planar diagonal input is normalized. Delta-time motion uses the existing joystick speed settings and collision-checked `GameSession.Adjust` path.
- Keyboard and joystick contributions are combined and clamped once per axis. Neither source overwrites the other, and simultaneous sources cannot double movement speed. Keyboard adjustment holds the observation centre until the keys and scene gesture end.
- Focus loss, backgrounding, dialogs, release, discard, replacement and restart clear input and require all held movement keys/pointers to return to neutral. Browser/OS modifier shortcuts also stop existing movement and require fresh input.
- Mouse stone rotation, blank-space orbit, wheel zoom, explicit release/discard buttons, sharing and confirmed restart retain their existing paths. UI keyboard navigation is disabled so arrows do not select action buttons. Enter/Space cannot release or discard, including direct submit events.
- Web input is scoped to a focused canvas. A capture-phase bridge prevents gameplay arrows from scrolling and rejects repeat keydowns inherited across blur/background transitions. Canvas/HTML share focus transitions reset stale keyboard state. Share inputs and browser shortcuts retain their normal behavior. WebGL does not globally capture page keyboard input.

## Verification

Unity 6000.3.20f1, final runtime code:

| Game View | Play Mode result | Duration |
| --- | --- | --- |
| 430 x 900 | 74 passed, 0 failed, 0 skipped | 70.45 s |
| 1280 x 720 | 74 passed, 0 failed, 0 skipped | 71.26 s |

`node --test tools/input.test.cjs tools/share.test.cjs`: 19/19 passed. The additional share contract confirms that opening/closing the actual share bridge transfers operation focus exactly once.

Eleven new Unity Play Mode tests cover simultaneous planar/height input, immediate stop and opposing keys, 30/60/120 fps and diagonal caps, yaw-relative movement at both poles, focus/pause/modal/browser-focus neutral gating, release/discard/restart replacement, pad/keyboard composition, tower collision/no-push, browser-shortcut cancellation, delayed supply, mixed mouse/keyboard rotation-observation-release-share-restart, and no keyboard UI submission.

The existing suite retains mobile joystick/lift, touch rotation, observation/pinch, touch cancellation, release/discard, resize/layout, sharing, physics and session regressions. The browser contract suite checks focused/unfocused arrows, repeated held keys after blur, background/canvas focus changes, release outside the page, HTML share focus ownership, pointer cancellation and both shortcut modifier orders.

An independent read-only review identified a modifier-after-movement edge case. It was fixed in both Unity and the browser bridge and covered by sequence tests. A second review confirmed the fix and found no further actionable defects. Initial timing fixtures sampled before the last LateUpdate; they were corrected to sample a fully running input interval rather than relaxing the movement expectations.

WebGL build succeeded (113.28 s). The generated input bridge matches the source byte-for-byte. Static validation checked all template references, gzip output and the WASM header. A temporary loopback preview at `http://127.0.0.1:8260/` returned HTTP 200 with no-store caching for the page and all seven referenced resources; the server was then stopped. This local check is not a public build link or browser-play verification. `git diff --check` passed.

## Browser acceptance and remaining limits

Actual desktop Chrome testing is blocked on HPC: the connected browser inventory is empty and opening Chrome through the supported browser tool returns `Browser is not available: chrome`. Consequently the required real Chrome continuous-play/resize/blur acceptance is **not claimed as passed**. The Unity integration and JavaScript contract tests are not a substitute for a rendered WebGL browser run.

Mobile coverage is the portrait Unity scene and synthetic touchscreen regression suite, not a physical phone. No deployed website has been changed and no new public build link is claimed. The existing published preview still predates these branches. The final WebGL output is at `Builds/Web`; serve that complete directory over HTTP/HTTPS for the remaining browser acceptance.

No merge was performed. Preexisting `Stone.mat` changes and `Assets/Resources.meta` remain uncommitted. GitHub issue assignment and PR creation remain blocked by the previously reported integration 403; neither the denied operations nor alternate write routes were retried.
