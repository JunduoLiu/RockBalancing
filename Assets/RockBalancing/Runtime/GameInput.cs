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
        [Range(0, 89)] public float MinimumPitch=5;
        [Range(0, 89)] public float MaximumPitch=85;
        [Min(.01f)] public float CameraClearance=.08f;
        float yaw = 25, pitch = 16, distance = 3.8f;
        Vector3 target = new Vector3(0, .3f, 0);
        bool panorama;
        Vector2 start, previous;
        bool active, dragged, blocked, rotating;
        bool requireNeutral;
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
            yaw = 25; pitch = 16; distance = 3.8f; panorama = false;
            target = Session.Pending != null ? Session.Pending.transform.position : new Vector3(0, Session.BaseTop + .28f, 0);
            CancelForFocusLoss();
        }
        public void Panorama()
        {
            float height=ObservationHeight();
            panorama=true; pitch=16;
            target=new Vector3(0,Session.BaseTop+height*.5f,0);
            distance=FramingDistance(height);
        }
        float ObservationHeight()=>Mathf.Max(.6f,Mathf.Max(Session.TowerTop(),
            Session.Pending!=null?Session.Pending.HighestPoint():Session.BaseTop)-Session.BaseTop);
        float FramingDistance(float height)
        {
            float tangent=Mathf.Tan(ViewCamera.fieldOfView*Mathf.Deg2Rad*.5f);
            // Leave the upper score and lower controls clear in the portrait view.
            return Mathf.Max(height/(2*tangent*.44f)*1.1f,.6f/Mathf.Max(.1f,ViewCamera.aspect)/tangent*1.15f);
        }
        public void CancelGesture() { active = false; selected = null; touchId = -1; pinch = 0; }
        public void CancelForFocusLoss(string unused="")
        {
            CancelGesture();requireNeutral=true;
            planar=Vector2.zero;lift=0;planarStone=null;liftStone=null;Adjusting=false;
            foreach(var pad in GetComponentsInChildren<DragPad>(true))pad.Cancel();
            foreach(var button in GetComponentsInChildren<StoneActionButton>(true))button.Cancel();
        }
        void CancelForStoneChange() => CancelForFocusLoss();
        public bool CanAdjust => !Modal && !requireNeutral && Session != null && Session.Pending != null;
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
            if (Modal) return;
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
                if(total==0 && (Mouse.current==null || !Mouse.current.leftButton.isPressed))requireNeutral=false;
                return;
            }
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

        void Zoom(float value)
        {
            panorama=false;
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
            if (active && Physics.Raycast(ViewCamera.ScreenPointToRay(position), out var hit, 100))
            {
                var stone = hit.collider.GetComponent<Stone>();
                rotating = stone != null && stone == selected;
                if (stone != null && stone != selected) blocked = true;
            }
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
                panorama=false;
                yaw = Mathf.Repeat(yaw + delta.x * OrbitSensitivity,360);
                pitch = Mathf.Clamp(pitch - delta.y * OrbitSensitivity, Mathf.Clamp(MinimumPitch,0,89), Mathf.Clamp(MaximumPitch,Mathf.Clamp(MinimumPitch,0,89),89));
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
            var right = Vector3.ProjectOnPlane(ViewCamera.transform.right, Vector3.up).normalized;
            var forward = Vector3.Cross(right, Vector3.up);
            var direction = planar.normalized * Strength(planar.magnitude);
            if (planarStone == stone && direction != Vector2.zero)
                Session.Adjust(stone, stone.transform.position + (right * direction.x + forward * direction.y) * (MaximumPlanarSpeed * dt), stone.transform.rotation);
            if (liftStone == stone && Strength(Mathf.Abs(lift)) > 0)
                Session.Adjust(stone, stone.transform.position + Vector3.up * (Mathf.Sign(lift) * Strength(Mathf.Abs(lift)) * MaximumHeightSpeed * dt), stone.transform.rotation);
        }
        void LateUpdate()
        {
            if (Session == null || ViewCamera == null) return;
            MoveHeldStone();
            if (!Adjusting && !(active && rotating))
            {
                if(panorama)
                {
                    float height=ObservationHeight();
                    target=Vector3.Lerp(target,new Vector3(0,Session.BaseTop+height*.5f,0),Time.deltaTime*FollowSpeed);
                    distance=Mathf.Max(distance,FramingDistance(height));
                }
                else if(Session.Pending!=null && !Session.Pending.Released)
                    target=Vector3.Lerp(target,Session.Pending.transform.position,Time.deltaTime*FollowSpeed);
            }
            PlaceCamera();
        }

        void PlaceCamera()
        {
            // Cover the near-plane corners as well as the camera point.
            float near=ViewCamera.nearClipPlane;
            float halfHeight=near*Mathf.Tan(ViewCamera.fieldOfView*Mathf.Deg2Rad*.5f);
            float clearance=Mathf.Max(CameraClearance, Mathf.Sqrt(near*near+halfHeight*halfHeight*(1+ViewCamera.aspect*ViewCamera.aspect)));
            float low=Mathf.Clamp(MinimumPitch,0,89), high=Mathf.Clamp(MaximumPitch,low,89);
            pitch=Mathf.Clamp(pitch,low,high);
            float safeDistance=Mathf.Max(.1f,distance);
            var direction=Quaternion.Euler(pitch,yaw,0)*Vector3.back;
            // A radial exclusion envelope prevents the preview or a nearby tower stone
            // from enclosing the camera/near plane, including while zooming.
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
            var position=target+direction*safeDistance;
            // Ground and base are horizontal solids in the playable scene. Keeping
            // the near-plane sphere above their surfaces also protects low angles.
            float floor=Session.Ground!=null?Session.Ground.bounds.max.y:float.NegativeInfinity;
            if(Session.Base!=null)
            {
                var b=Session.Base.bounds; b.Expand(clearance*2);
                if(position.x>=b.min.x && position.x<=b.max.x && position.z>=b.min.z && position.z<=b.max.z)
                    floor=Mathf.Max(floor,Session.Base.bounds.max.y);
            }
            position.y=Mathf.Max(position.y,floor+clearance);
            ViewCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position,Vector3.up));
        }
        void OnApplicationFocus(bool focus) { if (!focus) CancelForFocusLoss(); }
        void OnApplicationPause(bool pause) { if (pause) CancelForFocusLoss(); }
        void OnDisable() { CancelForFocusLoss(); }
    }
}
