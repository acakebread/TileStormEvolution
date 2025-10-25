using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using ClassicTilestorm;

namespace AssetViewerNamespace
{
	public class AssetViewer : MonoBehaviour
	{
		[Header("workaround for inverted .obj meshes")]
		public bool flip = false;
		private Vector3 mapCentre = Vector3.zero;
		private int activeTileCount = 0;

		[SerializeField] private string mapName = "Industrial 01";

		private GameObject pathRoot;
		private bool showCameraPath = false;
		private bool isAnimatingCamera = false;
		private List<WaypointData> cameraWaypoints;
		private int currentWaypointIndex;
		private float animationTimer;
		private float animationDuration = 2f;
		private bool isForward = true;

		private struct WaypointData
		{
			public Vector3 src;
			public Vector3 dst;
			public int tileIndex;
		}

		void Start()
		{
			DatabaseSerializer.Init(PreviewSettings.DatabaseJsonFile); 
			mapName = PreviewSettings.LoadMapName;
			mapCentre = Vector3.zero;
			activeTileCount = 0;
			Initialize();
		}

		void Initialize()
		{
			Debug.Log($"AssetViewer initialized: Maps.Count={DatabaseSerializer.Maps.Count}");
			DisplayMap();
		}

		void OnDestroy()
		{
			if (pathRoot != null)
			{
				Destroy(pathRoot);
			}
		}

		void DisplayMap()
		{
			DatabaseSerializer.Map map = string.IsNullOrEmpty(mapName) ? DatabaseSerializer.Maps.FirstOrDefault() : DatabaseSerializer.Maps.FirstOrDefault(m => m.name == mapName);

			if (map == null)
			{
				Debug.LogError("No map found!");
				return;
			}

			if (map.tiles == null || map.tiles.TileData == null || map.tiles.TileData.bytes == null)
			{
				Debug.LogError($"Invalid tiles data for map {map.name}!");
				return;
			}

			int width = map.tiles.nWidth;
			int height = map.tiles.nHeight;
			int[] tileIndices = map.tiles.TileData.bytes;

			if (tileIndices.Length != width * height)
			{
				Debug.LogError($"Tile indices length ({tileIndices.Length}) does not match grid size ({width}x{height}) for map {map.name}!");
				return;
			}

			GameObject mapRoot = new GameObject($"Map_{map.name}");
			mapRoot.transform.SetParent(transform, false);

			// Collect waypoints
			cameraWaypoints = new List<WaypointData>();
			if (map.waypoints != null && map.waypoints.Length > 0)
			{
				foreach (var wp in map.waypoints)
				{
					if (wp != null && wp.bCamera)
					{
						Vector3 src = new Vector3(wp.vSrc.fX, wp.vSrc.fY, wp.vSrc.fZ);
						Vector3 dst = wp.vDst?.fX != null ? new Vector3(wp.vDst.fX, wp.vDst.fY, wp.vDst.fZ) : src; // Fallback to src if vDst is null
						cameraWaypoints.Add(new WaypointData
						{
							src = src,
							dst = dst,
							tileIndex = wp.nTile
						});
					}
				}
				Debug.Log($"Found {cameraWaypoints.Count} camera waypoints");
			}

			for (int z = 0; z < height; z++)
			{
				for (int x = 0; x < width; x++)
				{
					int index = z * width + x;
					int defIndex = tileIndices[index];

					if (defIndex < 0 || defIndex >= map.defs.Length)
					{
						Debug.LogWarning($"Invalid defIndex {defIndex} at ({x}, {z}) in map {map.name}");
						continue;
					}

					string szType = map.defs[defIndex].szType;
					if ("tile_empty" == szType || "tile_invisible" == szType)
						continue;

					//string szTheme = map.defs[defIndex].szTheme;
					//if (string.IsNullOrEmpty(szType))
					//{
					//	Debug.LogWarning($"Null or empty szType at defIndex {defIndex} in map {map.name}");
					//	continue;
					//}

					//DatabaseSerializer.TileDef tileDef = DatabaseSerializer.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
					DatabaseSerializer.TileDef tileDef = DatabaseSerializer.TileDefs.FirstOrDefault(td => td.szType == szType);
					if (tileDef == null)
					{
						//Debug.LogWarning($"TileDef not found for szType={szType}, szTheme={szTheme} at ({x}, {z}) in map {map.name}");
						Debug.LogWarning($"TileDef not found for szType={szType} at ({x}, {z}) in map {map.name}");
						continue;
					}

					GameObject tileObj = new GameObject($"{tileDef.szType}_{x}_{z}");
					tileObj.transform.SetParent(mapRoot.transform, false);
					tileObj.transform.position = new Vector3(x, 0f, z);
					if (flip)
						tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);

					string geomPath = $"{PreviewSettings.GeometryPath}{tileDef.szGeom}".Replace(".x", "");
					GameObject geomAsset = Resources.Load<GameObject>(geomPath);
					if (geomAsset != null)
					{
						GameObject geomInstance = Instantiate(geomAsset, tileObj.transform);
						geomInstance.transform.localPosition = Vector3.zero;
						geomInstance.name = tileDef.szGeom;

						mapCentre += tileObj.transform.position;
						activeTileCount++;
					}
					else
					{
						Debug.LogWarning($"Geometry not found at {geomPath} for TileDef {tileDef.szType}");
						GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
						cube.transform.SetParent(tileObj.transform, false);
						cube.transform.localPosition = Vector3.zero;
						cube.transform.localScale = Vector3.one * 0.1f;
						cube.name = "Fallback_Cube";
						cube.SetActive(false);
					}

					//TextureSet textureSet = GetTextureForTileDef(tileDef, szTheme);
					//if (textureSet != null && textureSet.frames != null && textureSet.frames.Length > 0)
					//{
					//	TileAnimator animator = tileObj.AddComponent<TileAnimator>();
					//	animator.Initialize(textureSet);
					//}
					var textureFrames = TextureSetManager.GetTextureFrames(tileDef.szTheme);
					if (textureFrames?.Length > 0)
					{
						var animator = tileObj.AddComponent<TextureSetAnimator>();
						animator.Initialize(textureFrames);
					}
					else
					{
						//Debug.LogWarning($"No valid texture set for TileDef {tileDef.szType}, szTheme={szTheme}");
						Debug.LogWarning($"No valid texture set for TileDef {tileDef.szType}");
					}
				}
			}

			Debug.Log($"Displayed map {map.name} with {width}x{height} tiles");

			if (activeTileCount > 0)
				mapCentre /= activeTileCount;
			Camera.main.transform.position = mapCentre + (Vector3.up - Vector3.forward) * 8;
		}

		private DatabaseSerializer.TextureSet GetTextureForTileDef(DatabaseSerializer.TileDef tileDef, string szTheme)
		{
			DatabaseSerializer.Theme theme = DatabaseSerializer.Themes.FirstOrDefault(t => t.name == szTheme || t.szTileTextureSet == szTheme);
			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
			{
				DatabaseSerializer.TextureSet texSet = DatabaseSerializer.TextureSets.FirstOrDefault(ts => ts.name == theme.szTileTextureSet);
				if (texSet != null && texSet.frames != null && texSet.frames.Length > 0)
				{
					Debug.Log($"TextureSet found: {texSet.name}, frames={texSet.frames.Length}");
					return texSet;
				}
			}

			DatabaseSerializer.TextureSet fallbackSet = DatabaseSerializer.TextureSets.FirstOrDefault(ts => ts.name == szTheme);
			if (fallbackSet != null && fallbackSet.frames != null && fallbackSet.frames.Length > 0)
			{
				Debug.Log($"Fallback TextureSet: {fallbackSet.name}, frames={fallbackSet.frames.Length}");
				return fallbackSet;
			}

			return null;
		}

		private float SigmoidEase(float t)
		{
			float k = 10f;
			return 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));
		}

		void Update()
		{
			if (isAnimatingCamera && cameraWaypoints != null && cameraWaypoints.Count > 1)
			{
				animationTimer += Time.deltaTime;
				float t = Mathf.Clamp01(animationTimer / animationDuration);
				float easedT = SigmoidEase(t);

				int nextIndex = isForward ? currentWaypointIndex + 1 : currentWaypointIndex - 1;

				if (isForward && nextIndex >= cameraWaypoints.Count)
				{
					isForward = false;
					nextIndex = cameraWaypoints.Count - 2;
					animationTimer = 0;
					t = 0;
					easedT = 0;
				}
				else if (!isForward && nextIndex < 0)
				{
					isForward = true;
					nextIndex = 1;
					animationTimer = 0;
					t = 0;
					easedT = 0;
				}

				var wpCurrent = cameraWaypoints[currentWaypointIndex];
				var wpNext = cameraWaypoints[nextIndex];

				Camera.main.transform.position = Vector3.Lerp(wpCurrent.src, wpNext.src, easedT);
				Vector3 lookAt = Vector3.Lerp(wpCurrent.dst, wpNext.dst, easedT);
				Camera.main.transform.LookAt(lookAt);

				if (t >= 1f)
				{
					animationTimer = 0;
					currentWaypointIndex = nextIndex;
				}
			}
		}

		void DrawCameraPaths()
		{
			if (pathRoot != null)
			{
				Destroy(pathRoot);
			}
			if (!showCameraPath || cameraWaypoints == null || cameraWaypoints.Count == 0)
			{
				return;
			}

			pathRoot = new GameObject("CameraPaths");
			pathRoot.transform.SetParent(transform, false);

			for (int i = 0; i < cameraWaypoints.Count; i++)
			{
				var wp = cameraWaypoints[i];

				GameObject srcSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				srcSphere.transform.SetParent(pathRoot.transform, false);
				srcSphere.transform.position = wp.src;
				srcSphere.transform.localScale = Vector3.one * 0.3f;
				srcSphere.GetComponent<Renderer>().material.color = Color.green;

				GameObject dstSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				dstSphere.transform.SetParent(pathRoot.transform, false);
				dstSphere.transform.position = wp.dst;
				dstSphere.transform.localScale = Vector3.one * 0.3f;
				dstSphere.GetComponent<Renderer>().material.color = Color.red;

				if (i < cameraWaypoints.Count - 1)
				{
					var nextWp = cameraWaypoints[i + 1];
					GameObject lineObj = new GameObject($"Path_{i}");
					lineObj.transform.SetParent(pathRoot.transform, false);
					LineRenderer lr = lineObj.AddComponent<LineRenderer>();
					lr.material = new Material(Shader.Find("Sprites/Default"));
					lr.startColor = Color.cyan;
					lr.endColor = Color.cyan;
					lr.startWidth = 0.1f;
					lr.endWidth = 0.1f;
					lr.positionCount = 2;
					lr.SetPosition(0, wp.src);
					lr.SetPosition(1, nextWp.src);
				}
			}
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;

			if (GUI.Button(new Rect(10, 10, 100, 30), "Reload"))
			{
				foreach (Transform child in transform)
				{
					Destroy(child.gameObject);
				}
				Initialize();
			}

			if (GUI.Button(new Rect(120, 10, 150, 30), showCameraPath ? "Hide Path" : "Show Path"))
			{
				showCameraPath = !showCameraPath;
				DrawCameraPaths();
			}

			if (showCameraPath && GUI.Button(new Rect(280, 10, 150, 30), isAnimatingCamera ? "Stop Anim" : "Play Anim"))
			{
				if (isAnimatingCamera)
				{
					isAnimatingCamera = false;
					Debug.Log($"Animation stopped at WP {currentWaypointIndex}: pos={Camera.main.transform.position}");
				}
				else if (cameraWaypoints != null && cameraWaypoints.Count > 1)
				{
					isAnimatingCamera = true;
					currentWaypointIndex = 0;
					animationTimer = 0;
					isForward = true;
				}
			}
		}
	}
}