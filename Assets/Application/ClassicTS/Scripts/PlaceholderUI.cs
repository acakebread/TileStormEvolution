using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(MainController))]
	public class PlaceholderUI : MonoBehaviour
	{
		private MainController mainController => GetComponent<MainController>();

		// Basic animation system
		private bool isGuiVisible = false;
		private float hideTimer = 0f;
		private Rect guiRect = new Rect(0, -100, Screen.width, 40); // Full width
		private float targetY = -100; // Target Y position for animation
		private bool isMouseOverGui = false;

		private readonly float hideDelay = 2f; // 2 seconds before hiding
		private readonly float animationSpeed = 300f; // Pixels per second for animation

		// Button layout constants
		private readonly float buttonWidth = 90f;
		private readonly float buttonHeight = 30f;
		private readonly float buttonStartX = 10f; // Left margin for buttons
		private readonly float spacing = 10f;
		private readonly float labelWidth = 50f;
		private readonly float mapNameWidth = 120f;
		private readonly float panelGap = 5f;
		private readonly Color panelColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
		private readonly Color selectedTextColor = Color.green;
		private readonly Color unselectedTextColor = new Color(0.5f, 0.8f, 0.5f);

		private readonly Color modeUnselectedBg = new Color(0.1f, 0.3f, 0.1f);
		private readonly Color modeSelectedBg = new Color(0.4f, 0.4f, 0.15f);

		private Texture2D unselectedButtonTex;
		private Texture2D selectedButtonTex;
		private Texture2D panelTexture;

		private void Awake()
		{
			panelTexture = MakeTex(1, 1, panelColor);
			unselectedButtonTex = MakeTex(1, 1, modeUnselectedBg);
			selectedButtonTex = MakeTex(1, 1, modeSelectedBg);
		}

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

		private void Update()
		{
			// Handle left/right arrow key inputs for Previous/Next Level with repeat
			if (InputUtility.GetKeyRepeat(KeyCode.LeftArrow)) ChangeMap(-1);
			if (InputUtility.GetKeyRepeat(KeyCode.RightArrow)) ChangeMap(1);

			// In Editor mode, keep GUI visible without hiding
			if (PreviewSettings.CurrentMode == PreviewMode.Editor)
			{
				targetY = panelGap;
				isGuiVisible = true;
				hideTimer = 0f;
			}
			else
			{
				// Check if mouse is near the top of the screen (within 50 pixels)
				var mousePos = Input.mousePosition;
				isMouseOverGui = guiRect.Contains(new Vector2(mousePos.x, Screen.height - mousePos.y));

				if (mousePos.y >= Screen.height - 50 || isMouseOverGui)
				{
					targetY = panelGap;
					isGuiVisible = true;
					hideTimer = 0f;
				}
				else if (isGuiVisible)
				{
					hideTimer += Time.deltaTime;
					if (hideTimer >= hideDelay)
					{
						targetY = -100;
						isGuiVisible = false;
					}
				}
			}

			// Animate the GUI position
			var currentY = guiRect.y;
			var newY = Mathf.MoveTowards(currentY, targetY, animationSpeed * Time.deltaTime);
			guiRect.y = newY;
		}

		public void ChangeMap(int delta)
		{
			if (delta == 0)
			{
				mainController.LoadMap();
				return;
			}

			var currentIndex = ResourceManager.Maps.ToList().FindIndex(m => m.name == PreviewSettings.LoadMapName);
			currentIndex = (ResourceManager.Maps.Count + currentIndex + delta) % ResourceManager.Maps.Count;
			PreviewSettings.LoadMapName = ResourceManager.Maps[currentIndex].name;
			mainController.LoadMap();
		}

		// Get the bottom Y position of the panel in GUI coordinates
		public float GetPanelBottomY()
		{
			return guiRect.y + guiRect.height;
		}

		private void OnGUI()
		{
			// Only draw GUI if it's visible or animating
			if (guiRect.y < -90 && !isGuiVisible) return;

			// Register GUI rect for input blocking
			GUIManager.RegisterGuiRect(new Rect(0, guiRect.y - panelGap, Screen.width, guiRect.height + 2 * panelGap));

			// Set styles
			GUI.skin.button.fontSize = 16;
			GUI.skin.label.fontSize = 16;

			Color originalColor = GUI.color;
			Color originalBgColor = GUI.backgroundColor;
			GUI.color = new Color(0.75f, 0.75f, 1.0f);

			float currentX = buttonStartX;
			float y = guiRect.y;

			// Full screen width panel background
			float panelHeight = buttonHeight + (2 * panelGap);
			float panelY = y - panelGap;
			GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
			panelStyle.normal.background = panelTexture;
			GUI.color = Color.white;
			GUI.Box(new Rect(0, panelY, Screen.width, panelHeight), "", panelStyle);

			// Map name label
			GUI.Label(new Rect(currentX, y, labelWidth, buttonHeight), "Map:");
			currentX += labelWidth;

			GUI.Label(new Rect(currentX, y, mapNameWidth, buttonHeight), PreviewSettings.LoadMapName);
			currentX += mapNameWidth + spacing;

			// Mode buttons with custom colors
			GUIStyle modeButtonStyle = new GUIStyle(GUI.skin.button);

			// Editor button
			bool isEditorSelected = PreviewSettings.CurrentMode == PreviewMode.Editor;
			modeButtonStyle.normal.background = isEditorSelected ? selectedButtonTex : unselectedButtonTex;
			modeButtonStyle.normal.textColor = isEditorSelected ? selectedTextColor : unselectedTextColor;

			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.hover);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.active);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.focused);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.onNormal);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.onHover);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.onActive);

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Editor", modeButtonStyle))
			{
				PreviewSettings.CurrentMode = PreviewMode.Editor;
				mainController.SetPreviewMode(PreviewMode.Editor);
			}
			currentX += buttonWidth + spacing;

			// Player button
			bool isPlayerSelected = PreviewSettings.CurrentMode == PreviewMode.Player;
			modeButtonStyle.normal.background = isPlayerSelected ? selectedButtonTex : unselectedButtonTex;
			modeButtonStyle.normal.textColor = isPlayerSelected ? selectedTextColor : unselectedTextColor;

			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.hover);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.active);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.focused);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.onNormal);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.onHover);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.onActive);

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Player", modeButtonStyle))
			{
				PreviewSettings.CurrentMode = PreviewMode.Player;
				mainController.SetPreviewMode(PreviewMode.Player);
			}
			currentX += buttonWidth + spacing;

			// Cinema button
			bool isCinemaSelected = PreviewSettings.CurrentMode == PreviewMode.Cinema;
			modeButtonStyle.normal.background = isCinemaSelected ? selectedButtonTex : unselectedButtonTex;
			modeButtonStyle.normal.textColor = isCinemaSelected ? selectedTextColor : unselectedTextColor;

			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.hover);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.active);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.focused);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.onNormal);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.onHover);
			CopyStyleState(modeButtonStyle.normal, modeButtonStyle.onActive);

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Cinema", modeButtonStyle))
			{
				PreviewSettings.CurrentMode = PreviewMode.Cinema;
				mainController.SetPreviewMode(PreviewMode.Cinema);
			}
			currentX += buttonWidth + spacing;

			// Reset to default for navigation buttons
			GUI.skin.button.normal.background = null;
			GUI.color = originalColor;
			GUI.backgroundColor = originalBgColor;

			currentX += 20;

			// Navigation buttons (default style)
			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "<< Level")) ChangeMap(-1);
			currentX += buttonWidth + spacing;

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Level >>")) ChangeMap(1);
			currentX += buttonWidth + spacing;

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Reload")) ChangeMap(0);
			currentX += buttonWidth + spacing;

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Preset")) mainController.Preset();
			currentX += buttonWidth + spacing;

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Scramble")) mainController.Scramble();
			currentX += buttonWidth + spacing;

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Solve")) mainController.Solve();

			guiRect.height = panelHeight;

			GUI.color = originalColor;
			GUI.backgroundColor = originalBgColor;
		}

		// Helper to copy GUIStyleState (background and textColor)
		private void CopyStyleState(GUIStyleState source, GUIStyleState target)
		{
			target.background = source.background;
			target.textColor = source.textColor;
		}
	}
}