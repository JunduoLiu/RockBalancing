#if HERLILAN_HILLTOP_STUDIES
using System.Collections.Generic;
using Herlilan.Hilltop;
using UnityEngine;
using UnityEngine.Rendering;
namespace Herlilan.HilltopStudies
{
    [DefaultExecutionOrder(1400)]
    public sealed class HilltopBackgroundStudy : MonoBehaviour
    {
        public bool Active {get;private set;}
        public bool Ready {get;private set;}
        readonly List<Renderer> ridges=new List<Renderer>();
        readonly List<MaterialPropertyBlock> blocks=new List<MaterialPropertyBlock>();
        readonly List<Vector3> scales=new List<Vector3>();
        readonly List<Material> authored=new List<Material>();
        readonly List<Renderer> grass=new List<Renderer>();
        readonly List<Material> grassOriginals=new List<Material>();
        readonly List<Material> grassCopies=new List<Material>();
        readonly List<Transform> clouds=new List<Transform>();
        readonly List<Vector3> cloudPositions=new List<Vector3>();
        Material cloudMaterial; GameObject cloudRoot;
        public void Initialize(HilltopScene owner)
        {
            var far=owner.transform.Find("Distant soft hills");
            if(far!=null)foreach(var r in far.GetComponentsInChildren<Renderer>())
            {ridges.Add(r);var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);blocks.Add(b);scales.Add(r.transform.localScale);authored.Add(r.sharedMaterial);}
            foreach(var r in owner.Grass.GetComponentsInChildren<Renderer>())
            {
                grass.Add(r);grassOriginals.Add(r.sharedMaterial);var m=new Material(r.sharedMaterial){name="Study breeze (runtime copy)"};
                if(m.HasProperty("_Wind"))m.SetFloat("_Wind",m.GetFloat("_Wind")*1.18f);
                grassCopies.Add(m);
            }
            cloudRoot=new GameObject("Study distant cloud wisps");cloudRoot.transform.SetParent(owner.transform,false);
            cloudMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Study distant clouds (runtime)"};
            for(int i=0;i<6;i++)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Far cloud "+i;go.transform.SetParent(cloudRoot.transform,false);
                var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);
                go.transform.localPosition=new Vector3(-7+i*3.1f,-.8f+(i%2)*.16f,13+(i%3)*2);
                go.transform.localScale=new Vector3(2.5f+(i%3)*.7f,.22f+(i%2)*.08f,.7f);
                var r=go.GetComponent<Renderer>();r.sharedMaterial=cloudMaterial;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                clouds.Add(go.transform);cloudPositions.Add(go.transform.localPosition);
            }
            cloudRoot.SetActive(false);Ready=true;
        }
        public void SetEnabled(bool value)
        {
            if(!Ready||value==Active)return;Active=value;cloudRoot.SetActive(value);
            for(int i=0;i<ridges.Count;i++)
            {
                ridges[i].transform.localScale=value?Vector3.Scale(scales[i],new Vector3(1,.78f,1)):scales[i];
                if(!value)ridges[i].SetPropertyBlock(blocks[i]);
            }
            for(int i=0;i<grass.Count;i++)grass[i].sharedMaterial=value?grassCopies[i]:grassOriginals[i];
        }
        void LateUpdate()
        {
            if(!Ready||!Active)return;
            // Read the existing live lighting; never override its sun, sky, fog, volume or preset.
            Color atmosphere=RenderSettings.fogColor;
            Color[] hues={new Color(.64f,.75f,.79f),new Color(.68f,.79f,.76f),new Color(.82f,.78f,.67f)};
            for(int i=0;i<ridges.Count;i++)
            {
                var block=new MaterialPropertyBlock();
                Color original=authored[i].HasProperty("_BaseColor")?authored[i].GetColor("_BaseColor"):authored[i].color;
                block.SetColor("_BaseColor",Color.Lerp(original,Color.Lerp(atmosphere,hues[i%3],.3f),.70f));
                ridges[i].SetPropertyBlock(block);
            }
            cloudMaterial.color=Color.Lerp(atmosphere,new Color(.97f,.94f,.86f),.30f);
            for(int i=0;i<clouds.Count;i++)clouds[i].localPosition=cloudPositions[i]+Vector3.right*Mathf.Sin(Time.time*.025f+i)*.35f;
        }
        void OnDisable(){if(Ready)SetEnabled(false);}
        void OnDestroy(){if(Ready)SetEnabled(false);foreach(var m in grassCopies)if(m!=null)Destroy(m);if(cloudMaterial!=null)Destroy(cloudMaterial);if(cloudRoot!=null)Destroy(cloudRoot);}
    }
}



#endif

