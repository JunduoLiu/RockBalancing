# Hilltop original-scene UI and background review

Actual Unity Play Mode; original Hilltop.unity. UI samples: background OFF, lighting preset 0, same camera and stone group.

- PASS: Opt-in study attached to original Hilltop scene
- PASS: Scene path is original Hilltop.unity, not ZenTriptych or a replacement scene
- PASS: ZenTriptych presentation absent
- PASS: Pending stone 0
- PASS: Shared placement/release 0
- PASS: Pending stone 1
- PASS: Shared placement/release 1
- PASS: Pending stone 2
- PASS: Shared placement/release 2
- PASS: Current effective tower height uses original support chain
- PASS: UI 0 leaves environment, light, materials and colliders unchanged
- PASS: UI 0 retains both original hot areas
- PASS: UI 0 retains existing input delegates
- PASS: Shared input accepts operation 0
- PASS: UI 0 pointer handlers move/lift real pending stone
- PASS: Restore pending pose for equal-camera comparison 0
- PASS: UI 0 release/focus-cancel restores both handles
- PASS: Restore placement after cancel 0
- PASS: UI 0 permits repeated operation after cancel
- PASS: Restore repeated-operation pose 0
- PASS: UI 1 leaves environment, light, materials and colliders unchanged
- PASS: UI 1 retains both original hot areas
- PASS: UI 1 retains existing input delegates
- PASS: A1 returns to quiet marks and hides unchanged height
- PASS: A1 pointer hover reveals controls
- PASS: Shared input accepts operation 1
- PASS: UI 1 pointer handlers move/lift real pending stone
- PASS: UI 1 active feedback follows held state
- PASS: Restore pending pose for equal-camera comparison 1
- PASS: UI 1 release/focus-cancel restores both handles
- PASS: Restore placement after cancel 1
- PASS: UI 1 permits repeated operation after cancel
- PASS: Restore repeated-operation pose 1
- PASS: UI 2 leaves environment, light, materials and colliders unchanged
- PASS: UI 2 retains both original hot areas
- PASS: UI 2 retains existing input delegates
- PASS: Shared input accepts operation 2
- PASS: UI 2 pointer handlers move/lift real pending stone
- PASS: UI 2 active feedback follows held state
- PASS: Restore pending pose for equal-camera comparison 2
- PASS: UI 2 release/focus-cancel restores both handles
- PASS: Restore placement after cancel 2
- PASS: UI 2 permits repeated operation after cancel
- PASS: Restore repeated-operation pose 2
- PASS: Returning to original UI leaves Hilltop environment unchanged
- PASS: Original HUD restores both authored backgrounds
- PASS: Background retains contact-platform material and grass geometry
- PASS: Background preserves lighting controller sun and sky
- PASS: Background coexists with lighting preset 0
- PASS: Background coexists with lighting preset 1
- PASS: Background coexists with lighting preset 2
- PASS: Background coexists with lighting preset 3
- PASS: Background OFF restores all original scenery transforms/material references/property blocks
- PASS: Background toggles retain all original gameplay colliders

Errors: 1

ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.
Parameter name: index
System.Collections.Generic.List`1[T].get_Item (System.Int32 index) (at <41d29b352f6a475ab1bf7c6628b82790>:0)
UnityEditor.Search.SearchDatabase+<EnumerateAll>d__81.MoveNext () (at <40debc8f4f004a2d9999ccdf173d496c>:0)
System.Linq.Enumerable.TryGetFirst[TSource] (System.Collections.Generic.IEnumerable`1[T] source, System.Boolean& found) (at <80564d689e48448fa2a7101993ee7201>:0)
System.Linq.Enumerable.First[TSource] (System.Collections.Generic.IEnumerable`1[T] source) (at <80564d689e48448fa2a7101993ee7201>:0)
UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase () (at <40debc8f4f004a2d9999ccdf173d496c>:0)
UnityEditor.Search.SearchInit.IndexationOnStartup () (at <40debc8f4f004a2d9999ccdf173d496c>:0)
UnityEditor.EditorApplication.Internal_CallDelayFunctions () (at <a90f148a0c664ac8982a824605784e74>:0)


Controls are driven by scripted PointerEventData through the existing DragPad handlers. No OS keyboard/mouse or physical touch acceptance. Focus test invokes the existing cancel entry point plus presentation focus clearing, not an OS focus event.
