# Hilltop Unity review

Actual Hilltop Play Mode scene. Screenshots are Unity renders.

Automated visual/physics replay uses the shared GameSession placement and release APIs. It does not certify native keyboard, mouse or touch input.

- PASS: Shared stone factory provides a pending stone.
- PASS: Shared session uses the visible platform and hill colliders.
- PASS: Scene-local render pipeline is active.
- PASS: Camera post-processing is enabled.
- PASS: Minimal preview has no permanent release button.
- PASS: Next pending stone exists for release 0
- PASS: Placement accepts a clear pose 0
- PASS: Shared game session releases stone 0
- PASS: Released stone enters live physics 1
- PASS: Release supplies a different pending stone 1
- PASS: Platform contact contributes to current effective tower height.
- PASS: Next pending stone exists for release 1
- PASS: Placement accepts a clear pose 1
- PASS: Shared game session releases stone 1
- PASS: Released stone enters live physics 2
- PASS: Release supplies a different pending stone 2
- PASS: Next pending stone exists for release 2
- PASS: Placement accepts a clear pose 2
- PASS: Shared game session releases stone 2
- PASS: Released stone enters live physics 3
- PASS: Release supplies a different pending stone 3
- PASS: Next pending stone exists for release 3
- PASS: Placement accepts a clear pose 3
- PASS: Shared game session releases stone 3
- PASS: Released stone enters live physics 4
- PASS: Release supplies a different pending stone 4
- PASS: Next pending stone exists for release 4
- PASS: Placement accepts a clear pose 4
- PASS: Shared game session releases stone 4
- PASS: Released stone enters live physics 5
- PASS: Release supplies a different pending stone 5
- PASS: Green meadow pixels change over time with the camera fixed (wind animation).
- PASS: Shader supported: Herlilan/Hilltop/Natural Stone
- PASS: Shader supported: Herlilan/Hilltop/Meadow Wind
- PASS: Shader supported: Herlilan/Hilltop/Soft Sky
- PASS: Shared occlusion detects the hill from below.
- PASS: Occlusion preserves platform and ground collision.
- PASS: Shared restart clears the session and supplies a stone.
- PASS: Restart resets current effective tower height.

Errors: 0
