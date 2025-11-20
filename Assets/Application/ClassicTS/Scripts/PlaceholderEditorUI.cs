// File: PlaceholderEditorUI.cs
using UnityEngine;
using MassiveHadronLtd;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClassicTilestorm
{
	public class PlaceholderEditorUI : MonoBehaviour
	{
		private static PlaceholderEditorUI instance;
		public static PlaceholderEditorUI Instance
		{
			get
			{
				if (instance == null)
				{
					instance = FindAnyObjectByType<PlaceholderEditorUI>();
					if (instance == null)
					{
						Debug.LogError("PlaceholderEditorUI not found in scene!");
					}
				}
				return instance;
			}
		}

		private EditorController editorController;
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
		private Texture2D reloadBackgroundTexture;   // ← NEW: created once in Awake
		private Texture2D gridButtonBackgroundTexture;
		private Texture2D toggleOffBackgroundTexture;
		private Texture2D toggleOnBackgroundTexture;
		private Texture2D toggleHoverBackgroundTexture;

		private bool gridLinesEnabled = true;
		private int tempSelectedDefinitionGlobalIndex = 0;
		public EditorController.EditorMode currentMode = EditorController.EditorMode.Drag;

		private void Awake()
		{
			if (instance != null && instance != this)
			{
				Destroy(gameObject);
				return;
			}
			instance = this;

			panelBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.2f, 0.2f, 0.4f, 0.75f));
			saveBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.8f, 0.2f, 0.2f, 1f));
			reloadBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.2f, 0.6f, 1f, 1f));  // ← blue reload button
			gridButtonBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.5f, 0.5f, 0.5f, 1f));
			toggleOffBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.3f, 0.3f, 0.3f, 1f));
			toggleOnBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.3f, 0.6f, 0.3f, 1f));
			toggleHoverBackgroundTexture = TextureUtils.MakeTex(4, 4, new Color(0.4f, 0.4f, 0.4f, 1f));

			targetWidth = collapsedWidth;
		}

		public void Initialize(EditorController controller, MapManager map, Camera cam)
		{
			editorController = controller;
			mapManager = map;
			camera = cam;
		}

		public bool IsGuiControlActive()
		{
			return GUIUtility.hotControl != 0 || isMouseOverTileSelector || EventSystem.current.IsPointerOverGameObject();
		}

		public bool IsMouseOverGui()
		{
			// unchanged – left exactly as you had it
			if (isMouseOverTileSelector)
				return true;

			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y;

			float panelBottomY = GetPanelBottomY();
			float leftPanelX = margin;
			float leftPanelY = panelBottomY + spacing;
			float leftPanelWidth = buttonWidth + 20f;
			float leftPanelHeight = buttonHeight * 6 + spacing * 6; // now 6 buttons

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
			}
		}

		private float GetPanelBottomY()
		{
			var placeholderUI = FindAnyObjectByType<PlaceholderUI>();
			return placeholderUI != null ? placeholderUI.GetPanelBottomY() : margin;
		}

		private void OnGUI()
		{
			if (!editorController || !mapManager || !camera) return;

			float panelBottomY = GetPanelBottomY();
			Rect gridToggleRect = new Rect(margin, panelBottomY + spacing + 0 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect dragButtonRect = new Rect(margin, panelBottomY + spacing + 1 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect paintButtonRect = new Rect(margin, panelBottomY + spacing + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect saveButtonRect = new Rect(margin, panelBottomY + spacing + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect reloadButtonRect = new Rect(margin, panelBottomY + spacing + 4 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect exptButtonRect = new Rect(margin, panelBottomY + spacing + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight);
			Rect importButtonRect = new Rect(margin, panelBottomY + spacing + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight);

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
			toggleStyle.alignment = TextAnchor.MiddleCenter;
			toggleStyle.fixedWidth = buttonWidth;
			toggleStyle.fixedHeight = buttonHeight;

			bool dragToggled = GUI.Toggle(dragButtonRect, currentMode == EditorController.EditorMode.Drag, "Drag", toggleStyle);
			bool paintToggled = GUI.Toggle(paintButtonRect, currentMode == EditorController.EditorMode.Paint, "Paint", toggleStyle);

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
				editorController.UpdateGridLines(gridLinesEnabled);
			}

			// === SAVE DATABASE BUTTON ===
			GUIStyle saveButtonStyle = new GUIStyle(GUI.skin.button);
			saveButtonStyle.normal.background = saveBackgroundTexture;
			saveButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			saveButtonStyle.fontSize = 14;
			saveButtonStyle.alignment = TextAnchor.MiddleCenter;
			saveButtonStyle.fixedWidth = buttonWidth;
			saveButtonStyle.fixedHeight = buttonHeight;

			if (GUI.Button(saveButtonRect, "Save Database", saveButtonStyle))
			{
#if UNITY_EDITOR
				ResourceManager.SaveDatabase();
#else
                Debug.Log("Save Database only works in Editor");
#endif
			}

			// === RELOAD DATABASE BUTTON ===
			GUIStyle reloadButtonStyle = new GUIStyle(GUI.skin.button);
			reloadButtonStyle.normal.background = reloadBackgroundTexture;  // ← uses pre-made texture
			reloadButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			reloadButtonStyle.fontSize = 14;
			reloadButtonStyle.alignment = TextAnchor.MiddleCenter;
			reloadButtonStyle.fixedWidth = buttonWidth;
			reloadButtonStyle.fixedHeight = buttonHeight;

			if (GUI.Button(reloadButtonRect, "Reload Database", reloadButtonStyle))
			{
#if UNITY_EDITOR
				ResourceManager.LoadDatabase();
				Debug.Log("Database reloaded from original project asset");
#else
                Debug.Log("Reload Database only works in Editor");
#endif
			}

			// === EXPORT BUTTON ===
			GUIStyle exportButtonStyle = new GUIStyle(GUI.skin.button);
			exportButtonStyle.normal.background = saveBackgroundTexture;
			exportButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			exportButtonStyle.fontSize = 14;
			exportButtonStyle.alignment = TextAnchor.MiddleCenter;
			exportButtonStyle.fixedWidth = buttonWidth;
			exportButtonStyle.fixedHeight = buttonHeight;
			if (GUI.Button(exptButtonRect, "Export", exportButtonStyle))
				ExportCurrentMapAsAtomic();         
			
			// === IMPORT MAP BUTTON ===
			GUIStyle importButtonStyle = new GUIStyle(GUI.skin.button);
			importButtonStyle.normal.background = reloadBackgroundTexture; // reuse blue style
			importButtonStyle.padding = new RectOffset(10, 10, 5, 5);
			importButtonStyle.fontSize = 14;
			importButtonStyle.alignment = TextAnchor.MiddleCenter;
			importButtonStyle.fixedWidth = buttonWidth;
			importButtonStyle.fixedHeight = buttonHeight;

			if (GUI.Button(importButtonRect, "Import Map", importButtonStyle))
			{
				string path = EditorUtility.OpenFilePanel(
					"Import Atomic Map",
					ResourceSerializer.GetExportFolder(),
					"json"
				);

				if (!string.IsNullOrEmpty(path))
				{
					ResourceSerializer.ImportAtomicMap(path);

					// Optional: auto-reload if same name
					var main = FindFirstObjectByType<MainController>();
					if (main != null && mapManager != null)
					{
						string importedName = System.IO.Path.GetFileNameWithoutExtension(path);
						if (mapManager.CurrentMap.name == importedName)
						{
							main.ReloadCurrentMap();
						}
					}
				}
			}

			// Mode switching
			if (dragToggled && currentMode != EditorController.EditorMode.Drag)
			{
				currentMode = EditorController.EditorMode.Drag;
				editorController.SetMode(EditorController.EditorMode.Drag);
				GeometryUtil.HideGhostTile();
			}
			else if (paintToggled && currentMode != EditorController.EditorMode.Paint)
			{
				currentMode = EditorController.EditorMode.Paint;
				editorController.SetMode(EditorController.EditorMode.Paint);
				targetWidth = collapsedWidth;
				tileSelectorWidth = collapsedWidth;
				animationStartTime = Time.time;
			}

			// === TILE SELECTOR (unchanged) ===
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
				scrollPosition = GUI.BeginScrollView(
					scrollViewRect,
					scrollPosition,
					new Rect(0, 0, tileSelectorWidth - 40, ResourceManager.Definitions.Count * 40)
				);

				for (int i = 0; i < ResourceManager.Definitions.Count; i++)
				{
					var definition = ResourceManager.Definitions[i];
					string displayName = $"{definition.id} ({definition.texture})";
					Rect buttonRect = new(0, i * 40, tileSelectorWidth - 40, 35);

					if (i == tempSelectedDefinitionGlobalIndex)
					{
						GUI.color = Color.green;
					}
					if (GUI.Button(buttonRect, displayName))
					{
						tempSelectedDefinitionGlobalIndex = i;
						var selectedDefinition = ResourceManager.Definitions[i];
						int selectedMapDefIndex = mapManager.GetOrAddMapDefIndex(selectedDefinition.id);
						if (selectedMapDefIndex >= 0 && editorController.PaintMode != null)
						{
							editorController.PaintMode.SetDeinitionfIndex(selectedMapDefIndex, i);
							GeometryUtil.DestroyGhostTile();
							GeometryUtil.UpdateGhostTile(camera, mapManager, selectedDefinition);
						}
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
			if (reloadBackgroundTexture != null) Destroy(reloadBackgroundTexture);     // ← cleaned up
			if (gridButtonBackgroundTexture != null) Destroy(gridButtonBackgroundTexture);
			if (toggleOffBackgroundTexture != null) Destroy(toggleOffBackgroundTexture);
			if (toggleOnBackgroundTexture != null) Destroy(toggleOnBackgroundTexture);
			if (toggleHoverBackgroundTexture != null) Destroy(toggleHoverBackgroundTexture);
		}

		public void SetGridLinesEnabled(bool enabled)
		{
			gridLinesEnabled = enabled;
		}

		public bool GetGridLinesEnabled()
		{
			return gridLinesEnabled;
		}

		public int GetSelectedDefinitionGlobalIndex()
		{
			return tempSelectedDefinitionGlobalIndex;
		}

		public void SetSelectedDefinitionGlobalIndex(int index)
		{
			tempSelectedDefinitionGlobalIndex = index;
		}

		public void ExportCurrentMapAsAtomic()
		{
			ResourceSerializer.ExportAtomicMap(mapManager.CurrentMap);
		}
	}
}