using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RockBalancing.Tests
{
    public class SessionTests
    {
        GameObject root;
        GameSession session;
        Collider pedestal, ground;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Session test");
            session = root.AddComponent<GameSession>();
            pedestal = Cube("Base", new Vector3(0,-.08f,0), new Vector3(1,.16f,1));
            ground = Cube("Ground", new Vector3(0,-.3f,0), new Vector3(100,.1f,100));
        }
        Collider Cube(string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(root.transform);
            go.transform.position = position; go.transform.localScale = scale;
            return go.GetComponent<Collider>();
        }
        [UnityTearDown] public IEnumerator TearDown() { Object.Destroy(root); yield return null; }
        [UnityTest] public IEnumerator ReleaseTransfersPhysicsAndSuppliesOnlyOneSuccessor()
        {
            session.Initialize(new StoneFactory(), pedestal, ground);
            var first = session.Pending;
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Body.isKinematic, Is.True);
            Assert.That(session.Release(first), Is.True);
            Assert.That(first.Body.isKinematic, Is.False);
            var next = session.Pending;
            Assert.That(next, Is.Not.SameAs(first));
            Assert.That(session.Release(first), Is.False);
            _ = session.RequestNextAsync();
            Assert.That(session.Pending, Is.SameAs(next));
            Assert.That(session.Stones.Count, Is.EqualTo(2));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(first.Body.linearVelocity.y, Is.LessThan(0));
        }
        [UnityTest] public IEnumerator RestartClearsReleasedStonesAndResupplies()
        {
            session.Initialize(new StoneFactory(), pedestal, ground);
            var first = session.Pending;
            session.Release(first);
            session.Restart();
            Assert.That(session.Stones.Count, Is.EqualTo(1));
            Assert.That(session.Pending, Is.Not.SameAs(first));
            yield return null;
            Assert.That(first == null, Is.True);
        }
        sealed class DeferredFactory : IStoneFactory
        {
            public TaskCompletionSource<Stone> Completion = new TaskCompletionSource<Stone>();
            public Task<Stone> CreateAsync(Vector3 position, Transform parent) => Completion.Task;
        }
        [UnityTest] public IEnumerator FailedSupplyHasNoFakePendingAndCanRetry()
        {
            var factory = new DeferredFactory();
            session.Initialize(factory, pedestal, ground);
            Assert.That(session.Waiting, Is.True);
            Assert.That(session.Release(null), Is.False);
            factory.Completion.SetException(new System.Exception("Test failure"));
            yield return null;
            Assert.That(session.Stones, Is.Empty);
            factory.Completion = new TaskCompletionSource<Stone>();
            var request = session.RequestNextAsync();
            var stone = new StoneFactory().CreateAsync(Vector3.up, root.transform).Result;
            factory.Completion.SetResult(stone);
            yield return null;
            Assert.That(session.Pending, Is.SameAs(stone));
        }
        [UnityTest] public IEnumerator OldGenerationCannotEnterRestartedSession()
        {
            var factory = new DeferredFactory();
            session.Initialize(factory, pedestal, ground);
            var oldRequest = factory.Completion;
            factory.Completion = new TaskCompletionSource<Stone>();
            session.Restart();
            var oldStone = new StoneFactory().CreateAsync(Vector3.up, root.transform).Result;
            oldRequest.SetResult(oldStone);
            yield return null;
            Assert.That(session.Pending, Is.Null);
            var fresh = new StoneFactory().CreateAsync(Vector3.up, root.transform).Result;
            factory.Completion.SetResult(fresh);
            yield return null;
            Assert.That(session.Pending, Is.SameAs(fresh));
            Assert.That(session.Stones.Count, Is.EqualTo(1));
        }
    }
}
