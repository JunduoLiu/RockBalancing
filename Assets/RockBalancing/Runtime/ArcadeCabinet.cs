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
        internal Vector3 StickGrip => Joystick.TransformPoint(Vector3.up * .105f);

        Transform console, roof, bridge, trolley, cable, claw;
        readonly Transform[] window = new Transform[5];
        readonly Vector3[] windowScales = new Vector3[5];
        readonly Transform[] jaws = new Transform[3];
        readonly Quaternion[] jawHomes = new Quaternion[3];
        readonly Vector3[] jawAxes = new Vector3[3];
        Quaternion neutralStick;
        Vector3 roofHome, bridgeHome, trolleyHome, cableHome, clawHome, cableScale;
        Vector3 consoleHome, liftHome, lowerHome, releaseHome;
        const float WindowHeight = 1.159f;
        const float GantryClearance = .03f;
        const float CameraDistance = 4.97f;
        const float SupplyFollowSeconds = .18f;
        const float MinimumLift = .70f;
        const float LiftAllowance = .34f;
        const float OpenSeconds = .10f;
        const float ReturnSeconds = .16f;
        const float CloseSeconds = .14f;
        Vector3 releaseFrom;
        float jawReach, jawAngle, releaseAngle, releaseElapsed, extension, roofCeiling;
        float supplyRise, supplyVelocity, initialLift, maximumLift;
        Vector3 cameraFocus;
        bool releasing;
        Stone suppliedStone;
        ArcadeConsoleHitTest consoleHitTest;

        public void Initialize(GameObject model)
        {
            var body = Instantiate(model, transform).transform;
            body.name = model.name;
            console = body.Find("Console");
            consoleHome = console.localPosition;
            Joystick = console.Find("Joystick");
            LiftButton = console.Find("Button Lift");
            LowerButton = console.Find("Button Lower");
            ReleaseButton = console.Find("Button Release");
            consoleHitTest = new ArcadeConsoleHitTest(body, Joystick, LiftButton, LowerButton, ReleaseButton);
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
            liftHome = LiftButton.localPosition;
            lowerHome = LowerButton.localPosition;
            releaseHome = ReleaseButton.localPosition;
            roofHome = roof.position;
            roofCeiling = roof.GetComponent<Renderer>().bounds.min.y;
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

        public void PlaceCamera(Camera camera, float azimuth = 0)
        {
            float pitch = 12f + 3f * Mathf.Cos(azimuth * Mathf.Deg2Rad);
            PlacePerspectiveCamera(camera, azimuth, pitch);
        }

        internal void PlacePerspectiveCamera(Camera camera, float azimuth, float pitch)
        {
            float aspect = RenderAspect(camera);
            PlaceConsole(aspect, supplyRise);
            camera.orthographic = false;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.aspect = aspect;
            // Fit the upper working window and its horizontal footprint. The angular
            // bounds cover the cabinet around the full orbit, independent of tower height.
            float halfFovTangent = Mathf.Max(.36f, .245f / aspect);
            camera.fieldOfView = 2 * Mathf.Atan(halfFovTangent) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(pitch, 180 + azimuth, 0);
            camera.transform.SetPositionAndRotation(cameraFocus + rotation * Vector3.back * CameraDistance, rotation);
        }

        static float RenderAspect(Camera camera)
        {
            var target = camera.targetTexture;
            return target != null
                ? (float)target.width / target.height
                : (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
        }

        internal bool TryHitControl(Ray ray, float range, out float distance) =>
            consoleHitTest.TryHitControl(ray, range, out distance);

        void PlaceConsole(float aspect, float framingRise)
        {
            // A taller portrait window leaves space beneath the glass for the deck.
            float portrait = Mathf.InverseLerp(1f, .45f, aspect);
            console.localPosition = consoleHome + Vector3.up * (framingRise - .24f * portrait);
        }

        internal void ResetClaw()
        {
            releasing = false;
            suppliedStone = null;
        }

        public Vector3 LimitTravel(Stone pending, Vector3 to)
        {
            TrackSupply(pending);
            // If a collision stopped an automatic descent, the player can still
            // move sideways at that height to get clear of the obstacle.
            float ceiling = pending == null ? maximumLift : Mathf.Max(maximumLift, pending.transform.position.y);
            return new Vector3(
                Mathf.Clamp(to.x, -.45f, .45f),
                Mathf.Min(to.y, ceiling),
                Mathf.Clamp(to.z, -.44f, .44f));
        }

        void TrackSupply(Stone pending)
        {
            if (pending == suppliedStone) return;
            suppliedStone = pending;
            if (pending != null) initialLift = maximumLift = Mathf.Max(MinimumLift, pending.transform.position.y + LiftAllowance);
        }

        public void AnimateRelease()
        {
            releaseFrom = claw.position;
            releaseAngle = jawAngle;
            releaseElapsed = 0;
            releasing = true;
        }

        public void Follow(GameSession session)
        {
            var pending = session.Pending;
            var supply = session.StandardSupplyPoint;
            UpdateTravelCeiling(session, pending, supply.y);
            float restSupplyHeight = session.BaseTop + GameSession.SupplyClearance;
            float targetRise = supply.y - restSupplyHeight;
            supplyRise = Mathf.SmoothDamp(supplyRise, targetRise, ref supplyVelocity, SupplyFollowSeconds);
            cameraFocus = new Vector3(supply.x, restSupplyHeight + supplyRise, supply.z);

            var gripPoint = claw.position;
            float grip = jawAngle;
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

            float headroom = Mathf.Max(head.y, gripPoint.y) + GantryClearance;
            // The window and camera share the same smoothed vertical travel. Allow
            // extra clearance when the claw head physically reaches the canopy.
            float nextExtension = Mathf.Max(supplyRise, headroom - roofCeiling);
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
            var rise = Vector3.up * Mathf.Max(extension, headroom - cableHome.y);
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

        void UpdateTravelCeiling(GameSession session, Stone pending, float supplyHeight)
        {
            TrackSupply(pending);
            if (pending == null) return;
            // A collapse can reduce the legal lift range immediately. Keep any
            // release height inside that range and sweep down only as far as safe.
            maximumLift = supplyHeight < session.PendingSupplyHeight
                ? Mathf.Min(initialLift, Mathf.Max(MinimumLift, supplyHeight + LiftAllowance))
                : initialLift;
            if (maximumLift < initialLift) session.LowerPendingTo(maximumLift);
        }

        public void ShowControls(Vector2 stick, bool raising, bool lowering, bool releasePressed)
        {
            Joystick.localRotation = neutralStick * Quaternion.Euler(-stick.y * 11, 0, stick.x * 11);
            LiftButton.localPosition = liftHome + Vector3.down * (raising ? .012f : 0);
            LowerButton.localPosition = lowerHome + Vector3.down * (lowering ? .012f : 0);
            bool releasePulse = releasing && releaseElapsed < OpenSeconds;
            ReleaseButton.localPosition = releaseHome + Vector3.down * (releasePressed || releasePulse ? .012f : 0);
        }
    }
}
