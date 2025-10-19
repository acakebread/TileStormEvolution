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

		public GameCameraEditor(Camera camera) : base(camera) { }

		public override void Awake()
		{
			base.Awake();

			var cameraTransform = camera.transform;
			cameraTransform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);

			// Initialize mode instances
			dragMode = new GameCameraEditorDrag(camera);
			paintMode = new GameCameraEditorPaint(camera, mapManager, selectedMapDefIndex);
			activeMode = dragMode;
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
		}

		public override void Update()
		{
			base.Update();

			// Workaround: Reset hotControl on mouse release to handle drag outside GUI
			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
			{
				GUIUtility.hotControl = 0;
				Debug.Log("Manually reset hotControl on mouse release");
			}

			if (activeMode != null)
				activeMode.Update();
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

			// Mode toggle buttons stacked on the left
			Rect dragButtonRect = new Rect(margin, panelBottomY + spacing, buttonWidth, buttonHeight);
			Rect paintButtonRect = new Rect(margin, panelBottomY + spacing + buttonHeight + spacing, buttonWidth, buttonHeight);
			GUIManager.RegisterGuiRect(dragButtonRect);
			GUIManager.RegisterGuiRect(paintButtonRect);

			bool dragToggled = GUI.Toggle(dragButtonRect, currentMode == EditorMode.Drag, "Drag", "Button");
			bool paintToggled = GUI.Toggle(paintButtonRect, currentMode == EditorMode.Paint, "Paint", "Button");

			// Ensure radio button behavior
			if (dragToggled && currentMode != EditorMode.Drag)
			{
				currentMode = EditorMode.Drag;
				activeMode = dragMode;
				Debug.Log("Switched to Drag mode");
			}
			else if (paintToggled && currentMode != EditorMode.Paint)
			{
				currentMode = EditorMode.Paint;
				activeMode = paintMode;
				Debug.Log("Switched to Paint mode");
			}

			// Tile selector (visible only in Paint mode)
			if (currentMode == EditorMode.Paint)
			{
				float tileSelectorWidth = 300;
				float tileSelectorX = Screen.width - tileSelectorWidth - margin;
				float tileSelectorY = panelBottomY + spacing;
				float tileSelectorHeight = Screen.height - tileSelectorY - margin;
				Rect tileSelectorRect = new Rect(tileSelectorX, tileSelectorY, tileSelectorWidth, tileSelectorHeight);
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
							Debug.Log($"Selected tileDef: {selectedTileDef.szType} ({selectedTileDef.szTheme}), mapped to mapDefs index={selectedMapDefIndex}");
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