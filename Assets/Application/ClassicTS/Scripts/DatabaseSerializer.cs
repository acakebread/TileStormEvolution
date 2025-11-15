using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json; // ← Installed via Package Manager

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
			public string[] defs;
			public Waypoint[] waypoints;
			public int nWidth;
			public int nHeight;
			public int[] tiles;
			public int[] mixed;
			public Pickups Pickups;
			public string szEggbotCostume;
			public string szButtonID;
			public string szMusic;
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
			public VectorData vSrc;

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public VectorData vDst;
		}

		[Serializable]
		public class VectorData
		{
			public float fX;
			public float fY;
			public float fZ;

			public Vector3 ToVector3() => IsValidVector() ? new Vector3(fX, fY, fZ) : Vector3.zero;

			public bool IsValidVector()
			{
				var valid = !float.IsNaN(fX) && !float.IsInfinity(fX) &&
							!float.IsNaN(fY) && !float.IsInfinity(fY) &&
							!float.IsNaN(fZ) && !float.IsInfinity(fZ);
				if (!valid) Debug.LogWarning($"Invalid vector: fX={fX}, fY={fY}, fZ={fZ}");
				return valid;
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
					var settings = new JsonSerializerSettings
					{
						MissingMemberHandling = MissingMemberHandling.Ignore
					};

					data = JsonConvert.DeserializeObject<DatabaseData>(jsonContent, settings);
					if (data == null || data.maps == null)
					{
						Debug.LogError("Failed to parse JSON.");
						return null;
					}

					foreach (var map in data.maps)
					{
						if (map == null) { Debug.LogError("Null map"); return null; }
						if (map.defs == null) map.defs = new string[0];
						else if (map.defs.Any(string.IsNullOrEmpty)) { Debug.LogError("Bad defs"); return null; }
						if (map.nWidth <= 0 || map.nHeight <= 0) { Debug.LogError("Bad size"); return null; }
						if (map.tiles == null || map.tiles.Length != map.nWidth * map.nHeight) { Debug.LogError("Bad tiles"); return null; }
						if (map.mixed == null || map.mixed.Length != map.nWidth * map.nHeight) { Debug.LogError("Bad mixed"); return null; }

						// DO NOT initialize vSrc/vDst here
						// Let them stay null if not in JSON
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
								// CRITICAL: Only keep vSrc/vDst if they are valid AND non-zero
								if (wp.vSrc != null)
								{
									if (!wp.vSrc.IsValidVector() || (wp.vSrc.fX == 0 && wp.vSrc.fY == 0 && wp.vSrc.fZ == 0))
										wp.vSrc = null;
								}

								if (wp.vDst != null)
								{
									if (!wp.vDst.IsValidVector() || (wp.vDst.fX == 0 && wp.vDst.fY == 0 && wp.vDst.fZ == 0))
										wp.vDst = null;
								}
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