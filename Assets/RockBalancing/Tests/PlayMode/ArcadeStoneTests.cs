using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RockBalancing.Tests
{
    public sealed class ArcadeStoneTests
    {
        Scene scene;
        GameSession session;
        GameBootstrap bootstrap;

        [UnitySetUp] public IEnumerator OpenArcade()
        {
            const string path = "Assets/RockBalancing/Scenes/Arcade.unity";
            yield return SceneManager.LoadSceneAsync(path,
                new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics3D));
            scene = SceneManager.GetSceneByPath(path);
            yield return null;
            bootstrap = scene.GetRootGameObjects().Single(go => go.GetComponent<GameBootstrap>()).GetComponent<GameBootstrap>();
            session = bootstrap.GetComponentInChildren<GameSession>();
        }

        [UnityTearDown] public IEnumerator CloseArcade()
        {
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test] public void BroadPedestalTopAndBevelsMeetTheirVisibleContactPlanes()
        {
            var shape = session.Base;
            var mesh = shape.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices.Select(v => shape.transform.TransformPoint(v)).ToArray();
            var triangles = mesh.triangles;
            float topArea = 0;
            bool hasBevel = false;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var cross = Vector3.Cross(b - a, c - a);
                var normal = cross.normalized;
                var middle = (a + b + c) / 3;
                if (normal.y > .999f)
                {
                    topArea += cross.magnitude * .5f;
                    Assert.That(middle.y, Is.EqualTo(.08f).Within(.001f));
                }
                hasBevel |= normal.y > .1f && normal.y < .99f;
                var ray = new Ray(middle + normal * .1f, -normal);
                Assert.That(shape.Raycast(ray, out var hit, .2f), Is.True);
                Assert.That(Vector3.Distance(hit.point, middle), Is.LessThan(.001f));
                Assert.That(Vector3.Dot(hit.normal, normal), Is.GreaterThan(.995f));
            }
            Assert.That(hasBevel, Is.True, "The edges of the pedestal should have distinct planar bevels.");
            Assert.That(topArea, Is.GreaterThan(.7f), "Keep the broad playable top of the original metre-wide base.");
            Assert.That(shape.bounds.size.x, Is.EqualTo(1).Within(.002f));
            Assert.That(shape.bounds.size.z, Is.EqualTo(1).Within(.002f));
        }

        [Test] public void PendingStoneKeepsAFlatToneWithinTheSameLightingBand()
        {
            var stone = session.Pending.GetComponent<Renderer>();
            Color32 first = ReadTop(stone, 48);
            Color32 second = ReadTop(stone, 65);
            Assert.That(first.g, Is.InRange(35, 235), "Read a visible stone surface.");
            Assert.That(Mathf.Abs(first.g - second.g), Is.LessThanOrEqualTo(2),
                $"The broad, lit support face changes tone within one band ({first} to {second}).");
        }

        [Test] public void StoneRemainsDistinctAgainstThePedestalInShade()
        {
            var stone = ReadTop(session.Pending.GetComponent<Renderer>(), -90);
            var pedestal = ReadTop(session.Base.GetComponent<Renderer>(), -90);
            Assert.That((int)stone.g - pedestal.g, Is.GreaterThanOrEqualTo(20),
                $"A stone in shade must still stand out against its support ({stone} on {pedestal}).");
        }

        Color32 ReadTop(Renderer surface, float elevation)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("This comparison needs a graphics device.");

            var camera = bootstrap.GetComponentInChildren<Camera>();
            var sun = bootstrap.GetComponentInChildren<Light>();
            // Sample the actual game surface without the claw, selection guide or nearby stones.
            int layer = surface.gameObject.layer;
            surface.gameObject.layer = 30;
            surface.transform.rotation = Quaternion.identity;
            camera.cullingMask = 1 << surface.gameObject.layer;
            camera.transform.SetPositionAndRotation(surface.bounds.center + Vector3.up * (surface.bounds.extents.y + .3f),
                Quaternion.LookRotation(Vector3.down, Vector3.forward));
            camera.orthographic = true;
            camera.orthographicSize = .05f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(elevation, -35, 0);
            var target = RenderTexture.GetTemporary(64, 64, 24, RenderTextureFormat.ARGB32);
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixel.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
                return pixel.GetPixel(0, 0);
            }
            finally
            {
                surface.gameObject.layer = layer;
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixel);
            }
        }

        [Test] public void ElongatedStonesHaveADistinctlyLongStraightEdgedOutline()
        {
            bootstrap.StoneShapes.FlatProbability = 0;
            var randomState = Random.state;
            try
            {
                Random.InitState(4700);
                for (int sample = 0; sample < 6; sample++)
                {
                    session.Restart();
                    var mesh = session.Pending.GetComponent<MeshFilter>().sharedMesh;
                    var footprint = mesh.vertices.Select(v => new Vector2(v.x, v.z)).Distinct().ToArray();
                    var diameter = (from a in footprint from b in footprint select a - b)
                        .OrderByDescending(delta => delta.sqrMagnitude).First();
                    var across = Vector2.Perpendicular(diameter.normalized);
                    var widths = footprint.Select(p => Vector2.Dot(p, across));
                    Assert.That(widths.Max() - widths.Min(), Is.LessThan(diameter.magnitude * .60f),
                        "Long stones must be distinguishable from the broad, flat supply.");
                }
            }
            finally { Random.state = randomState; }
        }

        [UnityTest] public IEnumerator VisibleBroadFaceCanBeTurnedDownAndSupportTheReleasedStone()
        {
            bootstrap.StoneShapes.FlatProbability = 1;
            session.Restart();
            var stone = session.Pending;
            var mesh = stone.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;
            float upperArea = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                var face = cross.normalized;
                foreach (int corner in new[] { a, b, c })
                    Assert.That(Vector3.Dot(normals[corner], face), Is.GreaterThan(.999f),
                        "Each visible face must stay flat all the way to its edges.");
                if (face.y > .999f) upperArea += cross.magnitude * .5f;
            }
            Assert.That(upperArea, Is.GreaterThan(.002f), "The visible top offers a broad support face.");

            var upsideDown = Quaternion.AngleAxis(180, Vector3.right);
            var target = new Vector3(.15f, session.BaseTop + mesh.bounds.max.y + .015f, .1f);
            Assert.That(session.Adjust(stone, stone.transform.position, upsideDown), Is.True);
            Assert.That(session.Adjust(stone, target, upsideDown), Is.True);
            Assert.That(session.Release(stone), Is.True);
            var physics = scene.GetPhysicsScene();
            Physics.SyncTransforms();
            for (int i = 0; i < 180; i++) physics.Simulate(.02f);
            session.RefreshState();
            Assert.That(stone.Contacts, Does.Contain(session.Base));
            Assert.That(stone.Body.IsSleeping(), Is.True);
            Assert.That(Quaternion.Angle(stone.Body.rotation, upsideDown), Is.LessThan(2f));
            Assert.That(Vector2.Distance(new Vector2(target.x, target.z),
                new Vector2(stone.Body.position.x, stone.Body.position.z)), Is.LessThan(.005f));
            var supportHeights = vertices.Where(v => Mathf.Abs(v.y - mesh.bounds.max.y) < .00001f)
                .Select(v => (stone.Body.position + stone.Body.rotation * v).y);
            foreach (float height in supportHeights)
                Assert.That(height, Is.EqualTo(session.BaseTop).Within(.002f));
            yield break;
        }
    }
}
