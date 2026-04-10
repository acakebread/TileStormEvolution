#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using MassiveHadronLtd;           // your namespace
using ClassicTilestorm.Assets;
using ClassicTilestorm;    // where ProjectAssets lives

public class AssetManifestGenerator : IPreprocessBuildWithReport
{
	public int callbackOrder => 0;

	public void OnPreprocessBuild(BuildReport report)
	{
		GenerateAllManifests();
	}

	[MenuItem("Tools/Generate Asset Manifests %&M")]
	public static void GenerateAllManifests()
	{
		const string manifestRoot = "Assets/Resources/AssetManifests";
		if (!Directory.Exists(manifestRoot))
			Directory.CreateDirectory(manifestRoot);

		// These match the exact root arrays you already have in ProjectAssets
		WriteManifest("Models", GetNames<GameObject>(new[] { AssetPath.GeometryPath?.Trim('/') ?? "", "Levels", "Levels/Med" }));
		WriteManifest("Prefabs", GetNames<GameObject>(new[] { AssetPath.PrefabPath?.Trim('/') ?? "" }));
		WriteManifest("Textures", GetNames<Texture>(new[] { AssetPath.TexturePath?.Trim('/') ?? "" }));
		WriteManifest("Texture2Ds", GetNames<Texture2D>(new[] { AssetPath.TexturePath?.Trim('/') ?? "" }));
		WriteManifest("Materials", GetNames<Material>(new[] { AssetPath.MaterialPath?.Trim('/') ?? "" }));
		WriteManifest("Skycubes", GetNames<Material>(new[] { AssetPath.SkycubesPath?.Trim('/') ?? "" }));
		WriteManifest("Music", GetNames<AudioClip>(new[] { AssetPath.MusicPath?.Trim('/') ?? "" }));
		WriteManifest("Sounds", GetNames<AudioClip>(new[] { AssetPath.SoundPath?.Trim('/') ?? "" }));

		AssetDatabase.Refresh();
		Debug.Log("<color=cyan>Asset Manifests generated and ready for WebGL!</color>");
	}

	private static void WriteManifest(string manifestName, IEnumerable<string> names)
	{
		string path = $"Assets/Resources/AssetManifests/{manifestName}.txt";
		File.WriteAllLines(path, names.OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase));
	}

	// Re-uses the exact same logic you already have in ResourceUtils (no duplication of scanning code)
	private static IEnumerable<string> GetNames<T>(string[] roots) where T : UnityEngine.Object
	{
		return ResourceUtils.GetAssetNamesFromResources<T>(roots, ""); // manifestName ignored in Editor
	}
}
#endif