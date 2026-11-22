# Hilltop lighting validation

Unity Play Mode review. Images use a fixed camera and the same stones.

- PASS: Pending stone exists 0
- PASS: Existing placement and release work 0
- PASS: Pending stone exists 1
- PASS: Existing placement and release work 1
- PASS: Pending stone exists 2
- PASS: Existing placement and release work 2
- PASS: Sky uses a runtime copy.
- PASS: Volume uses a runtime copy.
- PASS: Preset applies immediately: Morning
- PASS: Preset applies immediately: Noon
- PASS: Preset applies immediately: Golden hour
- PASS: Preset applies immediately: Overcast
- PASS: Transition renders intermediate lighting.
- PASS: Interrupted transition starts from the current look without a jump.
- PASS: Interrupted transition reaches the new preset.
- PASS: Automatic cycle wraps from overcast to morning.
- PASS: Manual controls stop cycling and apply immediately.
- PASS: Disabling lighting restores authored assets.
- PASS: Disabling lighting restores the original sun.
- PASS: Re-enabling lighting recreates isolated runtime assets.
- PASS: Authored sky restored after Play Mode.
- PASS: Sky asset contents unchanged.
- PASS: Volume asset and all overrides unchanged.

Errors: 0

Keyboard shortcut delivery and physical-device interaction still require manual acceptance.
