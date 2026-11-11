using System.Threading.Tasks;
using UnityEngine;

namespace RockBalancing
{
    public interface IStoneFactory
    {
        Task<Stone> CreateAsync(Vector3 position, Transform parent);
    }

    public sealed class StoneFactory : IStoneFactory
    {
        readonly Material template;
        readonly StoneShapeSettings shapes;
        readonly System.Func<int> nextSeed;
        public StoneFactory(Material template=null, StoneShapeSettings shapes=null, System.Func<int> nextSeed=null)
        {
            this.template=template;
            this.shapes=shapes ?? new StoneShapeSettings();
            this.nextSeed=nextSeed ?? (() => Random.Range(int.MinValue, int.MaxValue));
        }
        public Task<Stone> CreateAsync(Vector3 position, Transform parent)
        {
            var go = new GameObject("Stone", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.name = "Stone";
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var mesh = PebbleMesh.Create(nextSeed(), shapes);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var shape = go.GetComponent<MeshCollider>();
            shape.sharedMesh = mesh;
            shape.convex = true;
            shape.isTrigger = true;
            var physicsMaterial = new PhysicsMaterial("Stone friction") {
                dynamicFriction = .65f, staticFriction = .8f, bounciness = .015f,
                frictionCombine = PhysicsMaterialCombine.Maximum, bounceCombine = PhysicsMaterialCombine.Minimum
            };
            shape.sharedMaterial = physicsMaterial;
            var material = MakeMaterial(new Color(.38f, .46f, .45f),template);
            go.GetComponent<Renderer>().sharedMaterial = material;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = Mathf.Max(.05f, PebbleMesh.Volume(mesh) * 2400f);
            body.linearDamping = .08f;
            body.angularDamping = .3f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var stone = go.AddComponent<Stone>();
            stone.OwnedMaterial = material;
            stone.OwnedMesh = mesh;
            stone.OwnedPhysicsMaterial = physicsMaterial;
            return Task.FromResult(stone);
        }

        public static Material MakeMaterial(Color color, Material template=null)
        {
            if(template!=null) return new Material(template) { color=color };
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .15f);
            return material;
        }
    }
}
