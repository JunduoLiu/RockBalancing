using UnityEngine;

namespace RockBalancing
{
    // Read the final camera pose and pending position after input/follow this frame.
    [DefaultExecutionOrder(200)]
    public sealed class PlacementGuide : MonoBehaviour
    {
        public GameSession Session;
        public Material Template;
        public Vector3 DropPosition { get; private set; }
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
            DropPosition=new Vector3(center.x,bottom,center.z);
            stem.SetPosition(0,center);stem.SetPosition(1,DropPosition);
            float radius=stone.Shape.bounds.extents.magnitude*.9f;
            var camera=Camera.main;
            // Keep the thin guides legible when observing a high or distant stone.
            // World-space minima retain the existing appearance at close distances.
            float pixelSize=camera!=null ? 2*Mathf.Max(camera.nearClipPlane,
                Vector3.Dot(center-camera.transform.position,camera.transform.forward))*
                Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,camera.pixelHeight) : 0;
            stem.startWidth=stem.endWidth=Mathf.Max(.008f,pixelSize*2);
            ring.startWidth=ring.endWidth=footprint.startWidth=footprint.endWidth=Mathf.Max(.003f,pixelSize*2);
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
