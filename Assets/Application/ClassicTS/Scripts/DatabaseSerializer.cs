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
			public Button[] buttons;
			public TextureSet[] texture_set;
		}

		[Serializable]
		public class Map
		{
			public string name;
			public bool bLightTiles;

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public string[] defs;

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public Waypoint[] waypoints;

			public int nWidth;
			public int nHeight;
			public int[] tiles;
			public int[] mixed;

			public Pickups Pickups;

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public string szEggbotCostume;

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public string szButtonID;

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public string szMusic;

			public bool ShouldSerializePickups()
			{
				return Pickups != null && Pickups.nPickupCount > 0;
			}
		}

		[Serializable]
		public class TileDef
		{
			public string szTheme;
			public string szType;
			public string szGeom;
			public bool bSlide;
			public bool bRoll;
			public bool bDock;
			public bool bConsole;
			public bool bDoor;
			public bool bStart;
			public bool bEnd;
			public int nPickup;
			public bool bPuzzleBlock;
			public bool bNorth;
			public bool bSouth;
			public bool bEast;
			public bool bWest;
		}

		[Serializable]
		public class Waypoint
		{
			public string name;
			public int nTile;

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public float[] vSrc;

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public float[] vDst;

			public Vector3 GetVSrc() => vSrc != null && vSrc.Length == 3 && IsValid(vSrc) ? new Vector3(vSrc[0], vSrc[1], vSrc[2]) : Vector3.zero;
			public Vector3 GetVDst() => vDst != null && vDst.Length == 3 && IsValid(vDst) ? new Vector3(vDst[0], vDst[1], vDst[2]) : Vector3.zero;

			private bool IsValid(float[] v)
			{
				return !float.IsNaN(v[0]) && !float.IsInfinity(v[0]) &&
					   !float.IsNaN(v[1]) && !float.IsInfinity(v[1]) &&
					   !float.IsNaN(v[2]) && !float.IsInfinity(v[2]);
			}

			public void SetVSrc(Vector3 vec)
			{
				vSrc = vec == Vector3.zero ? null : new[] { vec.x, vec.y, vec.z };
			}

			public void SetVDst(Vector3 vec)
			{
				vDst = vec == Vector3.zero ? null : new[] { vec.x, vec.y, vec.z };
			}
		}

		[Serializable]
		public class Pickups
		{
			public int nPickupCount;
		}

		[Serializable]
		public class Theme
		{
			public string name;
			public string szTileTextureSet;
		}

		[Serializable]
		public class Button
		{
			public string name;
			public string szTexture;
			public string szButtonText;
			public float fWidth;
			public float fHeight;
			public float fUVupX;
			public float fUVupY;
			public float fUVupW;
			public float fUVupH;
			public float fUVdownX;
			public float fUVdownY;
			public float fUVdownW;
			public float fUVdownH;
		}

		[Serializable]
		public class TextureSet
		{
			public string name;
			public bool bAlphaTest;
			public TextureFrame[] frames;
		}

		[Serializable]
		public class TextureFrame
		{
			public string name;
			public string szTexture;
			public float fDuration;
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
		public static IReadOnlyList<Button> Buttons => LoadAndGetData()?.buttons ?? Array.Empty<Button>();
		public static IReadOnlyList<TextureSet> TextureSets => LoadAndGetData()?.texture_set ?? Array.Empty<TextureSet>();

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

		private static DatabaseData LoadAndGetData()
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

					data = new DatabaseData();
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

						if (map.waypoints != null)
						{
							foreach (var wp in map.waypoints)
							{
								wp.vSrc = ParseVector(root, map, wp, "vSrc");
								wp.vDst = ParseVector(root, map, wp, "vDst");

								if (wp.vSrc != null && (wp.vSrc[0] == 0 && wp.vSrc[1] == 0 && wp.vSrc[2] == 0))
									wp.vSrc = null;
								if (wp.vDst != null && (wp.vDst[0] == 0 && wp.vDst[1] == 0 && wp.vDst[2] == 0))
									wp.vDst = null;
							}
						}
					}

					if (data.tiledefs == null || data.tiledefs.Any(td => string.IsNullOrEmpty(td?.szType)))
					{
						Debug.LogError("Bad tiledefs");
						return null;
					}

					isLoaded = true;
					VerifyData();
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

		private static float[] ParseVector(JObject root, Map map, Waypoint wp, string field)
		{
			var mapToken = root["maps"]?.Children<JObject>().FirstOrDefault(m => m["name"]?.ToString() == map.name);
			if (mapToken == null) return null;

			var wpToken = mapToken["waypoints"]?.Children<JObject>().FirstOrDefault(w => w["name"]?.ToString() == wp.name);
			if (wpToken == null) return null;

			var vecToken = wpToken[field];
			if (vecToken == null || vecToken.Type == JTokenType.Null) return null;

			if (vecToken.Type == JTokenType.Array)
			{
				var arr = (JArray)vecToken;
				if (arr.Count != 3) { Debug.LogWarning($"Invalid vector array for {field} in {wp.name}: expected 3 values."); return null; }
				return new float[] { arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>() };
			}

			if (vecToken.Type == JTokenType.Object)
			{
				Debug.LogError($"Legacy vector format {{fX,fY,fZ}} detected in {wp.name}.{field}. This is no longer supported. Convert your data to [x,y,z].");
				return null;
			}

			Debug.LogWarning($"Invalid vector format for {field} in {wp.name}: {vecToken.Type}");
			return null;
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
					foreach (var map in newData.maps)
					{
						if (map == null || map.defs == null || map.defs.Any(string.IsNullOrEmpty) ||
							map.nWidth <= 0 || map.nHeight <= 0 ||
							map.tiles == null || map.tiles.Length != map.nWidth * map.nHeight ||
							map.mixed == null || map.mixed.Length != map.nWidth * map.nHeight)
						{
							Debug.LogError("Invalid map data for save.");
							return;
						}

						if (map.waypoints != null)
						{
							foreach (var wp in map.waypoints)
							{
								if (wp.vSrc != null && (wp.vSrc[0] == 0 && wp.vSrc[1] == 0 && wp.vSrc[2] == 0))
									wp.vSrc = null;
								if (wp.vDst != null && (wp.vDst[0] == 0 && wp.vDst[1] == 0 && wp.vDst[2] == 0))
									wp.vDst = null;
							}
						}
					}

					var settings = new JsonSerializerSettings
					{
						NullValueHandling = NullValueHandling.Ignore,
						Formatting = Formatting.None
					};

					string jsonContent = JsonConvert.SerializeObject(newData, settings);

					string outputDir = Path.Combine(Application.persistentDataPath, "Data");
					Directory.CreateDirectory(outputDir);
					string outputPath = Path.Combine(outputDir, "database.json");
					File.WriteAllText(outputPath, jsonContent);
					Debug.Log($"Saved to {outputPath}");

					data = newData;
					isLoaded = true;
					VerifyData();
					OnDatabaseLoaded?.Invoke();
				}
				catch (Exception ex)
				{
					Debug.LogError($"Save failed: {ex.Message}\n{ex.StackTrace}");
				}
			}
		}

		private static void VerifyData()
		{
			Debug.Log("DatabaseSerializer: Verification complete.");
		}
	}
}