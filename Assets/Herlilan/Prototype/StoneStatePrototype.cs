using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Herlilan.Prototype
{
    // Experimental rules only; this prototype does not establish scoring decisions.
    public sealed class StoneStatePrototype : MonoBehaviour
    {
        public enum StoneState { Generating, Pending, Released, Discarded }
        sealed class Stone
        {
            public GameObject Object;
            public Rigidbody Body;
            public StoneState State;
            public PrototypeStoneContacts Contacts;
            public float QuietTime;
        }

        readonly List<Stone> stones = new List<Stone>();
        readonly Queue<string> history = new Queue<string>();
        Stone pending;
        Material groundMaterial, pendingMaterial, releasedMaterial, discardedMaterial;
        string notice = "Adjust the pending stone, then release or discard.";
        float nextAt;
        int serial;
        bool rotating;
        Collider groundCollider;
        float currentHeight, bestHeight;
        float candidateHeight, heightHoldTime;
        const float ScoreHoldDuration = 1f;
        int Score => Mathf.FloorToInt(currentHeight * 100);
        int BestScore => Mathf.FloorToInt(bestHeight * 100);
        readonly HashSet<Collider> supported = new HashSet<Collider>();
        float UiScale => Mathf.Max(1, Screen.height / 800f);

        bool OverPanel(Vector2 point) => new Rect(12, 12, 280, 560).Contains(
            new Vector2(point.x, Screen.height - point.y) / UiScale);

        bool MouseOnPlane(Vector2 point, out Vector3 world)
        {
            world = default;
            var camera = Camera.main;
            if (camera == null) return false;
            var ray = camera.ScreenPointToRay(point);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return false;
            world = ray.GetPoint(distance);
            world.y = Mathf.Clamp(world.y, 0.3f, 9);
            world.z = 0;
            return true;
        }

        void Start()
        {
            if (Camera.main != null) ConfigureCamera(Camera.main);
            pendingMaterial = MakeMaterial(new Color(0.25f, 0.8f, 1f));
            releasedMaterial = MakeMaterial(new Color(0.65f, 0.62f, 0.55f));
            discardedMaterial = MakeMaterial(new Color(0.95f, 0.5f, 0.25f));
            groundMaterial = MakeMaterial(new Color(0.23f, 0.3f, 0.27f));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Prototype ground";
            ground.transform.SetParent(transform);
            ground.transform.position = new Vector3(0, -0.25f, 0);
            ground.transform.localScale = new Vector3(18, 0.5f, 18);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            groundCollider = ground.GetComponent<Collider>();
            Spawn();
        }

        public static void ConfigureCamera(Camera camera)
        {
            camera.transform.position = new Vector3(0, 4.5f, -18);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
        }

        Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
        }

        void Update()
        {
            if (pending == null && Time.time >= nextAt) Spawn();
            var mouse = Mouse.current;
            if (pending == null || mouse == null || !Application.isFocused)
            {
                rotating = false;
                return;
            }
            Vector2 pointer = mouse.position.ReadValue();
            var stoneTransform = pending.Object.transform;
            if (rotating && !mouse.rightButton.isPressed)
            {
                rotating = false;
                if (Camera.main != null)
                {
                    Vector3 screenPosition = Camera.main.WorldToScreenPoint(stoneTransform.position);
                    if (screenPosition.z > 0)
                        mouse.WarpCursorPosition(new Vector2(screenPosition.x, screenPosition.y));
                }
                // The input device reports the warped position on the next input update.
                // Skip following this frame so the old cursor position cannot move the stone.
                return;
            }
            if (OverPanel(pointer) || pointer.x < 0 || pointer.y < 0 || pointer.x > Screen.width || pointer.y > Screen.height || !Application.isFocused) return;
            if (!mouse.rightButton.isPressed && MouseOnPlane(pointer, out var target))
            {
                target.x = Mathf.Clamp(target.x, -4, 4);
                target.z = Mathf.Clamp(target.z, -4, 4);
                stoneTransform.position = target;
            }
            if (mouse.rightButton.isPressed)
            {
                rotating = true;
                Vector2 delta = mouse.delta.ReadValue();
                stoneTransform.Rotate(Vector3.forward, -delta.x * 0.35f, Space.World);
            }
            if (mouse.leftButton.wasPressedThisFrame && !mouse.rightButton.isPressed) Finish(false);
        }

        void FixedUpdate()
        {
            // Prototype rule: only quiet stones connected to the ground through released stones count.
            // Discarded stones cannot provide a scoring support path.
            supported.Clear();
            supported.Add(groundCollider);
            foreach (var stone in stones)
            {
                if (stone.State != StoneState.Released) continue;
                bool quiet = stone.Body.IsSleeping() ||
                    (stone.Body.linearVelocity.sqrMagnitude < 0.0025f && stone.Body.angularVelocity.sqrMagnitude < 0.01f);
                stone.QuietTime = quiet ? stone.QuietTime + Time.fixedDeltaTime : 0;
            }
            float measuredHeight = 0;
            bool added;
            do
            {
                added = false;
                foreach (var stone in stones)
                {
                    if (stone.State != StoneState.Released || stone.QuietTime < 0.75f) continue;
                    var collider = stone.Object.GetComponent<Collider>();
                    if (supported.Contains(collider)) continue;
                    foreach (var contact in stone.Contacts.Touching)
                    {
                        if (!supported.Contains(contact)) continue;
                        supported.Add(collider);
                        measuredHeight = Mathf.Max(measuredHeight, collider.bounds.max.y);
                        added = true;
                        break;
                    }
                }
            } while (added);
            if (measuredHeight <= 0)
            {
                candidateHeight = heightHoldTime = currentHeight = 0;
                return;
            }
            if (Mathf.Abs(measuredHeight - candidateHeight) > 0.02f)
            {
                candidateHeight = measuredHeight;
                heightHoldTime = 0;
            }
            else heightHoldTime = Mathf.Min(ScoreHoldDuration, heightHoldTime + Time.fixedDeltaTime);
            // Remove a lost height immediately; award a new height only after the hold completes.
            currentHeight = Mathf.Min(currentHeight, measuredHeight);
            if (heightHoldTime >= ScoreHoldDuration)
            {
                currentHeight = measuredHeight;
                bestHeight = Mathf.Max(bestHeight, currentHeight);
            }
        }

        void Record(string message)
        {
            history.Enqueue(message);
            while (history.Count > 6) history.Dequeue();
        }

        void Spawn()
        {
            var obj = new GameObject();
            var mesh = CreateStoneMesh();
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>();
            var collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            obj.name = "Stone " + ++serial;
            obj.transform.SetParent(transform);
            obj.transform.position = new Vector3(0, 4, 0);
            obj.transform.localScale = new Vector3(Random.Range(0.8f, 1.5f), Random.Range(0.6f, 1.1f), Random.Range(0.8f, 1.4f));
            var body = obj.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            pending = new Stone { Object = obj, Body = body, State = StoneState.Generating,
                Contacts = obj.AddComponent<PrototypeStoneContacts>() };
            stones.Add(pending);
            Record(obj.name + ": Generating -> Pending");
            pending.State = StoneState.Pending;
            obj.GetComponent<Renderer>().sharedMaterial = pendingMaterial;
        }

        bool ValidPlacement()
        {
            Physics.SyncTransforms();
            var box = pending.Object.GetComponent<Collider>();
            foreach (var other in Physics.OverlapBox(box.bounds.center,
                         box.bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other == box) continue;
                if (Physics.ComputePenetration(box, box.transform.position, box.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > 0.015f)
                    return false;
            }
            return true;
        }

        void Finish(bool discard)
        {
            if (pending == null) return;
            if (!discard && !ValidPlacement())
            {
                notice = "Invalid release: overlapping another object. Still Pending.";
                Record("Release rejected: remains Pending");
                return;
            }
            if (discard)
            {
                // Temporary discard-button behavior, pending a gameplay decision.
                pending.Object.transform.position = new Vector3(6, 3, 0);
            }
            pending.State = discard ? StoneState.Discarded : StoneState.Released;
            pending.Object.GetComponent<Renderer>().sharedMaterial = discard ? discardedMaterial : releasedMaterial;
            pending.Body.isKinematic = false;
            pending.Body.useGravity = true;
            Record(pending.Object.name + ": Pending -> " + pending.State);
            pending = null;
            nextAt = Time.time + 0.6f;
            notice = "Physics owns the stone. Next stone arrives after 0.6s (experimental).";
        }

        void Restart()
        {
            foreach (var stone in stones)
            {
                stone.Object.SetActive(false);
                Destroy(stone.Object.GetComponent<MeshFilter>().sharedMesh);
                Destroy(stone.Object);
            }
            stones.Clear();
            pending = null;
            history.Clear();
            serial = 0;
            currentHeight = bestHeight = 0;
            candidateHeight = heightHoldTime = 0;
            supported.Clear();
            Record("Restart: cleared all stones");
            notice = "New session.";
            Spawn();
        }

        void OnGUI()
        {
            float scale = UiScale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(12, 12, 280, 560), GUI.skin.box);
            GUILayout.Label("STONE STATE PROTOTYPE | #3");
            var scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            GUILayout.Label("SCORE  " + Score, scoreStyle);
            GUILayout.Label("SESSION BEST  " + BestScore, scoreStyle);
            GUILayout.Label("Stable height: " + currentHeight.ToString("F2") + " | Best: " + bestHeight.ToString("F2"));
            GUILayout.Label("Height x 100 | Hold steady for 1 second");
            GUILayout.Label(candidateHeight > 0 && heightHoldTime < ScoreHoldDuration
                ? "Confirming height: " + heightHoldTime.ToString("F1") + " / 1.0s"
                : "Height confirmed / waiting for stable stones");
            GUILayout.Label("Cyan: Pending | Gray: Released | Orange: Discarded");
            GUILayout.Label("State: " + (pending == null ? "Waiting for next stone" : pending.State.ToString()));
            GUI.enabled = pending != null;
            if (pending != null)
            {
                GUILayout.Label("Mouse: move + height | Left click: release");
                GUILayout.Label("Right drag: rotate");
                GUILayout.Label("Release height: " + pending.Object.transform.position.y.ToString("F2"));
            }
            if (GUILayout.Button("Release", GUILayout.Height(44))) Finish(false);
            if (GUILayout.Button("Discard (experimental button)", GUILayout.Height(44))) Finish(true);
            GUI.enabled = true;
            if (GUILayout.Button("Restart", GUILayout.Height(44))) Restart();
            GUILayout.Label(notice);
            GUILayout.Label("Stones: " + stones.Count + " | Discarded stones do not score");
            foreach (var line in history) GUILayout.Label(line);
            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            foreach (var stone in stones)
                if (stone.Object != null) Destroy(stone.Object.GetComponent<MeshFilter>().sharedMesh);
            foreach (var material in new[] { groundMaterial, pendingMaterial, releasedMaterial, discardedMaterial })
                if (material != null) Destroy(material);
        }

        static Mesh CreateStoneMesh()
        {
            // An irregular icosahedron: each stone has its own silhouette and matching convex collision.
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var vertices = new[] {
                new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1)
            };
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = vertices[i].normalized * Random.Range(0.47f, 0.63f);
            int[] triangles = {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            // Split vertices for crisp stone facets rather than a smooth ball appearance.
            var faceted = new Vector3[triangles.Length];
            var indices = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i++) { faceted[i] = vertices[triangles[i]]; indices[i] = i; }
            var mesh = new Mesh { name = "Irregular prototype stone", vertices = faceted, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    public sealed class PrototypeStoneContacts : MonoBehaviour
    {
        public readonly HashSet<Collider> Touching = new HashSet<Collider>();
        void OnCollisionEnter(Collision collision) => Touching.Add(collision.collider);
        void OnCollisionStay(Collision collision) => Touching.Add(collision.collider);
        void OnCollisionExit(Collision collision) => Touching.Remove(collision.collider);
    }
}
