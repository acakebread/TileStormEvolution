using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MassiveHadronLtd;
using System.Collections;

namespace ClassicTilestorm
{
	// 1. Define a clean, minimal interface
	public interface ITileSelector
	{
		// TileSelector will call this when user picks a tile
		void OnTileSelected(HashId selectedHash);

		// TileSelector asks: "may I open the palette right now?"
		bool CanOpenPalette();
	}

	public class TileSelector : MonoBehaviour
	{
		[SerializeField] private Image panelTarget;
		[SerializeField] private RawImage gridTarget;
		[SerializeField] private RawImage focusTarget;
		[SerializeField] private TMP_Text statusText;

		public HashId SelectedHashId { get; private set; }

		private IconAtlas _atlas;
		private List<Definition> filteredDefs;

		private Rect gridScreenRect;
		private float panelHeight;
		private float panelY;
		private float panelTargetY;

		private float hideTimer;
		private bool allowHideDespiteMouseOverPanel;
		private bool panelWasShownByValidHover;

#if UNITY_WEBGL && !UNITY_EDITOR
		//private const int ICON_SIZE = 48;
		//private const int MAXIMUM_RENDER_TEXTURE_SIZE = 2048;
		private const int ICON_SIZE = 96;
		private const int MAXIMUM_RENDER_TEXTURE_SIZE = 4096;
#else
		private const int ICON_SIZE = 192;
		private const int MAXIMUM_RENDER_TEXTURE_SIZE = 8192;
#endif
		private const int COLUMNS = (MAXIMUM_RENDER_TEXTURE_SIZE - ICON_SIZE * 4) / ICON_SIZE;
		private const int PANEL_BORDER = 16;

		private readonly float hideDelay = 0.25f;
		private readonly float animSpeed = 3000f;
		private readonly float triggerZoneHeight = 40f;
		// ─── Trigger dwell logic ─────────────────────────────────────
		private float triggerEnterTime;
		private Vector2 triggerEnterPos;
		private bool triggerAttemptActive;

		[SerializeField] private float triggerDwellTime = 0.25f;
		[SerializeField] private float triggerMoveTolerance = 16f;

		private Canvas _canvas;
		private CanvasScaler _scaler;
		private Image _panelImage;
		private RawImage _gridOverlay;
		private RawImage _focusOverlay;
		private TMP_Text _statusText;

		// ─── Wobble (scale-based on focus overlay) ───────────────────────────
		private int pressedIndex = -1;          // tile pressed down on
		private float wobbleStartTime = -1f;
		[SerializeField] private float wobbleMaxAmplitude = 0.1f;
		[SerializeField] private float wobbleDecayTime = 0.3f;
		[SerializeField] private float wobbleFrequency = 12f;

		// Only one active handler at a time (editor use-case → simplest)
		private ITileSelector _receiver;
		internal ITileSelector Receiver
		{
			get => _receiver;
			set => _receiver = value;
		}

		private void NotifyTileSelected(HashId newHash)
		{
			_receiver?.OnTileSelected(newHash);
			SelectedHashId = newHash;   // keep your internal state
		}

		// Optional: expose for debugging / fallback
		public ITileSelector CurrentHandler => _receiver;

		private void Awake()
		{
			_panelImage = panelTarget?.GetComponent<Image>();
			_gridOverlay = gridTarget;
			_focusOverlay = focusTarget;
			_statusText = statusText;

			panelTarget.enabled = false;
			gridTarget.enabled = false;
			focusTarget.enabled = false;
			statusText.enabled = false;

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

			ResourceManager.OnDefininionsModified += () => RebuildAtlas();

			ReadyCallbackRegistry.Raise(this);
		}

		private void Start() => RebuildAtlas(); //private IEnumerator Start() { yield return null; RebuildAtlas(); }//needed to prevent black icons//private void Start() => RebuildAtlas();

		private Coroutine _atlasBuildCoroutine;   // KEEP this field

		public void RebuildAtlas()
		{
			// Stop any previous build (important!)
			if (_atlasBuildCoroutine != null)
			{
				TileSelectorCoroutineRunner.Stop(_atlasBuildCoroutine);
				_atlasBuildCoroutine = null;
			}

			// Dispose old atlas
			if (_atlas != null)
			{
				_atlas.Dispose();
				_atlas = null;
			}

			filteredDefs = ResourceManager.Definitions
				.Where(d => !d.IsDefaultEquivalent())
				.ToList();

			if (filteredDefs.Count == 0)
			{
				Debug.LogWarning("No definitions to build atlas for.");
				return;
			}

			// Create new atlas
			_atlas = new IconAtlas(
				ICON_SIZE,
				COLUMNS,
				filteredDefs,
				includeGround: false,
				background: null,
				yaw: 215f,
				pitch: 30f);

			if (_atlas != null)
			{
				// Start build using the persistent runner
				_atlasBuildCoroutine = TileSelectorCoroutineRunner.Start(BuildAtlasWithUIUpdates());
			}

			SelectedHashId = ResourceManager.DefaultHash;
			RecalculateLayout();
			panelY = panelTargetY = -panelHeight;
		}

		private IEnumerator BuildAtlasWithUIUpdates()
		{
			if (_atlas == null) yield break;

			var buildCoroutine = _atlas.BuildIconsCoroutine(iconsPerFrame: 1);

			while (buildCoroutine.MoveNext())
			{
				yield return buildCoroutine.Current;
				ScreenSpaceUtil.ForceRebuild();
			}

			ScreenSpaceUtil.ForceRebuild();
			Debug.Log("Atlas build + UI updates completed.");
		}

		private void Update()
		{
			if (_atlas == null) return;

			RecalculateLayout();

			// ─── Panel visibility ────────────────────────────────────────────────
			var mouseInTrigger = InputX.mouseInsideWindow && InputX.mousePosition.y <= triggerZoneHeight;

			var justEnteredCleanly = false;
			var mousePos = InputX.mousePosition;

			// Start attempt on entry OR if no attempt active
			if (!triggerAttemptActive && mouseInTrigger)
			{
				triggerAttemptActive = true;
				triggerEnterTime = Time.time;
				triggerEnterPos = mousePos;
			}

			// Cancel if left zone
			if (!mouseInTrigger)
			{
				triggerAttemptActive = false;
			}

			// If attempting
			if (triggerAttemptActive)
			{
				var moveDist = Vector2.Distance(mousePos, triggerEnterPos);

				// Cancel if moved too much
				if (moveDist > triggerMoveTolerance)
				{
					triggerAttemptActive = false;
				}
				// Fire if time reached
				else if (Time.time - triggerEnterTime >= triggerDwellTime &&
						 !InputX.GetMouseButton(0) &&
						 !InputX.GetMouseButton(1) &&
						 !InputX.GetMouseButton(2))
				{
					justEnteredCleanly = true;

					// Reset attempt so a new dwell can begin
					triggerAttemptActive = false;
				}
			}

			var mouseOverPanel = !allowHideDespiteMouseOverPanel && InputX.mousePosition.y <= (panelY + panelHeight);
			var allowedToOpen = _receiver == null || _receiver.CanOpenPalette();
			var wantsVisible = allowedToOpen && !allowHideDespiteMouseOverPanel && (justEnteredCleanly || (panelWasShownByValidHover && mouseOverPanel));

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

			if (allowHideDespiteMouseOverPanel && panelY <= -panelHeight + 1f)
				allowHideDespiteMouseOverPanel = false;

			UpdatePanelVisuals();

			if (false == mouseOverPanel) 
				return;

			// ─── Wobble trigger on mouse DOWN ────────────────────────────────────
			var mouseOverGrid = gridScreenRect.Contains(InputX.mousePosition);
			if (InputX.GetMouseButtonDown(0) && mouseOverGrid && wobbleStartTime < 0f) // ← only start if no wobble active
			{
				Vector2 uv = gridScreenRect.NormalisedPoint(InputX.mousePosition);
				if (_atlas.TryGetIndex(uv, out int idx) && idx >= 0 && idx < filteredDefs.Count)
				{
					pressedIndex = idx;
					wobbleStartTime = Time.time;
				}
			}

			// ─── Apply wobble to focus overlay (scale-based) ─────────────────────
			ApplyFocusWobble();

			// ─── Selection only on mouse UP over SAME tile ───────────────────────
			if (InputX.GetMouseButtonUp(0) && pressedIndex >= 0)
			{
				var resetWobble = false;
				var uv = gridScreenRect.NormalisedPoint(InputX.mousePosition);
				if (_atlas.TryGetIndex(uv, out int releaseIdx) && releaseIdx == pressedIndex)
					resetWobble = !TrySelectTile(releaseIdx);
				if (resetWobble)
				{
					pressedIndex = -1;
					wobbleStartTime = -1f;
					if (_focusOverlay) _focusOverlay.transform.localScale = Vector3.one;
				}
			}

			if (InputX.GetMouseButtonUp(1))
			{
				allowHideDespiteMouseOverPanel = true;
				panelTargetY = -panelHeight;
			}
		}

		private void ApplyFocusWobble()
		{
			if (wobbleStartTime < 0f || !_focusOverlay) return;

			var elapsed = Time.time - wobbleStartTime;

			// Stop exactly after wobbleDecayTime
			if (elapsed >= wobbleDecayTime)
			{
				wobbleStartTime = -1f;
				_focusOverlay.transform.localScale = Vector3.one;
				return;
			}

			// Normalized progress (0 → 1 over wobbleDecayTime)
			var t = elapsed / wobbleDecayTime;

			// Simple linear decay of amplitude (optional)
			var amplitude = wobbleMaxAmplitude * (1f - t); // optional, or just wobbleMaxAmplitude

			// Sine wobble
			var wobble = Mathf.Sin(elapsed * wobbleFrequency * Mathf.PI * 2f) * amplitude;

			var scale = 1f + wobble;
			_focusOverlay.transform.localScale = new Vector3(scale, scale, 1f);
		}

		private bool TrySelectTile(int index)
		{
			if (index < 0 || index >= filteredDefs.Count) return false;

			var newHash = filteredDefs[index].HashID;
			if (newHash != default)
			{
				allowHideDespiteMouseOverPanel = true;
				// Start delayed hide
				StopCoroutine(nameof(AutoHideAfterDelay)); // prevent stacking
				StartCoroutine(AutoHideAfterDelay(newHash));
				return true;
			}
			return false;
		}

		private IEnumerator AutoHideAfterDelay(HashId newHash)
		{
			yield return new WaitForSeconds(wobbleDecayTime + 0.2f);

			SelectedHashId = newHash;
			NotifyTileSelected(newHash);

			panelTargetY = -panelHeight;
			hideTimer = hideDelay;
		}

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
			var totalW = tex.width;
			var totalH = tex.height;

			var margin = PANEL_BORDER;
			var availW = Screen.width - 2f * margin;
			var scale = Mathf.Min(1f, availW / totalW);

			var drawW = totalW * scale;
			var drawH = totalH * scale;

			panelHeight = drawH + 2f * margin;

			var x = (Screen.width - drawW) * 0.5f;
			var gridBottom = panelY + margin;
			gridScreenRect = new Rect(x, gridBottom, drawW, drawH);
		}

		private void UpdatePanelVisuals()
		{
			if (Rows <= 0 || _panelImage == null) return;//if (allowHideDespiteMouseOverPanel || Rows <= 0 || _panelImage == null) return;

			var uiScale = 1f;
			if (_scaler && _scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
			{
				var sx = Screen.width / _scaler.referenceResolution.x;
				var sy = Screen.height / _scaler.referenceResolution.y;
				uiScale = Mathf.Lerp(sx, sy, _scaler.matchWidthOrHeight);
			}
			var inv = 1f / uiScale;

			var panelRT = _panelImage.rectTransform;
			panelRT.anchorMin = new Vector2(0, 0);
			panelRT.anchorMax = new Vector2(1, 0);
			panelRT.pivot = new Vector2(0.5f, 0);
			panelRT.anchoredPosition = new Vector2(0, panelY * inv);
			panelRT.sizeDelta = new Vector2(0, panelHeight * inv);

			var fullyHidden = Mathf.Approximately(panelY, panelTargetY) && panelTargetY < 0f;
			_panelImage.enabled = !fullyHidden;

			var showContent = !fullyHidden && _atlas != null;

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

			var mouseUV = gridScreenRect.NormalisedPoint(InputX.mousePosition);
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
				if (target != _focusOverlay)
					rt.anchoredPosition = new Vector2(info.ScreenRect.x * inv, info.ScreenRect.y * inv);
				else
				{
					// Center position for focus
					var cx = info.ScreenRect.x + info.ScreenRect.width * 0.5f;
					var cy = info.ScreenRect.y + info.ScreenRect.height * 0.5f;
					rt.anchoredPosition = new Vector2(cx * inv, cy * inv);
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
			var panelVisible = !Mathf.Approximately(panelY, panelTargetY) || panelTargetY >= 0f;
			if (!panelVisible)
				return "Hover near bottom of screen to open tile palette";

			var mouseOverGrid = gridScreenRect.Contains(InputX.mousePosition);
			var uv = gridScreenRect.NormalisedPoint(InputX.mousePosition);

			// Case 1: Mouse over a valid tile in grid → show its info
			if (mouseOverGrid && _atlas != null && _atlas.TryGetIndex(uv, out int idx) && idx >= 0 && idx < filteredDefs.Count)
			{
				var def = filteredDefs[idx];
				var name = def.name ?? "Unnamed Tile";

				var directions = "";
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

				var flagsStr = flagsList.Count > 0 ? $"<color=#88FFAA> • {string.Join(", ", flagsList)}</color>" : "";

				var message = $"<b><color=#FFD700>{name}</color></b>{directions}{flagsStr}";

				var secondary = new List<string>();
				if (!string.IsNullOrEmpty(def.model)) secondary.Add($"M:{def.model}");
				if (!string.IsNullOrEmpty(def.texture)) secondary.Add($"T:{def.texture}");
				if (!string.IsNullOrEmpty(def.material)) secondary.Add($"Mat:{def.material}");

				if (secondary.Count > 0)
					message += $"<color=#BBBBBB>  {string.Join("  ", secondary)}</color>";

				return message;
			}

			// Case 2: Mouse inside palette rect but NOT over any tile → show current selection
			if (gridScreenRect.Contains(InputX.mousePosition) && SelectedHashId != ResourceManager.DefaultHash)
			{
				var def = ResourceManager.GetDefinition(SelectedHashId);
				var selName = def?.name ?? "Unknown";

				var directions = "";
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

				var flagsStr = flagsList.Count > 0 ? $"<color=#88FFAA> • {string.Join(", ", flagsList)}</color>" : "";

				return $"<color=#88FF88>Selected:</color> <b><color=#FFD700>{selName}</color></b>{directions}{flagsStr}";
			}

			// Case 3: Mouse outside grid (or no selection) → default prompt
			return "Hover over a tile";
		}

		private void OnDestroy()
		{
			if (_atlasBuildCoroutine != null)
			{
				TileSelectorCoroutineRunner.Stop(_atlasBuildCoroutine);
				_atlasBuildCoroutine = null;
			}

			if (_atlas != null)
			{
				_atlas.Dispose();
				_atlas = null;
			}
		}
	}

	internal class TileSelectorCoroutineRunner : MonoBehaviour
	{
		private static TileSelectorCoroutineRunner _instance;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void CreateInstance()
		{
			if (_instance != null) return;

			var go = new GameObject("[TileSelector Coroutine Runner]")
			{
				hideFlags = HideFlags.HideAndDontSave
			};

			_instance = go.AddComponent<TileSelectorCoroutineRunner>();
			DontDestroyOnLoad(go);
		}

		public static Coroutine Start(IEnumerator routine) => _instance?.StartCoroutine(routine);

		public static void Stop(Coroutine coroutine)
		{
			if (_instance != null && coroutine != null)
				_instance.StopCoroutine(coroutine);
		}
	}
}