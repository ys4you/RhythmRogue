using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RhythmRogue.Editor
{
    /// <summary>
    /// One-click playtest build. Makes a Windows 64-bit development build (so PlaytestReporter's
    /// F8 bug report works), names it after the version, date and commit, writes a BUILD.txt,
    /// and zips it ready to send. Output goes to Builds/Playtest/, which git ignores.
    /// </summary>
    public static class PlaytestBuilder
    {
        private const string OutputRoot = "Builds/Playtest";
        private const string ExeName = "RhythmRogue.exe";
        private const string DialogTitle = "Build Playtest";

        /// <summary>
        /// Builds, zips and reveals a playtest build. Warns first if there are uncommitted changes,
        /// since the build then can't be traced back to a commit.
        /// </summary>
        [MenuItem("RhythmRogue/Build Playtest")]
        public static void BuildPlaytest()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "Windows Build Support is not installed for this Unity version. Add it in Unity Hub > Installs > Add modules.", "OK");
                return;
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, "No scenes are enabled in Build Settings.", "OK");
                return;
            }

            string commit = RunGit("rev-parse --short HEAD") ?? "nogit";
            bool dirty = !string.IsNullOrEmpty(RunGit("status --porcelain -- Assets Packages ProjectSettings"));

            if (dirty && !EditorUtility.DisplayDialog(DialogTitle,
                    "You have uncommitted changes in Assets, Packages or ProjectSettings. The build name will end in -dirty, so a tester's report can't be tied to a commit.\n\nCommit first, or build anyway?",
                    "Build anyway", "Cancel"))
                return;

            DateTime now = DateTime.Now;
            string buildName = $"RhythmRogue_v{PlayerSettings.bundleVersion}_{now:yyyy-MM-dd}_{commit}{(dirty ? "-dirty" : "")}";
            string folder = Path.GetFullPath(Path.Combine(OutputRoot, buildName));

            if (Directory.Exists(folder))
                Directory.Delete(folder, true);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(DialogTitle, $"Build {report.summary.result}. Check the Console for errors.", "OK");
                return;
            }

            File.WriteAllText(Path.Combine(folder, "BUILD.txt"), BuildInfo(buildName, now, commit, dirty));

            string zipPath = folder + ".zip";
            ZipBuild(folder, zipPath, buildName);

            Debug.Log($"[PlaytestBuilder] Playtest build ready: {zipPath}");
            EditorUtility.RevealInFinder(zipPath);
        }

        /// <summary>
        /// Zips the build folder under a top-level folder named after the build. Skips Unity's
        /// *_DoNotShip folders (debug symbols that should not go to testers).
        /// </summary>
        private static void ZipBuild(string folder, string zipPath, string buildName)
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(folder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (relative.Contains("DoNotShip"))
                        continue;

                    string entryName = (buildName + "/" + relative).Replace('\\', '/');
                    zip.CreateEntryFromFile(file, entryName, System.IO.Compression.CompressionLevel.Optimal);
                }
            }
        }

        /// <summary>The text of BUILD.txt: what this build is and what testers should do with it.</summary>
        private static string BuildInfo(string buildName, DateTime time, string commit, bool dirty)
        {
            return
                "RhythmRogue playtest build\n" +
                $"Build:   {buildName}\n" +
                $"Version: {PlayerSettings.bundleVersion}\n" +
                $"Date:    {time:yyyy-MM-dd HH:mm}\n" +
                $"Commit:  {commit}{(dirty ? " (plus uncommitted changes)" : "")}\n" +
                $"Unity:   {Application.unityVersion}\n" +
                "\n" +
                "For testers:\n" +
                "- Run RhythmRogue.exe.\n" +
                "- Found a bug? Press F8. It copies a bug report to your clipboard, so paste it into the form.\n" +
                $"- Put \"{buildName}\" in the Build version field of the feedback form.\n";
        }

        /// <summary>
        /// Runs a git command in the project folder and returns its trimmed output,
        /// or null if git is missing or the command fails.
        /// </summary>
        private static string RunGit(string arguments)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo("git", arguments)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };

                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    if (!process.WaitForExit(5000))
                        return null;
                    return process.ExitCode == 0 ? output : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
