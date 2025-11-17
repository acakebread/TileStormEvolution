// ResourceSerializer.cs — MINIFIED OUTPUT + NO WRAPPER
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	public static class ResourceSerializer
	{
		private static readonly string MapsFolder = Path.Combine(Application.streamingAssetsPath, "Maps");

		static ResourceSerializer()
		{
			if (!Directory.Exists(MapsFolder))
				Directory.CreateDirectory(MapsFolder);
		}

		public static Map[] TryLoadIndividualMaps()
		{
			if (!Directory.Exists(MapsFolder))
				return Array.Empty<Map>();

			string[] files = Directory.GetFiles(MapsFolder, "*.json", SearchOption.TopDirectoryOnly);
			var maps = new List<Map>();

			foreach (string file in files)
			{
				try
				{
					string json = File.ReadAllText(file);
					Map map = JsonConvert.DeserializeObject<Map>(json);

					if (map != null)
					{
						if (string.IsNullOrEmpty(map.name))
							map.name = Path.GetFileNameWithoutExtension(file);

						maps.Add(map);
					}
				}
				catch (Exception ex)
				{
					Debug.LogError($"[ResourceSerializer] Failed to load {file}: {ex.Message}");
				}
			}

			return maps.ToArray();
		}

		public static bool SaveMap(Map map)
		{
			if (map == null || string.IsNullOrEmpty(map.name))
				return false;

			try
			{
				string safeName = string.Join("_", map.name.Split(Path.GetInvalidFileNameChars()));
				string path = Path.Combine(MapsFolder, safeName + ".json");

				// MINIFIED OUTPUT
				string json = JsonConvert.SerializeObject(map, Formatting.None);
				File.WriteAllText(path, json);

				Debug.Log($"[ResourceSerializer] Saved (minified): {map.name}");
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[ResourceSerializer] Save failed: {ex.Message}");
				return false;
			}
		}
	}
}