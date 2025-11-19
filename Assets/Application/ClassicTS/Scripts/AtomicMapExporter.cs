using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	public static class AtomicMapExporter
	{
		private const string ExportFolder = "Maps";

		public static void Export(Map map, string overridePath = null)
		{
			if (map == null || string.IsNullOrEmpty(map.name))
			{
				Debug.LogError("AtomicMapExporter: Cannot export – map is null or has no name.");
				return;
			}

			// 1. Used tile types
			var usedTypes = map.table?
				.Where(t => !string.IsNullOrEmpty(t))
				.Distinct()
				.ToArray() ?? System.Array.Empty<string>();

			// 2. Only used definitions
			var usedDefs = ResourceManager.Definitions
				.Where(d => usedTypes.Contains(d.szType))
				.ToArray();

			// 3. Only used texture banks
			var usedBankNames = usedDefs
				.Where(d => !string.IsNullOrEmpty(d.szBank))
				.Select(d => d.szBank)
				.Distinct()
				.ToArray();

			var usedTextures = ResourceManager.TextureSets
				.Where(ts => usedBankNames.Contains(ts.name))
				.ToArray();

			// 4. Build AtomicMap (uses your existing AtomicMap class!)
			var atomic = new AtomicMap
			{
				map = map,
				definitions = usedDefs,
				textures = usedTextures
			};

			// 5. Serialize nicely
			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Formatting = Formatting.Indented
			};

			string json = JsonConvert.SerializeObject(atomic, settings);

			// 6. Write file
			string folder = string.IsNullOrEmpty(overridePath)
				? Path.Combine(Application.persistentDataPath, ExportFolder)
				: Path.GetDirectoryName(overridePath)!;

			Directory.CreateDirectory(folder);

			string filePath = string.IsNullOrEmpty(overridePath)
				? Path.Combine(folder, $"{map.name}.json")
				: overridePath;

			File.WriteAllText(filePath, json);

			Debug.Log($"[AtomicMap] Exported → {filePath}\n" +
					  $"   Definitions: {usedDefs.Length} | Textures: {usedTextures.Length}");
		}
	}
}