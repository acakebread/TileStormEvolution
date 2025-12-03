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
		private EditorControllerMovement activeMode;
		private EditorControllerDrag dragMode;
		private EditorControllerPaint paintMode;

		private enum EditorMode { Drag, Paint }
		private EditorMode currentMode = EditorMode.Drag;
		private PlaceholderEditorUI editorUI;

		public IMapManager iMapManager => mapManager;
		public PlaceholderEditorUI GetEditorUI() => editorUI;

		private GameObject gridLines;
		private bool gridEnabled = true;

		private void Awake()
		{
			editorUI = gameObject.AddComponent<PlaceholderEditorUI>();
			EditorUtil.InitializeGhostMaterial();

			editorUI.OnModeChanged += HandleModeChanged;
			editorUI.OnGridLinesToggled += EnableGridLines;
			editorUI.OnTileSelected += HandleTileSelected;
			editorUI.OnSaveDatabaseRequested += SaveDatabase;
			editorUI.OnReloadDatabaseRequested += LoadDatabase;
			editorUI.OnExportMapRequested += ExportMapAsAtomic;
			editorUI.OnImportMapRequested += ImportMapAsAtomic;

			dragMode = new EditorControllerDrag(this);
			paintMode = new EditorControllerPaint(this);
		}

		public void Initialise(MapManager map)
		{
			mapManager = map;
			UpdateGridLines();
			if (null != gridLines) gridLines.SetActive(isActiveAndEnabled && gridEnabled);
		}

		private void UpdateGridLines()
		{
			var active = gridLines ? gridLines.activeSelf : false;
			Destroy(gridLines);
			gridLines = GridLinesHelper.CreateGridLines(transform, mapManager ? mapManager.Width : 32, mapManager ? mapManager.Height : 32, extension: 16);
			gridLines.transform.localPosition = MapManager.tile_origin + new Vector3(-0.5f, 0f, -0.5f);
			gridLines.SetActive(active);
		}

		private void EnableGridLines(bool value) { gridEnabled = value; if (null == gridLines) return; gridLines.SetActive(value); }

		void OnEnable()
		{
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.SetCameraSystem(CameraModeRegistry.Editor, true);
			controller.UpdateGestureControllerState();

			editorUI.enabled = true;
			if (null != gridLines) gridLines.SetActive(gridEnabled);

			activeMode = currentMode == EditorMode.Drag ? dragMode : paintMode;
			activeMode.OnEnable();
		}

		void OnDisable()
		{
			if (null != gridLines) gridLines.SetActive(false);
			editorUI.enabled = false;
			EditorUtil.DestroyGhostTile();
		}

		private void Update()
		{
			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
				GUIUtility.hotControl = 0;

			activeMode?.Update();

			if (currentMode == EditorMode.Paint)
				editorUI.UpdatePaintMode();
		}

		private void OnGUI()
		{
			editorUI.DrawMainUI(currentMode.ToString(), gridEnabled);
			if (currentMode == EditorMode.Paint)
				editorUI.DrawPaintUI(paintMode.SelectedDefinitionID);
		}

		public void OnApplicationFocus(bool hasFocus) => activeMode?.OnApplicationFocus(hasFocus);

		private void HandleModeChanged(string mode)
		{
			if (System.Enum.TryParse<EditorMode>(mode, out var newMode) && currentMode != newMode)
			{
				currentMode = newMode;
				activeMode = newMode == EditorMode.Drag ? dragMode : paintMode;
				activeMode.OnEnable();
				if (currentMode != EditorMode.Paint)
					EditorUtil.HideGhostTile();
			}
		}

		private void HandleTileSelected(string defId) => paintMode.SetSelectedDefinitionById(defId);

		void OnDestroy()
		{
			if (null != gridLines) Destroy(gridLines);
			EditorUtil.DestroyGhostTile();

			editorUI.OnModeChanged -= HandleModeChanged;
			editorUI.OnGridLinesToggled -= EnableGridLines;
			editorUI.OnTileSelected -= HandleTileSelected;
			editorUI.OnSaveDatabaseRequested -= SaveDatabase;
			editorUI.OnReloadDatabaseRequested -= LoadDatabase;
			editorUI.OnExportMapRequested -= ExportMapAsAtomic;
			editorUI.OnImportMapRequested -= ImportMapAsAtomic;
		}

		public void OnMapChanged(bool resized, Vector3 originDelta)
		{
			if (mapManager == null) return;

			ResourceManager.ApplyMapChanges(mapManager.CurrentMap);

			if (resized)
			{
				UpdateGridLines();

				// Adjust editor camera to stay glued to map content
				if (originDelta != Vector3.zero)
				{
					if (!TryGetComponent<MainCameraController>(out var controller)) return;
					var editorCam = controller.activeSystem as GameCameraEditor;
					if (null != editorCam) editorCam.camera.transform.position += originDelta;
				}
			}
		}

		public void LoadDatabase()
		{
			var dbAsset = PreviewSettings.DatabaseJsonFile;
			if (dbAsset == null)
			{
				Debug.LogError("PreviewSettings.DatabaseJsonFile is not assigned in PreviewSettings!");
				return;
			}

			ResourceSerializer.Initialise(dbAsset);

			if (ResourceManager.database == null)
			{
				Debug.LogError("Failed to load database from DatabaseJsonFile!");
				return;
			}

			if (TryGetComponent<MainController>(out var main))
			{
				main.ReloadCurrentMap();
			}
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
					if (!TryGetComponent<MainController>(out var main)) return;
					main.ReloadCurrentMap();
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

//public void LoadExternalDatabase()//ToDo make work outside editor
//{
//	var dbAsset = PreviewSettings.DatabaseJsonFile;
//	if (dbAsset == null)
//	{
//		Debug.LogError("PreviewSettings.DatabaseJsonFile is not assigned!");
//		return;
//	}

//	string path = AssetDatabase.GetAssetPath(dbAsset);
//	if (string.IsNullOrEmpty(path))
//	{
//		Debug.LogError("DatabaseJsonFile has no valid path.");
//		return;
//	}

//	var _db = ResourceSerializer.LoadDatabase(path);
//	if (_db == null) return;

//	ResourceManager.database = _db;
//	if (!TryGetComponent<MainController>(out var main)) return;
//	main.ReloadCurrentMap();
//}