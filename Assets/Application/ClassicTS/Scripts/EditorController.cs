using UnityEngine;
using MassiveHadronLtd;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour
	{
		private MapManager mapManager;
		private GameObject gridLines;
		private EditorControllerMovement activeMode;
		private EditorControllerDrag dragMode;
		private EditorControllerPaint paintMode;
		public enum EditorMode { Drag, Paint }
		private PlaceholderEditorUI editorUI;

		// Public getter for paintMode
		public EditorControllerPaint PaintMode => paintMode;

		private void Awake()
		{
			if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

			editorUI = FindAnyObjectByType<PlaceholderEditorUI>();
			if (null == editorUI) Debug.LogWarning("PlaceholderEditorUI not found in scene!");

			GeometryUtil.InitializeGhostMaterial();
		}

		public void Initialise(MapManager map)
		{
			Destroy();

			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.SetCameraSystem(CameraModeRegistry.Editor, true);

			mapManager = map;

			editorUI = FindAnyObjectByType<PlaceholderEditorUI>();
			if (null == editorUI)
			{
				Debug.LogError("PlaceholderEditorUI not found in scene!");
				return;
			}
			var camera = controller.activeSystem?.camera;
			editorUI.Initialize(this, mapManager, camera);

			gridLines = null != mapManager ? GridLinesHelper.CreateGridLines(transform, mapManager.Width, mapManager.Height, 0f, MapManager.tile_origin.x - 0.5f) : null;//workaround for tile offset - should be done properly MapManager.tile_origin.x - 0.5f
			UpdateGridLines(editorUI.GetGridLinesEnabled() & isActiveAndEnabled);

			if (isActiveAndEnabled) OnEnable();
		}

		public void UpdateGridLines(bool value) { if (null != gridLines) gridLines.SetActive(value); }

		private void Update()
		{
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			var camera = controller.activeSystem?.camera;

			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
				GUIUtility.hotControl = 0;

			if (null != activeMode)
				activeMode.Update();

			if (editorUI.currentMode != (activeMode == dragMode ? EditorMode.Drag : EditorMode.Paint))
				SetMode(editorUI.currentMode);
		}

		void OnEnable()
		{
			if (null != editorUI)
			{
				editorUI.enabled = true;
				UpdateGridLines(editorUI.GetGridLinesEnabled());
			}

			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			var camera = controller.activeSystem?.camera;
			dragMode = new EditorControllerDrag(camera);
			paintMode = new EditorControllerPaint(camera, mapManager, "tile_empty");
			activeMode = editorUI.currentMode == EditorMode.Drag ? dragMode : paintMode;
			controller.UpdateGestureControllerState();
		}

		void OnDisable()
		{
			if (null != gridLines) gridLines.SetActive(false); // Only deactivate grid lines, don't reset UI state
			if (null != editorUI) editorUI.enabled = false;
		}

		void Destroy()
		{
			if (null != gridLines) Destroy(gridLines);
		}

		void OnDestroy()
		{
			Destroy();
			GeometryUtil.DestroyGhostTile();
		}

		public void OnApplicationFocus(bool hasFocus)
		{
			if (null != activeMode) activeMode.OnApplicationFocus(hasFocus);
		}

		public void SetMode(EditorMode mode)
		{
			activeMode = mode == EditorMode.Drag ? dragMode : paintMode;
		}

		public void ResizeMapTest(int x = 64, int z = 64)
		{
			if (null == mapManager || null == mapManager.CurrentMap) return;
			//mapManager.CurrentMap.Resize(64, 64, Map.Anchor.Center);
			if (mapManager.CurrentMap.Resize(64, 64, Map.Anchor.Center))
			{
				mapManager.CurrentMap.Consolidate();
				ResourceManager.ApplyMapChanges(mapManager.CurrentMap);
			}
			var main = FindFirstObjectByType<MainController>();
			if (null != main) main.ReloadCurrentMap();
		}

		public void CropMapTest()
		{
			if (null == mapManager || null == mapManager.CurrentMap) return;
			if (mapManager.CurrentMap.CropToContent())
			{
				mapManager.CurrentMap.Consolidate();
				ResourceManager.ApplyMapChanges(mapManager.CurrentMap);
			}
			var main = FindFirstObjectByType<MainController>();
			if (null != main) main.ReloadCurrentMap();
		}

		public void LoadDatabase()
		{
			var dbAsset = PreviewSettings.DatabaseJsonFile;

			if (dbAsset == null)
			{
				Debug.LogError("ResourceManager: DatabaseJsonFile not assigned in PreviewSettings!");
				return;
			}

			var _db = ResourceSerializer.LoadDatabase(dbAsset.text);
			Debug.Log("Database loaded from original project DatabaseJsonFile");
			ResourceManager.database = _db;

			// Optional: auto-reload if same name
			var main = FindFirstObjectByType<MainController>();
			if (null != main) main.ReloadCurrentMap();
		}

		public void SaveDatabase()
		{
#if UNITY_EDITOR
			if (null == ResourceManager.database)
			{
				Debug.LogError("Cannot save: database not loaded");
				return;
			}

			var database = PreviewSettings.DatabaseJsonFile;
			if (database == null)
			{
				Debug.LogError("PreviewSettings.DatabaseJsonFile is not assigned!");
				return;
			}

			string assetPath = AssetDatabase.GetAssetPath(database);
			if (string.IsNullOrEmpty(assetPath) || assetPath.Contains("Resources/unity_builtin_extra"))
			{
				Debug.LogError("Cannot save to project: not a real project asset.");
				return;
			}

			string fullPath = System.IO.Path.GetFullPath(assetPath);
			ResourceSerializer.SaveDatabase(ResourceManager.database, fullPath);
#else
            Debug.Log("Save Database only works in Editor");
#endif
		}

		public void ImportMapAsAtomic()
		{
#if UNITY_EDITOR
			string path = EditorUtility.OpenFilePanel("Import Atomic Map", PreviewSettings.ExportFolder, "json");

			if (!string.IsNullOrEmpty(path))
			{
				ResourceSerializer.ImportAtomicMap(path);

				// Optional: auto-reload if same name
				string importedName = System.IO.Path.GetFileNameWithoutExtension(path);
				if (null != mapManager && mapManager.CurrentMap.name == importedName)
				{ 
					var main = FindFirstObjectByType<MainController>();
					if (null != main) main.ReloadCurrentMap();
				}
			}
#else
    Debug.Log("Import currently only available in Unity Editor");
#endif
		}

		public void ExportMapAsAtomic()
		{
#if UNITY_EDITOR
			if (mapManager?.CurrentMap == null)
			{
				EditorUtility.DisplayDialog("Export Error", "No map is currently loaded.", "OK");
				return;
			}

			var map = mapManager.CurrentMap;
			string originalName = map.name;

			// Start from last remembered folder, fallback to default export folder
			string lastFolder = PlayerPrefs.GetString("ClassicTilestorm_LastExportFolder", PreviewSettingsStatic.ExportFolder);
			System.IO.Directory.CreateDirectory(lastFolder);

			string initialPath = System.IO.Path.Combine(lastFolder, originalName + ".json");
			string path = EditorUtility.SaveFilePanel("Export Map As Atomic JSON", lastFolder, originalName + ".json", "json");

			if (string.IsNullOrEmpty(path))
			{
				Debug.Log("Export cancelled by user.");
				return;
			}

			string chosenFolder = System.IO.Path.GetDirectoryName(path);
			string chosenName = System.IO.Path.GetFileNameWithoutExtension(path);

			// Remember this folder for next time
			PlayerPrefs.SetString("ClassicTilestorm_LastExportFolder", chosenFolder);
			PlayerPrefs.Save();

			bool nameChanged = !string.Equals(originalName, chosenName, System.StringComparison.Ordinal);

			try
			{
				if (nameChanged)
				{
					map.name = chosenName;
					Debug.Log($"Exporting map as: {chosenName}");
				}

				ResourceSerializer.ExportAtomicMap(map, chosenFolder, true);
				EditorUtility.DisplayDialog( "Export Successful", $"Map exported successfully!\n\n→ {path}", "OK");
				Debug.Log($"Map exported: {path}");
			}
			catch (System.Exception ex)
			{
				EditorUtility.DisplayDialog("Export Failed", $"Error during export:\n{ex.Message}", "OK");
				Debug.LogError($"Export failed: {ex}");
			}
			finally
			{
				// Always restore original name — critical!
				if (nameChanged)
					map.name = originalName;
			}

			// Optional: Update the "Locate Export Folder" button to point to the new location
			// (It already uses PreviewSettingsStatic.ExportFolder, but now user can go anywhere)
#else
    Debug.Log("Export currently only available in Unity Editor");
#endif
		}
	}
}