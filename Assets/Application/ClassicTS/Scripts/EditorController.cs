using UnityEngine;
using MassiveHadronLtd;

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

		private EditorMode currentMode = EditorMode.Drag;
		private bool gridLinesEnabled = true;
		private string selectedDefinitionId = "tile_empty";

		private PlaceholderEditorUI editorUI;

		public EditorControllerPaint PaintMode => paintMode;
		public PlaceholderEditorUI GetEditorUI() => editorUI;

		private void Awake()
		{
			editorUI = gameObject.AddComponent<PlaceholderEditorUI>();
			GeometryUtil.InitializeGhostMaterial();

			editorUI.OnModeChanged += HandleModeChanged;
			editorUI.OnGridLinesToggled += UpdateGridLines;
			editorUI.OnTileSelected += HandleTileSelected;
			editorUI.OnSaveDatabaseRequested += SaveDatabase;
			editorUI.OnReloadDatabaseRequested += LoadDatabase;
			editorUI.OnExportMapRequested += ExportMapAsAtomic;
			editorUI.OnImportMapRequested += ImportMapAsAtomic;
			editorUI.OnResizeMapTestRequested += () => ResizeMapTest(64, 64);
			editorUI.OnCropMapTestRequested += CropMapTest;
		}

		public void Initialise(MapManager map)
		{
			Destroy();

			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.SetCameraSystem(CameraModeRegistry.Editor, true);

			mapManager = map;

			var placeholderUI = FindAnyObjectByType<PlaceholderUI>();
			float bottomY = placeholderUI ? placeholderUI.GetPanelBottomY() : 10f;
			editorUI.Initialize(bottomY);

			gridLines = mapManager != null
				? GridLinesHelper.CreateGridLines(transform, mapManager.Width, mapManager.Height, 0f, MapManager.tile_origin.x - 0.5f)
				: null;

			UpdateGridLines(gridLinesEnabled);

			if (isActiveAndEnabled) OnEnable();
		}

		public void UpdateGridLines(bool value)
		{
			if (gridLines != null) gridLines.SetActive(value);
			gridLinesEnabled = value;
		}

		private void Update()
		{
			if (!TryGetComponent<MainCameraController>(out var _)) return;

			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
				GUIUtility.hotControl = 0;

			activeMode?.Update();

			if (currentMode == EditorMode.Paint)
				editorUI.UpdatePaintMode();
		}

		private void OnGUI()
		{
			editorUI.DrawMainUI(currentMode.ToString(), gridLinesEnabled);
			if (currentMode == EditorMode.Paint)
				editorUI.DrawPaintUI(selectedDefinitionId);
		}

		void OnEnable()
		{
			editorUI.enabled = true;
			UpdateGridLines(gridLinesEnabled);

			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			var camera = controller.activeSystem?.camera;

			dragMode = new EditorControllerDrag(camera, this);
			paintMode = new EditorControllerPaint(camera, mapManager, this, selectedDefinitionId);

			activeMode = currentMode == EditorMode.Drag ? dragMode : paintMode;
			controller.UpdateGestureControllerState();
		}

		void OnDisable()
		{
			if (gridLines != null) gridLines.SetActive(false);
			editorUI.enabled = false;
		}

		private void HandleModeChanged(string mode)
		{
			if (System.Enum.TryParse<EditorMode>(mode, out var newMode) && currentMode != newMode)
				SetMode(newMode);
		}

		private void HandleTileSelected(Definition def)
		{
			selectedDefinitionId = def.id;
			paintMode.SetSelectedDefinition(def);
		}

		public void SetMode(EditorMode mode)
		{
			currentMode = mode;
			activeMode = mode == EditorMode.Drag ? dragMode : paintMode;
			if (currentMode == EditorMode.Drag)
				GeometryUtil.HideGhostTile();
		}

		void Destroy()
		{
			if (gridLines != null)
			{
				Destroy(gridLines);
				gridLines = null;
			}
		}

		void OnDestroy()
		{
			Destroy();
			GeometryUtil.DestroyGhostTile();

			editorUI.OnModeChanged -= HandleModeChanged;
			editorUI.OnGridLinesToggled -= UpdateGridLines;
			editorUI.OnTileSelected -= HandleTileSelected;
			editorUI.OnSaveDatabaseRequested -= SaveDatabase;
			editorUI.OnReloadDatabaseRequested -= LoadDatabase;
			editorUI.OnExportMapRequested -= ExportMapAsAtomic;
			editorUI.OnImportMapRequested -= ImportMapAsAtomic;
			editorUI.OnResizeMapTestRequested -= () => ResizeMapTest(64, 64);
			editorUI.OnCropMapTestRequested -= CropMapTest;
		}

		public void ResizeMapTest(int x = 64, int z = 64)
		{
			if (mapManager == null || mapManager.CurrentMap == null) return;
			if (mapManager.CurrentMap.Resize(x, z, Map.Anchor.Center))
			{
				mapManager.CurrentMap.Consolidate();
				ResourceManager.ApplyMapChanges(mapManager.CurrentMap);
			}
			var main = FindFirstObjectByType<MainController>();
			if (main != null) main.ReloadCurrentMap();
		}

		public void CropMapTest()
		{
			if (mapManager == null || mapManager.CurrentMap == null) return;
			if (mapManager.CurrentMap.CropToContent())
			{
				mapManager.CurrentMap.Consolidate();
				ResourceManager.ApplyMapChanges(mapManager.CurrentMap);
			}
			var main = FindFirstObjectByType<MainController>();
			if (main != null) main.ReloadCurrentMap();
		}

		public void LoadDatabase()
		{
			var dbAsset = PreviewSettings.DatabaseJsonFile;
			if (dbAsset == null)
			{
				Debug.LogError("PreviewSettings.DatabaseJsonFile is not assigned!");
				return;
			}

			string path = AssetDatabase.GetAssetPath(dbAsset);
			if (string.IsNullOrEmpty(path))
			{
				Debug.LogError("DatabaseJsonFile has no valid path.");
				return;
			}

			var _db = ResourceSerializer.LoadDatabase(path);
			if (_db == null) return;

			ResourceManager.database = _db;
			var main = FindFirstObjectByType<MainController>();
			if (main != null) main.ReloadCurrentMap();
		}

		public void SaveDatabase()
		{
#if UNITY_EDITOR
			if (ResourceManager.database == null)
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
				string importedName = System.IO.Path.GetFileNameWithoutExtension(path);
				if (mapManager != null && mapManager.CurrentMap != null && mapManager.CurrentMap.name == importedName)
				{
					var main = FindFirstObjectByType<MainController>();
					if (main != null) main.ReloadCurrentMap();
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
			PlayerPrefs.SetString("ClassicTilestorm_LastExportFolder", chosenFolder);
			PlayerPrefs.Save();

			bool nameChanged = !string.Equals(originalName, chosenName, System.StringComparison.Ordinal);

			try
			{
				if (nameChanged) map.name = chosenName;
				ResourceSerializer.ExportAtomicMap(map, chosenFolder, true);
				EditorUtility.DisplayDialog("Export Successful", $"Map exported successfully!\n\nPath: {path}", "OK");
				Debug.Log($"Map exported: {path}");
			}
			catch (System.Exception ex)
			{
				EditorUtility.DisplayDialog("Export Failed", $"Error during export:\n{ex.Message}", "OK");
				Debug.LogError($"Export failed: {ex}");
			}
			finally
			{
				if (nameChanged) map.name = originalName;
			}
#else
            Debug.Log("Export currently only available in Unity Editor");
#endif
		}
	}
}