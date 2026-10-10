using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bidwarss.Editor
{
    // One-click and command-line builds. Output goes to Builds/ (git-ignored).
    // Batch example:
    //   Unity -batchmode -quit -projectPath <project> -executeMethod Bidwarss.Editor.BuildTools.LinuxServer
    // Dedicated Server builds need the matching "Dedicated Server" module installed from Unity Hub.
    public static class BuildTools
    {
        [MenuItem("Bidwarss/Build/Windows Client")]
        public static void WindowsClient() => Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, "Builds/WindowsClient/Bidwarss.exe");

        [MenuItem("Bidwarss/Build/Dedicated Server (Windows)")]
        public static void WindowsServer() => Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Server, "Builds/WindowsServer/BidwarssServer.exe");

        [MenuItem("Bidwarss/Build/Dedicated Server (Linux)")]
        public static void LinuxServer() => Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, "Builds/LinuxServer/BidwarssServer.x86_64");

        static void Build(BuildTarget target, StandaloneBuildSubtarget subtarget, string path)
        {
            if (EditorApplication.isPlaying) return;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("Build icin sahne yok. Once Bidwarss > Build Uploaded Depot (Co-op) calistir.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                subtarget = (int)subtarget,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                string message = "Build basarisiz: " + report.summary.result + " (" + report.summary.totalErrors + " hata). " +
                    "Dedicated Server icin Unity Hub'dan ilgili Dedicated Server modulunu kurdugundan emin ol.";
                if (Application.isBatchMode) { Debug.LogError(message); EditorApplication.Exit(1); return; }
                throw new InvalidOperationException(message);
            }
            Debug.Log("Bidwarss build hazir: " + path + " (" + report.summary.totalSize / (1024 * 1024) + " MB)");
        }
    }
}
