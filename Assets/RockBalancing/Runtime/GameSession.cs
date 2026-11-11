using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace RockBalancing
{
    public sealed class GameSession : MonoBehaviour
    {
        readonly List<Stone> stones = new List<Stone>();
        IStoneFactory factory;
        bool generating;
        int generation;
        readonly HashSet<Stone> connected = new HashSet<Stone>();
        readonly HashSet<Stone> joined = new HashSet<Stone>();
        readonly Queue<Stone> traversal = new Queue<Stone>();
        readonly Dictionary<Collider, Stone> byCollider = new Dictionary<Collider, Stone>();
        double promptUntil;
        bool movingEpisode;
        double quietSince = -1;
        [Min(0)] public float LinearMotionThreshold = .006f;
        [Min(0)] public float AngularMotionThreshold = .08f;
        public Func<double> Clock { get; set; } = () => Time.timeAsDouble;
        public int CurrentHeightMm { get; private set; }
        public bool Stabilizing => Clock() < promptUntil;
        public bool ShareBusy { get; private set; }
        public bool CanShare => !Stabilizing && !ShareBusy;
        public Stone Pending { get; private set; }
        public IReadOnlyList<Stone> Stones => stones;
        public bool Waiting => Pending == null;
        string feedback="";
        double feedbackUntil=double.PositiveInfinity;
        public string Feedback
        {
            get => Clock() < feedbackUntil ? feedback : "";
            private set { feedback=value;feedbackUntil=double.PositiveInfinity; }
        }
        void ContactFeedback() { Feedback="已碰到石头，请先移开或抬高";feedbackUntil=Clock()+2; }
        public int Epoch => generation;
        public Collider Base { get; private set; }
        public Collider Ground { get; private set; }
        public float BaseTop => Base == null ? 0 : Base.bounds.max.y;
        public event Action Restarted;
        public event Action PendingChanged;

        void LateUpdate() { RefreshState(); }
        public void RefreshState()
        {
            double now = Clock();
            connected.Clear(); traversal.Clear(); byCollider.Clear();
            foreach(var stone in stones)
                if(stone != null && stone.Released && stone.gameObject.activeSelf) byCollider[stone.Shape]=stone;
            foreach(var pair in byCollider)
                if(pair.Value.Contacts.Contains(Base)) { connected.Add(pair.Value); traversal.Enqueue(pair.Value); }
            while(traversal.Count > 0)
            {
                var stone = traversal.Dequeue();
                foreach(var contact in stone.Contacts)
                    if(contact != null && byCollider.TryGetValue(contact,out var other) && connected.Add(other)) traversal.Enqueue(other);
                // PhysX may send only one side of a contact after sleep/wake transitions.
                foreach(var pair in byCollider)
                    if(pair.Value.Contacts.Contains(stone.Shape) && connected.Add(pair.Value)) traversal.Enqueue(pair.Value);
            }
            float height = 0;
            bool newJoin=false, connectedMoving=false, episodeStillMoving=false;
            foreach(var stone in connected)
            {
                if (!stone.Discarded) height=Mathf.Max(height,stone.HighestPoint()-BaseTop);
                newJoin |= joined.Add(stone);
                connectedMoving |= IsMoving(stone);
            }
            foreach(var stone in connected)
                if(stone != null && stone.gameObject.activeSelf) episodeStillMoving |= IsMoving(stone);
            if (newJoin || (!movingEpisode && connectedMoving))
            {
                promptUntil = now + 1;
                movingEpisode=true; quietSince=-1;
            }
            if (movingEpisode)
            {
                if(episodeStillMoving) quietSince=-1;
                else if(quietSince < 0) quietSince=now;
                else if(now-quietSince >= .12) movingEpisode=false;
            }
            CurrentHeightMm = Mathf.FloorToInt(Mathf.Max(0,height)*1000f);
        }
        bool IsMoving(Stone stone) => !stone.Body.IsSleeping() &&
            (stone.Body.linearVelocity.sqrMagnitude > LinearMotionThreshold*LinearMotionThreshold ||
             stone.Body.angularVelocity.sqrMagnitude > AngularMotionThreshold*AngularMotionThreshold);
        public bool BeginShare()
        {
            RefreshState();
            if(!CanShare) return false;
            ShareBusy=true; return true;
        }
        public void EndShare(int epoch) { if(epoch == generation) ShareBusy=false; }

        public void Initialize(IStoneFactory stoneFactory, Collider baseShape, Collider groundShape)
        {
            factory = stoneFactory;
            Base = baseShape;
            Ground = groundShape;
            Restart();
        }

        public async Task RequestNextAsync()
        {
            if (generating || Pending != null || factory == null) return;
            generating = true;
            int requestEpoch = generation;
            try
            {
                Physics.SyncTransforms();
                var stone = await factory.CreateAsync(new Vector3(0, TowerTop() + .28f, 0), transform);
                if (this == null || requestEpoch != generation)
                {
                    if (stone != null) Destroy(stone.gameObject);
                    return;
                }
                if (stone == null) { Feedback = "等待下一颗 · 点击重试"; return; }
                // Another released stone can still be crossing the supply position.
                // Find free space before showing the trigger-only preview.
                int attempts=0;
                while(!PlacementValid(stone,stone.transform.position,stone.transform.rotation) && attempts++ < 512)
                    stone.transform.position += Vector3.up * .05f;
                if(!PlacementValid(stone,stone.transform.position,stone.transform.rotation))
                { Destroy(stone.gameObject); Feedback="等待下一颗 · 点击重试"; return; }
                stone.Session = this;
                // Preview poses are driven every render frame, not by physics.
                // Interpolation would restore an older physics pose between inputs.
                stone.Body.interpolation = RigidbodyInterpolation.None;
                Pending = stone;
                PendingChanged?.Invoke();
                stones.Add(stone);
                Feedback = "";
            }
            catch (Exception)
            {
                if (this != null && requestEpoch == generation) Feedback = "等待下一颗 · 点击重试";
            }
            finally
            {
                if (this != null && requestEpoch == generation) generating = false;
            }
        }

        public bool Adjust(Stone expected, Vector3 position, Quaternion rotation)
        {
            if (expected == null || expected != Pending) return false;
            var origin = expected.transform.position;
            var orientation = expected.transform.rotation;
            float previousDepth=PenetrationDepth(expected,origin,orientation);
            int steps = Mathf.CeilToInt(Mathf.Max(Vector3.Distance(origin,position)/.006f, Quaternion.Angle(orientation,rotation)/3));
            steps = Mathf.Clamp(steps, 1, 512);
            for(int i=1;i<=steps;i++)
            {
                float t = (float)i/steps;
                var p = Vector3.Lerp(origin,position,t);
                var q = Quaternion.Slerp(orientation,rotation,t);
                float depth=PenetrationDepth(expected,p,q);
                // A moving tower may enter the preview. Permit motion out of that
                // overlap, but never enable physics until it is fully clear.
                if (depth > .0002f && (previousDepth <= .0002f || depth >= previousDepth-.000001f))
                { ContactFeedback(); return false; }
                expected.transform.SetPositionAndRotation(p,q);
                previousDepth=depth;
            }
            Feedback="";
            return true;
        }

        bool PlacementValid(Stone stone, Vector3 position, Quaternion rotation)
            => PenetrationDepth(stone,position,rotation) <= .0002f;

        float PenetrationDepth(Stone stone, Vector3 position, Quaternion rotation)
        {
            Physics.SyncTransforms();
            float radius = stone.Shape.bounds.extents.magnitude + .02f;
            float maximum=0;
            foreach (var other in Physics.OverlapSphere(position,radius,~0,QueryTriggerInteraction.Ignore))
            {
                if (other == stone.Shape) continue;
                var otherPosition = other.transform.position;
                var otherRotation = other.transform.rotation;
                var body = other.attachedRigidbody;
                if (body != null)
                {
                    // Broadphase queries use the current physics pose, while an
                    // interpolated Transform can still show the previous frame.
                    // Use that same physics pose for the narrowphase, including
                    // a child collider's offset from its rigidbody.
                    var correction = body.rotation * Quaternion.Inverse(body.transform.rotation);
                    otherPosition = body.position + correction * (otherPosition - body.transform.position);
                    otherRotation = correction * otherRotation;
                }
                if (Physics.ComputePenetration(stone.Shape,position,rotation,other,otherPosition,otherRotation,
                    out _,out float depth)) maximum=Mathf.Max(maximum,depth);
            }
            return maximum;
        }

        public bool Release(Stone expected)
        {
            if (expected == null || Pending != expected) return false;
            if (!PlacementValid(expected,expected.transform.position,expected.transform.rotation))
            { ContactFeedback(); return false; }
            expected.Released = true;
            expected.Shape.isTrigger = false;
            expected.Body.isKinematic = false;
            expected.Body.interpolation = RigidbodyInterpolation.Interpolate;
            Pending = null;
            PendingChanged?.Invoke();
            _ = RequestNextAsync();
            return true;
        }

        public bool Discard(Stone expected)
        {
            if (expected == null || expected != Pending) return false;
            // Move the discarded stone clear of the tower before enabling physical collisions.
            var p = new Vector3(.8f, Mathf.Max(TowerTop()+.3f, expected.transform.position.y),0);
            if (!PlacementValid(expected,p,expected.transform.rotation)) return false;
            expected.transform.position = p;
            expected.Discarded = true;
            if (!Release(expected)) { expected.Discarded=false; return false; }
            expected.Body.linearVelocity = new Vector3(.5f,.2f,0);
            return true;
        }

        public void Remove(Stone stone)
        {
            if (stone == null || !stones.Remove(stone)) return;
            stone.gameObject.SetActive(false);
            joined.Remove(stone);
            foreach(var other in stones) if(other != null) other.Contacts.Remove(stone.Shape);
            Destroy(stone.gameObject);
        }

        public float TowerTop()
        {
            float top = BaseTop;
            foreach (var stone in connected)
                if (stone != null) top = Mathf.Max(top, stone.HighestPoint());
            return top;
        }

        public void Restart()
        {
            generation++;
            generating = false;
            Pending = null;
            foreach (var stone in stones)
                if (stone != null) { stone.gameObject.SetActive(false); Destroy(stone.gameObject); }
            stones.Clear();
            joined.Clear(); connected.Clear();
            promptUntil=0; CurrentHeightMm=0; ShareBusy=false;
            movingEpisode=false; quietSince=-1;
            Feedback = "";
            Restarted?.Invoke();
            _ = RequestNextAsync();
        }

        void OnDestroy() { generation++; }
    }
}
