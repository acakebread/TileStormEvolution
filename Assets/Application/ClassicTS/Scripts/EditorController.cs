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
		private EditorControllerMovement activeMode;

		private EditorControllerDrag dragMode;
		private EditorControllerPaint paintMode;
		private EditorControllerWaypoint waypointMode;
		private EditorControllerAttachment attachmentMode;

		public enum EditorMode { Drag, Paint, Waypoint, Attachment }
		private EditorMode? currentMode = null;
		public EditorMode CurrentMode => currentMode ?? EditorMode.Drag;

		// UI state
		private float panelYoffset = 10f;
		private const float margin = 10f;
		private const float spacing = 10f;
		private const float buttonWidth = 135f;
		private const float buttonHeight = 30f;

		private GameObject gridLines;
		private bool gridEnabled = true;

		public IMapManager iMapManager => mapManager;
		public event System.Action<int> OnChangeMapRequested; // delta or 0 for reload

		private void Awake()
		{
			// Modes
			dragMode = new EditorControllerDrag(this);
			paintMode = new EditorControllerPaint(this);
			waypointMode = new EditorControllerWaypoint(this);
			attachmentMode = new EditorControllerAttachment(this); 

			SetEditorMode(EditorMode.Drag);

			// Try to detect bottom panel offset from existing PlaceholderUI (optional compatibility)
			if (TryGetComponent<PlaceholderUI>(out var placeholderUI))
				panelYoffset = placeholderUI.GetPanelBottomY();
		}

		public void Initialise(MapManager map)
		{
			mapManager = map;
			UpdateGridLines();
			if (gridLines != null)
				gridLines.SetActive(isActiveAndEnabled && gridEnabled);

			var eggbotController = GetComponentInChildren<EggbotController>();
			if (null != eggbotController) eggbotController.gameObject.SetActive(!isActiveAndEnabled);

			if (isActiveAndEnabled)
				waypointMode?.OnMapChanged();
		}

		private void UpdateGridLines()
		{
			bool wasActive = gridLines != null && gridLines.activeSelf;
			if (gridLines != null) Destroy(gridLines);

			int width = mapManager ? mapManager.Width : 32;
			int height = mapManager ? mapManager.Height : 32;

			gridLines = GridLinesHelper.CreateGridLines(transform, width, height, extension: 16);
			//gridLines.layer = LayerMask.NameToLayer("ViewGizmos");
			SetLayerRecursively(gridLines.transform, LayerMask.NameToLayer("ViewGizmos"));
			gridLines.transform.localPosition = MapManager.tile_origin + new Vector3(-0.5f, 0f, -0.5f);
			gridLines.SetActive(wasActive);

			void SetLayerRecursively(Transform parent, int layer)
			{
				parent.gameObject.layer = layer;
				foreach (Transform child in parent)
					SetLayerRecursively(child, layer);
			}
		}

		private void OnGridLinesToggled(bool value)
		{
			gridEnabled = value;
			if (gridLines != null) gridLines.SetActive(value);
		}

		private void OnEnable()
		{
			if (TryGetComponent<MainCameraController>(out var controller))
			{
				controller.SetCameraSystem(CameraModeRegistry.Editor, false);
				controller.UpdateGestureControllerState();
			}

			var eggbotController = GetComponentInChildren<EggbotController>(true);
			if (null != eggbotController) eggbotController.gameObject.SetActive(false);

			if (gridLines != null) gridLines.SetActive(gridEnabled);
			activeMode?.OnEnable();
		}

		private void OnDisable()
		{
			activeMode?.OnDisable();
			if (gridLines != null) gridLines.SetActive(false);
			EditorUtil.DestroyGhostTile();

			var eggbotController = GetComponentInChildren<EggbotController>(true);
			if (null != eggbotController) eggbotController.gameObject.SetActive(true);
		}

		private void Update()
		{
			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
				GUIUtility.hotControl = 0;

			activeMode?.Update();
		}

		private void OnGUI()
		{
			DrawMainUI(CurrentMode.ToString(), gridEnabled);
			activeMode?.OnGui();
		}

		public void OnApplicationFocus(bool hasFocus)
		{
			activeMode?.OnApplicationFocus(hasFocus);
		}

		private void SetEditorMode(EditorMode newMode)
		{
			if (currentMode == newMode) return;

			activeMode?.OnDisable();

			currentMode = newMode;

			activeMode = newMode switch
			{
				EditorMode.Drag => dragMode,
				EditorMode.Paint => paintMode,
				EditorMode.Waypoint => waypointMode,
				EditorMode.Attachment => attachmentMode,
				_ => dragMode
			};

			activeMode?.OnEnable();
		}

		// ===================================================================
		// UI & Input Detection (formerly in PlaceholderEditorUI)
		// ===================================================================

		public bool IsGuiControlActive() => GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

		public bool IsMouseOverGui()
		{
			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y;

			// Left column buttons
			float leftY = panelYoffset + spacing;
			if (new Rect(margin, leftY, buttonWidth + 20f, buttonHeight * 9 + spacing * 9).Contains(mousePos))
				return true;

			// Delegate mode-specific GUI detection to the active mode
			if (activeMode != null)
				return activeMode.IsMouseOverModeGui();

			return false;
		}

		private void DrawMainUI(string mode, bool gridVisible)
		{
			float y = panelYoffset + spacing;
			if (GuiUtils.ColoredButton(new Rect(margin, y + 0 * (buttonHeight + spacing), buttonWidth, buttonHeight),gridVisible ? "Hide Grid" : "Show Grid",new Color(0.25f, 0.75f, 0.25f))) OnGridLinesToggled(!gridVisible);

			GUI.contentColor = mode == "Drag" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 1 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Drag")) SetEditorMode(EditorMode.Drag);

			GUI.contentColor = mode == "Paint" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Paint")) SetEditorMode(EditorMode.Paint);

			GUI.contentColor = mode == "Waypoint" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Waypoint")) SetEditorMode(EditorMode.Waypoint);

			GUI.contentColor = CurrentMode == EditorMode.Attachment ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 4 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Attachments")) SetEditorMode(EditorMode.Attachment);

			GUI.contentColor = Color.white;

			if (GuiUtils.ColoredButton(new Rect(margin, y + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Import Map", new Color(0.2f, 0.6f, 1f))) ImportMapAsAtomic();
			if (GuiUtils.ColoredButton(new Rect(margin, y + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Export Map", new Color(0.8f, 0.2f, 0.2f))) ExportMapAsAtomic();
			if (GuiUtils.ColoredButton(new Rect(margin, y + 7 * (buttonHeight + spacing), buttonWidth, buttonHeight), "(Re)Load Database", new Color(0.2f, 0.6f, 1f))) LoadDatabase();
			if (GuiUtils.ColoredButton(new Rect(margin, y + 8 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Save Database", new Color(0.8f, 0.2f, 0.2f))) SaveDatabase();
		}

		// ===================================================================
		// Map / Database actions (unchanged logic)
		// ===================================================================

		public void OnMapChanged(bool resized = false, Vector3 originDelta = default)
		{
			if (mapManager == null) return;

			ResourceManager.ApplyMapChanges(mapManager.CurrentMap);

			if (resized)
			{
				UpdateGridLines();

				if (originDelta != Vector3.zero)
				{
					if (TryGetComponent<MainCameraController>(out var controller))
					{
						if (controller.activeSystem is GameCameraEditor editorCam)
							editorCam.camera.transform.position += originDelta;
					}

					var eggbot = transform.GetComponentInChildren<EggbotController>(true);
					if (null != eggbot) eggbot.OnMapOriginShift(mapManager, originDelta);
				}
			}

			waypointMode?.OnMapChanged();
		}

		private void OnDestroy()
		{
			if (gridLines != null) Destroy(gridLines);
			EditorUtil.DestroyGhostTile();
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
				OnChangeMapRequested?.Invoke(0);
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

				if (mapManager?.CurrentMap != null && mapManager.CurrentMap.name == importedName)
				{
					if (TryGetComponent<MainController>(out var main))
						OnChangeMapRequested?.Invoke(0);
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