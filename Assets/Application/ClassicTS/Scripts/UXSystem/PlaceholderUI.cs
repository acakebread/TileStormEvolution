using UnityEngine;
using System;
using System.Collections.Generic;
using MassiveHadronLtd;
using MassiveHadronLtd.UI;

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
		private const float buttonWidth = 82f;
		private const float compactButtonWidth = 56f;
		private const float buttonHeight = 30f;
		private const float buttonStartX = 10f;
		private const float spacing = 8f;
		private const float labelWidth = 50f;
		private const float mapNameWidth = 120f;
		private const float panelGap = 5f;

		private readonly Color panelColor = new(0.2f, 0.2f, 0.4f, 0.75f);
		private readonly Color selectedTextColor = Color.green;
		private readonly Color unselectedTextColor = new(0.5f, 0.8f, 0.5f);
		private readonly Color modeUnselectedBg = new(0.1f, 0.3f, 0.1f);
		private readonly Color modeSelectedBg = new(0.4f, 0.4f, 0.15f);

		private Texture2D panelTexture;
		private GUIStyle mapNameStyle;

		// === NEW: Queue for safe processing of map changes ===
		private readonly Queue<int> mapChangeQueue = new Queue<int>();
		private int guard = 0;

		private void Awake() => panelTexture = TextureUtils.MakeTex(1, 1, panelColor);

		public static int PanelBottomY => 40;

		private void onChangeMapRequested(int value)
		{
			if (++guard > 1) return;
			mapChangeQueue.Enqueue(value);   // Queue instead of immediate invoke
		}

		//private void LateUpdate() => guard = 0;

		private void Update()
		{
			guard = 0;
			while (mapChangeQueue.Count > 0)
			{
				int delta = mapChangeQueue.Dequeue();
				OnChangeMapRequested?.Invoke(delta);
			}

			// Arrow keys remain in Update() — safe and responsive
			if (!UIFocusManager.AnyUIHasKeyboardFocus())
			{
				if (InputUtility.GetKeyRepeat(KeyCode.LeftArrow)) onChangeMapRequested(-1);
				if (InputUtility.GetKeyRepeat(KeyCode.RightArrow)) onChangeMapRequested(1);
			}

			// Visibility / animation logic unchanged
			if (ApplicationSettings.CurrentMode == ApplicationMode.Editor)
			{
				targetY = panelGap;
				isGuiVisible = true;
				hideTimer = 0f;
			}
			else
			{
				var mousePos = InputX.mousePosition;
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

			float newY = Mathf.MoveTowards(guiRect.y, targetY, animationSpeed * Time.deltaTime);
			guiRect.y = newY;
		}

		public float GetPanelBottomY() => guiRect.height + (guiRect.y >= 0 ? guiRect.y : 0);

		public static bool IsMouseOverGui() => new Rect(0, 0, Screen.width, 40)
			.Contains(new Vector3(InputX.mousePosition.x, Screen.height - InputX.mousePosition.y, InputX.mousePosition.z));

		private void OnGUI()
		{
			if (guiRect.y < -90 && !isGuiVisible)
				return;

			GUI.skin.button.fontSize = 16;
			GUI.skin.label.fontSize = 16;

			float currentX = buttonStartX;
			float y = guiRect.y;
			float panelHeight = buttonHeight + (2 * panelGap);
			float panelY = y - panelGap;

			//GUI.Box(new Rect(0, panelY, Screen.width, panelHeight), "",
			//	new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } });

			GUI.DrawTexture(new Rect(0, panelY, Screen.width, panelHeight), panelTexture);

			// Invisible options button
			GuiUtils.ColoredButton(new Rect(currentX, y, labelWidth + mapNameWidth, buttonHeight), "",
				new Color(0f, 0f, 0f, 0f), () => UIController.OpenPanel<OptionsPanel>());

			GUI.Label(new Rect(currentX, y, labelWidth, buttonHeight), "Map:");
			currentX += labelWidth;

			GUI.Label(new Rect(currentX, y, mapNameWidth, buttonHeight), MainController.CurrentMap?.name ?? ApplicationSettings.LoadMapName, GetMapNameStyle());
			currentX += mapNameWidth + spacing;

			DrawModeButton(currentX, y, "Editor", ApplicationMode.Editor);
			currentX += buttonWidth + spacing;
			DrawModeButton(currentX, y, "Player", ApplicationMode.Player);
			currentX += buttonWidth + spacing;
			DrawModeButton(currentX, y, "Cinema", ApplicationMode.Cinema);
			currentX += buttonWidth + spacing;

			currentX += 20;

			// Map change buttons — now queue safely
			GuiUtils.ColoredRepeatButton(new Rect(currentX, y, buttonWidth, buttonHeight), "<< Level",
				new Color(0.3f, 0.6f, 1f), () => onChangeMapRequested(-1), initialDelay: 0.35f);
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredRepeatButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Level >>",
				new Color(0.3f, 0.6f, 1f), () => onChangeMapRequested(1), initialDelay: 0.35f);
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Reload",
				new Color(0.6f, 0.6f, 0.2f), () => onChangeMapRequested(0));
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Preset",
				new Color(0.2f, 0.8f, 0.2f), () => OnPresetRequested?.Invoke());
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredRepeatButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Scramble",
				new Color(0.8f, 0.6f, 0.2f), () => OnScrambleRequested?.Invoke(), initialDelay: 0.1f, repeatInterval: 0f);
			currentX += buttonWidth + spacing;

			GuiUtils.ColoredButton(new Rect(currentX, y, buttonWidth, buttonHeight), "Solve",
				new Color(0.8f, 0.2f, 0.2f), () => OnSolveRequested?.Invoke());

			guiRect.height = panelHeight;

			void DrawModeButton(float cx, float cy, string label, ApplicationMode mode)
			{
				bool isSelected = ApplicationSettings.CurrentMode == mode;
				Color buttonColor = isSelected ? modeSelectedBg : modeUnselectedBg;
				Color textColor = isSelected ? selectedTextColor : unselectedTextColor;

				Color prev = GUI.contentColor;
				GUI.contentColor = textColor;

				GuiUtils.ColoredButton(new Rect(cx, cy, buttonWidth, buttonHeight), label, buttonColor,
					() =>
					{
						ApplicationSettings.CurrentMode = mode;
						OnModeChanged?.Invoke(mode);
					});

				GUI.contentColor = prev;
			}
		}

		private GUIStyle GetMapNameStyle()
		{
			if (mapNameStyle != null)
				return mapNameStyle;

			mapNameStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = Mathf.Max(8, Mathf.RoundToInt(GUI.skin.label.fontSize * 0.75f)),
				clipping = TextClipping.Clip,
				wordWrap = false,
				alignment = TextAnchor.MiddleLeft
			};
			return mapNameStyle;
		}
	}
}
