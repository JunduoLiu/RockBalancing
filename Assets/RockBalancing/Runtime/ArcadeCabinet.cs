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
        internal bool IsFront => face == 0;

        Transform bridge, trolley, cable, claw;
        Quaternion neutralStick;
        Vector3 bridgeHome, trolleyHome, cableHome, clawHome, cableScale;
        Vector3 liftHome, lowerHome;
        float jawReach;
        int face;

        public void Initialize(GameObject model)
        {
            var body = Instantiate(model, transform).transform;
            body.name = model.name;
            Joystick = body.Find("Joystick");
            LiftButton = body.Find("Button Lift");
            LowerButton = body.Find("Button Lower");
            ReleaseButton = body.Find("Button Release");
            bridge = body.Find("Bridge");
            trolley = body.Find("Trolley");
            cable = body.Find("Cable");
            claw = body.Find("Claw head");

            neutralStick = Joystick.localRotation;
            liftHome = LiftButton.position;
            lowerHome = LowerButton.position;
            bridgeHome = bridge.position;
            trolleyHome = trolley.position;
            cableHome = cable.position;
            clawHome = claw.position;
            cableScale = cable.localScale;
            jawReach = clawHome.y - claw.GetComponent<Renderer>().bounds.min.y;
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

            // The front pitch shows the tops of the physical controls through the same
            // frame as the claw and tower. The rear looks over the lower cabinet trim.
            var focus = new Vector3(0, IsFront ? .38f : .43f, 0);
            var offset = new Vector3(0, IsFront ? 1.28f : .76f, 4.8f);
            camera.orthographicSize = Mathf.Max(1.25f, .86f / camera.aspect);
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
            // Portrait crops at the front edge of the pane. Wider views crop just
            // above the deck instead. A level view keeps the near edge of the base
            // from blocking the bottom of the tower.
            bool portrait = halfHeight >= minimumHalfHeight;
            float height = portrait ? Mathf.Clamp(1.4f - halfHeight, .43f, .62f) : .086f + minimumHalfHeight;
            var focus = new Vector3(0, height, -.02f);
            camera.orthographicSize = Mathf.Max(halfHeight, minimumHalfHeight);
            camera.transform.SetPositionAndRotation(focus + outside * 4.8f, aroundCabinet * FrontHeading);
        }

        internal void Turn(int direction) => face = (face + direction + 4) % 4;

        internal void ResetFace() => face = 0;

        public Vector3 LimitTravel(Vector3 from, Vector3 to)
        {
            // Leave space for the claw under the trolley and inside the four panes.
            // Supply still comes from the session. A stone supplied above the carriage
            // can move down freely without teleporting or changing the supply height.
            return new Vector3(
                Mathf.Clamp(to.x, -.45f, .45f),
                Mathf.Min(to.y, Mathf.Max(from.y, .70f)),
                Mathf.Clamp(to.z, -.44f, .44f));
        }

        public void Follow(Stone pending)
        {
            if (pending == null) return;
            var position = pending.transform.position;
            var depth = Vector3.forward * position.z;
            var offset = depth + Vector3.right * position.x;
            bridge.position = bridgeHome + depth;
            trolley.position = trolleyHome + offset;
            cable.position = cableHome + offset;
            float headHeight = Mathf.Min(cableHome.y - .03f, pending.HighestPoint() + jawReach * .88f);
            claw.position = new Vector3(position.x, headHeight, position.z);
            cable.localScale = Vector3.Scale(cableScale, new Vector3(1,
                (cableHome.y - headHeight) / (cableHome.y - clawHome.y), 1));
        }

        public void ShowControls(Vector2 stick, bool raising, bool lowering)
        {
            Joystick.localRotation = neutralStick * Quaternion.Euler(-stick.y * 11, 0, stick.x * 11);
            LiftButton.position = liftHome + Vector3.down * (raising ? .012f : 0);
            LowerButton.position = lowerHome + Vector3.down * (lowering ? .012f : 0);
        }
    }
}
