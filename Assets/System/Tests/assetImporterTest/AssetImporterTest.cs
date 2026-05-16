using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class AssetImporterTest : MonoBehaviour
	{
		[Header("Asset Importer Test")]
		[Tooltip("Last successfully imported model folder")]
		public string lastImportedPath;
		[Tooltip("How imported Wavefront assets are organized on disk")]
		public WavefrontAssetImporter.ImportOption importOption = WavefrontAssetImporter.ImportOption.HashIdFolder;

		private void OnGUI()
		{
			GUILayout.BeginArea(new Rect(20, 20, 400, 300));
			GUILayout.Label("=== Asset Importer Test ===", GUILayout.Height(30));

			if (GUILayout.Button("Import .OBJ + Dependencies", GUILayout.Height(50)))
			{
				ImportObjFile();
			}

			if (!string.IsNullOrEmpty(lastImportedPath))
			{
				GUILayout.Space(10);
				GUILayout.Label("Last Imported:", GUILayout.Height(20));
				GUILayout.Label(lastImportedPath, GUILayout.Height(40));

				if (GUILayout.Button("Open Imported Folder", GUILayout.Height(30)))
				{
					OpenImportedFolder();
				}
			}

			GUILayout.EndArea();
		}

		public void ImportObjFile()
		{
#if UNITY_EDITOR
			string path = UnityEditor.EditorUtility.OpenFilePanel(
				"Select Wavefront .obj file",
				"",
				"obj");

			if (!string.IsNullOrEmpty(path))
			{
				lastImportedPath = WavefrontAssetImporter.ImportWavefrontModel(path, importOption);

				if (!string.IsNullOrEmpty(lastImportedPath))
				{
					Debug.Log($"✅ Import successful!\nPath: {lastImportedPath}");
				}
			}
#else
            Debug.LogWarning("File dialog is only available in the Unity Editor for now.");
#endif
		}

		private void OpenImportedFolder()
		{
#if UNITY_EDITOR
			if (!string.IsNullOrEmpty(lastImportedPath))
			{
				string folder = Path.GetDirectoryName(lastImportedPath);
				UnityEditor.EditorUtility.RevealInFinder(folder);
			}
#endif
		}
	}
}


//using System.IO;
//using MassiveHadronLtd.FileBrowserUtil;
//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public class AssetImporterTest : MonoBehaviour
//	{
//		[Header("Asset Importer Test")]
//		[Tooltip("Last successfully imported model folder")]
//		public string lastImportedPath;
//		[Tooltip("How imported Wavefront assets are organized on disk")]
//		public WavefrontAssetImporter.ImportOption importOption = WavefrontAssetImporter.ImportOption.HashIdFolder;

//		private void OnGUI()
//		{
//			GUILayout.BeginArea(new Rect(20, 20, 400, 300));
//			GUILayout.Label("=== Asset Importer Test ===", GUILayout.Height(30));

//			if (GUILayout.Button("Import .OBJ + Dependencies", GUILayout.Height(50)))
//			{
//				ImportObjFile();
//			}

//			if (!string.IsNullOrEmpty(lastImportedPath))
//			{
//				GUILayout.Space(10);
//				GUILayout.Label("Last Imported:", GUILayout.Height(20));
//				GUILayout.Label(lastImportedPath, GUILayout.Height(40));

//				if (GUILayout.Button("Open Imported Folder", GUILayout.Height(30)))
//				{
//					OpenImportedFolder();
//				}
//			}

//			GUILayout.EndArea();
//		}

//		public void ImportObjFile()
//		{
//			RuntimeFileBrowser.OpenObjFile(
//				"Select Wavefront .obj file",
//				path =>
//				{
//					lastImportedPath = WavefrontAssetImporter.ImportWavefrontModel(path, importOption);

//					if (!string.IsNullOrEmpty(lastImportedPath))
//					{
//						Debug.Log($"✅ Import successful!\nPath: {lastImportedPath}");
//					}
//				},
//				RuntimeFileBrowser.GetDefaultRootFolder());
//		}

//		private void OpenImportedFolder()
//		{
//#if UNITY_EDITOR
//			if (!string.IsNullOrEmpty(lastImportedPath))
//			{
//				string folder = Path.GetDirectoryName(lastImportedPath);
//				UnityEditor.EditorUtility.RevealInFinder(folder);
//			}
//#endif
//		}
//	}
//}
