using UnityEngine;

namespace RockBalancing
{
    // The cabinet provides a view and travel envelope. GameSession owns every stone.
    public sealed class ArcadeCabinet : MonoBehaviour
    {
        public static Quaternion FrontHeading => Quaternion.Euler(0, 180, 0);

        public Transform Joystick { get; private set; }
        public Transform LiftButton { get; private set; }
        public Transform LowerButton { get; private set; }
        public Transform ReleaseButton { get; private set; }
        internal Vector3 StickGrip => Joystick.position + new Vector3(0, .159f, -.013f);
        internal bool IsFront => face == 0;

        Transform roof, bridge, trolley, cable, claw;
        readonly Transform[] window = new Transform[5];
        readonly Vector3[] windowScales = new Vector3[5];
        readonly Transform[] jaws = new Transform[3];
        readonly Quaternion[] jawHomes = new Quaternion[3];
        readonly Vector3[] jawAxes = new Vector3[3];
        Quaternion neutralStick;
        Vector3 roofHome, bridgeHome, trolleyHome, cableHome, clawHome, cableScale;
        Vector3 liftHome, lowerHome, releaseHome;
        const float WindowHeight = 1.159f;
        const float GantryClearance = .03f;
        const float OpenSeconds = .10f;
        const float ReturnSeconds = .16f;
        const float CloseSeconds = .14f;
        Vector3 releaseFrom;
        float jawReach, jawAngle, releaseAngle, releaseElapsed, extension, maximumLift;
        bool releasing;
        Stone suppliedStone;
        int face;

        public void Initialize(GameObject model)
        {
            var body = Instantiate(model, transform).transform;
            body.name = model.name;
            Joystick = body.Find("Joystick");
            LiftButton = body.Find("Button Lift");
            LowerButton = body.Find("Button Lower");
            ReleaseButton = body.Find("Button Release");
            roof = body.Find("Roof");
            bridge = body.Find("Bridge");
            trolley = body.Find("Trolley");
            cable = body.Find("Cable");
            claw = body.Find("Claw head");
            var windowParts = new[] { "Window posts", "Glass Front", "Glass Right", "Glass Rear", "Glass Left" };
            for (int i = 0; i < window.Length; i++)
            {
                window[i] = body.Find(windowParts[i]);
                windowScales[i] = window[i].localScale;
            }

            neutralStick = Joystick.localRotation;
            liftHome = LiftButton.position;
            lowerHome = LowerButton.position;
            releaseHome = ReleaseButton.position;
            roofHome = roof.position;
            bridgeHome = bridge.position;
            trolleyHome = trolley.position;
            cableHome = cable.position;
            clawHome = claw.position;
            cableScale = cable.localScale;
            for (int i = 0; i < jaws.Length; i++)
            {
                var jaw = body.Find($"Claw jaw {i + 1}");
                jaws[i] = jaw;
                jawReach = Mathf.Max(jawReach, clawHome.y - jaw.GetComponent<Renderer>().bounds.min.y);
                var radial = Vector3.ProjectOnPlane(jaw.position - clawHome, Vector3.up).normalized;
                jaw.SetParent(claw, true);
                jawAxes[i] = claw.InverseTransformDirection(Vector3.Cross(Vector3.down, radial));
                jawHomes[i] = jaw.localRotation;
            }
        }

        public void PlaceCamera(Camera camera)
        {
            var aroundCabinet = Quaternion.Euler(0, -face * 90, 0);
            camera.orthographic = true;
            if (face % 2 != 0)
            {
                PlaceSideCamera(camera, aroundCabinet);
                return;
            }

            camera.rect = new Rect(0, 0, 1, 1);
            camera.ResetProjectionMatrix();
            // The front pitch shows the tops of the physical controls through the same
            // frame as the claw and tower. The rear looks over the lower cabinet trim.
            var focus = new Vector3(0, (IsFront ? .38f : .43f) + extension * .5f, 0);
            var offset = new Vector3(0, IsFront ? 1.28f : .76f, 4.8f);
            camera.orthographicSize = Mathf.Max(1.25f, .86f / camera.aspect) + extension * .53f;
            camera.transform.SetPositionAndRotation(
                focus + aroundCabinet * offset,
                aroundCabinet * Quaternion.Euler(IsFront ? 14.93f : 9f, 180, 0));
        }

        void PlaceSideCamera(Camera camera, Quaternion aroundCabinet)
        {
            const float windowHalfWidth = .62f;
            const float minimumHalfHeight = .76f;
            var outside = aroundCabinet * Vector3.forward;
            var halfHeight = windowHalfWidth / camera.aspect;
            // Keep a level view through the pane. On wide screens use a narrower
            // viewport so the console stays outside the frame below the pane.
            float height = Mathf.Clamp(1.4f - halfHeight, .43f, .62f);
            var focus = new Vector3(0, height + extension * .5f, -.02f);
            camera.orthographicSize = Mathf.Max(halfHeight, minimumHalfHeight) + extension * .5f;
            float width = Mathf.Min(1, windowHalfWidth / (camera.orthographicSize * camera.aspect));
            camera.rect = new Rect((1 - width) * .5f, 0, width, 1);
            if (width < 1)
                camera.projectionMatrix = Matrix4x4.Ortho(-windowHalfWidth, windowHalfWidth,
                    -camera.orthographicSize, camera.orthographicSize, camera.nearClipPlane, camera.farClipPlane);
            else
                camera.ResetProjectionMatrix();
            camera.transform.SetPositionAndRotation(focus + outside * 4.8f, aroundCabinet * FrontHeading);
        }

        internal void Turn(int direction) => face = (face + direction + 4) % 4;

        internal void ResetFace()
        {
            face = 0;
            releasing = false;
            suppliedStone = null;
        }

        public Vector3 LimitTravel(Stone pending, Vector3 to)
        {
            TrackSupply(pending);
            return new Vector3(
                Mathf.Clamp(to.x, -.45f, .45f),
                Mathf.Min(to.y, maximumLift),
                Mathf.Clamp(to.z, -.44f, .44f));
        }

        void TrackSupply(Stone pending)
        {
            if (pending == suppliedStone) return;
            suppliedStone = pending;
            if (pending != null) maximumLift = Mathf.Max(.70f, pending.transform.position.y + .34f);
        }

        public void AnimateRelease()
        {
            releaseFrom = claw.position;
            releaseAngle = jawAngle;
            releaseElapsed = 0;
            releasing = true;
        }

        public void Follow(Stone pending, float towerTop)
        {
            TrackSupply(pending);
            if (pending == null && !releasing) return;
            var gripPoint = claw.position;
            float grip = releaseAngle;
            if (pending != null)
            {
                gripPoint = pending.transform.position;
                gripPoint.y = pending.HighestPoint() + jawReach * .82f;
                var extents = pending.Surface.bounds.extents;
                grip = Mathf.Clamp((Mathf.Max(extents.x, extents.z) - .095f) * 200, -6, 15);
            }
            var head = gripPoint;
            if (releasing)
            {
                const float duration = OpenSeconds + ReturnSeconds + CloseSeconds;
                float limit = pending == null ? OpenSeconds : duration;
                releaseElapsed = Mathf.Min(limit, releaseElapsed + Mathf.Min(Time.unscaledDeltaTime, .05f));
                float open = Mathf.SmoothStep(0, 1, releaseElapsed / OpenSeconds);
                float travel = Mathf.SmoothStep(0, 1, (releaseElapsed - OpenSeconds) / ReturnSeconds);
                float close = Mathf.SmoothStep(0, 1, (releaseElapsed - OpenSeconds - ReturnSeconds) / CloseSeconds);
                head = Vector3.Lerp(releaseFrom + Vector3.up * (.042f * open), gripPoint, travel);
                grip = Mathf.Lerp(releaseAngle, grip, travel) + 36f * (open - close);
                releasing = releaseElapsed < duration;
            }

            float headroom = Mathf.Max(Mathf.Max(head.y, gripPoint.y) + GantryClearance, towerTop + .08f);
            float needed = Mathf.Max(0, headroom - cableHome.y);
            float nextExtension = Mathf.Max(needed, extension - Time.deltaTime * 1.4f);
            if (nextExtension != extension)
            {
                extension = nextExtension;
                roof.position = roofHome + Vector3.up * extension;
                var stretch = new Vector3(1, 1 + extension / WindowHeight, 1);
                for (int i = 0; i < window.Length; i++)
                    window[i].localScale = Vector3.Scale(windowScales[i], stretch);
            }

            var depth = Vector3.forward * head.z;
            var offset = depth + Vector3.right * head.x;
            var rise = Vector3.up * extension;
            bridge.position = bridgeHome + depth + rise;
            trolley.position = trolleyHome + offset + rise;
            cable.position = cableHome + offset + rise;
            claw.position = head;
            cable.localScale = Vector3.Scale(cableScale, new Vector3(1,
                (cable.position.y - head.y) / (cableHome.y - clawHome.y), 1));
            jawAngle = grip;
            for (int i = 0; i < jaws.Length; i++)
                jaws[i].localRotation = Quaternion.AngleAxis(jawAngle, jawAxes[i]) * jawHomes[i];
        }

        public void ShowControls(Vector2 stick, bool raising, bool lowering, bool releasePressed)
        {
            Joystick.localRotation = neutralStick * Quaternion.Euler(-stick.y * 11, 0, stick.x * 11);
            LiftButton.position = liftHome + Vector3.down * (raising ? .012f : 0);
            LowerButton.position = lowerHome + Vector3.down * (lowering ? .012f : 0);
            bool releasePulse = releasing && releaseElapsed < OpenSeconds;
            ReleaseButton.position = releaseHome + Vector3.down * (releasePressed || releasePulse ? .012f : 0);
        }
    }
}
