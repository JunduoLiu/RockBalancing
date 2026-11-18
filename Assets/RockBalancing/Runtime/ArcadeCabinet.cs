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
        const float CameraDistance = 4.97f;
        const float FrontPitch = 15f;
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

        public void PlaceCamera(Camera camera)
        {
            if (IsFront)
            {
                PlacePerspectiveCamera(camera, 0, FrontPitch);
                return;
            }

            var aroundCabinet = Quaternion.Euler(0, -face * 90, 0);
            float renderAspect = RenderAspect(camera);
            camera.orthographic = true;
            if (face % 2 != 0)
            {
                PlaceSideCamera(camera, aroundCabinet, renderAspect);
                return;
            }

            camera.rect = new Rect(0, 0, 1, 1);
            camera.aspect = renderAspect;
            var focus = Vector3.up * (.43f + extension * .5f);
            var offset = new Vector3(0, .76f, 4.8f);
            camera.orthographicSize = Mathf.Max(1.25f, .86f / renderAspect) + extension * .53f;
            camera.transform.SetPositionAndRotation(focus + aroundCabinet * offset, aroundCabinet * Quaternion.Euler(9, 180, 0));
        }

        internal void PlacePerspectiveCamera(Camera camera, float azimuth, float pitch)
        {
            float aspect = RenderAspect(camera);
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
            // Use the full render surface every frame. The camera's automatic aspect
            // can already reflect the cropped side viewport from the previous frame.
            var target = camera.targetTexture;
            return target != null
                ? (float)target.width / target.height
                : (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
        }

        void PlaceSideCamera(Camera camera, Quaternion aroundCabinet, float renderAspect)
        {
            const float windowHalfWidth = .62f;
            const float minimumHalfHeight = .76f;
            var outside = aroundCabinet * Vector3.forward;
            var halfHeight = windowHalfWidth / renderAspect;
            // Keep a level view through the pane. On wide screens use a narrower
            // viewport so the console stays outside the frame below the pane.
            float height = Mathf.Clamp(1.4f - halfHeight, .43f, .62f);
            var focus = new Vector3(0, height + extension * .5f, -.02f);
            camera.orthographicSize = Mathf.Max(halfHeight, minimumHalfHeight) + extension * .5f;
            float width = Mathf.Min(1, windowHalfWidth / (camera.orthographicSize * renderAspect));
            camera.rect = new Rect((1 - width) * .5f, 0, width, 1);
            camera.aspect = renderAspect * width;
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
            float towerTop = session.TowerTop();
            float targetRise = towerTop - session.BaseTop;
            supplyRise = Mathf.SmoothDamp(supplyRise, targetRise, ref supplyVelocity, SupplyFollowSeconds);
            var supply = session.StandardSupplyPoint;
            cameraFocus = supply + Vector3.up * (supplyRise - targetRise);

            TrackSupply(pending);
            if (pending != null)
            {
                // Retain the player's release height unless it exceeds the lift range
                // as the working window returns from an earlier, taller tower.
                float retreatSupply = Mathf.Max(cameraFocus.y, supply.y);
                maximumLift = retreatSupply < session.PendingSupplyHeight
                    ? Mathf.Min(initialLift, Mathf.Max(MinimumLift, retreatSupply + LiftAllowance))
                    : initialLift;
                if (maximumLift < initialLift) session.LowerPendingTo(maximumLift);
            }

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
            // The gantry can retract into the canopy as the claw approaches the top.
            // Extend the glass only when the actual head would meet the roof.
            float needed = Mathf.Max(supplyRise, headroom - roofCeiling);
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
