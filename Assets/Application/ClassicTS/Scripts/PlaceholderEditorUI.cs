using UnityEngine;
using MassiveHadronLtd;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class PlaceholderEditorUI : MonoBehaviour
	{
		private MapManager mapManager;
		private new Camera camera;

		private readonly float margin = 10f;
		private readonly float spacing = 10f;
		private readonly float buttonWidth = 125f;
		private readonly float buttonHeight = 30f;
		private float tileSelectorWidth = 120f;
		private readonly float fullWidth = 300f;
		private readonly float collapsedWidth = 120f;
		private float mouseExitTime = 0f;
		private bool isMouseOverTileSelector;
		private readonly float autoHideDelay = 1f;
		private float targetWidth;
		private float animationStartTime;
		private readonly float animationDuration = 0.3f;
		private Vector2 scrollPosition = Vector2.zero;

		private Texture2D panelBackgroundTexture;
		private Texture2D saveBackgroundTexture;
		private Texture2D reloadBackgroundTexture;
		private Texture2D gridButtonBackgroundTexture;
		private Texture2D toggleOffBackgroundTexture;
		private Texture2D toggleOnBackgroundTexture;
		private Texture2D toggleHoverBackgroundTexture;

		private bool gridLinesEnabled = true;
		private string selectedDefinitionId = "tile_empty";

		public EditorController.EditorMode currentMode = EditorController.EditorMode.Drag;

		// === EVENTS ONLY — NO DIRECT EditorController CALLS ===
		public event System.Action<EditorController.EditorMode> OnModeChanged;
		public event System.Action<bool> OnGridLinesToggled;
		public event System.Action OnSaveDatabaseRequested;
		public event System.Action OnReloadDatabaseRequested;
		public event System.Action OnExportMapRequested;
		public event System.Action OnImportMapRequested;
		public event System.Action OnResizeMapTestRequested;
		public event System.Action OnCropMapTestRequested;
		public event System.Action<Definition> OnTileSelected;

		private void Awake()
		{
			panelBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.2f, 0.2f, 0.4f, 0.75f));
			saveBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.8f, 0.2f, 0.2f, 1f));
			reloadBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.2f, 0.6f, 1f, 1f));
			gridButtonBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.5f, 0.5f, 0.5f, 1f));
			toggleOffBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.3f, 0.3f, 0.3f, 1f));
			toggleOnBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.3f, 0.6f, 0.3f, 1f));
			toggleHoverBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.4f, 0.4f, 0.4f, 1f));

			targetWidth = collapsedWidth;
		}

		public void Initialize(MapManager map, Camera cam)
		{
			mapManager = map;
			camera = cam;
		}

		public bool IsGuiControlActive() => GUIUtility.hotControl != 0 || isMouseOverTileSelector || EventSystem.current.IsPointerOverGameObject();

		public bool IsMouseOverGui()
		{
			if (isMouseOverTileSelector)
				return true;

			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y;

			float panelBottomY = GetPanelBottomY();
			float leftPanelX = margin;
			float leftPanelY = panelBottomY + spacing;
			float leftPanelWidth = buttonWidth + 20f;
			float leftPanelHeight = buttonHeight * 6 + spacing * 6;

			Rect leftPanelRect = new Rect(leftPanelX, leftPanelY, leftPanelWidth, leftPanelHeight);
			if (leftPanelRect.Contains(mousePos))
				return true;

			if (currentMode == EditorController.EditorMode.Paint)
			{
				float tileSelectorX = Screen.width - tileSelectorWidth - margin;
				float tileSelectorY = panelBottomY + spacing;
				float tileSelectorHeight = Screen.height - tileSelectorY - margin;

				Rect tileSelectorRect = new Rect(tileSelectorX, tileSelectorY, tileSelectorWidth, tileSelectorHeight);
				if (tileSelectorRect.Contains(mousePos))
					return true;
			}

			Rect exportButtonRect = new Rect(Screen.width - 190, 50, 180, 50);
			if (exportButtonRect.Contains(mousePos))
				return true;

			return false;
		}

		public bool IsMouseInsideWindow()
		{
			Vector2 mousePos = Input.mousePosition;
			return mousePos.x >= 0 && mousePos.x <= Screen.width && mousePos.y >= 0 && mousePos.y <= Screen.height;
		}

		private void Update()
		{
			if (currentMode == EditorController.EditorMode.Paint)
			{
				Vector2 tileSelectorMousePos = Input.mousePosition;
				tileSelectorMousePos.y = Screen.height - tileSelectorMousePos.y;

				bool wasMouseOverTileSelector = isMouseOverTileSelector;
				float tileSelectorX = Screen.width - tileSelectorWidth - margin;
				float tileSelectorY = GetPanelBottomY() + spacing;
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
						mouseExitTime = Time.time;

					if (mouseExitTime > 0f && Time.time - mouseExitTime >= autoHideDelay && targetWidth != collapsedWidth)
					{
						targetWidth = collapsedWidth;
						animationStartTime = Time.time;
					}
				}

				float t = Mathf.Clamp01((Time.time - animationStartTime) / animationDuration);
				tileSelectorWidth = Mathf.Lerp(tileSelectorWidth, targetWidth, t);
			}
		}

		private float GetPanelBottomY()
		{
			var placeholderUI = FindAnyObjectByType<PlaceholderUI>();
			return placeholderUI != null ? placeholderUI.GetPanelBottomY() : margin;
		}

		private void OnGUI()
		{
			if (!mapManager || !camera) return;

			float panelBottomY = GetPanelBottomY();
			Rect gridToggleRect = new(margin, panelBottomY + spacing + 0 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect dragButtonRect = new(margin, panelBottomY + spacing + 1 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect paintButtonRect = new(margin, panelBottomY + spacing + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect resizeButtonRect = new(margin, panelBottomY + spacing + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect cropButtonRect = new(margin, panelBottomY + spacing + 4 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect saveButtonRect = new(margin, panelBottomY + spacing + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect reloadButtonRect = new(margin, panelBottomY + spacing + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect exptButtonRect = new(margin, panelBottomY + spacing + 7 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect importButtonRect = new(margin, panelBottomY + spacing + 8 * (buttonHeight + spacing), buttonWidth, buttonHeight);

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

			// === GRID TOGGLE ===
			GUIStyle gridButtonStyle = new GUIStyle(GUI.skin.button);
			gridButtonStyle.normal.background = gridButtonBackgroundTexture;
			gridButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			gridButtonStyle.fontSize = 14;
			gridButtonStyle.alignment = TextAnchor.MiddleCenter;
			gridButtonStyle.fixedWidth = buttonWidth;
			gridButtonStyle.fixedHeight = buttonHeight;
			if (GUI.Button(gridToggleRect, gridLinesEnabled ? "Hide Grid" : "Show Grid", gridButtonStyle))
			{
				gridLinesEnabled = !gridLinesEnabled;
				OnGridLinesToggled?.Invoke(gridLinesEnabled);
			}

			bool dragToggled = GUI.Toggle(dragButtonRect, currentMode == EditorController.EditorMode.Drag, "Drag", toggleStyle);
			bool paintToggled = GUI.Toggle(paintButtonRect, currentMode == EditorController.EditorMode.Paint, "Paint", toggleStyle);

			// === RESIZE TEST BUTTON ===
			GUIStyle resizeButtonStyle = new GUIStyle(GUI.skin.button);
			resizeButtonStyle.normal.background = reloadBackgroundTexture;
			resizeButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			resizeButtonStyle.fontSize = 14;
			resizeButtonStyle.alignment = TextAnchor.MiddleCenter;
			resizeButtonStyle.fixedWidth = buttonWidth;
			resizeButtonStyle.fixedHeight = buttonHeight;

			if (GUI.Button(resizeButtonRect, "Resize Test", resizeButtonStyle))
				OnResizeMapTestRequested?.Invoke();

			if (GUI.Button(cropButtonRect, "Crop Test", resizeButtonStyle))
				OnCropMapTestRequested?.Invoke();

			// === SAVE DATABASE BUTTON ===
			GUIStyle saveButtonStyle = new GUIStyle(GUI.skin.button);
			saveButtonStyle.normal.background = saveBackgroundTexture;
			saveButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			saveButtonStyle.fontSize = 14;
			saveButtonStyle.alignment = TextAnchor.MiddleCenter;
			saveButtonStyle.fixedWidth = buttonWidth;
			saveButtonStyle.fixedHeight = buttonHeight;

			if (GUI.Button(saveButtonRect, "Save Database", saveButtonStyle))
				OnSaveDatabaseRequested?.Invoke();

			// === RELOAD DATABASE BUTTON ===
			GUIStyle reloadButtonStyle = new GUIStyle(GUI.skin.button);
			reloadButtonStyle.normal.background = reloadBackgroundTexture;
			reloadButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			reloadButtonStyle.fontSize = 14;
			reloadButtonStyle.alignment = TextAnchor.MiddleCenter;
			reloadButtonStyle.fixedWidth = buttonWidth;
			reloadButtonStyle.fixedHeight = buttonHeight;

			if (GUI.Button(reloadButtonRect, "Reload Database", reloadButtonStyle))
				OnReloadDatabaseRequested?.Invoke();

			// === EXPORT BUTTON ===
			GUIStyle exportButtonStyle = new GUIStyle(GUI.skin.button);
			exportButtonStyle.normal.background = saveBackgroundTexture;
			exportButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			exportButtonStyle.fontSize = 14;
			exportButtonStyle.alignment = TextAnchor.MiddleCenter;
			exportButtonStyle.fixedWidth = buttonWidth;
			exportButtonStyle.fixedHeight = buttonHeight;
			if (GUI.Button(exptButtonRect, "Export", exportButtonStyle))
				OnExportMapRequested?.Invoke();

			// === IMPORT MAP BUTTON ===
			GUIStyle importButtonStyle = new GUIStyle(GUI.skin.button);
			importButtonStyle.normal.background = reloadBackgroundTexture;
			importButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			importButtonStyle.fontSize = 14;
			importButtonStyle.alignment = TextAnchor.MiddleCenter;
			importButtonStyle.fixedWidth = buttonWidth;
			importButtonStyle.fixedHeight = buttonHeight;

			if (GUI.Button(importButtonRect, "Import Map", importButtonStyle))
				OnImportMapRequested?.Invoke();

			// Mode switching
			if (dragToggled && currentMode != EditorController.EditorMode.Drag)
			{
				currentMode = EditorController.EditorMode.Drag;
				OnModeChanged?.Invoke(EditorController.EditorMode.Drag);
				GeometryUtil.HideGhostTile();
			}
			else if (paintToggled && currentMode != EditorController.EditorMode.Paint)
			{
				currentMode = EditorController.EditorMode.Paint;
				OnModeChanged?.Invoke(EditorController.EditorMode.Paint);
				targetWidth = collapsedWidth;
				tileSelectorWidth = collapsedWidth;
				animationStartTime = Time.time;
			}

			// === TILE SELECTOR ===
			if (currentMode == EditorController.EditorMode.Paint)
			{
				float tileSelectorX = Screen.width - tileSelectorWidth - margin;
				float tileSelectorY = panelBottomY + spacing;
				float tileSelectorHeight = Screen.height - tileSelectorY - margin;
				Rect tileSelectorRect = new Rect(tileSelectorX, tileSelectorY, tileSelectorWidth, tileSelectorHeight);

				GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
				panelStyle.normal.background = panelBackgroundTexture;
				GUI.Box(tileSelectorRect, "Tile Selector", panelStyle);

				Rect scrollViewRect = new Rect(tileSelectorX + 10, tileSelectorY + 30, tileSelectorWidth - 20, tileSelectorHeight - 40);
				scrollPosition = GUI.BeginScrollView(scrollViewRect, scrollPosition, new Rect(0, 0, tileSelectorWidth - 40, ResourceManager.Definitions.Count * 40));

				for (int i = 0; i < ResourceManager.Definitions.Count; i++)
				{
					var definition = ResourceManager.Definitions[i];
					string displayName = $"{definition.id} ({definition.texture})";
					Rect buttonRect = new(0, i * 40, tileSelectorWidth - 40, 35);

					if (definition.id == selectedDefinitionId)
						GUI.color = Color.green;

					if (GUI.Button(buttonRect, displayName))
					{
						selectedDefinitionId = definition.id;
						OnTileSelected?.Invoke(definition);
					}

					GUI.color = Color.white;
				}

				GUI.EndScrollView();
			}
		}

		private void OnDestroy()
		{
			if (panelBackgroundTexture != null) Destroy(panelBackgroundTexture);
			if (saveBackgroundTexture != null) Destroy(saveBackgroundTexture);
			if (reloadBackgroundTexture != null) Destroy(reloadBackgroundTexture);
			if (gridButtonBackgroundTexture != null) Destroy(gridButtonBackgroundTexture);
			if (toggleOffBackgroundTexture != null) Destroy(toggleOffBackgroundTexture);
			if (toggleOnBackgroundTexture != null) Destroy(toggleOnBackgroundTexture);
			if (toggleHoverBackgroundTexture != null) Destroy(toggleHoverBackgroundTexture);
		}

		public void SetGridLinesEnabled(bool enabled) => gridLinesEnabled = enabled;
		public bool GetGridLinesEnabled() => gridLinesEnabled;
		public void SetSelectedDefinitionId(string id)
		{
			if (!string.IsNullOrEmpty(id))
				selectedDefinitionId = id;
		}
	}
}