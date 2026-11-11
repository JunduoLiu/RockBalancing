using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RockBalancing.Tests
{
    public sealed class FlatStoneTests
    {
        Scene scene;
        PhysicsScene physics;
        GameObject root;
        GameSession session;
        Collider pedestal, ground;
        double elapsed;

        [SetUp] public void Setup()
        {
            scene = SceneManager.CreateScene("Flat stone physics", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
            root = new GameObject("Flat stone session");
            SceneManager.MoveGameObjectToScene(root, scene);
            session = root.AddComponent<GameSession>();
            session.Clock = () => elapsed;
            pedestal = Cube("Base", new Vector3(0, -.08f, 0), new Vector3(1, .16f, 1));
            ground = Cube("Ground", new Vector3(0, -.3f, 0), new Vector3(100, .1f, 100));
        }

        Collider Cube(string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(root.transform);
            go.transform.position = position; go.transform.localScale = scale;
            return go.GetComponent<Collider>();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Object.Destroy(root);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        void StartSession(float probability, int seed, float min = .18f, float max = .28f)
        {
            elapsed = 0;
            session.Initialize(new StoneFactory(shapes: new StoneShapeSettings {
                FlatProbability = probability, MinimumThicknessRatio = min, MaximumThicknessRatio = max
            }, nextSeed: () => seed++), pedestal, ground);
        }

        void Step(int frames)
        {
            Physics.SyncTransforms();
            for (int i = 0; i < frames; i++)
            {
                physics.Simulate(.02f);
                elapsed += .02;
                session.RefreshState();
            }
        }

        Stone Drop(float surface, float x = 0, float clearance = .006f)
        {
            var stone = session.Pending;
            var position = new Vector3(x, surface - stone.OwnedMesh.bounds.min.y + clearance, 0);
            // Use the public session boundary to validate the final pose and release.
            Assert.That(session.Adjust(stone, new Vector3(x, Mathf.Max(position.y, stone.transform.position.y), 0), Quaternion.identity), Is.True);
            Assert.That(session.Adjust(stone, position, Quaternion.identity), Is.True);
            Assert.That(session.Release(stone), Is.True);
            return stone;
        }

        [Test] public void FlatAndIrregularShapesAreClosedConvexAndKeepTheirScale()
        {
            foreach (float probability in new[] { 0f, 1f })
            for (int seed = 0; seed < 48; seed++)
            {
                var mesh = PebbleMesh.Create(seed, new StoneShapeSettings { FlatProbability = probability });
                try
                {
                    var v = mesh.vertices; var t = mesh.triangles;
                    float diameter = v.Max(a => v.Max(b => Vector3.Distance(a, b)));
                    Assert.That(diameter, Is.InRange(.13f, .3f));
                    Assert.That(PebbleMesh.Volume(mesh), Is.GreaterThan(.0001f));
                    Assert.That(t.Length / 3, Is.LessThanOrEqualTo(255));
                    var edges = new Dictionary<(int, int), int>();
                    float upperArea = 0, lowerArea = 0;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        var cross = Vector3.Cross(v[t[i+1]] - v[t[i]], v[t[i+2]] - v[t[i]]);
                        Assert.That(cross.magnitude, Is.GreaterThan(1e-7f));
                        foreach (var p in v) Assert.That(Vector3.Dot(cross.normalized, p-v[t[i]]), Is.LessThan(.00001f));
                        if (cross.normalized.y > .9999f) upperArea += cross.magnitude / 2;
                        if (cross.normalized.y < -.9999f) lowerArea += cross.magnitude / 2;
                        for (int j = 0; j < 3; j++)
                        {
                            int a = t[i+j], b = t[i+(j+1)%3];
                            var key = (Mathf.Min(a,b), Mathf.Max(a,b));
                            edges[key] = edges.TryGetValue(key, out int count) ? count+1 : 1;
                        }
                    }
                    Assert.That(edges.Values.All(count => count == 2), Is.True);
                    if (probability == 1)
                    {
                        Assert.That(mesh.bounds.size.y, Is.InRange(.027f, .0645f));
                        Assert.That(upperArea, Is.GreaterThan(.003f));
                        Assert.That(lowerArea, Is.EqualTo(upperArea).Within(.000001f));
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test] public void ThinStoneKeepsMeshMassAndOrientationAndSettlesWithoutPenetration()
        {
            StartSession(1, 123, .12f, .12f);
            var stone = session.Pending;
            var mesh = stone.OwnedMesh;
            Assert.That(stone.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(((MeshCollider)stone.Shape).sharedMesh));
            Assert.That(stone.Body.mass, Is.EqualTo(PebbleMesh.Volume(mesh) * 2400).Within(.00001f));
            Assert.That(Vector3.Dot(stone.transform.up, Vector3.up), Is.GreaterThan(.999f));
            var size = mesh.bounds.size;
            Drop(0); Step(200);
            Assert.That(stone.Body.IsSleeping(), Is.True);
            Assert.That(stone.Shape.bounds.min.y, Is.GreaterThan(-.002f));
            var settled = stone.transform.position;
            Step(100);
            Assert.That(Vector3.Distance(settled, stone.transform.position), Is.LessThan(.001f));
            Assert.That(mesh.bounds.size, Is.EqualTo(size));
            Assert.That(session.CurrentHeightMm, Is.GreaterThan(0));
            Assert.That(stone.Body.constraints, Is.EqualTo(RigidbodyConstraints.None));
            Assert.That(stone.Body.isKinematic, Is.False);
            Assert.That(session.Pending, Is.Not.Null);
        }

        [Test] public void ClearlyOverbalancedFlatStoneFallsAndSupplyContinues()
        {
            StartSession(1, 123);
            var stone = Drop(0, .54f);
            Step(250);
            Assert.That(stone == null || !stone.gameObject.activeSelf, Is.True);
            Assert.That(session.CurrentHeightMm, Is.Zero);
            Assert.That(session.Pending, Is.Not.Null);
        }

        [Test] public void HighOffCentreReleaseWakesAndDisturbsRestingFlatTower()
        {
            StartSession(1, 123);
            var first = Drop(0); Step(150);
            var second = Drop(first.HighestPoint()); Step(200);
            Assert.That(first.Body.IsSleeping() && second.Body.IsSleeping(), Is.True);
            var origin = second.transform.position;
            var rotation = second.transform.rotation;
            Drop(second.HighestPoint(), .07f, .8f);
            bool woke = false;
            float maxMotion = 0;
            for (int i = 0; i < 200; i++)
            {
                Step(1);
                if (second == null || !second.gameObject.activeSelf) { maxMotion = 1; break; }
                woke |= !second.Body.IsSleeping();
                maxMotion = Mathf.Max(maxMotion, Vector3.Distance(origin, second.transform.position), Quaternion.Angle(rotation, second.transform.rotation) * .001f);
            }
            Assert.That(woke, Is.True);
            Assert.That(maxMotion, Is.GreaterThan(.003f));
            Assert.That(first.Body.isKinematic, Is.False);
        }

        [Test] public void FixedSeedDistributionAndStackingComparison()
        {
            // This is an experiment report, not a random success-rate assertion.
            // Every candidate receives the same 12 sequences of five seeds and poses.
            var report = new List<string>();
            foreach (var candidate in new[] { (0f,.18f,.28f), (.5f,.18f,.28f), (.7f,.18f,.28f), (.85f,.18f,.28f), (.85f,.12f,.20f), (.85f,.24f,.34f) })
            {
                float probability = candidate.Item1;
                int flats = 0, retained = 0, complete = 0, slips = 0, totalDepth = 0;
                float thickness = 0, mass = 0, height = 0, maxDrift = 0;
                for (int trial = 0; trial < 12; trial++)
                {
                    StartSession(probability, 1000 + trial * 10, candidate.Item2, candidate.Item3);
                    var released = new List<(Stone stone, Vector3 position)>();
                    for (int index = 0; index < 5; index++)
                    {
                        var pending = session.Pending;
                        if (pending.OwnedMesh.name.StartsWith("Flat")) flats++;
                        thickness += pending.OwnedMesh.bounds.size.y;
                        mass += pending.Body.mass;
                        var stone = Drop(session.TowerTop(), index % 2 == 0 ? -.012f : .012f);
                        released.Add((stone, stone.transform.position));
                        Step(150);
                    }
                    Step(100);
                    int count = 0;
                    var depths = new Dictionary<Collider, int> { [pedestal] = 0 };
                    int stackDepth = 0;
                    foreach (var entry in released.Where(e => e.stone != null && e.stone.gameObject.activeSelf).OrderBy(e => e.stone.transform.position.y))
                    {
                        int depth = -1;
                        foreach (var support in entry.stone.Contacts)
                            if (depths.TryGetValue(support, out int below)) depth = Mathf.Max(depth, below+1);
                        if (depth < 0) continue;
                        depths[entry.stone.Shape] = depth;
                        stackDepth = Mathf.Max(stackDepth, depth);
                    }
                    foreach (var entry in released)
                    {
                        if (entry.stone == null || !entry.stone.gameObject.activeSelf) { slips++; continue; }
                        if (depths.ContainsKey(entry.stone.Shape)) count++;
                        var delta = entry.stone.transform.position - entry.position; delta.y = 0;
                        if (delta.magnitude > .02f) slips++;
                        maxDrift = Mathf.Max(maxDrift, delta.magnitude);
                    }
                    retained += count;
                    totalDepth += stackDepth;
                    if (stackDepth == 5) complete++;
                    height += session.CurrentHeightMm;
                }
                var line = $"FLAT_COMPARISON probability={probability:F2} thicknessRatio={candidate.Item2:F2}-{candidate.Item3:F2} seeds=1000+10*trial+[0..4] trials=12 stones=60 flats={flats} meanThicknessMm={thickness/60*1000:F2} meanMassKg={mass/60:F3} retained={retained} slippedOrRemoved={slips} fiveStoneStacks={complete} meanStackDepth={totalDepth/12f:F2} meanHeightMm={height/12:F1} maxSurvivorDriftMm={maxDrift*1000:F1}";
                Debug.Log(line);
                report.Add(line);
            }
            string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../.scratch"));
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllLines(System.IO.Path.Combine(directory, "issue22-comparison.txt"), report);
        }
    }
}
