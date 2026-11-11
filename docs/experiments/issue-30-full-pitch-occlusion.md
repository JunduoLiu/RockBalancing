# #30 Full-range observation and scenery occlusion

Issue: https://github.com/JunduoLiu/RockBalancing/issues/30
Base: #29, commit `14a2f4e41f717913b7c198d549a795007f12b728`.
Branch: `codex/issue-30-full-pitch-occlusion`.

## Implementation

- Continuous horizontal orbit and configurable pitch through exact -90 and +90 degrees. Camera rotation comes directly from pitch/yaw, preserving a stable horizontal right axis at both poles rather than using a singular world-up LookRotation.
- Removed the old ground/base height correction. Stone exclusion still protects the camera and its near-plane corners, including overlapping exclusion envelopes regardless of the order of stones.
- Renderer-only occlusion treatment samples the pending stone and tower bounds. Blocking base fades to 8% opacity; blocking ground fades completely. A wider exit margin and 180 ms clear-time hold suppress threshold chatter; transitions take up to 160 ms.
- A dedicated, Resources-referenced URP alpha shader avoids runtime Lit-variant stripping in WebGL. No depth writes while faded. Original material references are restored when clear and when the component is disabled/destroyed. No collider, support, score, release-height, or stone-transform changes.
- Rotation picking ignores faded scenery so a visible pending stone can still be rotated from below. Planar adjustment retains the existing yaw-only basis and observation/follow ownership.
- Sharing wraps only the synchronous scene render in a nestable opaque-material scope. Original materials are used for the full-scene composition; gameplay materials and camera state are restored, including exception unwinding.

## Automated checks

Unity 6000.3.20f1, final smooth-alpha implementation:

| Game View | Play Mode result | Duration |
| --- | --- | --- |
| 430 x 900 | 63 passed, 0 failed, 0 skipped | 54.79 s |
| 1280 x 720 | 63 passed, 0 failed, 0 skipped | 54.82 s |

`node --test tools/share.test.cjs`: 9/9 passed.

WebGL build succeeded with the new shader included. The index, loader, data, framework, and WASM files each returned HTTP 200 from a loopback-only preview with no-store caching. The temporary server was stopped after verification. Shader inspection and `git diff --check` reported no errors.

Eight new observation regressions cover exact/near poles across five headings, touch input at both poles, scenery fade/hold/restoration without physics mutation, same-frame share pixel equality against an opaque reference, exception-safe material restoration, rotation through faded scenery, yaw-relative movement/follow, offset high-tower observation and replacement, and overlapping stone envelopes. Existing zoom tests now check actual near-plane corners below ground, while existing interaction/session/physics tests retain release, discard, waiting, restart, and support coverage.

An initial image-comparison fixture compared different gameplay guide orientations. It was corrected to compare the same frame and pose with/without the opaque scope. The enlarged-preview boundary fixture now sets pitch directly because that oversized stone occupies the former blank-drag location. Both corrected fixtures and the entire suite pass.

An independent read-only code review found no must-fix runtime issue. Visual QA rejected an initial screen-door shader because its pixel pattern was distracting from below; the delivered shader uses smooth alpha blending.

## Demo and screenshots

All images are actual Unity Game View renders, not mockups. The short demo scripts camera poses for reproducibility; automated tests separately exercise real Input System mouse and touchscreen events.

- [Short continuous orbit, poles, below-base rotation, offset/follow, replacement and restart](issue-30-orbit-demo.gif)
- [Top pole, phone](issue-30-top-phone.png)
- [Bottom pole with scenery faded, phone](issue-30-bottom-phone.png)
- [Offset pending stone from below](issue-30-offset-bottom-phone.png)
- [Minimum zoom, distance 0.7](issue-30-near-zoom-phone.png)
- [Maximum zoom, distance 40](issue-30-far-zoom-phone.png)
- [Opaque scenery restored after restart](issue-30-restored-phone.png)
- [Share captured while gameplay view is below the base](issue-30-share-from-below.png)
- [High tower, desktop 70 degrees](issue-30-high-1280x720-70.png)
- [High tower, desktop -60 degrees](issue-30-high-1280x720--60.png)
- [High tower, desktop bottom pole](issue-30-high-1280x720--90.png)
- [High tower, phone 70 degrees](issue-30-high-430x900-70.png)
- [High tower, phone -60 degrees](issue-30-high-430x900--60.png)
- [High tower, phone bottom pole](issue-30-high-430x900--90.png)

## Scope and verification limits

Phone coverage is the portrait playable Unity scene plus synthetic touchscreen input, not physical iOS/Android hardware. Actual desktop WebGL UI/console QA could not run: the connected browser inventory was empty and both Chrome and the in-app browser returned “Browser is not available.” Build/HTTP checks are not claimed as browser-rendering verification. No deployed website is changed. This branch implements #30 only and is not merged. The preexisting Stone.mat modification and Assets/Resources.meta are left uncommitted. GitHub assignment/PR creation remains blocked by the previously reported integration 403; no retry or alternate write route was attempted.
