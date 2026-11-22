using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Herlilan.Hilltop
{
    // Deterministic authoring geometry. Playable stones still use the shared PebbleMesh.
    public static class HilltopGeometry
    {
        public static float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x*x + z*z);
            float falloff = Mathf.Max(0, r - .55f);
            return -.08f - .17f*falloff*falloff + .045f*Mathf.Sin(x*1.9f)*Mathf.Sin(z*1.6f)*Mathf.SmoothStep(0,1,falloff);
        }

        public static Mesh Hill(float radius=6, int rings=34, int sides=96)
        {
            var vertices=new List<Vector3>(); var indices=new List<int>();
            vertices.Add(new Vector3(0,Height(0,0),0));
            for(int ring=1;ring<=rings;ring++)
            for(int side=0;side<sides;side++)
            {
                float a=side*Mathf.PI*2/sides, r=radius*ring/rings;
                float x=Mathf.Cos(a)*r, z=Mathf.Sin(a)*r;
                vertices.Add(new Vector3(x,Height(x,z),z));
            }
            for(int side=0;side<sides;side++)
            { indices.Add(0);indices.Add(1+(side+1)%sides);indices.Add(1+side); }
            for(int ring=0;ring<rings-1;ring++)
            for(int side=0;side<sides;side++)
            {
                int a=1+ring*sides+side,b=1+ring*sides+(side+1)%sides,c=a+sides,d=b+sides;
                indices.Add(a);indices.Add(b);indices.Add(c);
                indices.Add(b);indices.Add(d);indices.Add(c);
            }
            return Finish("Hilltop soft mound",vertices,indices);
        }

        public static Mesh Platform()
        {
            const int sides=32;
            var v=new List<Vector3>(); var t=new List<int>();
            float[] radii={.27f,.35f,.33f,.28f};
            float[] heights={-.085f,-.04f,.035f,.08f};
            for(int ring=0;ring<4;ring++)
            for(int n=0;n<sides;n++)
            {
                float a=n*Mathf.PI*2/sides;
                float r=radii[ring]*(1+.045f*Mathf.Sin(3*a+.4f)+.025f*Mathf.Sin(7*a));
                v.Add(new Vector3(Mathf.Cos(a)*r,heights[ring],Mathf.Sin(a)*r*.93f));
            }
            for(int ring=0;ring<3;ring++)
            for(int n=0;n<sides;n++)
            {
                int a=ring*sides+n,b=ring*sides+(n+1)%sides;
                t.Add(a);t.Add(a+sides);t.Add(b);t.Add(b);t.Add(a+sides);t.Add(b+sides);
            }
            for(int n=1;n<sides-1;n++)
            { t.Add(0);t.Add(n);t.Add(n+1);t.Add(3*sides);t.Add(3*sides+n+1);t.Add(3*sides+n); }
            return Finish("Hilltop weathered stone platform",v,t);
        }

        public static Mesh Grass(int patch, int blades=2200)
        {
            var rng=new System.Random(731+patch*97);
            float Range(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
            var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();var colors=new List<Color>();var normals=new List<Vector3>();
            for(int n=0;n<blades;n++)
            {
                float angle=Range(0,Mathf.PI*2);
                float radius=Mathf.Sqrt(Range(.13f,22));
                float x=Mathf.Cos(angle)*radius,z=Mathf.Sin(angle)*radius;
                float tuft=Range(0,Mathf.PI*2),height=Range(.045f,.12f)*Mathf.Lerp(.45f,1,Mathf.InverseLerp(.35f,1.1f,radius));
                float width=Range(.004f,.010f);
                var root=new Vector3(x,Height(x,z)-.006f,z);
                var right=new Vector3(Mathf.Cos(tuft),0,Mathf.Sin(tuft))*width;
                var bend=new Vector3(Range(-.045f,.045f),0,Range(-.045f,.045f));
                int start=v.Count;
                v.Add(root-right);v.Add(root+right);
                v.Add(root+Vector3.up*height*.62f+ bend*.4f-right*.55f);
                v.Add(root+Vector3.up*height*.62f+ bend*.4f+right*.55f);
                v.Add(root+Vector3.up*height+bend);
                t.Add(start);t.Add(start+2);t.Add(start+1);t.Add(start+1);t.Add(start+2);t.Add(start+3);t.Add(start+2);t.Add(start+4);t.Add(start+3);
                uv.Add(new Vector2(0,0));uv.Add(new Vector2(1,0));uv.Add(new Vector2(0,.62f));uv.Add(new Vector2(1,.62f));uv.Add(new Vector2(.5f,1));
                float shade=Range(.15f,1);
                var normal=new Vector3(Mathf.Sin(tuft)*.3f,1,-Mathf.Cos(tuft)*.3f).normalized;
                for(int j=0;j<5;j++){ colors.Add(new Color(shade,1,1,1));normals.Add(normal); }
            }
            var mesh=Finish("Wind grass patch "+patch,v,t);
            mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetNormals(normals);
            var bounds=mesh.bounds;bounds.Expand(.2f);mesh.bounds=bounds;
            return mesh;
        }

        static Mesh Finish(string name,List<Vector3> vertices,List<int> triangles)
        {
            var mesh=new Mesh { name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16 };
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
