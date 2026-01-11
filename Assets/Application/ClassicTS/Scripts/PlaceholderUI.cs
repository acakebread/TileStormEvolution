using UnityEngine;
using System;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
    public class PlaceholderUI : MonoBehaviour
    {
        public event Action<ApplicationMode> OnModeChanged;
        public event Action<int> OnChangeMapRequested; // delta or 0 for reload
        public event Action OnPresetRequested;
        public event Action OnScrambleRequested;
        public event Action OnSolveRequested;

        // Basic animation system
        private bool isGuiVisible = false;
        private float hideTimer = 0f;
        private Rect guiRect = new Rect(0, -100, Screen.width, 40);
        private float targetY = -100;
        private bool isMouseOverGui = false;

        private readonly float hideDelay = 2f;
        private readonly float animationSpeed = 300f;

        // Layout constants
        private const float buttonWidth = 90f;
        private const float buttonHeight = 30f;
        private const float buttonStartX = 10f;
        private const float spacing = 10f;
        private const float labelWidth = 50f;
        private const float mapNameWidth = 120f;
        private const float panelGap = 5f;

        private readonly Color panelColor = new (0.2f, 0.2f, 0.4f, 0.75f);
        private readonly Color selectedTextColor = Color.green;
        private readonly Color unselectedTextColor = new (0.5f, 0.8f, 0.5f);
        private readonly Color modeUnselectedBg = new (0.1f, 0.3f, 0.1f);
        private readonly Color modeSelectedBg = new (0.4f, 0.4f, 0.15f);

        private Texture2D panelTexture;
        
        private void Awake() => panelTexture = TextureUtils.MakeTex(1, 1, panelColor);

        public static int PanelBottomY => 40;// guiRect.height;

		private void Update()
        {
            // Handle arrow key navigation
            if (InputUtility.GetKeyRepeat(KeyCode.LeftArrow)) OnChangeMapRequested?.Invoke(-1);
            if (InputUtility.GetKeyRepeat(KeyCode.RightArrow)) OnChangeMapRequested?.Invoke(1);

            // Always visible in Editor mode
            if (ApplicationSettings.CurrentMode == ApplicationMode.Editor)
            {
                targetY = panelGap;
                isGuiVisible = true;
                hideTimer = 0f;
            }
            else
            {
                var mousePos = Input.mousePosition;
                bool nearTop = mousePos.y >= Screen.height - 50;
                isMouseOverGui = guiRect.Contains(new Vector2(mousePos.x, Screen.height - mousePos.y));

                if (nearTop || isMouseOverGui)
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

            // Animate panel
            float newY = Mathf.MoveTowards(guiRect.y, targetY, animationSpeed * Time.deltaTime);
            guiRect.y = newY;
        }

		public float GetPanelBottomY() => guiRect.height + (guiRect.y >= 0 ? guiRect.y : 0);

		private void OnGUI()
		{
			// Visibility check first (safe and cheap)
			if (guiRect.y < -90 && !isGuiVisible)
				return;

			// ALWAYS run this setup — needed for input handling
			GUIManager.RegisterGuiRect(new Rect(0, guiRect.y - panelGap, Screen.width, guiRect.height + 2 * panelGap));

			GUI.skin.button.fontSize = 16;
			GUI.skin.label.fontSize = 16;

			float currentX = buttonStartX;
			float y = guiRect.y;
			float panelHeight = buttonHeight + (2 * panelGap);
			float panelY = y - panelGap;

			// Draw background EVERY Repaint — no conditional, no flag
			GUI.Box(new Rect(0, panelY, Screen.width, panelHeight), "", new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } });

			// ALWAYS draw all buttons and labels — critical for input + visuals
			GUI.Label(new Rect(currentX, y, labelWidth, buttonHeight), "Map:");
			currentX += labelWidth;

			GUI.Label(new Rect(currentX, y, mapNameWidth, buttonHeight), ApplicationSettings.LoadMapName);
			currentX += mapNameWidth + spacing;

			DrawModeButton(currentX, y, "Editor", ApplicationMode.Editor);
			currentX += buttonWidth + spacing;
			DrawModeButton(currentX, y, "Player", ApplicationMode.Player);
			currentX += buttonWidth + spacing;
			DrawModeButton(currentX, y, "Cinema", ApplicationMode.Cinema);
			currentX += buttonWidth + spacing;

			currentX += 20;

			GuiUtils.ColoredRepeatButton(new Rect(currentX, y, buttonWidth, buttonHeight), "<< Level", new Color(0.3f, 0.6f, 1f), () => OnChangeMapRequested?.Invoke(-1), initialDelay: 0.35f);
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredRepeatButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Level >>", new Color(0.3f, 0.6f, 1f), () => OnChangeMapRequested?.Invoke(1), initialDelay: 0.35f);
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Reload", new Color(0.6f, 0.6f, 0.2f), () => OnChangeMapRequested?.Invoke(0));
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Preset", new Color(0.2f, 0.8f, 0.2f), () => OnPresetRequested?.Invoke());
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredRepeatButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Scramble", new Color(0.8f, 0.6f, 0.2f), () => OnScrambleRequested?.Invoke(), initialDelay: 0.1f, repeatInterval: 0f);
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Solve", new Color(0.8f, 0.2f, 0.2f), () => OnSolveRequested?.Invoke());

			guiRect.height = panelHeight;

			void DrawModeButton(float cx, float cy, string label, ApplicationMode mode)
			{
				bool isSelected = ApplicationSettings.CurrentMode == mode;
				Color buttonColor = isSelected ? modeSelectedBg : modeUnselectedBg;
				Color textColor = isSelected ? selectedTextColor : unselectedTextColor;

				Color prevContent = GUI.contentColor;
				GUI.contentColor = textColor;

				GuiUtils.ColoredButton(
					new Rect(cx, cy, buttonWidth, buttonHeight),
					label,
					buttonColor,
					() =>
					{
						ApplicationSettings.CurrentMode = mode;
						OnModeChanged?.Invoke(mode);
					});

				GUI.contentColor = prevContent;
			}
		}

		////I don't think this cures anything
		//private static bool hasDrawnThisFrame = false;
		//private void LateUpdate() => hasDrawnThisFrame = false;

		//private void OnGUI()
		//{
		//	// Visibility check first (safe and cheap)
		//	if (guiRect.y < -90 && !isGuiVisible)
		//		return;

		//	// ALWAYS run this setup — needed for input handling
		//	GUIManager.RegisterGuiRect(new Rect(0, guiRect.y - panelGap, Screen.width, guiRect.height + 2 * panelGap));

		//	GUI.skin.button.fontSize = 16;
		//	GUI.skin.label.fontSize = 16;

		//	float currentX = buttonStartX;
		//	float y = guiRect.y;
		//	float panelHeight = buttonHeight + (2 * panelGap);
		//	float panelY = y - panelGap;

		//	// Draw background ONLY once per frame on Repaint
		//	if (Event.current.type == EventType.Repaint && !hasDrawnThisFrame)
		//	{
		//		GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
		//		panelStyle.normal.background = panelTexture;
		//		GUI.Box(new Rect(0, panelY, Screen.width, panelHeight), "", panelStyle);

		//		hasDrawnThisFrame = true; // Mark background as drawn
		//	}

		//	// ALWAYS run labels and ALL buttons — critical for layout, hover, clicks, repeat
		//	GUI.Label(new Rect(currentX, y, labelWidth, buttonHeight), "Map:");
		//	currentX += labelWidth;

		//	GUI.Label(new Rect(currentX, y, mapNameWidth, buttonHeight), PreviewSettings.LoadMapName);
		//	currentX += mapNameWidth + spacing;

		//	DrawModeButton(currentX, y, "Editor", PreviewMode.Editor);
		//	currentX += buttonWidth + spacing;
		//	DrawModeButton(currentX, y, "Player", PreviewMode.Player);
		//	currentX += buttonWidth + spacing;
		//	DrawModeButton(currentX, y, "Cinema", PreviewMode.Cinema);
		//	currentX += buttonWidth + spacing;

		//	currentX += 20;

		//	GuiUtils.ColoredRepeatButton(new Rect(currentX, y, buttonWidth, buttonHeight), "<< Level", new Color(0.3f, 0.6f, 1f), () => OnChangeMapRequested?.Invoke(-1), initialDelay: 0.35f);
		//	currentX += buttonWidth + spacing;

		//	GuiUtils.ColoredRepeatButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Level >>", new Color(0.3f, 0.6f, 1f), () => OnChangeMapRequested?.Invoke(1), initialDelay: 0.35f);
		//	currentX += buttonWidth + spacing;

		//	GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Reload", new Color(0.6f, 0.6f, 0.2f), () => OnChangeMapRequested?.Invoke(0));
		//	currentX += buttonWidth + spacing;

		//	GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Preset", new Color(0.2f, 0.8f, 0.2f), () => OnPresetRequested?.Invoke());
		//	currentX += buttonWidth + spacing;

		//	GuiUtils.ColoredRepeatButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Scramble", new Color(0.8f, 0.6f, 0.2f), () => OnScrambleRequested?.Invoke(), initialDelay: 0.1f, repeatInterval: 0f);
		//	currentX += buttonWidth + spacing;

		//	GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Solve", new Color(0.8f, 0.2f, 0.2f), () => OnSolveRequested?.Invoke());

		//	guiRect.height = panelHeight;

		//	// Local DrawModeButton (unchanged)
		//	void DrawModeButton(float cx, float cy, string label, PreviewMode mode)
		//	{
		//		bool isSelected = PreviewSettings.CurrentMode == mode;
		//		Color buttonColor = isSelected ? modeSelectedBg : modeUnselectedBg;
		//		Color textColor = isSelected ? selectedTextColor : unselectedTextColor;

		//		Color prevContent = GUI.contentColor;
		//		GUI.contentColor = textColor;

		//		GuiUtils.ColoredButton(
		//			new Rect(cx, cy, buttonWidth, buttonHeight),
		//			label,
		//			buttonColor,
		//			() =>
		//			{
		//				PreviewSettings.CurrentMode = mode;
		//				OnModeChanged?.Invoke(mode);
		//			});

		//		GUI.contentColor = prevContent;
		//	}
		//}
	}
}