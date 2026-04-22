using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClassicTilestorm.Editor
{
	public class GeometryReadWriteImporter : AssetPostprocessor
	{
		private static readonly string[] GeometryRoots =
		{
			"Assets/Application/ClassicTS/Resources/ClassicTS/Geometry/",
			"Assets/System/Resources/Geometry/",
			"Assets/Application/Production/Resources/Levels/",
			"Assets/Art/Levels/"
		};

		private static bool IsGeometryModelPath(string path)
		{
			if (string.IsNullOrEmpty(path)) return false;

			var normalized = path.Replace('\\', '/');
			if (!(normalized.EndsWith(".fbx") || normalized.EndsWith(".obj")))
				return false;

			return GeometryRoots.Any(root => normalized.StartsWith(root));
		}

		void OnPreprocessModel()
		{
			if (!IsGeometryModelPath(assetPath))
				return;

			if (assetImporter is not ModelImporter importer)
				return;

			if (!importer.isReadable)
				importer.isReadable = true;
		}

		[MenuItem("Tools/ClassicTS/Geometry/Enable ReadWrite On Geometry Models")]
		private static void EnableReadWriteOnGeometryModels()
		{
			var modelPaths = AssetDatabase
				.FindAssets("t:Model")
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where(IsGeometryModelPath)
				.ToArray();

			var changed = 0;
			foreach (var path in modelPaths)
			{
				if (AssetImporter.GetAtPath(path) is not ModelImporter importer || importer.isReadable)
					continue;

				importer.isReadable = true;
				importer.SaveAndReimport();
				changed++;
			}

			Debug.Log($"GeometryReadWriteImporter: enabled Read/Write on {changed} model(s).");
		}
	}
}
