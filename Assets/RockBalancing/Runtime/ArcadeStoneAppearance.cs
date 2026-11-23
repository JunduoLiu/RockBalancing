using UnityEngine;

namespace RockBalancing
{
    static class ArcadeStoneAppearance
    {
        const int OrdinaryCount = 3;
        static readonly int FinishProperty = Shader.PropertyToID("_StoneFinish");

        // This order matches the Arcade Cel shader's Stone finish values (1–6).
        static readonly (string Name, Color32 Color)[] Finishes =
        {
            ("Granite", new Color32(174, 143, 123, 255)),
            ("Shale", new Color32(108, 135, 157, 255)),
            ("Basalt", new Color32(62, 66, 79, 255)),
            ("Crystal", new Color32(170, 226, 233, 255)),
            ("Gemstone", new Color32(157, 65, 186, 255)),
            ("Gold", new Color32(240, 172, 54, 255)),
        };

        public static Material Create(Material template)
        {
            int finish = Random.value < .7f
                ? Random.Range(0, OrdinaryCount)
                : Random.Range(OrdinaryCount, Finishes.Length);
            var appearance = Finishes[finish];
            var material = template != null
                ? new Material(template)
                : new Material(Shader.Find("RockBalancing/Arcade Cel"));
            material.name = appearance.Name;
            material.color = appearance.Color;
            if (material.HasProperty(FinishProperty)) material.SetFloat(FinishProperty, finish + 1);
            return material;
        }
    }
}
