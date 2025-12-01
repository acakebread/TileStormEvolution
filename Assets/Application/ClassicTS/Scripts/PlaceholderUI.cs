using UnityEngine;
using System;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
    public class PlaceholderUI : MonoBehaviour
    {
        public event Action<PreviewMode> OnModeChanged;
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

        private readonly Color panelColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
        private readonly Color selectedTextColor = Color.green;
        private readonly Color unselectedTextColor = new Color(0.5f, 0.8f, 0.5f);
        private readonly Color modeUnselectedBg = new Color(0.1f, 0.3f, 0.1f);
        private readonly Color modeSelectedBg = new Color(0.4f, 0.4f, 0.15f);

        private Texture2D panelTexture;
        
        private void Awake() => panelTexture = TextureUtils.MakeTex(1, 1, panelColor);

        private void Update()
        {
            // Handle arrow key navigation
            if (InputUtility.GetKeyRepeat(KeyCode.LeftArrow)) OnChangeMapRequested?.Invoke(-1);
            if (InputUtility.GetKeyRepeat(KeyCode.RightArrow)) OnChangeMapRequested?.Invoke(1);

            // Always visible in Editor mode
            if (PreviewSettings.CurrentMode == PreviewMode.Editor)
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
            if (guiRect.y < -90 && !isGuiVisible) return;

            GUIManager.RegisterGuiRect(new Rect(0, guiRect.y - panelGap, Screen.width, guiRect.height + 2 * panelGap));

            GUI.skin.button.fontSize = 16;
            GUI.skin.label.fontSize = 16;

            float currentX = buttonStartX;
            float y = guiRect.y;
            float panelHeight = buttonHeight + (2 * panelGap);
            float panelY = y - panelGap;

            // Background panel
            GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
            GUI.Box(new Rect(0, panelY, Screen.width, panelHeight), "", panelStyle);

            // Map name
            GUI.Label(new Rect(currentX, y, labelWidth, buttonHeight), "Map:");
            currentX += labelWidth;

            GUI.Label(new Rect(currentX, y, mapNameWidth, buttonHeight), PreviewSettings.LoadMapName);
            currentX += mapNameWidth + spacing;

            // Mode buttons (Editor / Player / Cinema)
            DrawModeButton(ref currentX, y, "Editor", PreviewMode.Editor);
            DrawModeButton(ref currentX, y, "Player", PreviewMode.Player);
            DrawModeButton(ref currentX, y, "Cinema", PreviewMode.Cinema);

            currentX += 20;

            // Navigation & action buttons using GuiUtils
            GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "<< Level", new Color(0.3f, 0.6f, 1f), () => OnChangeMapRequested?.Invoke(-1));
            currentX += buttonWidth + spacing;

            GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Level >>", new Color(0.3f, 0.6f, 1f), () => OnChangeMapRequested?.Invoke(1));
            currentX += buttonWidth + spacing;

            GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Reload", new Color(0.6f, 0.6f, 0.2f), () => OnChangeMapRequested?.Invoke(0));
            currentX += buttonWidth + spacing;

            GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Preset", new Color(0.2f, 0.8f, 0.2f), () => OnPresetRequested?.Invoke());
            currentX += buttonWidth + spacing;

            GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Scramble", new Color(0.8f, 0.6f, 0.2f), () => OnScrambleRequested?.Invoke());
            currentX += buttonWidth + spacing;

            GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Solve", new Color(0.8f, 0.2f, 0.2f), () => OnSolveRequested?.Invoke());

            guiRect.height = panelHeight;
        }

		private void DrawModeButton(ref float currentX, float y, string label, PreviewMode mode)
		{
			bool isSelected = PreviewSettings.CurrentMode == mode;
			Color buttonColor = isSelected ? modeSelectedBg : modeUnselectedBg;
			Color textColor = isSelected ? selectedTextColor : unselectedTextColor;

			// Save state
			Color prevContent = GUI.contentColor;

			// This is the key: force the text color that ColoredButton will use
			GUI.contentColor = textColor;

			GuiUtils.ColoredButton(
				new Rect(currentX, y, buttonWidth, buttonHeight),
				label,
				buttonColor,
				() =>
				{
					PreviewSettings.CurrentMode = mode;
					OnModeChanged?.Invoke(mode);
				});

			// Restore
			GUI.contentColor = prevContent;

			currentX += buttonWidth + spacing;
		}
	}
}