using UnityEngine;
using UnityEngine.EventSystems;
using MassiveHadronLtd;

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
		private readonly float collapsedWidth = 120f; // Collapsed width (matches buttonWidth)
		private float mouseExitTime = 0f; // Time when mouse last exited tile selector
		private bool isMouseOverTileSelector; // Track if mouse is over tile selector
		private readonly float autoHideDelay = 1f; // 1 second delay for auto-hide
		private float targetWidth; // Target width for animation
		private float animationStartTime; // Time when animation started
		private readonly float animationDuration = 0.3f; // Duration for width animation

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

			// Update ghost tile position
			if (currentMode == EditorMode.Paint && !GUIManager.IsMouseOverGui() && !EventSystem.current.IsPointerOverGameObject())
			{
				if (tempSelectedTileDefGlobalIndex >= 0 && tempSelectedTileDefGlobalIndex < DatabaseSerializer.TileDefs.Count)
				{
					var tileDef = DatabaseSerializer.TileDefs[tempSelectedTileDefGlobalIndex];
					GeometryUtil.UpdateGhostTile(camera, mapManager, tileDef);
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
			if (camera == null) return;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = fieldOfView;
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

			// Mode toggle buttons and Save button stacked on the left
			Rect dragButtonRect = new Rect(margin, panelBottomY + spacing, buttonWidth, buttonHeight);
			Rect paintButtonRect = new Rect(margin, panelBottomY + spacing + buttonHeight + spacing, buttonWidth, buttonHeight);
			Rect saveButtonRect = new Rect(margin, panelBottomY + spacing + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			GUIManager.RegisterGuiRect(dragButtonRect);
			GUIManager.RegisterGuiRect(paintButtonRect);
			GUIManager.RegisterGuiRect(saveButtonRect);

			bool dragToggled = GUI.Toggle(dragButtonRect, currentMode == EditorMode.Drag, "Drag", "Button");
			bool paintToggled = GUI.Toggle(paintButtonRect, currentMode == EditorMode.Paint, "Paint", "Button");

			// Save button (red)
			GUIStyle saveButtonStyle = new GUIStyle(GUI.skin.button);
			saveButtonStyle.normal.background = MakeTex(1, 1, new Color(0.8f, 0.2f, 0.2f)); // Red background
			if (GUI.Button(saveButtonRect, "Save", saveButtonStyle))
			{
				mapManager.SaveChanges();
			}

			// Ensure radio button behavior
			if (dragToggled && currentMode != EditorMode.Drag)
			{
				currentMode = EditorMode.Drag;
				activeMode = dragMode;
				GeometryUtil.HideGhostTile();
			}
			else if (paintToggled && currentMode != EditorMode.Paint)
			{
				currentMode = EditorMode.Paint;
				activeMode = paintMode;
				targetWidth = collapsedWidth; // Start collapsed in Paint mode
				tileSelectorWidth = collapsedWidth; // Immediate collapse on mode switch
				animationStartTime = Time.time;
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
				panelStyle.normal.background = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.4f, 0.75f));
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
							// Update ghost tile immediately
							GeometryUtil.DestroyGhostTile(); // Ensure old tile is removed
							GeometryUtil.UpdateGhostTile(camera, mapManager, selectedTileDef);
						}
					}
					GUI.color = Color.white;
				}

				GUI.EndScrollView();
			}
		}

		// Helper to create a texture for the tile selector background
		private Texture2D MakeTex(int width, int height, Color col)
		{
			Color[] pix = new Color[width * height];
			for (int i = 0; i < pix.Length; i++)
				pix[i] = col;
			Texture2D result = new Texture2D(width, height);
			result.SetPixels(pix);
			result.Apply();
			return result;
		}
	}
}