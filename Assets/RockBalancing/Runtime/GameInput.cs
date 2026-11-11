using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RockBalancing
{
    [DefaultExecutionOrder(100)]
    public sealed class GameInput : MonoBehaviour
    {
        public GameSession Session;
        public Camera ViewCamera;
        public ObservationOcclusion Occlusion { get; private set; }
        bool modal;
        public bool Modal
        {
            get => modal;
            set { if (value && !modal) CancelForFocusLoss(); modal = value; }
        }
        public bool Adjusting;
        [Min(.01f)] public float RotationSensitivity=.45f;
        [Min(.01f)] public float OrbitSensitivity=.25f;
        [Range(0, .9f)] public float JoystickDeadZone=.12f;
        [Min(.01f)] public float MaximumPlanarSpeed=.35f;
        [Min(.01f)] public float MaximumHeightSpeed=.45f;
        [Min(.1f)] public float ActionRepeatDelay=.4f;
        [Min(.01f)] public float FollowSpeed=2;
        [Min(.1f)] public float MinimumZoom=.7f;
        [Min(1)] public float MaximumZoom=40;
        [Range(-90, 90)] public float MinimumPitch=-90;
        [Range(-90, 90)] public float MaximumPitch=90;
        [Min(.01f)] public float CameraClearance=.08f;
        float yaw = 25, pitch = 16, distance = 3.8f;
        Vector3 target = new Vector3(0, .3f, 0);
        Vector2 start, previous;
        bool active, dragged, blocked, rotating;
        bool requireNeutral;
        bool focusLost, paused, browserFocused = true;
        Vector2 keyboardPlanar;
        float keyboardLift;
        bool keyboardHeld;
        Stone selected;
        int touchId = -1;
        float pinch;
        Vector2 planar;
        float lift;
        Stone planarStone, liftStone;
        double nextActionTime;
        readonly Dictionary<int, bool> controlTouches = new Dictionary<int, bool>();
        readonly HashSet<int> liveTouches = new HashSet<int>();
        readonly List<int> endedTouches = new List<int>();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Let HTML share controls and other page content own their own keys.
            WebGLInput.captureAllKeyboardInput = false;
#endif
            Occlusion = gameObject.AddComponent<ObservationOcclusion>();
            Occlusion.Initialize(Session);
            Session.PendingChanged += CancelForStoneChange;
            Session.Restarted += ResetView;
            if(Session.Pending!=null) target=Session.Pending.transform.position;
        }
        void OnDestroy()
        {
            if (Session == null) return;
            Session.PendingChanged -= CancelForStoneChange;
            Session.Restarted -= ResetView;
        }
        public void ResetView()
        {
            yaw = 25; pitch = 16; distance = 3.8f;
            target = Session.Pending != null ? Session.Pending.transform.position : new Vector3(0, Session.BaseTop + .28f, 0);
            CancelForFocusLoss();
        }
        public void CancelGesture() { active = false; selected = null; touchId = -1; pinch = 0; }
        public void CancelForFocusLoss(string unused="")
        {
            CancelGesture();requireNeutral=true;
            keyboardPlanar=Vector2.zero;keyboardLift=0;keyboardHeld=false;
            planar=Vector2.zero;lift=0;planarStone=null;liftStone=null;Adjusting=false;
            foreach(var pad in GetComponentsInChildren<DragPad>(true))pad.Cancel();
            foreach(var button in GetComponentsInChildren<StoneActionButton>(true))button.Cancel();
        }
        void CancelForStoneChange() => CancelForFocusLoss();
        public bool CanAdjust => !Modal && !focusLost && !paused && browserFocused && !requireNeutral && Session != null && Session.Pending != null;
        public bool CanAct => CanAdjust && Time.unscaledTimeAsDouble >= nextActionTime;
        public bool ActOnStone(Stone expected, bool discard)
        {
            if (!CanAct || expected == null || expected != Session.Pending) return false;
            CancelForFocusLoss();
            nextActionTime = Time.unscaledTimeAsDouble + ActionRepeatDelay;
            return discard ? Session.Discard(expected) : Session.Release(expected);
        }

        void Update()
        {
            if (Modal || focusLost || paused || !browserFocused) return;
            var keyboard=Keyboard.current;
            if(keyboard!=null && (keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed || keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed))
            {
                // Browser/OS shortcuts must not leave a previously held move running.
                CancelForFocusLoss();return;
            }
            var touch = Touchscreen.current;
            int count = 0, total = 0;
            bool canceled = false;
            liveTouches.Clear();
            UnityEngine.InputSystem.Controls.TouchControl first = null, second = null;
            if (touch != null)
                foreach (var t in touch.touches)
                {
                    int id = t.touchId.ReadValue();
                    if (controlTouches.ContainsKey(id) && t.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled) canceled = true;
                    if (!t.press.isPressed) continue;
                    total++; liveTouches.Add(id);
                    if (!controlTouches.TryGetValue(id, out bool control))
                        controlTouches[id] = control = IsUI(t.position.ReadValue());
                    if (control) continue;
                    if (count == 0) first = t; else if(count == 1) second = t;
                    count++;
                }
            endedTouches.Clear();
            foreach (var pair in controlTouches) if (!liveTouches.Contains(pair.Key)) endedTouches.Add(pair.Key);
            foreach (int id in endedTouches) controlTouches.Remove(id);
            if (canceled) { CancelForFocusLoss(); return; }
            if(requireNeutral)
            {
                if(total==0 && (Mouse.current==null || !Mouse.current.leftButton.isPressed) && !MovementKeyHeld())requireNeutral=false;
                return;
            }
            ReadKeyboard();
            if (count >= 2)
            {
                active = false;
                blocked = true;
                float current = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());
                if (pinch > 0) Zoom(distance * pinch / Mathf.Max(1, current));
                pinch = current;
                return;
            }
            if (pinch > 0) { if (count == 0) { CancelGesture(); blocked = false; } return; }
            if (count == 1)
            {
                if (touchId != first.touchId.ReadValue()) { touchId = first.touchId.ReadValue(); Begin(first.position.ReadValue()); }
                else Move(first.position.ReadValue());
                return;
            }
            if (touchId != -1)
            {
                var end=previous;
                if(touch!=null)foreach(var t in touch.touches)if(t.touchId.ReadValue()==touchId)
                {
                    if(t.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.Canceled){CancelGesture();return;}
                    end=t.position.ReadValue();break;
                }
                End(end);touchId=-1;return;
            }
            if (total > 0) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            var p = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) Begin(p);
            if (mouse.leftButton.isPressed) Move(p);
            if (mouse.leftButton.wasReleasedThisFrame) End(p);
            float scroll=mouse.scroll.ReadValue().y;
            if(scroll!=0) Zoom(distance - scroll * .0015f);
        }

        static bool MovementKeyHeld()
        {
            var keys=Keyboard.current;
            return keys!=null && (keys.wKey.isPressed || keys.aKey.isPressed || keys.sKey.isPressed || keys.dKey.isPressed || keys.upArrowKey.isPressed || keys.downArrowKey.isPressed);
        }
        void ReadKeyboard()
        {
            var keys=Keyboard.current;
            keyboardHeld=CanAdjust && MovementKeyHeld();
            keyboardPlanar=keyboardHeld ? Vector2.ClampMagnitude(new Vector2(
                (keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),
                (keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0)),1) : Vector2.zero;
            keyboardLift=keyboardHeld ? (keys.upArrowKey.isPressed?1:0)-(keys.downArrowKey.isPressed?1:0) : 0;
        }
        // Called by the Web template when the canvas gains/loses operation focus.
        public void SetBrowserInputFocus(string focused)
        {
            bool next=focused=="1";
            if(next==browserFocused)return;
            browserFocused=next;
            CancelForFocusLoss();
            if(!next && Keyboard.current!=null) InputSystem.ResetDevice(Keyboard.current);
        }
        void Zoom(float value)
        {
            distance=Mathf.Clamp(value, Mathf.Max(.1f,MinimumZoom), Mathf.Max(MinimumZoom,MaximumZoom));
        }

        void Begin(Vector2 position)
        {
            start = previous = position;
            dragged = false;
            blocked = IsUI(position);
            active = !blocked;
            selected = Session.Pending;
            rotating = false;
            if (active && ObservationHit(ViewCamera.ScreenPointToRay(position), out var hit))
            {
                var stone = hit.collider.GetComponent<Stone>();
                rotating = stone != null && stone == selected;
                if (stone != null && stone != selected) blocked = true;
            }
        }
        bool ObservationHit(Ray ray, out RaycastHit nearest)
        {
            nearest = default;
            float distance = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(ray, ViewCamera.farClipPlane))
            {
                // Faded scenery must not steal rotation gestures from visible stones.
                if (Occlusion != null && Occlusion.IsFaded(hit.collider)) continue;
                if (hit.distance >= distance) continue;
                nearest = hit; distance = hit.distance;
            }
            return distance < float.PositiveInfinity;
        }
        bool IsUI(Vector2 p)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = p }, uiHits);
            return uiHits.Count > 0;
        }
        void Move(Vector2 p)
        {
            Vector2 delta = p - previous;
            previous = p;
            if (!active || blocked) return;
            if ((p - start).magnitude > Mathf.Max(8, Screen.height * .008f)) dragged = true;
            if (!dragged) return;
            if (rotating && selected != null)
            {
                var rotation = Quaternion.AngleAxis(-delta.x * RotationSensitivity, ViewCamera.transform.up) *
                               Quaternion.AngleAxis(delta.y * RotationSensitivity, ViewCamera.transform.right) * selected.transform.rotation;
                Session.Adjust(selected, selected.transform.position, rotation);
            }
            else
            {
                yaw = Mathf.Repeat(yaw + delta.x * OrbitSensitivity,360);
                pitch = Mathf.Clamp(pitch - delta.y * OrbitSensitivity, Mathf.Clamp(MinimumPitch,-90,90), Mathf.Clamp(MaximumPitch,Mathf.Clamp(MinimumPitch,-90,90),90));
            }
        }
        void End(Vector2 p)
        {
            active = false;
            selected = null;
        }
        public void HoldPlanar(bool held)
        {
            planarStone = held && CanAdjust ? Session.Pending : null;
            if (planarStone == null) planar = Vector2.zero;
            Adjusting = planarStone != null || liftStone != null;
        }
        public void HoldHeight(bool held)
        {
            liftStone = held && CanAdjust ? Session.Pending : null;
            if (liftStone == null) lift = 0;
            Adjusting = planarStone != null || liftStone != null;
        }
        public void MoveStone(Vector2 offset) { planar = Vector2.ClampMagnitude(offset, 1); }
        public void RaiseStone(Vector2 offset) { lift = Mathf.Clamp(offset.y, -1, 1); }
        float Strength(float magnitude) => Mathf.Clamp01((magnitude - JoystickDeadZone) / Mathf.Max(.01f, 1 - JoystickDeadZone));
        void MoveHeldStone()
        {
            if (!CanAdjust) return;
            var stone = Session.Pending;
            float dt = Mathf.Min(Time.deltaTime, .1f);
            // Use this frame's horizontal observation heading, independent of pitch,
            // ground clearance corrections and the previous frame's camera transform.
            var heading = Quaternion.Euler(0, yaw, 0);
            var right = heading * Vector3.right;
            var forward = heading * Vector3.forward;
            // Combine sources once, so simultaneous touch/mouse pads and keyboard
            // never double the speed. Opposing inputs cancel on each axis.
            var padDirection = planarStone==stone ? planar.normalized * Strength(planar.magnitude) : Vector2.zero;
            var direction = Vector2.ClampMagnitude(padDirection+keyboardPlanar,1);
            if (direction != Vector2.zero)
                Session.Adjust(stone, stone.transform.position + (right * direction.x + forward * direction.y) * (MaximumPlanarSpeed * dt), stone.transform.rotation);
            float heightInput=Mathf.Clamp((liftStone==stone ? Mathf.Sign(lift)*Strength(Mathf.Abs(lift)) : 0)+keyboardLift,-1,1);
            if(heightInput!=0)
                Session.Adjust(stone, stone.transform.position + Vector3.up * (heightInput * MaximumHeightSpeed * dt), stone.transform.rotation);
        }
        void LateUpdate()
        {
            if (Session == null || ViewCamera == null) return;
            MoveHeldStone();
            // A scene gesture owns the observation centre until all its fingers end.
            // Otherwise releasing a joystick while still orbiting/pinching makes the
            // centre drift under the observing finger.
            if (!Adjusting && !keyboardHeld && !active && pinch <= 0)
            {
                float follow = 1 - Mathf.Exp(-FollowSpeed * Time.deltaTime);
                if(Session.Pending!=null && !Session.Pending.Released)
                    target=Vector3.Lerp(target,Session.Pending.transform.position,follow);
            }
            PlaceCamera();
        }

        void PlaceCamera()
        {
            // Cover the near-plane corners as well as the camera point.
            float near=ViewCamera.nearClipPlane;
            float halfHeight=near*Mathf.Tan(ViewCamera.fieldOfView*Mathf.Deg2Rad*.5f);
            float clearance=Mathf.Max(CameraClearance, Mathf.Sqrt(near*near+halfHeight*halfHeight*(1+ViewCamera.aspect*ViewCamera.aspect)));
            float low=Mathf.Clamp(MinimumPitch,-90,90), high=Mathf.Clamp(MaximumPitch,low,90);
            pitch=Mathf.Clamp(pitch,low,high);
            float safeDistance=Mathf.Max(.1f,distance);
            var rotation=Quaternion.Euler(pitch,yaw,0);
            var direction=rotation*Vector3.back;
            // A radial exclusion envelope prevents the preview or a nearby tower stone
            // from enclosing the camera/near plane, including while zooming.
            // Increasing the radius past one stone may enter an interval tested
            // earlier. Repeat until stable so list order cannot leave the near plane
            // inside another stone (especially when looking up through a tower).
            for (int pass = 0; pass < Session.Stones.Count; pass++)
            {
                float before = safeDistance;
                foreach(var stone in Session.Stones)
                {
                    if(stone==null || !stone.gameObject.activeInHierarchy) continue;
                    var bounds=stone.Shape.bounds;
                    float radius=bounds.extents.magnitude+clearance;
                    var offset=bounds.center-target;
                    float along=Vector3.Dot(offset,direction);
                    float perpendicularSquared=offset.sqrMagnitude-along*along;
                    if(perpendicularSquared>=radius*radius) continue;
                    float extent=Mathf.Sqrt(Mathf.Max(0,radius*radius-perpendicularSquared));
                    if(along-extent<safeDistance && along+extent>0)
                        safeDistance=Mathf.Max(safeDistance,along+extent);
                }
                if (safeDistance == before) break;
            }
            var position=target+direction*safeDistance;
            // Preserve the yaw-defined right axis at both exact poles. LookRotation
            // with world-up is singular here and can roll or flip unexpectedly.
            // Scenery fades instead of lifting the camera out of negative pitch.
            ViewCamera.transform.SetPositionAndRotation(position,rotation);
            if (Occlusion != null) Occlusion.UpdateView(ViewCamera, clearance);

        }
        void OnApplicationFocus(bool focus) { focusLost=!focus; if (!focus) CancelForFocusLoss(); }
        void OnApplicationPause(bool pause) { paused=pause; if (pause) CancelForFocusLoss(); }
        void OnDisable() { CancelForFocusLoss(); }
    }
}
