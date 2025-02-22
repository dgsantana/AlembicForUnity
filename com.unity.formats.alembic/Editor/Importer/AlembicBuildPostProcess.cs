using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityEditor.Formats.Alembic.Importer
{
    static class AlembicBuildPostProcess
    {
        internal static readonly List<KeyValuePair<string, string>> FilesToCopy = new List<KeyValuePair<string, string>>();
        internal static bool HaveAlembicInstances = false;
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (!TargetIsSupported(target))
            {
                if (HaveAlembicInstances)
                {
                    Debug.LogWarning(
                        "Alembic only supports the following build targets: Windows 64-bit, MacOS X, Linux 64-bit or Stadia");
                }

                HaveAlembicInstances = false;

                return;
            }

            foreach (var files in FilesToCopy)
            {
                var dir = Path.GetDirectoryName(files.Value);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(files.Key, files.Value, true);
            }
            FilesToCopy.Clear();
        }

        public static bool TargetIsSupported(BuildTarget target)
        {
            return target == BuildTarget.StandaloneOSX || target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneLinux64 || target == BuildTarget.Stadia;
        }
    }

    class AlembicProcessScene : IProcessSceneWithReport
    {
        public int callbackOrder
        {
            get { return 9999;} // Best if we are lest in the chain to catch potential Alembics that were created during a Scene post process.
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !AlembicBuildPostProcess.TargetIsSupported(report.summary.platform))
            {
                AlembicBuildPostProcess.HaveAlembicInstances |= scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<AlembicStreamPlayer>(true)).Any();
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var players = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AlembicStreamPlayer>(true));
            var pathToStreamingAssets = GetStreamingAssetsPath(report.summary);
            foreach (var p in players)
            {
                ProcessAlembicStreamPlayerAssets(p, pathToStreamingAssets);
            }
            SceneManager.SetActiveScene(activeScene);
        }

        static void ProcessAlembicStreamPlayerAssets(AlembicStreamPlayer streamPlayer, string streamingAssetsPath)
        {
            streamPlayer.StreamDescriptor = streamPlayer.StreamDescriptor.Clone();// make a copy
            var srcPath = streamPlayer.StreamDescriptor.PathToAbc;

            // Avoid name collisions by hashing the full path
            var hashedFilename = HashSha1(srcPath) + ".abc";
            var dstPath = Path.Combine(streamingAssetsPath, hashedFilename);
            AlembicBuildPostProcess.FilesToCopy.Add(new KeyValuePair<string, string>(srcPath, dstPath));

            streamPlayer.StreamDescriptor.PathToAbc = hashedFilename;
        }

        static string GetStreamingAssetsPath(BuildSummary summary)
        {
            switch (summary.platform)
            {
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(summary.outputPath, "Contents/Resources/Data/StreamingAssets");
                case BuildTarget.Stadia:
                    return Path.Combine(summary.outputPath, "layout/files/Data/StreamingAssets");
                case BuildTarget.StandaloneLinux64:
                case BuildTarget.StandaloneWindows64:
                    var name = Path.ChangeExtension(summary.outputPath, null);
                    return name + "_Data/StreamingAssets";
                default:
                    throw new NotImplementedException();
            }
        }

        static string HashSha1(string value)
        {
            var sha1 = SHA1.Create();
            var inputBytes = Encoding.ASCII.GetBytes(value);
            var hash = sha1.ComputeHash(inputBytes);
            var sb = new StringBuilder();
            foreach (var t in hash)
            {
                sb.Append(t.ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
