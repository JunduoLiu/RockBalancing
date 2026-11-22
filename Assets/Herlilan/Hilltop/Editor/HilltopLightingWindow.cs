using UnityEditor;
using UnityEngine;

namespace Herlilan.Hilltop.Editor
{
    public sealed class HilltopLightingWindow : EditorWindow
    {
        [MenuItem("Herlilan/Hilltop/Live lighting")]
        public static void Open() => GetWindow<HilltopLightingWindow>("Hilltop Lighting");
        void OnInspectorUpdate() => Repaint();
        void OnGUI()
        {
            var lighting = Object.FindFirstObjectByType<HilltopLighting>();
            if (!EditorApplication.isPlaying || lighting == null)
            {
                EditorGUILayout.HelpBox("Play the Hilltop scene to compare lighting. Changes are temporary and reset when Play stops.",MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("HILLTOP / LIGHT STUDY",EditorStyles.boldLabel);
            EditorGUILayout.LabelField(HilltopLighting.Names[lighting.Selected] + (lighting.IsTransitioning ? "  / blending" : ""));
            EditorGUILayout.Space();
            for (int i=0;i<HilltopLighting.Names.Length;i++)
                if (GUILayout.Button((i+1)+"  "+HilltopLighting.Names[i],GUILayout.Height(28)))
                { lighting.AutoCycle=false; lighting.Select(i); }
            lighting.AutoCycle=EditorGUILayout.Toggle("Auto cycle (L)",lighting.AutoCycle);
            lighting.CycleSeconds=EditorGUILayout.Slider("Seconds per look",lighting.CycleSeconds,5,30);
            lighting.TransitionSeconds=EditorGUILayout.Slider("Blend seconds",lighting.TransitionSeconds,.1f,8);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fine tune current look",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float elevation=EditorGUILayout.Slider("Sun elevation",lighting.Elevation,5,85);
            float azimuth=EditorGUILayout.Slider("Sun azimuth",Mathf.DeltaAngle(0,lighting.Azimuth),-180,180);
            float intensity=EditorGUILayout.Slider("Sun intensity",lighting.Intensity,0,3);
            float exposure=EditorGUILayout.Slider("Exposure",lighting.Exposure,-1,1.5f);
            if(EditorGUI.EndChangeCheck()) lighting.SetManual(elevation,azimuth,intensity,exposure);
            EditorGUILayout.HelpBox("Game view: 1–4 select a look; L toggles cycling. Fine tuning stops cycling. Select a preset again to reset its values.",MessageType.None);
        }
    }
}
