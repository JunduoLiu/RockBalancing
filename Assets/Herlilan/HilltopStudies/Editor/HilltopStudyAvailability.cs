using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Herlilan.HilltopStudies.Editor
{
    // This entry point has no dependency on the optional Hilltop or gameplay assemblies.
    public static class HilltopStudyAvailability
    {
        [MenuItem("Herlilan/Hilltop Studies/Check prerequisites")]
        public static void Check()
        {
            const string root="Assets/Herlilan/Hilltop/";
            var required=new List<string> {
                "Scenes/Hilltop.unity","Runtime/Herlilan.Hilltop.asmdef",
                "Runtime/HilltopScene.cs","Runtime/HilltopInterface.cs","Runtime/HilltopLighting.cs",
                "Runtime/HilltopGeometry.cs",
                "Shaders/HilltopMeadow.shader","Shaders/HilltopSky.shader","Shaders/HilltopStone.shader",
                "Settings/HilltopPipeline.asset","Settings/HilltopRenderer.asset","Settings/HilltopVolume.asset",
                "Materials/Meadow_breeze.mat","Materials/Mossy_hill.mat","Materials/River_stone.mat",
                "Materials/Soft_morning_sky.mat","Materials/Weathered_limestone.mat",
                "Meshes/Hill.asset","Meshes/Platform.asset","Meshes/Distant_mound.asset"
            };
            for(int i=0;i<12;i++)required.Add("Meshes/Grass_"+i+".asset");
            for(int i=0;i<9;i++)required.Add("Materials/Distant_sage_"+i+".mat");
            var missing=new List<string>();
            foreach(var path in required)
            {
                if(!File.Exists(root+path))missing.Add(root+path);
                if(!File.Exists(root+path+".meta"))missing.Add(root+path+".meta");
            }
            if(missing.Count>0)
            {
                Debug.LogWarning("Hilltop studies cannot be previewed: the separately maintained Hilltop baseline is incomplete. This review branch does not include it.\n"+string.Join("\n",missing));
                return;
            }
#if HERLILAN_HILLTOP_STUDIES
            Debug.Log("Hilltop files are present and studies are compiled. Open Hilltop.unity, enable experiments from this menu, then Play. F6 original / F7 A1 / F8 A2 / F9 background.");
#else
            Debug.Log("Hilltop files are present. To compile these opt-in studies, add HERLILAN_HILLTOP_STUDIES to the active target's Scripting Define Symbols in Player Settings. This check does not modify project settings.");
#endif
        }
    }
}

