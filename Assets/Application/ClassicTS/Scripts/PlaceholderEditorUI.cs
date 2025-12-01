using UnityEngine;
using UnityEngine.EventSystems;
using System;

namespace ClassicTilestorm
{
	public class PlaceholderEditorUI : MonoBehaviour
	{
		private float panelBottomY = 10f;

		private const float margin = 10f;
		private const float spacing = 10f;
		private const float buttonWidth = 135f;
		private const float buttonHeight = 30f;

		private float tileSelectorWidth = 120f;
		private const float fullWidth = 300f;
		private const float collapsedWidth = 120f;
		private float mouseExitTime = 0f;
		private bool isMouseOverTileSelector = false;
		private const float autoHideDelay = 1f;
		private float targetWidth = 120f;
		private float animationStartTime = 0f;
		private const float animationDuration = 0.3f;
		private Vector2 scrollPosition = Vector2.zero;

		public event Action<string> OnModeChanged;
		public event Action<bool> OnGridLinesToggled;
		public event Action OnSaveDatabaseRequested;
		public event Action OnReloadDatabaseRequested;
		public event Action OnExportMapRequested;
		public event Action OnImportMapRequested;
		public event Action OnResizeMapTestRequested;
		public event Action OnCropMapTestRequested;
		public event Action<Definition> OnTileSelected;

		public void Initialize(float bottomY) => panelBottomY = bottomY;

		public bool IsGuiControlActive()
			=> GUIUtility.hotControl != 0 || isMouseOverTileSelector || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

		public bool IsMouseInsideWindow()
		{
			Vector2 p = Input.mousePosition;
			return p.x >= 0 && p.x <= Screen.width && p.y >= 0 && p.y <= Screen.height;
		}

		public bool IsMouseOverGui()
		{
			if (isMouseOverTileSelector) return true;

			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y;

			float leftY = panelBottomY + spacing;
			if (new Rect(margin, leftY, buttonWidth + 20f, buttonHeight * 9 + spacing * 9).Contains(mousePos))
				return true;

			float tx = Screen.width - tileSelectorWidth - margin;
			float ty = panelBottomY + spacing;
			float th = Screen.height - ty - margin;
			return new Rect(tx, ty, tileSelectorWidth, th).Contains(mousePos);
		}

		public void UpdatePaintMode()
		{
			Vector2 mp = Input.mousePosition;
			mp.y = Screen.height - mp.y;

			bool wasOver = isMouseOverTileSelector;

			float x = Screen.width - tileSelectorWidth - margin;
			float y = panelBottomY + spacing;
			float h = Screen.height - y - margin;

			isMouseOverTileSelector = new Rect(x, y, tileSelectorWidth, h).Contains(mp);

			// EXPAND: when mouse enters
			if (isMouseOverTileSelector && !wasOver)
			{
				targetWidth = fullWidth;
				animationStartTime = Time.time;
				mouseExitTime = 0f; // cancel any collapse timer
			}

			// COLLAPSE: start timer only once when mouse leaves
			if (!isMouseOverTileSelector && wasOver && mouseExitTime == 0f)
			{
				mouseExitTime = Time.time;
			}

			// After 1 second away → collapse
			if (!isMouseOverTileSelector && mouseExitTime > 0f && Time.time - mouseExitTime >= autoHideDelay)
			{
				targetWidth = collapsedWidth;
				animationStartTime = Time.time;
				mouseExitTime = 0f;
			}

			// Smooth animation
			float t = Mathf.Clamp01((Time.time - animationStartTime) / animationDuration);
			tileSelectorWidth = Mathf.Lerp(tileSelectorWidth, targetWidth, t);
		}

		private static GUIStyle coloredButtonStyle;
		private static Texture2D solidWhite;
		private static Texture2D solidDark;
		private static Texture2D solidBright;

		private static void InitStaticTextures()
		{
			// These are created ONCE, when the editor loads or script recompiles
			solidWhite = CreateTex(Color.white);
			solidDark = CreateTex(new Color(0.75f, 0.75f, 0.75f));
			solidBright = CreateTex(new Color(1.15f, 1.15f, 1.15f));
		}
		private static void EnsureStyles()
		{
			if (coloredButtonStyle != null) return;
			if (solidWhite == null) InitStaticTextures();

			coloredButtonStyle = new GUIStyle();

			// CRITICAL: Assign a real font or text disappears — FIXED PATH FOR MODERN UNITY
			coloredButtonStyle.font = GUI.skin.button.font;  // Copy from skin (usually safe)
			if (coloredButtonStyle.font == null)
				coloredButtonStyle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");  // ← THIS WAS THE CRASH

			coloredButtonStyle.fontSize = 12;
			coloredButtonStyle.fontStyle = FontStyle.Bold;
			coloredButtonStyle.alignment = TextAnchor.MiddleCenter;

			// Proper button look
			coloredButtonStyle.border = new RectOffset(8, 8, 8, 8);
			coloredButtonStyle.padding = new RectOffset(4, 4, 4, 4);

			// Backgrounds
			coloredButtonStyle.normal.background = solidWhite;
			coloredButtonStyle.hover.background = solidBright;
			coloredButtonStyle.active.background = solidDark;
			coloredButtonStyle.onNormal.background = solidWhite;
			coloredButtonStyle.onHover.background = solidBright;
			coloredButtonStyle.onActive.background = solidDark;

			// TEXT — NOW IT WILL ACTUALLY SHOW (WHITE, NOT BLACK)
			Color textCol = Color.white;
			coloredButtonStyle.normal.textColor = textCol;
			coloredButtonStyle.hover.textColor = textCol;
			coloredButtonStyle.active.textColor = textCol;
			coloredButtonStyle.focused.textColor = textCol;
			coloredButtonStyle.onNormal.textColor = textCol;
			coloredButtonStyle.onHover.textColor = textCol;
			coloredButtonStyle.onActive.textColor = textCol;
			coloredButtonStyle.onFocused.textColor = textCol;
		}

		private void ColoredButton(Rect r, string text, Color col, Action onClick)
		{
			EnsureStyles();

			// Save everything
			Color prevColor = GUI.color;
			Color prevContentColor = GUI.contentColor;
			Color prevBackground = GUI.backgroundColor;

			// Set background tint (this colors the button)
			GUI.backgroundColor = col;

			// CRITICAL: Force content color to white so text stays white
			GUI.contentColor = Color.white;

			// GUI.color must be white or it will still tint the text!
			GUI.color = Color.white;

			if (GUI.Button(r, text, coloredButtonStyle))
				onClick?.Invoke();

			// Restore everything
			GUI.color = prevColor;
			GUI.contentColor = prevContentColor;
			GUI.backgroundColor = prevBackground;
		}

		private static Texture2D CreateTex(Color col)
		{
			var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			tex.SetPixel(0, 0, col);
			tex.Apply();
			return tex;
		}

		public void DrawMainUI(string mode, bool gridVisible)
		{
			float y = panelBottomY + spacing;

			Color prevColor = GUI.color;
			Color prevContentColor = GUI.contentColor;
			Color prevBgColor = GUI.backgroundColor;

			ColoredButton(new Rect(margin, y + 0 * (buttonHeight + spacing), buttonWidth, buttonHeight),
				gridVisible ? "Hide Grid" : "Show Grid",
				new Color(0.5f, 0.5f, 0.5f),
				() => OnGridLinesToggled?.Invoke(!gridVisible));

			GUI.contentColor = mode == "Drag" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 1 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Drag"))
				OnModeChanged?.Invoke("Drag");

			GUI.contentColor = mode == "Paint" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Paint"))
				OnModeChanged?.Invoke("Paint");
			GUI.contentColor = Color.white;

			GUI.color = Color.white;
			GUI.contentColor = Color.white;
			GUI.backgroundColor = Color.white;

			ColoredButton(new Rect(margin, y + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Resize Test", new Color(0.7f, 0.6f, 0.2f), OnResizeMapTestRequested);
			ColoredButton(new Rect(margin, y + 4 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Crop Test", new Color(0.7f, 0.6f, 0.2f), OnCropMapTestRequested);
			ColoredButton(new Rect(margin, y + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Import Map", new Color(0.2f, 0.6f, 1f), OnImportMapRequested);
			ColoredButton(new Rect(margin, y + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Export Map", new Color(0.8f, 0.2f, 0.2f), OnExportMapRequested);
			ColoredButton(new Rect(margin, y + 7 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Reload Database", new Color(0.2f, 0.6f, 1f), OnReloadDatabaseRequested);
			ColoredButton(new Rect(margin, y + 8 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Save Database", new Color(0.8f, 0.2f, 0.2f), OnSaveDatabaseRequested);

			GUI.color = prevColor;
			GUI.contentColor = prevContentColor;
			GUI.backgroundColor = prevBgColor;
		}

		public void DrawPaintUI(string selectedId)
		{
			float tx = Screen.width - tileSelectorWidth - margin;
			float ty = panelBottomY + spacing;
			float th = Screen.height - ty - margin;
			Rect selectorRect = new Rect(tx, ty, tileSelectorWidth, th);

			GUI.backgroundColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
			GUI.Box(selectorRect, "Tile Selector");
			GUI.backgroundColor = Color.white;

			Rect view = new Rect(tx + 10, ty + 30, tileSelectorWidth - 20, th - 40);
			scrollPosition = GUI.BeginScrollView(view, scrollPosition,
				new Rect(0, 0, tileSelectorWidth - 40, ResourceManager.Definitions.Count * 40));

			for (int i = 0; i < ResourceManager.Definitions.Count; i++)
			{
				var def = ResourceManager.Definitions[i];
				string label = $"{def.id} ({def.texture})";
				Rect btn = new Rect(0, i * 40, tileSelectorWidth - 40, 35);

				if (def.id == selectedId) GUI.color = Color.green;
				if (GUI.Button(btn, label)) OnTileSelected?.Invoke(def);
				GUI.color = Color.white;
			}
			GUI.EndScrollView();
		}
	}
}