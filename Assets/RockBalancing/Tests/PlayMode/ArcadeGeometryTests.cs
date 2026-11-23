using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RockBalancing.Tests
{
    public sealed class ArcadeGeometryTests
    {
        Scene scene;
        GameSession session;

        [UnitySetUp] public IEnumerator OpenArcade()
        {
            const string path = "Assets/RockBalancing/Scenes/Arcade.unity";
            yield return SceneManager.LoadSceneAsync(path,
                new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics3D));
            scene = SceneManager.GetSceneByPath(path);
            yield return null;
            session = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<GameSession>()).Single();
        }

        [UnityTearDown] public IEnumerator CloseArcade()
        {
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test] public void SeededArcadeSupplyMeetsTheGeometryRulesThroughDiscardAndReplacement()
        {
            var savedRandom = Random.state;
            var faceCounts = new HashSet<int>();
            var inclinedTopNormals = new HashSet<Vector2Int>();
            try
            {
                for (int seed = 5100; seed < 5132; seed++)
                {
                    Random.InitState(seed);
                    session.Restart();
                    var first = session.Pending;
                    CheckStone(first, $"Seed {seed}, restart");
                    Assert.That(session.Adjust(first, first.transform.position, Quaternion.Euler(37, 61, 17)), Is.True);
                    CheckStone(first, $"Seed {seed}, rotated");
                    Assert.That(session.Discard(first), Is.True, $"Seed {seed}");
                    AssertHasSupportFace(first, $"Seed {seed}, discarded");
                    Assert.That(session.Pending, Is.Not.SameAs(first));
                    session.Remove(first);

                    var second = session.Pending;
                    CheckStone(second, $"Seed {seed}, after discard");
                    Assert.That(session.Release(second), Is.True, $"Seed {seed}");
                    AssertHasSupportFace(second, $"Seed {seed}, released");
                    Assert.That(session.Pending, Is.Not.SameAs(second));
                    session.Remove(second);

                    var third = session.Pending;
                    CheckStone(third, $"Seed {seed}, after release");
                    Assert.That(session.Release(third), Is.True, $"Seed {seed}");
                    AssertHasSupportFace(third, $"Seed {seed}, replacement released");
                }
            }
            finally { Random.state = savedRandom; }

            Assert.That(faceCounts.Count, Is.GreaterThanOrEqualTo(3), "Supply varies the straight-edged silhouettes.");
            Assert.That(inclinedTopNormals.Count, Is.GreaterThanOrEqualTo(4), "Supply varies the visible top face orientations.");

            void CheckStone(Stone stone, string sample)
            {
                Assert.That(stone, Is.Not.Null, sample);
                var mesh = stone.Surface.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(((MeshCollider)stone.Shape).sharedMesh, Is.SameAs(mesh), sample);
                var faces = VisibleFaces(mesh);
                Assert.That(faces.Count, Is.InRange(5, 9), sample);
                faceCounts.Add(faces.Count);
                var dimensions = mesh.bounds.size;
                Assert.That(dimensions.z, Is.LessThanOrEqualTo(dimensions.x), sample);
                Assert.That(dimensions.y / dimensions.z, Is.InRange(.25f, .60f), sample);
                float area = faces.Sum(face => face.Sum(triangle => triangle.Area));
                for (int i = 0; i < faces.Count; i++)
                {
                    var face = faces[i];
                    Assert.That(face.Sum(triangle => triangle.Area) / area, Is.GreaterThanOrEqualTo(.03f), sample);
                    var normal = face[0].Normal;
                    if (normal.y > .70f && normal.y < .999f)
                        inclinedTopNormals.Add(Vector2Int.RoundToInt(new Vector2(normal.x, normal.z) * 10));
                    for (int j = i + 1; j < faces.Count; j++)
                    {
                        if (face.Any(a => faces[j].Any(a.SharesEdge)))
                            Assert.That(180f - Vector3.Angle(normal, faces[j][0].Normal), Is.InRange(25f, 155f), sample);
                    }
                }
            }
        }

        static void AssertHasSupportFace(Stone stone, string sample) =>
            Assert.That(VisibleFaces(stone.OwnedMesh).Any(face => ContainsProjection(face, stone.Body.centerOfMass)),
                Is.True, $"{sample}: at least one face supports the rigidbody's center of mass.");

        [UnityTest] public IEnumerator AnInclinedVisibleFaceCanBeTurnedDownAndRestOnThePedestal()
        {
            var savedRandom = Random.state;
            List<Triangle> support = null;
            Stone stone = null;
            try
            {
                for (int seed = 5100; seed < 5132 && support == null; seed++)
                {
                    Random.InitState(seed);
                    session.Restart();
                    stone = session.Pending;
                    support = VisibleFaces(stone.OwnedMesh).FirstOrDefault(face =>
                    {
                        var normal = face[0].Normal;
                        return normal.y > .75f && normal.y < .995f && ContainsProjection(face, stone.Body.centerOfMass);
                    });
                }
            }
            finally { Random.state = savedRandom; }
            Assert.That(support, Is.Not.Null, "The seeded supply contains a usable inclined face.");

            var orientation = Quaternion.FromToRotation(support[0].Normal, Vector3.down);
            float lowest = stone.OwnedMesh.vertices.Min(point => (orientation * point).y);
            var releasePoint = new Vector3(.13f, session.BaseTop - lowest + .012f, .12f);
            Assert.That(session.Adjust(stone, stone.transform.position, orientation), Is.True);
            Assert.That(session.Adjust(stone, releasePoint, orientation), Is.True);
            Assert.That(session.Release(stone), Is.True);
            Assert.That(ContainsProjection(support, stone.Body.centerOfMass), Is.True,
                "The actual rigidbody's center of mass projects inside the chosen visible face.");

            var physics = scene.GetPhysicsScene();
            Physics.SyncTransforms();
            for (int i = 0; i < 180; i++) physics.Simulate(.02f);
            session.RefreshState();
            Assert.That(stone.Contacts, Does.Contain(session.Base));
            Assert.That(stone.Body.IsSleeping(), Is.True);
            Assert.That(Quaternion.Angle(stone.Body.rotation, orientation), Is.LessThan(2f));
            foreach (var corner in support.SelectMany(triangle => triangle.Corners).Distinct())
                Assert.That((stone.Body.position + stone.Body.rotation * corner).y,
                    Is.EqualTo(session.BaseTop).Within(.002f));
            yield break;
        }

        static bool ContainsProjection(List<Triangle> face, Vector3 point)
        {
            foreach (var triangle in face)
            {
                var corners = triangle.Corners;
                if (Vector3.Dot(Vector3.Cross(corners[1] - corners[0], point - corners[0]), triangle.Normal) >= -1e-8f &&
                    Vector3.Dot(Vector3.Cross(corners[2] - corners[1], point - corners[1]), triangle.Normal) >= -1e-8f &&
                    Vector3.Dot(Vector3.Cross(corners[0] - corners[2], point - corners[2]), triangle.Normal) >= -1e-8f)
                    return true;
            }
            return false;
        }

        readonly struct Triangle
        {
            public readonly Vector3[] Corners;
            public readonly Vector3 Normal;
            public readonly float Area;

            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Corners = new[] { a, b, c };
                var cross = Vector3.Cross(b - a, c - a);
                Normal = cross.normalized;
                Area = cross.magnitude * .5f;
            }

            public bool SharesEdge(Triangle other) =>
                Corners.Count(corner => other.Corners.Contains(corner)) == 2;
        }

        static List<List<Triangle>> VisibleFaces(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            var remaining = new List<Triangle>();
            for (int i = 0; i < indices.Length; i += 3)
                remaining.Add(new Triangle(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]]));

            var faces = new List<List<Triangle>>();
            while (remaining.Count > 0)
            {
                var face = new List<Triangle> { remaining[0] };
                remaining.RemoveAt(0);
                for (int i = 0; i < face.Count; i++)
                {
                    var triangle = face[i];
                    for (int j = remaining.Count - 1; j >= 0; j--)
                    {
                        if (Vector3.Dot(triangle.Normal, remaining[j].Normal) < .99999f ||
                            !triangle.SharesEdge(remaining[j])) continue;
                        face.Add(remaining[j]);
                        remaining.RemoveAt(j);
                    }
                }
                faces.Add(face);
            }
            return faces;
        }
    }
}
