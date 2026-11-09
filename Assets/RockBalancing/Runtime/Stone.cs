using System.Collections.Generic;
using UnityEngine;

namespace RockBalancing
{
    public sealed class Stone : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public Collider Shape { get; private set; }
        public Renderer Surface { get; private set; }
        public bool Released { get; internal set; }
        public bool Discarded { get; internal set; }
        public GameSession Session { get; internal set; }
        public readonly HashSet<Collider> Contacts = new HashSet<Collider>();
        public Mesh OwnedMesh { get; set; }
        public Material OwnedMaterial { get; set; }
        public PhysicsMaterial OwnedPhysicsMaterial { get; set; }
        Vector3[] vertices;
        public float HighestPoint()
        {
            if (OwnedMesh == null) return Shape.bounds.max.y;
            if (vertices == null) vertices = OwnedMesh.vertices;
            float max = float.MinValue;
            foreach (var vertex in vertices) max = Mathf.Max(max, transform.TransformPoint(vertex).y);
            return max;
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Shape = GetComponent<Collider>();
            Surface = GetComponent<Renderer>();
        }

        void OnCollisionEnter(Collision collision) { Contact(collision); }
        void OnCollisionStay(Collision collision) { Contact(collision); }
        void OnCollisionExit(Collision collision) { Contacts.Remove(collision.collider); }
        void Contact(Collision collision)
        {
            if (!Released || Session == null) return;
            if (collision.collider == Session.Ground) { Session.Remove(this); return; }
            Contacts.Add(collision.collider);
        }
        void OnDestroy()
        {
            if (OwnedMesh != null) Destroy(OwnedMesh);
            if (OwnedMaterial != null) Destroy(OwnedMaterial);
            if (OwnedPhysicsMaterial != null) Destroy(OwnedPhysicsMaterial);
        }
    }
}
