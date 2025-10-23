using UnityEngine;
using UnityEngine.EventSystems;
using MassiveHadronLtd;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public class GameCameraEditor : CameraBase
	{
		private MapManager _mapManager;
		public MapManager mapManager
		{
			get => _mapManager;
			set
			{
				_mapManager = value;
				OnMapManagerChanged();
			}
		}
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
		private Vector3 mouseDownPosLMB; // Mouse position on LMB down for placement
		private int mouseDownMapIndex = -1; // Map index on LMB down
		private List<int> tileDefCycleList; // List of TileDef indices for cycling
		private int cycleIndex = 0; // Current position in cycle list
		private GameObject gridLines;
		private bool gridLinesEnabled = true; // Toggle for grid lines
		private static Texture2D panelBackgroundTexture; // Static texture for tile selector
		private static Texture2D saveBackgroundTexture; // Static texture for save button
		private static Texture2D gridButtonBackgroundTexture; // Static texture for grid toggle button
		private static Texture2D toggleOffBackgroundTexture; // Static texture for toggle off state
		private static Texture2D toggleOnBackgroundTexture; // Static texture for toggle on state
		private static Texture2D toggleHoverBackgroundTexture; // Static texture for toggle hover state

		public GameCameraEditor(Camera camera) : base(camera) { }

		public override void Awake()
		{
			base.Awake();

			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

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

			// Initialize static textures (4x4 for better scaling)
			if (panelBackgroundTexture == null)
				panelBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.2f, 0.2f, 0.4f, 0.75f));

			if (saveBackgroundTexture == null)
				saveBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.8f, 0.2f, 0.2f, 1f));

			if (gridButtonBackgroundTexture == null)
				gridButtonBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.5f, 0.5f, 0.5f, 1f));

			if (toggleOffBackgroundTexture == null)
				toggleOffBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.3f, 0.3f, 0.3f, 1f)); // Dark gray for off

			if (toggleOnBackgroundTexture == null)
				toggleOnBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.3f, 0.6f, 0.3f, 1f)); // Greenish for on

			if (toggleHoverBackgroundTexture == null)
				toggleHoverBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.4f, 0.4f, 0.4f, 1f)); // Lighter gray for hover
		}

		private void InitializeGridLines() => gridLines = GridLinesHelper.CreateGridLines(mapManager.transform, mapManager.Width, mapManager.Height, gridLinesEnabled);
		private void UpdateGridLines()
		{
			if (null == gridLines) InitializeGridLines();
			gridLines.SetActive(gridLinesEnabled);
		}

		private void OnMapManagerChanged() 
		{
			Debug.Log("MapManager changed or reloaded");
		}

		public override void Update()
		{
			base.Update();

			// Workaround: Reset hotControl on mouse release to handle drag outside GUI
			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
				GUIUtility.hotControl = 0;

			if (activeMode != null)
				activeMode.Update();

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

		private void UpdateTileCycleList(string currentTileType)
		{
			// Define suffix groups
			var singleDirections = new[] { " n", " e", " s", " w" };
			var doubleLinear = new[] { " ew", " ns", " we", " sn" };
			var doubleDiagonal = new[] { " ne", " nw", " se", " sw" };
			string[] selectedGroup = null;

			// Determine the base tile type by removing the suffix from currentTileType
			string derivedBaseTileType = currentTileType;
			foreach (var suffix in singleDirections.Concat(doubleLinear).Concat(doubleDiagonal))
			{
				if (currentTileType.EndsWith(suffix))
				{
					derivedBaseTileType = currentTileType.Substring(0, currentTileType.Length - suffix.Length);
					break;
				}
			}

			// Determine the group based on the current tile's suffix
			if (singleDirections.Any(suffix => currentTileType.EndsWith(suffix)))
				selectedGroup = singleDirections;
			else if (doubleLinear.Any(suffix => currentTileType.EndsWith(suffix)))
				selectedGroup = doubleLinear;
			else if (doubleDiagonal.Any(suffix => currentTileType.EndsWith(suffix)))
				selectedGroup = doubleDiagonal;
			else
				selectedGroup = singleDirections; // Fallback to single directions if no suffix or base tile

			tileDefCycleList = new List<int>();

			// Include base tile if it exists
			for (int i = 0; i < DatabaseSerializer.TileDefs.Count; i++)
			{
				if (DatabaseSerializer.TileDefs[i].szType == derivedBaseTileType)
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
					if (DatabaseSerializer.TileDefs[i].szType == derivedBaseTileType + suffix)
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

			float panelBottomY = 0f; // Start at top of screen
			if (placeholderUI != null)
				panelBottomY = placeholderUI.GetPanelBottomY();

			// Mode toggle buttons, Save button, and Grid toggle stacked on the left
			Rect dragButtonRect = new Rect(margin, panelBottomY + spacing, buttonWidth, buttonHeight);
			Rect paintButtonRect = new Rect(margin, panelBottomY + spacing + buttonHeight + spacing, buttonWidth, buttonHeight);
			Rect saveButtonRect = new Rect(margin, panelBottomY + spacing + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect gridToggleRect = new Rect(margin, panelBottomY + spacing + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			GUIManager.RegisterGuiRect(dragButtonRect);
			GUIManager.RegisterGuiRect(paintButtonRect);
			GUIManager.RegisterGuiRect(saveButtonRect);
			GUIManager.RegisterGuiRect(gridToggleRect);

			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y; // Convert to GUI coordinates

			// Toggle style for Drag and Paint
			GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
			toggleStyle.normal.background = toggleOffBackgroundTexture;
			toggleStyle.onNormal.background = toggleOnBackgroundTexture;
			toggleStyle.hover.background = toggleHoverBackgroundTexture;
			toggleStyle.onHover.background = toggleHoverBackgroundTexture;
			toggleStyle.active.background = toggleOnBackgroundTexture;
			toggleStyle.onActive.background = toggleOnBackgroundTexture;
			toggleStyle.padding = new RectOffset(10, 10, 5, 5);
			toggleStyle.fontSize = 14;
			toggleStyle.alignment = TextAnchor.MiddleCenter;
			toggleStyle.fixedWidth = buttonWidth;
			toggleStyle.fixedHeight = buttonHeight;

			bool dragToggled = GUI.Toggle(dragButtonRect, currentMode == EditorMode.Drag, "Drag", toggleStyle);
			bool paintToggled = GUI.Toggle(paintButtonRect, currentMode == EditorMode.Paint, "Paint", toggleStyle);

			GUIStyle saveButtonStyle = new GUIStyle(GUI.skin.button);
			saveButtonStyle.normal.background = saveBackgroundTexture;
			saveButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			saveButtonStyle.fontSize = 14;
			saveButtonStyle.alignment = TextAnchor.MiddleCenter;
			saveButtonStyle.fixedWidth = buttonWidth;
			saveButtonStyle.fixedHeight = buttonHeight;
			if (GUI.Button(saveButtonRect, "Save", saveButtonStyle))
				mapManager.SaveChanges();

			GUIStyle gridButtonStyle = new GUIStyle(GUI.skin.button);
			gridButtonStyle.normal.background = gridButtonBackgroundTexture;
			gridButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			gridButtonStyle.fontSize = 14;
			gridButtonStyle.alignment = TextAnchor.MiddleCenter;
			gridButtonStyle.fixedWidth = buttonWidth;
			gridButtonStyle.fixedHeight = buttonHeight;
			if (GUI.Button(gridToggleRect, gridLinesEnabled ? "Hide Grid" : "Show Grid", gridButtonStyle))
			{
				Debug.Log("Grid Toggle Clicked!");
				gridLinesEnabled = !gridLinesEnabled;
				UpdateGridLines();
			}

			if (dragToggled && currentMode != EditorMode.Drag)
			{
				Debug.Log("Drag Mode Selected");
				currentMode = EditorMode.Drag;
				activeMode = dragMode;
				GeometryUtil.HideGhostTile();
			}
			else if (paintToggled && currentMode != EditorMode.Paint)
			{
				Debug.Log("Paint Mode Selected");
				currentMode = EditorMode.Paint;
				activeMode = paintMode;
				targetWidth = collapsedWidth; // Start collapsed
				tileSelectorWidth = collapsedWidth; // Immediate collapse
				animationStartTime = Time.time;
			}

			if (currentMode == EditorMode.Paint)
			{
				// Calculate mouse position in GUI coordinates
				Vector2 tileSelectorMousePos = Input.mousePosition;
				tileSelectorMousePos.y = Screen.height - tileSelectorMousePos.y;

				// Update tile selector width
				bool wasMouseOverTileSelector = isMouseOverTileSelector;
				float tileSelectorX = Screen.width - tileSelectorWidth - margin;
				float tileSelectorY = panelBottomY + spacing;
				float tileSelectorHeight = Screen.height - tileSelectorY - margin;
				Rect tileSelectorRect = new Rect(tileSelectorX, tileSelectorY, tileSelectorWidth, tileSelectorHeight);
				isMouseOverTileSelector = tileSelectorRect.Contains(tileSelectorMousePos);

				if (isMouseOverTileSelector)
				{
					if (targetWidth != fullWidth)
					{
						targetWidth = fullWidth;
						animationStartTime = Time.time;
					}
					mouseExitTime = 0f;
				}
				else
				{
					if (wasMouseOverTileSelector)
					{
						mouseExitTime = Time.time;
					}
					if (mouseExitTime > 0f && Time.time - mouseExitTime >= autoHideDelay && targetWidth != collapsedWidth)
					{
						targetWidth = collapsedWidth;
						animationStartTime = Time.time;
					}
				}

				float t = Mathf.Clamp01((Time.time - animationStartTime) / animationDuration);
				tileSelectorWidth = Mathf.Lerp(tileSelectorWidth, targetWidth, t);

				tileSelectorX = Screen.width - tileSelectorWidth - margin;
				tileSelectorRect = new Rect(tileSelectorX, tileSelectorY, tileSelectorWidth, tileSelectorHeight);
				GUIManager.RegisterGuiRect(tileSelectorRect);

				GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
				panelStyle.normal.background = panelBackgroundTexture;
				GUI.Box(tileSelectorRect, "Tile Selector", panelStyle);

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
							UpdateTileCycleList(selectedTileDef.szType);
							cycleIndex = tileDefCycleList.IndexOf(i);
							if (cycleIndex < 0) cycleIndex = 0;
							GeometryUtil.DestroyGhostTile();
							GeometryUtil.UpdateGhostTile(camera, mapManager, selectedTileDef);
						}
					}
					GUI.color = Color.white;
				}

				GUI.EndScrollView();

				// Track mouse down for LMB to verify same grid cell
				if (!isMouseOverTileSelector && !EventSystem.current.IsPointerOverGameObject() && Input.GetMouseButtonDown(0))
				{
					mouseDownPosLMB = Input.mousePosition;
					Ray ray = camera.ScreenPointToRay(mouseDownPosLMB);
					Plane plane = new Plane(Vector3.up, Vector3.zero);
					if (plane.Raycast(ray, out float enter))
					{
						Vector3 worldPos = ray.GetPoint(enter);
						mouseDownMapIndex = mapManager.WorldToMapIndex(worldPos);
					}
					else
					{
						mouseDownMapIndex = -1;
					}
				}

				// Handle tile placement and cycling on mouse up (LMB)
				if (!isMouseOverTileSelector && !EventSystem.current.IsPointerOverGameObject() && Input.GetMouseButtonUp(0))
				{
					Ray ray = camera.ScreenPointToRay(Input.mousePosition);
					Plane plane = new Plane(Vector3.up, Vector3.zero);
					if (plane.Raycast(ray, out float enter))
					{
						Vector3 worldPos = ray.GetPoint(enter);
						int mapIndex = mapManager.WorldToMapIndex(worldPos);
						if (mapIndex >= 0 && mapIndex < mapManager.Count && mapIndex == mouseDownMapIndex)
						{
							// Get the current tile's MapTileDef index at the clicked position
							int currentMapDefIndex = mapManager.GetTileDefIndexAt(mapIndex);
							var selectedTileDef = DatabaseSerializer.TileDefs[tempSelectedTileDefGlobalIndex];
							int selectedTileDefIndex = mapManager.GetOrAddMapDefIndex(selectedTileDef.szType, selectedTileDef.szTheme);

							// Compare szType and szTheme of the current and selected tiles
							bool tilesMatch = false;
							var mapDefs = mapManager.GetMapDefs();
							if (currentMapDefIndex >= 0 && currentMapDefIndex < mapDefs.Length)
							{
								var currentTileDef = mapDefs[currentMapDefIndex];
								tilesMatch = currentTileDef.szType == selectedTileDef.szType && currentTileDef.szTheme == selectedTileDef.szTheme;
							}

							if (tilesMatch)
							{
								// Same tile type, cycle to the next in the group
								if (tileDefCycleList != null && tileDefCycleList.Count > 1)
								{
									cycleIndex = (cycleIndex + 1) % tileDefCycleList.Count;
									tempSelectedTileDefGlobalIndex = tileDefCycleList[cycleIndex];
									var newTileDef = DatabaseSerializer.TileDefs[tempSelectedTileDefGlobalIndex];
									selectedMapDefIndex = mapManager.GetOrAddMapDefIndex(newTileDef.szType, newTileDef.szTheme);
									paintMode.SetTileDefIndex(selectedMapDefIndex);
									GeometryUtil.DestroyGhostTile();
									GeometryUtil.UpdateGhostTile(camera, mapManager, newTileDef);
									paintMode.PlaceTileAtMousePosition();
								}
							}
							else
							{
								// Different tile type, place the selected tile
								paintMode.SetTileDefIndex(selectedTileDefIndex);
								paintMode.PlaceTileAtMousePosition();
							}
						}
					}
					mouseDownMapIndex = -1; // Reset after mouse up
				}
			}
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			if (gridLines != null)
				Object.Destroy(gridLines);
			GeometryUtil.DestroyGhostTile();

			// Clean up static textures
			if (panelBackgroundTexture != null)
			{
				Object.Destroy(panelBackgroundTexture);
				panelBackgroundTexture = null;
			}
			if (saveBackgroundTexture != null)
			{
				Object.Destroy(saveBackgroundTexture);
				saveBackgroundTexture = null;
			}
			if (gridButtonBackgroundTexture != null)
			{
				Object.Destroy(gridButtonBackgroundTexture);
				gridButtonBackgroundTexture = null;
			}
			if (toggleOffBackgroundTexture != null)
			{
				Object.Destroy(toggleOffBackgroundTexture);
				toggleOffBackgroundTexture = null;
			}
			if (toggleOnBackgroundTexture != null)
			{
				Object.Destroy(toggleOnBackgroundTexture);
				toggleOnBackgroundTexture = null;
			}
			if (toggleHoverBackgroundTexture != null)
			{
				Object.Destroy(toggleHoverBackgroundTexture);
				toggleHoverBackgroundTexture = null;
			}
		}
	}
}