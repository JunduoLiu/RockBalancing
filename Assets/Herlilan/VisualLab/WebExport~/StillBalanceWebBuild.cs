using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class StillBalanceWebBuild
{
    public static void Run()
    {
        if (!Application.isBatchMode || !File.Exists(".stillbalance-export"))
            throw new InvalidOperationException("Run in the isolated export project.");
        PlayerSettings.WebGL.template = "PROJECT:StillBalance";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.defaultWebScreenWidth = 430;
        PlayerSettings.defaultWebScreenHeight = 900;
        PlayerSettings.productName = "Still Balance";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Herlilan/VisualLab/StillBalance.unity" },
            locationPathName = "Export", target = BuildTarget.WebGL
        });
        if (report.summary.result != BuildResult.Succeeded || !File.Exists("Export/index.html"))
            throw new InvalidOperationException("Still Balance Web build failed.");
    }
}
