using UnityEngine;
using UnityEngine.EventSystems;
using MassiveHadronLtd;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public class GameCameraEditor : CameraBase
	{
		public MapManager mapManager;
		private GameCameraEditorMovement activeMode;
		private GameCameraEditorDrag dragMode;
		private GameCameraEditorPaint paintMode;
		private enum EditorMode { Drag, Paint }
		private EditorMode currentMode = EditorMode.Drag;
		private Vector2 scrollPosition = Vector2.zero;
		private int selectedMapDefIndex = 0; // Index into mapDefs
		private int tempSelectedTileDefGlobalIndex = 0; // Index into DatabaseSerializer.TileDefs
		private PlaceholderUI placeholderUI; // Reference to PlaceholderUI
		private float tileSelectorWidth = 120f; // Current width, start collapsed
		private readonly float fullWidth = 300f; // Expanded width
		private readonly float collapsedWidth = 120f; // Collapsed width
		private float mouseExitTime = 0f; // Time when mouse last exited tile selector
		private bool isMouseOverTileSelector; // Track if mouse is over tile selector
		private readonly float autoHideDelay = 1f; // 1 second delay for auto-hide
		private float targetWidth; // Target width for animation
		private float animationStartTime; // Time when animation started
		private readonly float animationDuration = 0.3f; // Duration for width animation
		private Vector3 mouseDownPos; // Mouse position on RMB down for delete
		private int lastClickedMapIndex = -1; // Last clicked tile index for cycling
		private List<int> tileDefCycleList; // List of TileDef indices for cycling
		private int cycleIndex = 0; // Current position in cycle list
		private GameObject gridLinesObject; // GameObject for LineRenderer grid
		private LineRenderer gridLineRenderer; // LineRenderer for grid lines
		private bool gridLinesEnabled = true; // Toggle for grid lines

		public GameCameraEditor(Camera camera) : base(camera) { }

		public override void Awake()
		{
			base.Awake();

			var cameraTransform = camera.transform;
			cameraTransform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);

			dragMode = new GameCameraEditorDrag(camera);
			paintMode = new GameCameraEditorPaint(camera, mapManager, selectedMapDefIndex);
			activeMode = dragMode;
			targetWidth = collapsedWidth; // Initialize to collapsed
		}

		public override void Start()
		{
			base.Start();
			camera.fieldOfView = 60f;
			postProcessingEnabled = false;
			dragMode.Initialize();
			paintMode.Initialize();

			if (!Object.FindAnyObjectByType<EventSystem>())
				new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

			placeholderUI = Object.FindAnyObjectByType<PlaceholderUI>();
			if (placeholderUI == null)
				Debug.LogWarning("PlaceholderUI not found in scene!");

			// Initialize ghost material
			GeometryUtil.InitializeGhostMaterial();

			// Initialize grid lines
			InitializeGridLines();
		}

		private void InitializeGridLines()
		{
			if (gridLinesObject != null)
			{
				Object.Destroy(gridLinesObject);
			}

			gridLinesObject = new GameObject("GridLines");
			gridLinesObject.transform.SetParent(mapManager.transform, false);

			float y = 0f; // Map at y=0
			int width = mapManager.Width;
			int height = mapManager.Height;
			float offset = 0f; // Adjust to 0 if tiles are at integer coords, 0.5 if centered

			// Create vertical lines (along X)
			for (int x = 0; x <= width; x++)
			{
				float xPos = x + offset;
				var lineObj = new GameObject($"VerticalLine_{x}");
				lineObj.transform.SetParent(gridLinesObject.transform, false);
				var lr = lineObj.AddComponent<LineRenderer>();
				lr.material = MaterialUtils.CreateOpaqueUnlitMaterial(new Color(0.25f, 0.45f, 0.65f, 1f));
				lr.startWidth = 0.02f;
				lr.endWidth = 0.02f;
				lr.useWorldSpace = true;
				lr.positionCount = 2;
				lr.SetPosition(0, new Vector3(xPos, y, 0 + offset));
				lr.SetPosition(1, new Vector3(xPos, y, height + offset));
				lr.enabled = gridLinesEnabled;
			}

			// Create horizontal lines (along Z)
			for (int z = 0; z <= height; z++)
			{
				float zPos = z + offset;
				var lineObj = new GameObject($"HorizontalLine_{z}");
				lineObj.transform.SetParent(gridLinesObject.transform, false);
				var lr = lineObj.AddComponent<LineRenderer>();
				lr.material = MaterialUtils.CreateOpaqueUnlitMaterial(new Color(0.25f, 0.45f, 0.65f, 1f));
				lr.startWidth = 0.02f;
				lr.endWidth = 0.02f;
				lr.useWorldSpace = true;
				lr.positionCount = 2;
				lr.SetPosition(0, new Vector3(0 + offset, y, zPos));
				lr.SetPosition(1, new Vector3(width + offset, y, zPos));
				lr.enabled = gridLinesEnabled;
			}
		}

		private void UpdateGridLines()
		{
			if (mapManager == null || gridLinesObject == null) return;

			var lineRenderers = gridLinesObject.GetComponentsInChildren<LineRenderer>();
			foreach (var lr in lineRenderers)
			{
				lr.enabled = gridLinesEnabled;
			}
		}

		public override void Update()
		{
			base.Update();

			// Workaround: Reset hotControl on mouse release to handle drag outside GUI
			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
			{
				GUIUtility.hotControl = 0;
			}

			if (activeMode != null)
				activeMode.Update();

			// Update grid lines
			UpdateGridLines();

			// Update ghost tile position and handle delete
			if (currentMode == EditorMode.Paint && !GUIManager.IsMouseOverGui() && !EventSystem.current.IsPointerOverGameObject())
			{
				if (tempSelectedTileDefGlobalIndex >= 0 && tempSelectedTileDefGlobalIndex < DatabaseSerializer.TileDefs.Count)
				{
					var tileDef = DatabaseSerializer.TileDefs[tempSelectedTileDefGlobalIndex];
					GeometryUtil.UpdateGhostTile(camera, mapManager, tileDef);

					// Track mouse down for delete (RMB)
					if (Input.GetMouseButtonDown(1))
					{
						mouseDownPos = Input.mousePosition;
					}

					// Handle click-and-release for delete (RMB)
					if (Input.GetMouseButtonUp(1))
					{
						float mouseMoveDistance = Vector3.Distance(Input.mousePosition, mouseDownPos);
						if (mouseMoveDistance < 5f) // Threshold: 5 pixels
						{
							int emptyTileDefIndex = mapManager.GetOrAddMapDefIndex("tile_empty", "Default");
							if (emptyTileDefIndex >= 0)
							{
								paintMode.SetTileDefIndex(emptyTileDefIndex);
								paintMode.PlaceTileAtMousePosition();
								lastClickedMapIndex = -1; // Reset cycle on delete
							}
						}
					}
				}
				else
				{
					GeometryUtil.HideGhostTile();
				}
			}
			else
			{
				GeometryUtil.HideGhostTile();
			}
		}

		public override void OnApplicationFocus(bool hasFocus)
		{
			base.OnApplicationFocus(hasFocus);
			if (activeMode != null)
				activeMode.OnApplicationFocus(hasFocus);
		}

		protected override void OnRender()
		{
			base.OnRender();
			if (camera == null) return;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = fieldOfView;
		}

		public override void OnEnable()
		{
			base.OnEnable();
			gridLinesEnabled = true;
			UpdateGridLines();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			gridLinesEnabled = false;
			UpdateGridLines();
		}

		private void UpdateTileCycleList(string baseTileType, string currentTileType)
		{
			// Define suffix groups
			var singleDirections = new[] { " n", " e", " s", " w" };
			var doubleLinear = new[] { " ew", " ns", " we", " sn" };
			var doubleDiagonal = new[] { " ne", " nw", " se", " sw" };
			string[] selectedGroup = null;

			// Determine the group based on the current tile's suffix
			if (currentTileType == baseTileType)
			{
				selectedGroup = singleDirections; // Default to single directions for base tile
			}
			else
			{
				if (singleDirections.Any(suffix => currentTileType.EndsWith(suffix)))
					selectedGroup = singleDirections;
				else if (doubleLinear.Any(suffix => currentTileType.EndsWith(suffix)))
					selectedGroup = doubleLinear;
				else if (doubleDiagonal.Any(suffix => currentTileType.EndsWith(suffix)))
					selectedGroup = doubleDiagonal;
				else
					selectedGroup = singleDirections; // Fallback
			}

			tileDefCycleList = new List<int>();

			// Include base tile if it exists
			for (int i = 0; i < DatabaseSerializer.TileDefs.Count; i++)
			{
				if (DatabaseSerializer.TileDefs[i].szType == baseTileType)
				{
					tileDefCycleList.Add(i);
					break;
				}
			}

			// Add tiles with suffixes from the selected group
			foreach (var suffix in selectedGroup)
			{
				for (int i = 0; i < DatabaseSerializer.TileDefs.Count; i++)
				{
					if (DatabaseSerializer.TileDefs[i].szType == baseTileType + suffix)
					{
						tileDefCycleList.Add(i);
						break;
					}
				}
			}
		}

		public override void OnGUI()
		{
			base.OnGUI();
			GUIManager.ResetGuiState(); // Clear rects at start of OnGUI

			float buttonWidth = 120;
			float buttonHeight = 30;
			float margin = 10;
			float spacing = 10;

			// Get PlaceholderUI panel bottom Y for stacking buttons
			float panelBottomY = placeholderUI != null ? placeholderUI.GetPanelBottomY() : buttonHeight + margin;

			// Mode toggle buttons, Save button, and Grid toggle stacked on the left
			Rect dragButtonRect = new Rect(margin, panelBottomY + spacing, buttonWidth, buttonHeight);
			Rect paintButtonRect = new Rect(margin, panelBottomY + spacing + buttonHeight + spacing, buttonWidth, buttonHeight);
			Rect saveButtonRect = new Rect(margin, panelBottomY + spacing + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect gridToggleRect = new Rect(margin, panelBottomY + spacing + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			GUIManager.RegisterGuiRect(dragButtonRect);
			GUIManager.RegisterGuiRect(paintButtonRect);
			GUIManager.RegisterGuiRect(saveButtonRect);
			GUIManager.RegisterGuiRect(gridToggleRect);

			bool dragToggled = GUI.Toggle(dragButtonRect, currentMode == EditorMode.Drag, "Drag", "Button");
			bool paintToggled = GUI.Toggle(paintButtonRect, currentMode == EditorMode.Paint, "Paint", "Button");

			// Save button (red)
			GUIStyle saveButtonStyle = new GUIStyle(GUI.skin.button);
			saveButtonStyle.normal.background = TextureUtils.MakeTex(1, 1, new Color(0.8f, 0.2f, 0.2f)); // Red background
			if (GUI.Button(saveButtonRect, "Save", saveButtonStyle))
			{
				mapManager.SaveChanges();
			}

			// Grid toggle button
			if (GUI.Button(gridToggleRect, gridLinesEnabled ? "Hide Grid" : "Show Grid"))
			{
				gridLinesEnabled = !gridLinesEnabled;
				UpdateGridLines();
			}

			// Ensure radio button behavior
			if (dragToggled && currentMode != EditorMode.Drag)
			{
				currentMode = EditorMode.Drag;
				activeMode = dragMode;
				GeometryUtil.HideGhostTile();
				lastClickedMapIndex = -1; // Reset cycle
			}
			else if (paintToggled && currentMode != EditorMode.Paint)
			{
				currentMode = EditorMode.Paint;
				activeMode = paintMode;
				targetWidth = collapsedWidth; // Start collapsed
				tileSelectorWidth = collapsedWidth; // Immediate collapse
				animationStartTime = Time.time;
				lastClickedMapIndex = -1; // Reset cycle
			}

			// Tile selector (visible only in Paint mode)
			if (currentMode == EditorMode.Paint)
			{
				// Calculate mouse position in GUI coordinates
				Vector2 mousePos = Input.mousePosition;
				mousePos.y = Screen.height - mousePos.y; // Convert to GUI coordinates

				// Update tile selector width before defining rects
				bool wasMouseOverTileSelector = isMouseOverTileSelector;
				float tileSelectorX = Screen.width - tileSelectorWidth - margin;
				float tileSelectorY = panelBottomY + spacing;
				float tileSelectorHeight = Screen.height - tileSelectorY - margin;
				Rect tileSelectorRect = new Rect(tileSelectorX, tileSelectorY, tileSelectorWidth, tileSelectorHeight);
				isMouseOverTileSelector = tileSelectorRect.Contains(mousePos);

				// Handle auto-expand and auto-hide
				if (isMouseOverTileSelector)
				{
					if (targetWidth != fullWidth)
					{
						targetWidth = fullWidth; // Expand when mouse is over
						animationStartTime = Time.time;
					}
					mouseExitTime = 0f; // Reset timer
				}
				else
				{
					if (wasMouseOverTileSelector)
					{
						mouseExitTime = Time.time; // Start timer when mouse exits
					}
					if (mouseExitTime > 0f && Time.time - mouseExitTime >= autoHideDelay && targetWidth != collapsedWidth)
					{
						targetWidth = collapsedWidth; // Collapse after 1 second
						animationStartTime = Time.time;
					}
				}

				// Animate tile selector width
				float t = Mathf.Clamp01((Time.time - animationStartTime) / animationDuration);
				tileSelectorWidth = Mathf.Lerp(tileSelectorWidth, targetWidth, t);

				// Redefine rects with updated width
				tileSelectorX = Screen.width - tileSelectorWidth - margin;
				tileSelectorRect = new Rect(tileSelectorX, tileSelectorY, tileSelectorWidth, tileSelectorHeight);
				GUIManager.RegisterGuiRect(tileSelectorRect);

				// Draw background
				GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
				panelStyle.normal.background = TextureUtils.MakeTex(1, 1, new Color(0.2f, 0.2f, 0.4f, 0.75f));
				GUI.Box(tileSelectorRect, "Tile Selector", panelStyle);

				// Scroll view for tiles
				Rect scrollViewRect = new Rect(tileSelectorX + 10, tileSelectorY + 30, tileSelectorWidth - 20, tileSelectorHeight - 40);
				GUIManager.RegisterGuiRect(scrollViewRect);
				scrollPosition = GUI.BeginScrollView(
					scrollViewRect,
					scrollPosition,
					new Rect(0, 0, tileSelectorWidth - 40, DatabaseSerializer.TileDefs.Count * 40)
				);

				for (int i = 0; i < DatabaseSerializer.TileDefs.Count; i++)
				{
					var tileDef = DatabaseSerializer.TileDefs[i];
					string displayName = $"{tileDef.szType} ({tileDef.szTheme})";
					Rect buttonRect = new Rect(0, i * 40, tileSelectorWidth - 40, 35);

					// Green highlight for selected tile
					if (i == tempSelectedTileDefGlobalIndex)
					{
						GUI.color = Color.green;
					}
					if (GUI.Button(buttonRect, displayName))
					{
						tempSelectedTileDefGlobalIndex = i;
						var selectedTileDef = DatabaseSerializer.TileDefs[i];
						selectedMapDefIndex = mapManager.GetOrAddMapDefIndex(selectedTileDef.szType, selectedTileDef.szTheme);
						if (selectedMapDefIndex >= 0)
						{
							paintMode.SetTileDefIndex(selectedMapDefIndex);
							// Update tile cycle list based on the selected tile's group
							UpdateTileCycleList(selectedTileDef.szType, selectedTileDef.szType);
							cycleIndex = tileDefCycleList.IndexOf(i);
							if (cycleIndex < 0) cycleIndex = 0; // Fallback to start
																// Update ghost tile
							GeometryUtil.DestroyGhostTile();
							GeometryUtil.UpdateGhostTile(camera, mapManager, selectedTileDef);
							lastClickedMapIndex = -1; // Reset cycle
						}
					}
					GUI.color = Color.white;
				}

				GUI.EndScrollView();

				// Handle tile placement and cycling (LMB)
				if (!isMouseOverTileSelector && !EventSystem.current.IsPointerOverGameObject() && Input.GetMouseButtonUp(0))
				{
					Ray ray = camera.ScreenPointToRay(Input.mousePosition);
					Plane plane = new Plane(Vector3.up, Vector3.zero);
					if (plane.Raycast(ray, out float enter))
					{
						Vector3 worldPos = ray.GetPoint(enter);
						int mapIndex = mapManager.WorldToMapIndex(worldPos);
						if (mapIndex >= 0 && mapIndex < mapManager.Count)
						{
							if (mapIndex == lastClickedMapIndex && tileDefCycleList != null && tileDefCycleList.Count > 1)
							{
								// Cycle to next tile in the group
								cycleIndex = (cycleIndex + 1) % tileDefCycleList.Count;
								tempSelectedTileDefGlobalIndex = tileDefCycleList[cycleIndex];
								var newTileDef = DatabaseSerializer.TileDefs[tempSelectedTileDefGlobalIndex];
								selectedMapDefIndex = mapManager.GetOrAddMapDefIndex(newTileDef.szType, newTileDef.szTheme);
								paintMode.SetTileDefIndex(selectedMapDefIndex);
								GeometryUtil.DestroyGhostTile();
								GeometryUtil.UpdateGhostTile(camera, mapManager, newTileDef);
							}
							else
							{
								// Place the selected tile (first click or new position)
								paintMode.SetTileDefIndex(selectedMapDefIndex);
							}
							paintMode.PlaceTileAtMousePosition();
							lastClickedMapIndex = mapIndex;
						}
					}
				}
			}
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			if (gridLinesObject != null)
			{
				Object.Destroy(gridLinesObject);
			}
			GeometryUtil.DestroyGhostTile();
		}
	}
}