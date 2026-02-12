using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MassiveHadronLtd;
using System.Collections;

namespace ClassicTilestorm
{
	public class TileSelector : MonoBehaviour
	{
		public HashId SelectedHashId { get; private set; }

		public event Action<HashId> OnTileSelected;

		private IconAtlas _atlas;
		private List<Definition> filteredDefs;

		private Rect gridScreenRect;
		private float panelHeight;
		private float panelY;
		private float panelTargetY;

		private float hideTimer;
		private bool allowHideDespiteMouseOverPanel;
		private bool panelWasShownByValidHover;
		private bool mouseInTriggerZoneLastFrame;

		private const int ICON_SIZE = 192;
		private const int MAXIMUM_RENDER_TEXTURE_SIZE = 8192;
		private const int COLUMNS = (MAXIMUM_RENDER_TEXTURE_SIZE - ICON_SIZE * 4) / ICON_SIZE;
		private const int PANEL_BORDER = 16;

		private readonly float hideDelay = 0.25f;
		private readonly float animSpeed = 3000f;
		private readonly float triggerZoneHeight = 40f;

		private Canvas _canvas;
		private CanvasScaler _scaler;
		private Image _panelImage;
		private RawImage _gridOverlay;
		private RawImage _focusOverlay;
		private TMP_Text _statusText;

		public Func<bool> CanOpenPalette;

		// ─── Wobble (scale-based on focus overlay) ───────────────────────────
		private int pressedIndex = -1;          // tile pressed down on
		private float wobbleStartTime = -1f;
		[SerializeField] private float wobbleMaxAmplitude = 0.1f;
		[SerializeField] private float wobbleDecayTime = 0.3f;
		[SerializeField] private float wobbleFrequency = 12f;

		private void Awake()
		{
			_panelImage = EditorScreen.PanelTarget?.GetComponent<Image>();
			_gridOverlay = EditorScreen.GridTarget;
			_focusOverlay = EditorScreen.FocusTarget;
			_statusText = EditorScreen.StatusText;

			var canvasObj = _panelImage?.GetComponentInParent<Canvas>();
			if (canvasObj)
			{
				_canvas = canvasObj;
				_scaler = _canvas.GetComponent<CanvasScaler>();
			}

			if (_focusOverlay != null)
			{
				// Set pivot to center once (so scale happens from middle)
				_focusOverlay.rectTransform.pivot = new Vector2(0.5f, 0.5f);
			}
		}

		public IEnumerator Start()
		{
			yield return null;
			filteredDefs = ResourceManager.Definitions
				.Where(d => !d.IsDefaultEquivalent())
				.ToList();

			_atlas = DefinitionIconRenderUtil.CreateIconAtlas(
				iconSize: ICON_SIZE,
				columns: COLUMNS,
				filteredDefs: filteredDefs,
				includeGround: false,
				background: null,
				yaw: 35f,
				pitch: 30f
			);

			if (_atlas == null)
				Debug.LogWarning("Failed to generate icon atlas — palette empty.");

			SelectedHashId = ResourceManager.DefaultHash;

			RecalculateLayout();
			panelY = panelTargetY = -panelHeight;
		}

		private void Update()
		{
			if (_atlas == null) return;

			RecalculateLayout();

			// ─── Panel visibility ────────────────────────────────────────────────
			bool mouseInTrigger = InputX.mouseInsideWindow && Input.mousePosition.y <= triggerZoneHeight;

			bool justEnteredCleanly =
				!mouseInTriggerZoneLastFrame &&
				 mouseInTrigger &&
				!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2);

			bool mouseOverPanel = !allowHideDespiteMouseOverPanel &&
								  Input.mousePosition.y <= (panelY + panelHeight);

			bool allowedToOpen = CanOpenPalette == null || CanOpenPalette();

			bool wantsVisible =
				allowedToOpen &&
				(justEnteredCleanly || (panelWasShownByValidHover && mouseOverPanel));

			if (wantsVisible)
			{
				panelTargetY = 0f;
				hideTimer = 0f;
				panelWasShownByValidHover = true;
			}
			else
			{
				hideTimer += Time.deltaTime;
				if (hideTimer >= hideDelay)
				{
					panelTargetY = -panelHeight;
					if (panelY <= -panelHeight + 1f)
						panelWasShownByValidHover = false;
				}
			}

			panelY = Mathf.MoveTowards(panelY, panelTargetY, animSpeed * Time.deltaTime);
			mouseInTriggerZoneLastFrame = mouseInTrigger;

			if (allowHideDespiteMouseOverPanel && panelY <= -panelHeight + 1f)
				allowHideDespiteMouseOverPanel = false;

			UpdatePanelVisuals();

			// ─── Wobble trigger on mouse DOWN ────────────────────────────────────
			bool mouseOverGrid = gridScreenRect.Contains(Input.mousePosition);
			if (Input.GetMouseButtonDown(0) && mouseOverGrid && wobbleStartTime < 0f) // ← only start if no wobble active
			{
				Vector2 uv = gridScreenRect.NormalisedPoint(Input.mousePosition);
				if (_atlas.TryGetIndex(uv, out int idx) && idx >= 0 && idx < filteredDefs.Count)
				{
					pressedIndex = idx;
					wobbleStartTime = Time.time;
				}
			}

			// ─── Apply wobble to focus overlay (scale-based) ─────────────────────
			ApplyFocusWobble();

			// ─── Selection only on mouse UP over SAME tile ───────────────────────
			if (Input.GetMouseButtonUp(0) && pressedIndex >= 0)
			{
				Vector2 uv = gridScreenRect.NormalisedPoint(Input.mousePosition);
				if (_atlas.TryGetIndex(uv, out int releaseIdx) && releaseIdx == pressedIndex)
				{
					TrySelectTile(releaseIdx);
				}
				pressedIndex = -1;
				wobbleStartTime = -1f;
				if (_focusOverlay) _focusOverlay.transform.localScale = Vector3.one;
			}
		}

		private void ApplyFocusWobble()
		{
			if (wobbleStartTime < 0f || !_focusOverlay) return;

			float elapsed = Time.time - wobbleStartTime;

			// Stop exactly after wobbleDecayTime
			if (elapsed >= wobbleDecayTime)
			{
				wobbleStartTime = -1f;
				_focusOverlay.transform.localScale = Vector3.one;
				return;
			}

			// Normalized progress (0 → 1 over wobbleDecayTime)
			float t = elapsed / wobbleDecayTime;

			// Simple linear decay of amplitude (optional)
			float amplitude = wobbleMaxAmplitude * (1f - t); // optional, or just wobbleMaxAmplitude

			// Sine wobble
			float wobble = Mathf.Sin(elapsed * wobbleFrequency * Mathf.PI * 2f) * amplitude;

			float scale = 1f + wobble;
			_focusOverlay.transform.localScale = new Vector3(scale, scale, 1f);
		}


		private void TrySelectTile(int index)
		{
			if (index < 0 || index >= filteredDefs.Count) return;

			var newHash = filteredDefs[index].HashID;
			if (newHash != default)
			{
				SelectedHashId = newHash;

				// Close panel
				panelTargetY = -panelHeight;
				hideTimer = hideDelay;
				allowHideDespiteMouseOverPanel = true;

				OnTileSelected?.Invoke(SelectedHashId);
			}
		}

		//public bool IsMouseOverPalette() => Input.mousePosition.y <= (panelY + panelHeight);

		private int Rows => filteredDefs?.Count > 0 ? Mathf.CeilToInt((float)filteredDefs.Count / COLUMNS) : 0;

		private void RecalculateLayout()
		{
			if (_atlas?.Texture == null)
			{
				panelHeight = 0f;
				gridScreenRect = Rect.zero;
				return;
			}

			var tex = _atlas.Texture;
			float totalW = tex.width;
			float totalH = tex.height;

			float margin = PANEL_BORDER;
			float availW = Screen.width - 2f * margin;
			float scale = Mathf.Min(1f, availW / totalW);

			float drawW = totalW * scale;
			float drawH = totalH * scale;

			panelHeight = drawH + 2f * margin;

			float x = (Screen.width - drawW) * 0.5f;
			float gridBottom = panelY + margin;
			gridScreenRect = new Rect(x, gridBottom, drawW, drawH);
		}

		private void UpdatePanelVisuals()
		{
			if (Rows <= 0 || _panelImage == null) return;

			float uiScale = 1f;
			if (_scaler && _scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
			{
				float sx = Screen.width / _scaler.referenceResolution.x;
				float sy = Screen.height / _scaler.referenceResolution.y;
				uiScale = Mathf.Lerp(sx, sy, _scaler.matchWidthOrHeight);
			}
			float inv = 1f / uiScale;

			var panelRT = _panelImage.rectTransform;
			panelRT.anchorMin = new Vector2(0, 0);
			panelRT.anchorMax = new Vector2(1, 0);
			panelRT.pivot = new Vector2(0.5f, 0);
			panelRT.anchoredPosition = new Vector2(0, panelY * inv);
			panelRT.sizeDelta = new Vector2(0, panelHeight * inv);

			bool fullyHidden = Mathf.Approximately(panelY, panelTargetY) && panelTargetY < 0f;
			_panelImage.enabled = !fullyHidden;

			bool showContent = !fullyHidden && _atlas != null;

			if (_statusText)
			{
				_statusText.enabled = showContent;
				if (showContent)
					_statusText.text = GetStatusMessage();
			}

			if (!showContent)
			{
				if (_gridOverlay) _gridOverlay.enabled = false;
				if (_focusOverlay) _focusOverlay.enabled = false;
				return;
			}

			Vector2 mouseUV = gridScreenRect.NormalisedPoint(Input.mousePosition);
			var gridInfo = ScreenSpaceUtil.GetGridRenderInfo(_atlas, gridScreenRect, mouseUV);
			var focusInfo = ScreenSpaceUtil.GetFocusRenderInfo(_atlas, gridScreenRect, mouseUV);

			UpdateOverlay(_gridOverlay, gridInfo, inv);
			UpdateOverlay(_focusOverlay, focusInfo, inv);
		}

		private void UpdateOverlay(RawImage target, ScreenSpaceUtil.RenderInfo info, float inv)
		{
			if (!target) return;

			var rt = target.rectTransform;
			rt.anchorMin = rt.anchorMax = new Vector2(0, 0);

			// Grid stays bottom-left
			if (target == _gridOverlay)
				rt.pivot = new Vector2(0, 0);
			else
				rt.pivot = new Vector2(0.5f, 0.5f); // focus overlay scales from center

			if (info.IsValid)
			{
				if (target == _focusOverlay)
				{
					// Center position for focus
					float cx = info.ScreenRect.x + info.ScreenRect.width * 0.5f;
					float cy = info.ScreenRect.y + info.ScreenRect.height * 0.5f;

					rt.anchoredPosition = new Vector2(cx * inv, cy * inv);
				}
				else
				{
					rt.anchoredPosition = new Vector2(info.ScreenRect.x * inv, info.ScreenRect.y * inv);
				}

				rt.sizeDelta = new Vector2(info.ScreenRect.width * inv, info.ScreenRect.height * inv);

				target.texture = info.Texture;
				target.enabled = true;
			}
			else
			{
				target.enabled = false;
			}
		}

		private string GetStatusMessage()
		{
			// Quick exit if panel is fully hidden
			bool panelVisible = !Mathf.Approximately(panelY, panelTargetY) || panelTargetY >= 0f;
			if (!panelVisible)
				return "Hover near bottom of screen to open tile palette";

			bool mouseOverGrid = gridScreenRect.Contains(Input.mousePosition);
			Vector2 uv = gridScreenRect.NormalisedPoint(Input.mousePosition);

			// Case 1: Mouse over a valid tile in grid → show its info
			if (mouseOverGrid && _atlas != null && _atlas.TryGetIndex(uv, out int idx) && idx >= 0 && idx < filteredDefs.Count)
			{
				var def = filteredDefs[idx];
				string name = def.name ?? "Unnamed Tile";

				string directions = "";
				if (def.North || def.South || def.East || def.West)
				{
					var dirs = new List<string>();
					if (def.North) dirs.Add("N");
					if (def.South) dirs.Add("S");
					if (def.East) dirs.Add("E");
					if (def.West) dirs.Add("W");
					directions = $" <color=#AACCFF>({string.Join("-", dirs)})</color>";
				}

				var flagsList = new List<string>();
				if (def.Drag) flagsList.Add("Drag");
				if (def.Roll) flagsList.Add("Roll");
				if (def.Dock) flagsList.Add("Dock");
				if (def.Door) flagsList.Add("Door");
				if (def.Start) flagsList.Add("Start");
				if (def.End) flagsList.Add("End");
				if (def.Console) flagsList.Add("Console");
				if (def.PuzzleBlock) flagsList.Add("Puzzle");
				if (def.Sway) flagsList.Add("Sway");
				if (def.Wash) flagsList.Add("Wash");

				string flagsStr = flagsList.Count > 0 ? $"<color=#88FFAA> • {string.Join(", ", flagsList)}</color>" : "";

				string message = $"<b><color=#FFD700>{name}</color></b>{directions}{flagsStr}";

				var secondary = new List<string>();
				if (!string.IsNullOrEmpty(def.model)) secondary.Add($"M:{def.model}");
				if (!string.IsNullOrEmpty(def.texture)) secondary.Add($"T:{def.texture}");
				if (!string.IsNullOrEmpty(def.material)) secondary.Add($"Mat:{def.material}");

				if (secondary.Count > 0)
					message += $"<color=#BBBBBB>  {string.Join("  ", secondary)}</color>";

				return message;
			}

			// Case 2: Mouse inside palette rect but NOT over any tile → show current selection
			if (gridScreenRect.Contains(Input.mousePosition) && SelectedHashId != ResourceManager.DefaultHash)
			{
				var def = ResourceManager.GetDefinition(SelectedHashId);
				string selName = def?.name ?? "Unknown";

				string directions = "";
				if (def?.North ?? false) directions += "N";
				if (def?.South ?? false) directions += (directions.Length > 0 ? "-" : "") + "S";
				if (def?.East ?? false) directions += (directions.Length > 0 ? "-" : "") + "E";
				if (def?.West ?? false) directions += (directions.Length > 0 ? "-" : "") + "W";
				if (!string.IsNullOrEmpty(directions)) directions = $" <color=#AACCFF>({directions})</color>";

				var flagsList = new List<string>();
				if (def?.Drag ?? false) flagsList.Add("Drag");
				if (def?.Roll ?? false) flagsList.Add("Roll");
				if (def?.Dock ?? false) flagsList.Add("Dock");
				if (def?.Door ?? false) flagsList.Add("Door");
				if (def?.Start ?? false) flagsList.Add("Start");
				if (def?.End ?? false) flagsList.Add("End");
				if (def?.Console ?? false) flagsList.Add("Console");
				if (def?.PuzzleBlock ?? false) flagsList.Add("Puzzle");
				if (def?.Sway ?? false) flagsList.Add("Sway");
				if (def?.Wash ?? false) flagsList.Add("Wash");

				string flagsStr = flagsList.Count > 0 ? $"<color=#88FFAA> • {string.Join(", ", flagsList)}</color>" : "";

				return $"<color=#88FF88>Selected:</color> <b><color=#FFD700>{selName}</color></b>{directions}{flagsStr}";
			}

			// Case 3: Mouse outside grid (or no selection) → default prompt
			return "Hover over a tile";
		}
	}
}