using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GameController))]
	public class PlaceholderUI : MonoBehaviour
	{
		private GameController gameController => GetComponent<GameController>();

		// basic animation system
		private bool isGuiVisible = false;
		private float hideTimer = 0f;
		private Rect guiRect = new Rect(0, -100, Screen.width, 40); // Full width
		private float targetY = -100; // Target Y position for animation
		private bool isMouseOverGui = false;

		private readonly float hideDelay = 3f; // 3 seconds before hiding
		private readonly float animationSpeed = 300f; // Pixels per second for animation

		// Button layout constants
		private readonly float buttonWidth = 100f;
		private readonly float buttonHeight = 30f;
		private readonly float spacing = 10f;
		private readonly float labelWidth = 50f; // Increased from 40f to prevent clipping
		private readonly float panelGap = 5f; // Gap above and below buttons within panel
		private readonly float buttonStartX = 10f; // Left margin for buttons
		private readonly Color panelColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
		private readonly Color selectedTextColor = Color.green;
		private readonly Color unselectedTextColor = new Color(0.5f, 0.8f, 0.5f); // Dull green

		private Texture2D panelTexture;

		private void Awake()
		{
			panelTexture = MakeTex(1, 1, panelColor);
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
			if (InputUtility.GetKeyRepeat(KeyCode.LeftArrow)) ChangeMap(-1); // Previous map
			if (InputUtility.GetKeyRepeat(KeyCode.RightArrow)) ChangeMap(1); // Next map

			// Check if mouse is near the top of the screen (within 50 pixels)
			var mousePos = Input.mousePosition;
			var isMouseNearTop = mousePos.y >= Screen.height - 50;

			// Check if mouse is over the GUI area
			isMouseOverGui = guiRect.Contains(new Vector2(mousePos.x, Screen.height - mousePos.y));

			if (isMouseNearTop || isMouseOverGui)
			{
				// Show GUI by setting target position to panelGap (buttons start with gap from top)
				targetY = panelGap;
				isGuiVisible = true;
				hideTimer = 0f; // Reset timer when mouse is near or over GUI
			}
			else if (isGuiVisible)
			{
				// Increment timer when mouse is not over GUI or near top
				hideTimer += Time.deltaTime;
				if (hideTimer >= hideDelay)
				{
					// Hide GUI by setting target position off-screen
					targetY = -100;
					isGuiVisible = false;
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
				gameController.LoadMap(); // Reload current map
				return;
			}

			var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == PreviewSettings.LoadMapName);
			currentIndex = (DatabaseLoader.Maps.Count + currentIndex + delta) % DatabaseLoader.Maps.Count;
			PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
			gameController.LoadMap();
		}

		private void OnGUI()
		{
			// Only draw GUI if it's visible or animating
			if (guiRect.y < -90 && !isGuiVisible) return; // Skip drawing if fully hidden

			// Set styles
			GUI.skin.button.fontSize = 16;
			GUI.skin.label.fontSize = 16; // Adjusted to match buttons; previously no labels so no effect

			Color originalColor = GUI.color;
			GUI.color = new Color(0.75f, 0.75f, 1.0f);

			float currentX = buttonStartX;
			float y = guiRect.y; // Buttons and label at animated y

			// Full screen width panel background, solid bar filling the entire top region, animating with buttons
			float panelHeight = buttonHeight + (2 * panelGap);
			float panelY = y - panelGap; // Panel starts panelGap above buttons
			GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
			panelStyle.normal.background = panelTexture;
			GUI.color = Color.white; // Ensure no tinting of the panel texture
			GUI.Box(new Rect(0, panelY, Screen.width, panelHeight), GUIContent.none, panelStyle);

			// Mode label (aligned with buttons, no offset, using custom style for vertical alignment)
			GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
			labelStyle.alignment = TextAnchor.MiddleLeft;
			labelStyle.normal.textColor = Color.green;
			GUI.color = originalColor;
			GUI.Label(new Rect(currentX, y, labelWidth, buttonHeight), "Mode:", labelStyle);

			// Advance for label
			currentX += labelWidth + spacing;

			// Player button
			bool isPlayerSelected = PreviewSettings.CurrentMode == PreviewMode.Player;
			GUI.backgroundColor = isPlayerSelected ? Color.green : Color.gray;
			GUI.color = isPlayerSelected ? selectedTextColor : unselectedTextColor;
			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Player"))
				gameController.SetPreviewMode(PreviewMode.Player);
			GUI.color = originalColor;
			GUI.backgroundColor = Color.white;
			currentX += buttonWidth + spacing;

			// Cinema button
			bool isCinemaSelected = PreviewSettings.CurrentMode == PreviewMode.Cinema;
			GUI.backgroundColor = isCinemaSelected ? Color.green : Color.gray;
			GUI.color = isCinemaSelected ? selectedTextColor : unselectedTextColor;
			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Cinema"))
				gameController.SetPreviewMode(PreviewMode.Cinema);
			GUI.color = originalColor;
			GUI.backgroundColor = Color.white;
			currentX += buttonWidth + spacing;

			// Editor button
			bool isEditorSelected = PreviewSettings.CurrentMode == PreviewMode.Editor;
			GUI.backgroundColor = isEditorSelected ? Color.green : Color.gray;
			GUI.color = isEditorSelected ? selectedTextColor : unselectedTextColor;
			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Editor"))
				gameController.SetPreviewMode(PreviewMode.Editor);
			GUI.color = originalColor;
			GUI.backgroundColor = Color.white;
			currentX += buttonWidth + spacing;

			// End of mode section

			currentX += 20;

			// Navigation buttons
			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "<< Level")) ChangeMap(-1);
			currentX += buttonWidth + spacing;

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Level >>")) ChangeMap(1);
			currentX += buttonWidth + spacing;

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Reload")) ChangeMap(0);
			currentX += buttonWidth + spacing;

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Scramble")) gameController.mapManager.Scramble();
			currentX += buttonWidth + spacing;

			if (GUI.Button(new Rect(currentX, y, buttonWidth, buttonHeight), "Solve")) gameController.mapManager.Solve();

			// Update guiRect height if needed, but since full width, it's fine
			guiRect.height = panelHeight; // Match panel height for mouse detection

			GUI.color = originalColor;
		}
	}
}