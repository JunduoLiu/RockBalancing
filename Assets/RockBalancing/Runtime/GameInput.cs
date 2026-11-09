using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RockBalancing
{
    public sealed class GameInput : MonoBehaviour
    {
        public GameSession Session;
        public Camera ViewCamera;
        public bool Modal;
        public bool Adjusting;
        [Min(.01f)] public float RotationSensitivity=.45f;
        [Min(.01f)] public float OrbitSensitivity=.25f;
        [Min(.01f)] public float PlanarSensitivity=2;
        [Min(.01f)] public float HeightSensitivity=3;
        [Min(.01f)] public float FollowSpeed=2;
        [Min(.1f)] public float MinimumZoom=.7f;
        [Min(1)] public float MaximumZoom=40;
        float yaw = 25, distance = 3.8f, targetY = .3f;
        Vector2 start, previous;
        double downTime;
        bool active, dragged, blocked, rotating;
        bool requireNeutral;
        Stone selected;
        int touchId = -1;
        float pinch;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        public void ResetView() { yaw = 25; distance = 3.8f; targetY = .3f; CancelGesture(); }
        public void Panorama()
        {
            float height=ObservationHeight();
            targetY=Session.BaseTop+height*.5f;
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
            foreach(var pad in GetComponentsInChildren<DragPad>())pad.Cancel();
        }

        void Update()
        {
            if (Modal) { CancelGesture(); return; }
            var touch = Touchscreen.current;
            int count = 0;
            UnityEngine.InputSystem.Controls.TouchControl first = null, second = null;
            if (touch != null)
                foreach (var t in touch.touches)
                    if (t.press.isPressed) { if (count == 0) first = t; else if(count == 1) second = t; count++; }
            if(requireNeutral)
            {
                if(count==0 && (Mouse.current==null || !Mouse.current.leftButton.isPressed))requireNeutral=false;
                return;
            }
            if (count >= 2)
            {
                active = false;
                blocked = true;
                float current = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());
                if (pinch > 0) distance = Mathf.Clamp(distance * pinch / Mathf.Max(1, current), MinimumZoom, MaximumZoom);
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
            var mouse = Mouse.current;
            if (mouse == null) return;
            var p = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) Begin(p);
            if (mouse.leftButton.isPressed) Move(p);
            if (mouse.leftButton.wasReleasedThisFrame) End(p);
            distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * .0015f, MinimumZoom, MaximumZoom);
        }

        void Begin(Vector2 position)
        {
            start = previous = position;
            downTime = Time.unscaledTimeAsDouble;
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
            else yaw += delta.x * OrbitSensitivity;
        }
        void End(Vector2 p)
        {
            dragged |= (p-start).magnitude > Mathf.Max(8,Screen.height*.008f);
            if (active && !blocked && !rotating && !dragged && Time.unscaledTimeAsDouble - downTime < .35 && !IsUI(p))
                Session.Release(selected);
            active = false;
            selected = null;
        }
        public void MoveStone(Vector2 delta)
        {
            if(Modal||pinch>0||requireNeutral)return;
            var stone = Session.Pending;
            if (stone == null) return;
            var right = Vector3.ProjectOnPlane(ViewCamera.transform.right, Vector3.up).normalized;
            var forward = Vector3.ProjectOnPlane(ViewCamera.transform.forward, Vector3.up).normalized;
            Session.Adjust(stone, stone.transform.position + (right * delta.x + forward * delta.y) * PlanarSensitivity, stone.transform.rotation);
        }
        public void RaiseStone(Vector2 delta)
        {
            if(Modal||pinch>0||requireNeutral)return;
            var stone = Session.Pending;
            if (stone != null) Session.Adjust(stone, stone.transform.position + Vector3.up * (delta.y * HeightSensitivity), stone.transform.rotation);
        }
        void LateUpdate()
        {
            if (Session == null || ViewCamera == null) return;
            if (!Adjusting && !(active && rotating))
            {
                float height=ObservationHeight();
                targetY=Mathf.Lerp(targetY,Session.BaseTop+height*.5f,Time.deltaTime*FollowSpeed);
                distance=Mathf.Max(distance,FramingDistance(height));
            }
            var target = new Vector3(0, targetY, 0);
            ViewCamera.transform.position = target + Quaternion.Euler(16, yaw, 0) * new Vector3(0, 0, -distance);
            ViewCamera.transform.LookAt(target);
        }
        void OnApplicationFocus(bool focus) { if (!focus) CancelForFocusLoss(); }
        void OnApplicationPause(bool pause) { if (pause) CancelForFocusLoss(); }
    }
}
