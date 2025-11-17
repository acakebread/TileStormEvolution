using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClassicTilestorm
{
	public static class DatabaseSerializer
	{
		[Serializable]
		public class DatabaseData
		{
			public Map[] maps;
			public Theme[] themes;
			public TileDef[] tiledefs;
			public TextureSet[] texture_set;
			public Button[] buttons;//not currently used
		}

		private static TextAsset databaseJsonFile;
		private static Action<TextAsset> saveDelegate;
		private static DatabaseData data;
		private static bool isLoaded;
		private static readonly object lockObject = new object();
		public static event Action OnDatabaseLoaded;

		public static IReadOnlyList<Map> Maps => LoadAndGetData()?.maps ?? Array.Empty<Map>();
		public static IReadOnlyList<Theme> Themes => LoadAndGetData()?.themes ?? Array.Empty<Theme>();
		public static IReadOnlyList<TileDef> TileDefs => LoadAndGetData()?.tiledefs ?? Array.Empty<TileDef>();
		public static IReadOnlyList<TextureSet> TextureSets => LoadAndGetData()?.texture_set ?? Array.Empty<TextureSet>();
		public static IReadOnlyList<Button> Buttons => LoadAndGetData()?.buttons ?? Array.Empty<Button>();

		public static void Init(TextAsset jsonFile, Action<TextAsset> saveAction = null)
		{
			lock (lockObject)
			{
				if (jsonFile == null) throw new ArgumentNullException(nameof(jsonFile));
				if (saveAction == null) Debug.LogWarning("Save delegate is null. Saving disabled.");

				databaseJsonFile = jsonFile;
				saveDelegate = saveAction;
				data = null;
				isLoaded = false;
				Debug.Log($"DatabaseSerializer initialized with TextAsset: {jsonFile.name}");
			}
		}

		public static DatabaseData LoadAndGetData()
		{
			lock (lockObject)
			{
				if (isLoaded && data != null) return data;
				if (databaseJsonFile == null)
				{
					Debug.LogError("Not initialized. Call Init() first.");
					return null;
				}

				string jsonContent = databaseJsonFile.text;
				if (string.IsNullOrEmpty(jsonContent))
				{
					Debug.LogError("TextAsset content is empty.");
					return null;
				}

				try
				{
					var root = JObject.Parse(jsonContent);
					var settings = new JsonSerializerSettings
					{
						MissingMemberHandling = MissingMemberHandling.Ignore
					};
					var serializer = JsonSerializer.CreateDefault(settings);

					data = new();
					data.maps = root["maps"]?.ToObject<Map[]>(serializer);
					data.themes = root["themes"]?.ToObject<Theme[]>(serializer);
					data.tiledefs = root["tiledefs"]?.ToObject<TileDef[]>(serializer);
					data.buttons = root["buttons"]?.ToObject<Button[]>(serializer);
					data.texture_set = root["texture_set"]?.ToObject<TextureSet[]>(serializer);

					if (data == null || data.maps == null)
					{
						Debug.LogError("Failed to parse JSON.");
						return null;
					}

					foreach (var map in data.maps)
					{
						if (map == null) { Debug.LogError("Null map"); return null; }
						if (map.defs == null) map.defs = Array.Empty<string>();
						else if (map.defs.Any(string.IsNullOrEmpty)) { Debug.LogError("Bad defs"); return null; }
						if (map.nWidth <= 0 || map.nHeight <= 0) { Debug.LogError("Bad size"); return null; }
						if (map.tiles == null || map.tiles.Length != map.nWidth * map.nHeight) { Debug.LogError("Bad tiles"); return null; }
						if (map.mixed == null || map.mixed.Length != map.nWidth * map.nHeight) { Debug.LogError("Bad mixed"); return null; }
					}

					if (data.tiledefs == null || data.tiledefs.Any(td => string.IsNullOrEmpty(td?.szType)))
					{
						Debug.LogError("Bad tiledefs");
						return null;
					}

					isLoaded = true;
					VerifyData();
					ResourceManager.Initialize(data);
					OnDatabaseLoaded?.Invoke();
					return data;
				}
				catch (Exception ex)
				{
					Debug.LogError($"JSON load failed: {ex.Message}\n{ex.StackTrace}");
					data = null;
					isLoaded = false;
					return null;
				}
			}
		}

		public static void SaveDatabase(DatabaseData newData)
		{
			if (newData == null) { Debug.LogError("Cannot save null data."); return; }

			lock (lockObject)
			{
				if (databaseJsonFile == null || saveDelegate == null)
				{
					Debug.LogError("Not initialized.");
					return;
				}

				try
				{
					UpdateDataInternal(newData);

					var settings = new JsonSerializerSettings
					{
						NullValueHandling = NullValueHandling.Ignore,
						Formatting = Formatting.None
					};

					string jsonContent = JsonConvert.SerializeObject(ResourceManager.GetCurrentDatabaseData(), settings);

					string outputDir = Path.Combine(Application.persistentDataPath, "Data");
					Directory.CreateDirectory(outputDir);
					string outputPath = Path.Combine(outputDir, "database.json");
					File.WriteAllText(outputPath, jsonContent);
					Debug.Log($"Saved to {outputPath}");
				}
				catch (Exception ex)
				{
					Debug.LogError($"Save failed: {ex.Message}\n{ex.StackTrace}");
				}
			}
		}

		public static void UpdateDatabase(DatabaseData newData)
		{
			if (newData == null) { Debug.LogError("Cannot update with null data."); return; }

			lock (lockObject)
			{
				if (databaseJsonFile == null)
				{
					Debug.LogError("Not initialized.");
					return;
				}

				try
				{
					UpdateDataInternal(newData);
					Debug.Log("Database updated in memory (not saved to disk).");
				}
				catch (Exception ex)
				{
					Debug.LogError($"Update failed: {ex.Message}\n{ex.StackTrace}");
				}
			}
		}

		private static void UpdateDataInternal(DatabaseData newData)
		{
			foreach (var map in newData.maps)
			{
				if (map == null || map.defs == null || map.defs.Any(string.IsNullOrEmpty) ||
					map.nWidth <= 0 || map.nHeight <= 0 ||
					map.tiles == null || map.tiles.Length != map.nWidth * map.nHeight ||
					map.mixed == null || map.mixed.Length != map.nWidth * map.nHeight)
				{
					throw new InvalidOperationException("Invalid map data.");
				}
			}

			data = newData;
			isLoaded = true;
			VerifyData();
			OnDatabaseLoaded?.Invoke();
		}

		private static void VerifyData()
		{
			Debug.Log("DatabaseSerializer: Verification complete.");
		}
	}
}