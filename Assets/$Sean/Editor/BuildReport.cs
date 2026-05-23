#pragma warning disable UDR0001
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
// using UnityEditor.AddressableAssets;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using static UnityEditor.EditorApplication;

namespace pigbrain.core.Project
{
    using static pigbrain.core.Project.Core;
    using static pigbrain.core.Project.BuildReport;

    #region Builder
    public static class ProjectBuilder
    {
        public const string TempAssetPath = "Assets/__BuildTemp";
        public static void CreateTempAssetPath()
        {
            if (!AssetDatabase.IsValidFolder("Assets/__BuildTemp"))
                AssetDatabase.CreateFolder("Assets", "__BuildTemp");
        }

        static string GetCodeOptimisation() => EditorUserBuildSettings.GetPlatformSettings(
            BuildPipeline.GetBuildTargetName(BuildTarget.WebGL), "CodeOptimization");
        static void SetCodeOptimisation(string v) => EditorUserBuildSettings.SetPlatformSettings(
            BuildPipeline.GetBuildTargetName(BuildTarget.WebGL), "CodeOptimization", v);
        readonly static string[] CodeOptimisationNames = Enum.GetNames(typeof(BuildCodeOptimization))
            .Select(n => n == "ShorterBuildTime" ? "BuildTimes" : n).ToArray();

        public enum BuildCodeOptimization
        { ShorterBuildTime, RuntimeSpeed, RuntimeSpeedLTO, DiskSize, DiskSizeLTO }

        #region └Draw buttons
        internal static void DrawGUI()
        {
            Core.DrawHeader("Build:");

            var profile = BuildProfile.GetActiveBuildProfile();
            var names = CodeOptimisationNames;
            var index = Array.IndexOf(names, GetCodeOptimisation());

            // Options
            var selectedIndex = EditorGUILayout.Popup("Code Optimization", index, names);
            if (selectedIndex != index)
            {
                SetCodeOptimisation(names[selectedIndex]);
                EditorUtility.SetDirty(profile);
            }

            // Buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build")) delayCall += ButtonBuild;
            if (GUILayout.Button("Build & Run")) delayCall += ButtonBuildRun;
            if (GUILayout.Button("Build & Run (Brotli)")) delayCall += ButtonBuildRunBrotli;
            EditorGUILayout.EndHorizontal();

            var report = GetReportObject();
            if (report) EditorGUILayout.ObjectField(report, report.GetType(), true);
        }
        #endregion

        #region └Build
        static void ButtonBuild() => Build();
        static void ButtonBuildRun() => Build(BuildOptions.AutoRunPlayer);
        static void ButtonBuildRunBrotli() => Build(BuildOptions.AutoRunPlayer, true);

        static void Build(BuildOptions options = BuildOptions.None, bool useFallback = false)
        {
            const string PrefKey = "ProjectController.LastBuildPath";
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var lastPath = EditorPrefs.GetString(PrefKey, "");
            var directory = string.IsNullOrEmpty(lastPath) ? "" : Path.GetDirectoryName(lastPath);
            var fileName = string.IsNullOrEmpty(lastPath) ? Application.productName
                : Path.GetFileNameWithoutExtension(lastPath);

            // Override name
            BuildVersion.Increment();
            fileName = $"{Application.productName}-{Application.version}" + (useFallback ? "-B" : "");

            var path = EditorUtility.SaveFilePanel("Build Location", directory, fileName, "");
            if (string.IsNullOrEmpty(path))
            {
                BuildVersion.Restore();
                return;
            }

            EditorPrefs.SetString(PrefKey, path);

            var oldFallback = PlayerSettings.WebGL.decompressionFallback;
            var buildOptions = options;
            try
            {
                Debug.Log("#### START BUILD");
                if (useFallback)
                    PlayerSettings.WebGL.decompressionFallback = true;
                // BuildStripper.BackupAssets();

                var report = BuildPipeline.BuildPlayer(scenes, path, EditorUserBuildSettings.activeBuildTarget, buildOptions);
                if (report.summary.result == BuildResult.Cancelled) throw new Exception("Build cancelled!");

                CreateReport(report);
            }
            catch
            {
                Debug.Log("Build did not complete!");
                BuildVersion.Restore();
            }
            finally
            {
                // BuildStripper.RestoreAssets();
                PlayerSettings.WebGL.decompressionFallback = oldFallback;
                Debug.Log("#### FINISHED BUILD");
            }
        }
        #endregion

        #region └Create Report
        const string BuildReportAssetPath = "Assets/Build Report.asset";
        static void CreateReport(UnityEditor.Build.Reporting.BuildReport report)
        {
            DependencyUtils.BuildCache();
            var entries = report.packedAssets
                .SelectMany(p => p.contents)
                .GroupBy(c => c.sourceAssetPath)
                .Select(g =>
                {
                    var path = g.Key;
                    var size = g.Sum(x => (long)x.packedSize);
                    var asset = string.IsNullOrEmpty(path)
                        ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    return new Entry(asset, path, size);
                })
                .OrderByDescending(x => x.size)
                .ToArray();

            // persist
            var db = TryCreateReportObject();
            db.entries = entries;
            db.buildPath = report.summary.outputPath;
            db.version = PlayerSettings.bundleVersion;
            db.applicationName = PlayerSettings.productName;

            Debug.Log($"Build location: {Path.Combine(db.buildPath, "Build")}");
            db.buildSize = PathUtility.GetDirectorySize(Path.Combine(db.buildPath, "Build"));
            // db.bundleSize = PathUtility.GetDirectorySize(Path.Combine(db.buildPath, "StreamingAssets"));

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        static BuildReport LastReport;
        static BuildReport GetReportObject()
        {
            if (LastReport) return LastReport;
            return AssetDatabase.LoadAssetAtPath<BuildReport>(BuildReportAssetPath) is BuildReport db
                ? LastReport = db
                : null;
        }

        static BuildReport TryCreateReportObject()
        {
            if (LastReport) return LastReport;
            if (AssetDatabase.LoadAssetAtPath<BuildReport>(BuildReportAssetPath) is not BuildReport db)
            {
                db = ScriptableObject.CreateInstance<BuildReport>();
                Directory.CreateDirectory(Path.GetDirectoryName(BuildReportAssetPath));
                AssetDatabase.CreateAsset(db, BuildReportAssetPath);
            }
            LastReport = db;
            return db;
        }
        #endregion
    }
    #endregion

    #region Build Report
    public class BuildReport : ScriptableObject
    {
        // public ulong buildSize, bundleSize;
        public Entry[] entries;
        public GroupID groupFilter = GroupID.Everything;
        public View view;
        public string version, applicationName, buildPath;

        public ulong buildSize, bundleSize;

        [Serializable]
        public class Entry
        {
            public UnityEngine.Object asset;
            public string path;
            public long size;
            public GroupID group;
            public UnityEngine.Object dependency;

            public Entry(UnityEngine.Object asset, string path, long size)
            {
                this.path = path;
                this.asset = asset;
                this.size = size;
                this.group = GetAssetGroup(asset, path);
                this.dependency = DependencyUtils.ResolveRoot(asset);
            }
            internal static GroupID GetAssetGroup(UnityEngine.Object asset, string path)
            {
                if (!asset) return GroupID.Other;
                if (asset is ScriptableObject) return GroupID.ScriptableObject;
                var ext = Path.GetExtension(path).ToLowerInvariant();
                return ext switch
                {
                    ".cs" => GroupID.Code,
                    ".png" or ".jpg" or ".jpeg" or ".tga" or ".psd" or ".exr" => GroupID.Texture,
                    ".spriteatlas" => GroupID.Sprite,
                    ".fbx" or ".obj" or ".blend" or ".dae" => GroupID.Model,
                    ".mesh" => GroupID.Mesh,
                    ".wav" or ".mp3" or ".ogg" or ".aiff" => GroupID.AudioClip,
                    ".mixer" => GroupID.AudioMixer,
                    ".mat" => GroupID.Material,
                    ".shader" or ".shadergraph" => GroupID.Shader,
                    ".compute" => GroupID.ComputeShader,
                    ".prefab" => GroupID.Prefab,
                    ".anim" => GroupID.AnimationClip,
                    ".ttf" or ".otf" or ".fontsettings" => GroupID.Font,
                    ".unity" => GroupID.Scene,
                    _ => GroupID.Other,
                };
            }
            public static implicit operator bool(Entry empty) => empty != null;
        }

        public enum GroupID
        {
            Other = 1 << 0,
            Code = 1 << 1,
            Texture = 1 << 2,
            Model = 1 << 3,
            AudioClip = 1 << 4,
            Render = 1 << 5,
            Prefab = 1 << 6,
            AnimationClip = 1 << 7,
            AudioMixer = 1 << 8,
            ComputeShader = 1 << 9,
            Font = 1 << 10,
            Graph = 1 << 11,
            Material = 1 << 12,
            Mesh = 1 << 13,
            Scene = 1 << 14,
            Shader = 1 << 15,
            Sprite = 1 << 16,
            ScriptableObject = 1 << 17,
            Everything = -1,
        }
        public enum View { Default, Memory, GroupID, Dependency }
    }
    #endregion

    #region BuildVersion
    public class BuildVersion
    {
        static string LastVersion;
        public static void Increment()
        {
            LastVersion = PlayerSettings.bundleVersion;
            string[] parts = LastVersion.Split('.');
            int major = parts.Length >= 1 && int.TryParse(parts[0], out int o1) ? o1 : 1;
            int minor = parts.Length >= 2 && int.TryParse(parts[1], out int o2) ? o2 : 0;
            int patch = parts.Length >= 3 && int.TryParse(parts[2], out int o3) ? o3 : 0;
            string newVersion = $"{major}.{minor}.{++patch}";
            PlayerSettings.bundleVersion = newVersion;
            Debug.LogWarning($"Version incremented from {LastVersion} to {newVersion}");
        }

        public static void Restore()
        {
            if (string.IsNullOrEmpty(LastVersion)) return;
            PlayerSettings.bundleVersion = LastVersion;
            LastVersion = null;
        }
    }
    #endregion

    #region Dependency
    public static class DependencyUtils
    {
        static Dictionary<string, string> AssetToScene;
        static Dictionary<string, UnityEngine.Object> AddressableCache;

        public static void BuildCache()
        {
            AssetToScene = new();
            AddressableCache = new();

            // Scene dependencies
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var deps = AssetDatabase.GetDependencies(scene.path, true);
                foreach (var d in deps)
                    if (!AssetToScene.ContainsKey(d))
                        AssetToScene[d] = scene.path;
            }

            // Addressables
            // var settings = AddressableAssetSettingsDefaultObject.Settings;
            // if (settings != null)
            // {
            //     foreach (var group in settings.groups)
            //     {
            //         if (group == null) continue;
            //         foreach (var entry in group.entries)
            //         {
            //             var path = AssetDatabase.GUIDToAssetPath(entry.guid);
            //             if (!string.IsNullOrEmpty(path) && !AddressableCache.ContainsKey(path))
            //                 AddressableCache[path] = entry.MainAsset;
            //         }
            //     }
            // }
        }

        public static UnityEngine.Object ResolveRoot(UnityEngine.Object obj)
        {
            if (!obj) return null;

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return null;

            if (AssetToScene != null && AssetToScene.TryGetValue(path, out var scenePath))
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

            if (AddressableCache != null && AddressableCache.TryGetValue(path, out var addr))
                return addr;

            if (path.Contains("/Resources/"))
                return obj;

            return null;
        }
    }
    #endregion
}

#region Editor
namespace pigbrain.game.Boxhead.Environment
{
    using UnityEditor;
    using UnityEngine;
    using pigbrain.core.Project;
    using static pigbrain.core.Project.BuildReport;
    using static pigbrain.core.Project.Core;

    [CustomEditor(typeof(BuildReport))]
    public class BuildReport_Editor : Editor
    {
        BuildReport report => target as BuildReport;

        static GUIStyle Right;
        static GUILayoutOption SmallWidth;
        static GUILayoutOption DependencyWidth;

        public override void OnInspectorGUI()
        {
            Right ??= new(EditorStyles.textField) { alignment = TextAnchor.MiddleRight };
            SmallWidth ??= GUILayout.MaxWidth(50 * 1.3f);
            DependencyWidth ??= GUILayout.MaxWidth(50 * 4);

            var buildPath = Path.GetFileName(report.buildPath);

            var buildSize = $"{MemoryProfiler.FormatBytes(report.buildSize)}";
            var bundleSize = $"{MemoryProfiler.FormatBytes(report.bundleSize)}";
            EditorGUILayout.TextField($"{report.applicationName} {report.version} build ({buildPath}): {buildSize} bundle: {bundleSize}", EditorStyles.whiteLargeLabel);

            report.groupFilter = (GroupID)EditorGUILayout.EnumFlagsField("Filter", report.groupFilter);
            report.view = (View)EditorGUILayout.EnumPopup("View", report.view);

            var entries = report.entries.Where(e => report.groupFilter.HasFlag(e.group)).ToArray();
            DrawEntryHeader();

            switch (report.view)
            {
                default: View_Default(entries); break;
                case View.GroupID: View_GroupID(entries); break;
                case View.Memory: View_Memory(entries); break;
                case View.Dependency: View_Dependency(entries); break;
            }
        }

        void View_Default(Entry[] entries)
        {
            foreach (Entry entry in entries) DrawEntry(entry);
        }

        void View_Dependency(Entry[] entries)
        {
            var groups = entries
                .GroupBy(e => e.dependency)
                .OrderByDescending(g => g.Sum(e => e.size));

            foreach (var group in groups)
            {
                var dep = group.Key;
                var total = (ulong)group.Sum(e => e.size);

                EditorGUILayout.LabelField(
                    dep ? $"{dep.name} ({MemoryProfiler.FormatBytes(total)})"
                        : $"Unresolved ({MemoryProfiler.FormatBytes(total)})",
                    EditorStyles.boldLabel);

                foreach (var entry in group.OrderByDescending(e => e.size))
                    DrawEntry(entry);
            }
        }

        (int min, int max, string label)[] memoryGroups = new (int min, int max, string label)[]
        {
            (0, 100, "Under 100 bytes"),
            (100, 1000, "100 bytes to 1 KB"),
            (1000, 10000, "1 KB to 10 KB"),
            (10000, 100000, "10 KB to 100 KB"),
            (100000, 1000000, "100 KB to 1 MB"),
            (1000000, 10000000, "1 MB to 10 MB"),
            (10000000, 100000000, "10 MB to 100 MB"),
            (100000000, 1000000000, "100 MB to 1 GB"),
        };
        void View_Memory(Entry[] entries)
        {
            var groups = memoryGroups
                .Select(g => (g, items: entries.Where(e => e.size >= g.min && e.size < g.max).OrderByDescending(e => e.size).ToArray()))
                .Where(x => x.items.Length > 0)
                .OrderByDescending(x => x.g.min);

            foreach (var (g, items) in groups)
            {
                EditorGUILayout.LabelField($"{g.label} ({MemoryProfiler.FormatBytes((ulong)items.Sum(e => e.size))})", EditorStyles.boldLabel);
                foreach (var entry in items) DrawEntry(entry);
            }
        }

        void View_GroupID(Entry[] entries)
        {
            var groups = entries
                .GroupBy(e => e.group)
                .OrderByDescending(g => g.Sum(e => e.size));

            foreach (var group in groups)
            {
                var total = (ulong)group.Sum(e => e.size);
                EditorGUILayout.LabelField($"{group.Key} ({MemoryProfiler.FormatBytes(total)})", EditorStyles.boldLabel);

                foreach (var entry in group.OrderByDescending(e => e.size))
                    DrawEntry(entry);
            }
        }

        void DrawEntryHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Asset:");
                GUILayout.Label("Dependency:");
                GUILayout.Label("Type:", SmallWidth);
                GUILayout.Label("Size:", SmallWidth);
            }
        }

        void DrawEntry(Entry entry)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (entry.asset) EditorGUILayout.ObjectField(entry.asset, typeof(Object), true);
                else EditorGUILayout.TextField(entry.path);

                if (entry.dependency) EditorGUILayout.ObjectField(entry.dependency, typeof(Object), true);
                else EditorGUILayout.TextField(entry.path);

                EditorGUILayout.EnumPopup(entry.group, SmallWidth);
                EditorGUILayout.TextField(MemoryProfiler.FormatBytes(entry.size), Right, SmallWidth);
            }
        }
    }
}
#endregion



// #region Watcher
// [InitializeOnLoad]
// public class BuildWatcher : IPreprocessBuildWithReport, IPostprocessBuildWithReport, IProcessSceneWithReport
// {
//     public enum BuildCodeOptimization
//     {
//         ShorterBuildTime,
//         RuntimeSpeed,
//         RuntimeSpeedLTO,
//         DiskSize,
//         DiskSizeLTO
//     }

//     public int callbackOrder => 0;

//     static BuildWatcher()
//     {
//         CompilationPipeline.compilationStarted -= OnCompilationStarted;
//         CompilationPipeline.compilationStarted += OnCompilationStarted;
//         CompilationPipeline.compilationFinished -= OnCompilationFinished;
//         CompilationPipeline.compilationFinished += OnCompilationFinished;
//     }

//     static void OnCompilationStarted(object context) { }
//     static void OnCompilationFinished(object context) { }
//     public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report) =>
//         Debug.Log($"Build started: {report.summary.platform} -> {report.summary.outputPath}");
//     public void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report) =>
//         Debug.Log($"Build finished: {report.summary.result} | {report.summary.totalSize} bytes | {report.summary.totalTime}");
//     public void OnProcessScene(Scene scene, UnityEditor.Build.Reporting.BuildReport report) =>
//         Debug.Log($"Processing scene: {scene.path}");
// }
// #endregion

#region CORE IMPORT
namespace pigbrain.core.Project
{
    public static class Core
    {
        public static class MemoryProfiler
        {
            public static string FormatBytes(long b) => FormatBytes((ulong)b);
            public static string FormatBytes(ulong b, ulong bSize = 1024)
            {
                ulong kb = bSize, mb = kb * bSize, gb = mb * bSize;
                if (b >= gb) return $"{b / (double)gb:0.0} GB";
                if (b >= mb) return $"{b / (double)mb:0.0} MB";
                if (b >= kb) return $"{b / (double)kb:0.0} KB";
                return $"{b} B";
            }
        }

        public static void DrawHeader(string name) =>
            DrawHeader(name, new Color(0.2f, 0.4f, 0.8f, 0.5f));

        public static void DrawHeader(string name, Color color)
        {
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.DrawRect(rect, color);
            EditorGUI.LabelField(rect, name, EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
        }

        public static void DrawHeader(Rect position, string label, Color color = default)
        {
            Rect p = new(position.x - 100, position.y, position.width + 200, position.height);
            EditorGUI.DrawRect(p, color);
            // EditorGUI.DrawRect(position.AddX(-100).AddW(200), color);
            EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
        }

        public static class PathUtility
        {
            public static ulong GetDirectorySize(string dir)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;

                ulong size = 0;
                var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    try { size += (ulong)new FileInfo(f).Length; }
                    catch { }
                }
                return size;
            }
        }
    }
}
#endregion
