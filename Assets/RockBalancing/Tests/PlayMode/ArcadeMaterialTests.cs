using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RockBalancing.Tests
{
    public sealed class ArcadeMaterialTests
    {
        static readonly string[] Ordinary = { "Granite", "Shale", "Basalt" };
        static readonly string[] Special = { "Crystal", "Gemstone", "Gold" };

        Scene scene;
        GameBootstrap bootstrap;
        GameSession session;

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

        [UnityTest] public IEnumerator ArcadeSupplyAndReplacementBothOfferAllSixMaterialsAtTheExpectedFrequency()
        {
            const int trials = 320;
            var initial = new List<string>();
            var replacements = new List<string>();
            var previous = Random.state;
            try
            {
                Random.InitState(5200);
                for (int i = 0; i < trials; i++)
                {
                    session.Restart();
                    initial.Add(FinishOf(session.Pending));
                    Assert.That(session.Discard(session.Pending), Is.True);
                    replacements.Add(FinishOf(session.Pending));
                    if ((i + 1) % 16 == 0) yield return null;
                }
            }
            finally { Random.state = previous; }

            foreach (var (stage, supply) in new[] { ("Launch", initial), ("Replacement", replacements) })
            {
                Assert.That(supply.Distinct(), Is.EquivalentTo(Ordinary.Concat(Special)));
                float ordinaryShare = supply.Count(Ordinary.Contains) / (float)trials;
                var counts = supply.GroupBy(finish => finish).Select(group => $"{group.Key} {group.Count()}");
                TestContext.WriteLine($"{stage} ordinary {ordinaryShare:P1} ({string.Join(", ", counts)})");
                Assert.That(ordinaryShare, Is.InRange(.60f, .80f), stage);
            }
        }

        [Test] public void FixedGeometryAndReleasePoseGiveEveryMaterialTheSameStackingResult()
        {
            var factory = new StoneFactory(bootstrap.StoneMaterial, new StoneShapeSettings { FlatProbability = 1 },
                nextSeed: () => 5202, arcade: true);
            var tested = new HashSet<string>();
            var physics = scene.GetPhysicsScene();
            var previous = Random.state;
            Vector3[] geometry = null;
            float expectedMass = 0;
            Vector3 expectedPosition = default;
            Vector3 expectedTop = default;
            Quaternion expectedRotation = default;
            try
            {
                Random.InitState(5202);
                session.Initialize(factory, session.Base, session.Ground);
                for (int trial = 0; trial < 100 && tested.Count < 6; trial++, session.Restart())
                {
                    var stone = session.Pending;
                    if (!tested.Add(FinishOf(stone))) continue;
                    var mesh = stone.GetComponent<MeshFilter>().sharedMesh;
                    var finish = FinishOf(stone);
                    var contact = stone.Shape.sharedMaterial;
                    Assert.That(contact.staticFriction, Is.EqualTo(.8f), finish);
                    Assert.That(contact.dynamicFriction, Is.EqualTo(.65f), finish);
                    Assert.That(contact.bounciness, Is.EqualTo(.015f), finish);
                    Assert.That(contact.frictionCombine, Is.EqualTo(PhysicsMaterialCombine.Maximum), finish);
                    Assert.That(contact.bounceCombine, Is.EqualTo(PhysicsMaterialCombine.Minimum), finish);

                    var rotation = Quaternion.Euler(4, 26, -3);
                    var target = new Vector3(.06f, session.BaseTop + mesh.bounds.extents.y + .09f, -.03f);
                    Assert.That(session.Adjust(stone, target, rotation), Is.True, finish);
                    Assert.That(session.Release(stone), Is.True, finish);
                    Physics.SyncTransforms();
                    for (int i = 0; i < 150; i++) physics.Simulate(.02f);
                    var upper = session.Pending;
                    target.y = session.BaseTop + mesh.bounds.size.y + mesh.bounds.extents.y + .08f;
                    Assert.That(session.Adjust(upper, target, rotation), Is.True, finish);
                    Assert.That(session.Release(upper), Is.True, finish);
                    Physics.SyncTransforms();
                    for (int i = 0; i < 150; i++) physics.Simulate(.02f);
                    session.RefreshState();
                    Assert.That(upper.Contacts, Does.Contain(stone.Shape), finish);
                    Assert.That(upper.Body.IsSleeping(), Is.True, finish);

                    Assert.That(stone.Contacts, Does.Contain(session.Base), finish);
                    Assert.That(stone.Body.IsSleeping(), Is.True, finish);
                    if (geometry == null)
                    {
                        geometry = mesh.vertices;
                        expectedMass = stone.Body.mass;
                        expectedPosition = stone.Body.position;
                        expectedTop = upper.Body.position;
                        expectedRotation = stone.Body.rotation;
                    }
                    Assert.That(mesh.vertices, Is.EqualTo(geometry), finish);
                    Assert.That(stone.Body.mass, Is.EqualTo(expectedMass), finish);
                    Assert.That(Vector3.Distance(expectedPosition, stone.Body.position), Is.LessThan(.005f), finish);
                    Assert.That(Vector3.Distance(expectedTop, upper.Body.position), Is.LessThan(.005f), finish);
                    Assert.That(Quaternion.Angle(expectedRotation, stone.Body.rotation), Is.LessThan(1f), finish);
                }
                Assert.That(tested, Is.EquivalentTo(Ordinary.Concat(Special)));
            }
            finally { Random.state = previous; }
        }

        [Test] public void StoneFinishesKeepVisibleDetailAndContrastAgainstThePedestal()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("This comparison needs a graphics device.");

            var factory = new StoneFactory(bootstrap.StoneMaterial, new StoneShapeSettings { FlatProbability = 1 },
                nextSeed: () => 5202, arcade: true);
            var observed = new Dictionary<string, int>();
            var tested = new HashSet<string>();
            var pedestalColor = AverageColor(ReadTop(session.Base.GetComponent<Renderer>(), -90));
            var previous = Random.state;
            try
            {
                Random.InitState(5202);
                session.Initialize(factory, session.Base, session.Ground);
                for (int trial = 0; trial < 100 && tested.Count < 6; trial++, session.Restart())
                {
                    var stone = session.Pending;
                    var finish = FinishOf(stone);
                    if (!tested.Add(finish)) continue;
                    var surface = stone.GetComponent<Renderer>();
                    Vector3 shaded = AverageColor(ReadTop(surface, -90));
                    Assert.That(Vector3.Distance(shaded, pedestalColor), Is.GreaterThan(25), finish);

                    if (!Ordinary.Contains(finish)) continue;
                    var tones = ReadTop(surface, 48).Select(pixel => (pixel.r + pixel.g + pixel.b) / 3).ToArray();
                    observed.Add(finish, tones.Max() - tones.Min());
                }
            }
            finally { Random.state = previous; }

            Assert.That(tested, Is.EquivalentTo(Ordinary.Concat(Special)));
            Assert.That(observed["Granite"], Is.GreaterThanOrEqualTo(14));
            Assert.That(observed["Shale"], Is.GreaterThanOrEqualTo(14));
            Assert.That(observed["Basalt"], Is.LessThan(10));
        }

        static Vector3 AverageColor(Color32[] pixels)
        {
            var total = Vector3.zero;
            foreach (var pixel in pixels) total += new Vector3(pixel.r, pixel.g, pixel.b);
            return total / pixels.Length;
        }

        Color32[] ReadTop(Renderer surface, float elevation)
        {
            var camera = bootstrap.GetComponentInChildren<Camera>();
            var sun = bootstrap.GetComponentInChildren<Light>();
            // Read only a small patch within the broad top face, away from edges and the claw.
            int layer = surface.gameObject.layer;
            surface.gameObject.layer = 30;
            camera.cullingMask = 1 << surface.gameObject.layer;
            camera.transform.SetPositionAndRotation(surface.bounds.center + Vector3.up * (surface.bounds.extents.y + .3f),
                Quaternion.LookRotation(Vector3.down, Vector3.forward));
            camera.orthographic = true;
            camera.orthographicSize = .022f;
            camera.aspect = 1;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(elevation, -35, 0);
            var target = RenderTexture.GetTemporary(64, 64, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                return pixels.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null;
                surface.gameObject.layer = layer;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
            }
        }

        static string FinishOf(Stone stone) => stone.GetComponent<Renderer>().sharedMaterial.name;
    }
}
