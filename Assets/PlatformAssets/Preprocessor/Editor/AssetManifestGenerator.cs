#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;

public class AssetManifestGenerator : IPreprocessBuildWithReport
{
	public int callbackOrder => 0;

	public void OnPreprocessBuild(BuildReport report)
	{
		if (report.summary.platform == BuildTarget.WebGL)
			GenerateAllManifests();
	}

	[MenuItem("Tools/Generate Asset Manifests %&M")]
	public static void GenerateAllManifests()
	{
		// Ensure the manifest folder exists inside Resources
		const string manifestFolder = "Assets/Resources/AssetManifests";
		if (!Directory.Exists(manifestFolder))
			Directory.CreateDirectory(manifestFolder);

		// Call Initialize so the roots are registered (including any dev remapping paths)
		AssetConfiguration.Initialize();

		WriteManifest("Models", CollectNames<GameObject>(AssetRegistry<GameObject>.GetRegisteredModelRoots()));
		WriteManifest("Prefabs", CollectNames<GameObject>(AssetRegistry<GameObject>.GetRegisteredPrefabRoots()));
		WriteManifest("Textures", CollectNames<Texture>(AssetRegistry<Texture>.GetRegisteredTextureRoots()));
		WriteManifest("Texture2Ds", CollectNames<Texture2D>(AssetRegistry<Texture2D>.GetRegisteredTexture2DRoots()));
		WriteManifest("Materials", CollectNames<Material>(AssetRegistry<Material>.GetRegisteredMaterialRoots()));
		WriteManifest("Skycubes", CollectNames<Material>(AssetRegistry<Material>.GetRegisteredSkyboxRoots()));
		WriteManifest("Sounds", CollectNames<AudioClip>(AssetRegistry<AudioClip>.GetRegisteredSoundRoots()));
		WriteManifest("Music", CollectNames<AudioClip>(AssetRegistry<AudioClip>.GetRegisteredMusicRoots()));

		AssetDatabase.Refresh();
		Debug.Log("<color=cyan>Asset Manifests generated successfully for WebGL!</color>");
	}

	private static void WriteManifest(string manifestName, IEnumerable<string> names)
	{
		string path = $"Assets/Resources/AssetManifests/{manifestName}.txt";
		File.WriteAllLines(path, names.OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase));
	}

	// Helper to collect names using the same fast scanner we use at runtime
	private static IEnumerable<string> CollectNames<T>(IEnumerable<string> roots) where T : UnityEngine.Object
	{
		// Pass empty string as manifestName – ignored in Editor
		return ResourceUtils.GetAssetNamesFromResources<T>(roots.ToArray(), "");
	}
}
#endif