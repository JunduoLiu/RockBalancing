using UnityEngine;

namespace RockBalancing
{
    public sealed class PlacementGuide : MonoBehaviour
    {
        public GameSession Session;
        public Material Template;
        LineRenderer stem, ring, footprint;
        Material material;
        void Start()
        {
            material = Template!=null?new Material(Template):new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = new Color(.92f,.68f,.29f);
            if(material.HasProperty("_Cull"))material.SetFloat("_Cull",0);
            stem=MakeLine("Drop guide",2,.008f);
            ring=MakeLine("Selected stone outline",65,.003f);
            footprint=MakeLine("Drop position",65,.003f);
        }
        LineRenderer MakeLine(string name,int count,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(transform);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;
            line.positionCount=count;line.startWidth=line.endWidth=width;
            line.alignment=LineAlignment.View;line.numCapVertices=3;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
        void LateUpdate()
        {
            var stone=Session.Pending;
            stem.enabled=ring.enabled=footprint.enabled=stone!=null;
            if(stone==null)return;
            var center=stone.transform.position;
            float bottom=Session.Ground.bounds.max.y+.004f;
            if(Physics.Raycast(center,Vector3.down,out var hit,100,~0,QueryTriggerInteraction.Ignore)) bottom=hit.point.y+.004f;
            stem.SetPosition(0,center);stem.SetPosition(1,new Vector3(center.x,bottom,center.z));
            float radius=stone.Shape.bounds.extents.magnitude*.9f;
            var camera=Camera.main;
            Vector3 right=camera!=null?camera.transform.right:Vector3.right;
            Vector3 up=camera!=null?camera.transform.up:Vector3.up;
            for(int i=0;i<65;i++)
            {
                float t=i*Mathf.PI/32;
                ring.SetPosition(i,center+(right*Mathf.Cos(t)+up*Mathf.Sin(t))*radius);
                footprint.SetPosition(i,new Vector3(center.x+Mathf.Cos(t)*radius,bottom,center.z+Mathf.Sin(t)*radius));
            }
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
