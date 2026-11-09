using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RockBalancing.Tests
{
    public sealed class PhysicsRulesTests
    {
        GameObject root;
        GameSession session;
        Collider pedestal,ground;
        double now;
        sealed class CubeFactory : IStoneFactory
        {
            public Task<Stone> CreateAsync(Vector3 position,Transform parent)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=Vector3.one*.1f;
                go.GetComponent<Collider>().isTrigger=true;
                var body=go.AddComponent<Rigidbody>();body.isKinematic=true;
                var stone=go.AddComponent<Stone>();
                stone.OwnedMesh=Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
                go.GetComponent<MeshFilter>().sharedMesh=stone.OwnedMesh;
                return Task.FromResult(stone);
            }
        }
        [SetUp] public void Setup()
        {
            root=new GameObject("Physics rules");session=root.AddComponent<GameSession>();
            pedestal=Cube("Base",new Vector3(0,-.08f,0),new Vector3(1,.16f,1));
            ground=Cube("Ground",new Vector3(0,-.3f,0),new Vector3(100,.1f,100));
            now=0;session.Clock=()=>now;
            session.Initialize(new CubeFactory(),pedestal,ground);
        }
        Collider Cube(string name,Vector3 p,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform);
            go.transform.position=p;go.transform.localScale=scale;return go.GetComponent<Collider>();
        }
        [UnityTearDown] public IEnumerator TearDown(){Object.Destroy(root);yield return null;}
        IEnumerator Settle(int frames=70){for(int i=0;i<frames;i++)yield return new WaitForFixedUpdate();session.RefreshState();}
        Stone Drop(Vector3 position)
        {
            var stone=session.Pending;stone.transform.position=position;Physics.SyncTransforms();
            Assert.That(session.Release(stone),Is.True);return stone;
        }
        [UnityTest] public IEnumerator SweepStopsAtStoneAndAtBaseWithoutPushing()
        {
            var obstacle=Drop(new Vector3(0,.052f,0));yield return Settle();
            var preview=session.Pending;var resting=obstacle.transform.position;
            Assert.That(session.Adjust(preview,new Vector3(0,-1,0),Quaternion.identity),Is.False);
            Assert.That(preview.transform.position.y,Is.GreaterThanOrEqualTo(obstacle.Shape.bounds.max.y+.049f));
            Assert.That(obstacle.transform.position,Is.EqualTo(resting));
            Assert.That(preview.Body.isKinematic&&preview.Shape.isTrigger,Is.True);
            Assert.That(session.Adjust(preview,new Vector3(0,1,0),Quaternion.Euler(17,50,8)),Is.True);
        }
        [UnityTest] public IEnumerator FinalReleaseRechecksMovedObstacleAndAllowsEscape()
        {
            var preview=session.Pending;
            var obstacle=Cube("Moving obstacle",preview.transform.position+Vector3.up*.08f,Vector3.one*.1f);
            Physics.SyncTransforms();
            Assert.That(session.Release(preview),Is.False);
            Assert.That(preview.Body.isKinematic,Is.True);
            Assert.That(session.Adjust(preview,preview.transform.position-Vector3.up*.08f,preview.transform.rotation),Is.True);
            Assert.That(session.Release(preview),Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator TranslationAndRotationStopBeforeContact()
        {
            var obstacle=Drop(new Vector3(0,.052f,0));yield return Settle();
            var preview=session.Pending;preview.transform.position=new Vector3(-.3f,.052f,0);Physics.SyncTransforms();
            Assert.That(session.Adjust(preview,new Vector3(.3f,.052f,0),Quaternion.identity),Is.False);
            Assert.That(preview.transform.position.x,Is.LessThan(-.099f));
            preview.transform.position=new Vector3(-.3f,.055f,0);preview.transform.localScale=new Vector3(.3f,.1f,.1f);Physics.SyncTransforms();
            Assert.That(session.Adjust(preview,preview.transform.position,Quaternion.Euler(0,0,90)),Is.False);
            Assert.That(Quaternion.Angle(preview.transform.rotation,Quaternion.identity),Is.LessThan(90));
            Assert.That(session.Release(preview),Is.True,"The last accepted rotation must remain valid");
        }
        [UnityTest] public IEnumerator HeightUsesConnectedMeshAndExcludesFlyingPendingAndDiscard()
        {
            Assert.That(session.CurrentHeightMm,Is.Zero);
            var first=Drop(new Vector3(0,.15f,0));session.RefreshState();
            Assert.That(session.CurrentHeightMm,Is.Zero);
            yield return Settle();
            float top=first.OwnedMesh.vertices.Max(v=>first.transform.TransformPoint(v).y);
            Assert.That(session.CurrentHeightMm,Is.EqualTo(Mathf.FloorToInt(top*1000)));
            Assert.That(session.CurrentHeightMm,Is.InRange(95,102));
            var discarded=session.Pending;Assert.That(session.Discard(discarded),Is.True);
            discarded.Body.position=new Vector3(0,.155f,0);discarded.Body.linearVelocity=Vector3.zero;
            yield return Settle();
            Assert.That(session.CurrentHeightMm,Is.InRange(95,102),"Discarded stone cannot increase height");
            var third=Drop(new Vector3(0,.26f,0));yield return Settle();
            Assert.That(session.CurrentHeightMm,Is.InRange(290,305),"Ordinary stone remains connected through a discarded support");
            first.Body.position=new Vector3(3,2,0);
            yield return Settle(3);
            Assert.That(session.CurrentHeightMm,Is.Zero,"Disconnected airborne chain has no measured height");
        }
        [UnityTest] public IEnumerator HeightFloorsMillimetresWithoutStabilityWait()
        {
            var stone=session.Pending;
            stone.transform.localScale=new Vector3(.1f,.8238f,.1f);
            stone.transform.position=new Vector3(0,.4119f,0);
            Physics.SyncTransforms();Assert.That(session.Release(stone),Is.True);
            stone.Body.constraints=RigidbodyConstraints.FreezeAll;
            yield return Settle(3);
            Assert.That(session.CurrentHeightMm,Is.EqualTo(823));
            Assert.That(session.Stabilizing,Is.True);
        }
        [UnityTest] public IEnumerator GroundRemovalContinuesSessionAndDoesNotRestart()
        {
            int epoch=session.Epoch;
            var fallen=Drop(new Vector3(2,.2f,0));
            yield return Settle();
            Assert.That(fallen==null,Is.True);
            Assert.That(session.Pending,Is.Not.Null);
            Assert.That(session.Stones.Count,Is.EqualTo(1));
            Assert.That(session.Epoch,Is.EqualTo(epoch));
        }
        [UnityTest] public IEnumerator FixedPromptExpiresDuringMotionAndEmptyScene()
        {
            var first=Drop(new Vector3(0,.052f,0));yield return Settle();
            Assert.That(session.Stabilizing,Is.True);Assert.That(session.BeginShare(),Is.False);
            now=.999;session.RefreshState();Assert.That(session.Stabilizing,Is.True);
            now=1;session.RefreshState();Assert.That(session.Stabilizing,Is.False);
            Assert.That(session.BeginShare(),Is.True);Assert.That(session.BeginShare(),Is.False);session.EndShare(session.Epoch);
            // Let a stopped physical body enter a new movement episode.
            now=1.2;session.RefreshState();now=1.4;session.RefreshState();
            first.Body.linearVelocity=Vector3.right*.03f;
            session.RefreshState();Assert.That(session.Stabilizing,Is.True);
            now=2.39;session.RefreshState();Assert.That(session.Stabilizing,Is.True);
            now=2.4;session.RefreshState();Assert.That(session.Stabilizing,Is.False,"Continuing motion cannot extend the timer");
            var next=Drop(new Vector3(.3f,.052f,0));yield return Settle(10);
            Assert.That(session.Stabilizing,Is.True,"First support join starts a new second");
            session.Remove(first);session.Remove(next);session.RefreshState();
            Assert.That(session.CurrentHeightMm,Is.Zero);Assert.That(session.Stabilizing,Is.True);
            now=3.4;session.RefreshState();Assert.That(session.Stabilizing,Is.False);
            session.Restart();Assert.That(session.CurrentHeightMm,Is.Zero);Assert.That(session.CanShare,Is.True);
        }
        [Test] public void RandomMeshesAreClosedConvexAndWithinSizeBudget()
        {
            for(int seed=0;seed<20;seed++)
            {
                var mesh=PebbleMesh.Create(seed);var v=mesh.vertices;var t=mesh.triangles;
                float diameter=0;
                foreach(var a in v)foreach(var b in v)diameter=Mathf.Max(diameter,Vector3.Distance(a,b));
                Assert.That(diameter,Is.LessThan(.3f));
                Assert.That(PebbleMesh.Volume(mesh),Is.GreaterThan(.0001f));
                Assert.That(t.Length/3,Is.LessThanOrEqualTo(255));
                for(int i=0;i<t.Length;i+=3)
                {
                    var normal=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).normalized;
                    foreach(var p in v)Assert.That(Vector3.Dot(normal,p-v[t[i]]),Is.LessThan(.00001f));
                }
                var edges=new System.Collections.Generic.Dictionary<(int,int),int>();
                for(int i=0;i<t.Length;i+=3)for(int j=0;j<3;j++)
                {int a=t[i+j],b=t[i+(j+1)%3];var key=(Mathf.Min(a,b),Mathf.Max(a,b));edges[key]=edges.TryGetValue(key,out int count)?count+1:1;}
                Assert.That(edges.Values.All(count=>count==2),Is.True,"Every hull edge must have exactly two faces");
                Object.DestroyImmediate(mesh);
            }
        }
        [UnityTest] public IEnumerator FiftyReleasedStonesDoNotStopSupply()
        {
            for(int i=0;i<50;i++)Drop(new Vector3(2,i*.2f+1,0));
            Assert.That(session.Stones.Count,Is.EqualTo(51));
            Assert.That(session.Pending,Is.Not.Null);
            yield return new WaitForFixedUpdate();
            Assert.That(session.Pending.Body.isKinematic,Is.True);
        }
    }
}
