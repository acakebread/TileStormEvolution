using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MassiveHadronLtd;
using System.Collections;
using System.Threading;
using System;

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
		private const int ICON_SIZE = 48;
		private const int MAXIMUM_RENDER_TEXTURE_SIZE = 2048;
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

			ResourceManager.OnDefininionsModified += () => Rebuild();

			ReadyCallbackRegistry.Raise(this);
		}

		//public void Start() { }
		private void OnEnable() { if (null == _atlas) Rebuild(); }
		//private void OnDisable() { }

		//private CancellationTokenSource _rebuildCts;   // null when no rebuild is active
		public void Rebuild()
		{
			//// Cancel and clean up any previous rebuild attempt
			//_rebuildCts?.Cancel();
			//_rebuildCts?.Dispose();           // Important: dispose old source
			//_rebuildCts = null;

			//// Start fresh
			//_rebuildCts = new CancellationTokenSource();
			//_ = RebuildAtlasAsync(_rebuildCts.Token);

			RebuildAtlas();
		}

		//private async Awaitable RebuildAtlasAsync(CancellationToken ct)
		//{
		//	GameObject stateCamera = null;
		//	GameObject cube = null;
		//	//GameObject lightObj = null;

		//	try
		//	{
		//		//await AsyncExtensions.WaitFramesAsync(3, ct);
		//		ct.ThrowIfCancellationRequested();   // early exit if already cancelled

		//		stateCamera = new GameObject("RenderStateCamera");
		//		var renderCam = stateCamera.AddComponent<Camera>();
		//		renderCam.pixelRect = new Rect(0, 0, 4, 4);
		//		//renderCam.pixelRect = new Rect(0, 0, 64, 64);

		//		//RenderSettings.ambientMode = overrideSettings.ambientMode;
		//		//RenderSettings.ambientLight = Color.white;
		//		//RenderSettings.ambientIntensity = 1f;
		//		//RenderSettings.skybox = overrideSettings.skybox;
		//		//RenderSettings.ambientProbe = overrideSettings.ambientProbe;
		//		//RenderSettings.subtractiveShadowColor = overrideSettings.subtractiveShadowColor;

		//		var cameraRenderSettingsOverride = stateCamera.AddComponent<CameraRenderSettingsOverride>();
		//		cameraRenderSettingsOverride.OverrideSettings = new(
		//			ambientMode: UnityEngine.Rendering.AmbientMode.Flat,
		//			ambientLight: Color.white * 1.2f,
		//			ambientIntensity: 1f,
		//			skybox: null,
		//			ambientProbe: default,
		//			subtractiveShadowColor: RenderSettings.subtractiveShadowColor
		//		);

		//		cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		//		cube.transform.position = Vector3.forward * 100f;
		//		cube.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { color = Color.black };


		//		//lightObj = new GameObject("IconLight");// { hideFlags = HideFlags.HideAndDontSave };

		//		//lightObj.transform.rotation = Quaternion.Euler(90, 0, 0);
		//		////lightObj.transform.SetParent(_root.transform, false);
		//		//var _iconLight = lightObj.AddComponent<Light>();
		//		//_iconLight.type = LightType.Directional;
		//		//_iconLight.intensity = 1.2f;//overwritten later so ignore this value
		//		//_iconLight.color = new Color(1f, 0.98f, 0.95f);
		//		//_iconLight.shadows = LightShadows.None;
		//		////_iconLight.enabled = false;
		//		//_iconLight.range = 999f;
		//		//_iconLight.lightmapBakeType = LightmapBakeType.Baked;

		//		// Wait with cancellation support
		//		await AsyncExtensions.WaitFramesAsync(3, ct);
		//		ct.ThrowIfCancellationRequested();

		//		filteredDefs = ResourceManager.Definitions
		//			.Where(d => !d.IsDefaultEquivalent())
		//			.ToList();

		//		ct.ThrowIfCancellationRequested();

		//		_atlas = new IconAtlas(
		//			ICON_SIZE,
		//			COLUMNS,
		//			filteredDefs,
		//			includeGround: false,
		//			background: null,
		//			yaw: 215f,
		//			pitch: 30f);

		//		if (_atlas == null)
		//			Debug.LogWarning("Failed to generate icon atlas — palette empty.");

		//		//await AsyncExtensions.WaitFramesAsync(3, ct);
		//		//ct.ThrowIfCancellationRequested();

		//		//_atlas = new IconAtlas(
		//		//	ICON_SIZE,
		//		//	COLUMNS,
		//		//	filteredDefs,
		//		//	includeGround: false,
		//		//	background: null,
		//		//	yaw: 35f,
		//		//	pitch: 30f);

		//		ct.ThrowIfCancellationRequested();

		//		SelectedHashId = ResourceManager.DefaultHash;

		//		RecalculateLayout();
		//		panelY = panelTargetY = -panelHeight;

		//		ReadyCallbackRegistry.Raise(this);
		//	}
		//	catch (OperationCanceledException)
		//	{
		//		// Optional: log or handle cancellation specifically
		//		Debug.Log("Rebuild cancelled");
		//	}
		//	catch (Exception ex)
		//	{
		//		Debug.LogException(ex);
		//	}
		//	finally
		//	{
		//		if (stateCamera != null)
		//		{
		//			Destroy(stateCamera);
		//		}

		//		// Optional: clear reference only if this is still the active CTS
		//		// (helps avoid race if very rapid calls)
		//		if (_rebuildCts != null && _rebuildCts.Token == ct)
		//		{
		//			_rebuildCts.Dispose();
		//			_rebuildCts = null;
		//		}

		//		if (cube != null)
		//			Destroy(cube);

		//		//if (lightObj != null)
		//		//	Destroy(lightObj);
		//	}
		//}

		private void RebuildAtlas()
		{
			filteredDefs = ResourceManager.Definitions
				.Where(d => !d.IsDefaultEquivalent())
				.ToList();

			_atlas = new IconAtlas(
				ICON_SIZE,
				COLUMNS,
				filteredDefs,
				includeGround: false,
				background: null,
				yaw: 215f,
				pitch: 30f);

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
			bool mouseInTrigger = InputX.mouseInsideWindow && InputX.mousePosition.y <= triggerZoneHeight;

			bool justEnteredCleanly = false;
			Vector2 mousePos = InputX.mousePosition;

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
				float moveDist = Vector2.Distance(mousePos, triggerEnterPos);

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

			bool mouseOverPanel = !allowHideDespiteMouseOverPanel &&
								  InputX.mousePosition.y <= (panelY + panelHeight);

			bool allowedToOpen = _receiver == null || _receiver.CanOpenPalette();

			bool wantsVisible =
				allowedToOpen &&
				!allowHideDespiteMouseOverPanel &&
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

			if (allowHideDespiteMouseOverPanel && panelY <= -panelHeight + 1f)
				allowHideDespiteMouseOverPanel = false;

			UpdatePanelVisuals();

			if (false == mouseOverPanel) 
				return;

			// ─── Wobble trigger on mouse DOWN ────────────────────────────────────
			bool mouseOverGrid = gridScreenRect.Contains(InputX.mousePosition);
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
				Vector2 uv = gridScreenRect.NormalisedPoint(InputX.mousePosition);
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
			//if (allowHideDespiteMouseOverPanel || Rows <= 0 || _panelImage == null) return;
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

			Vector2 mouseUV = gridScreenRect.NormalisedPoint(InputX.mousePosition);
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

			bool mouseOverGrid = gridScreenRect.Contains(InputX.mousePosition);
			Vector2 uv = gridScreenRect.NormalisedPoint(InputX.mousePosition);

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
			if (gridScreenRect.Contains(InputX.mousePosition) && SelectedHashId != ResourceManager.DefaultHash)
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

		//private void OnEnable()
		//{
		//	UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginRender;
		//	//UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnEndRender;
		//}

		//private void OnDisable()
		//{
		//	UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginRender;
		//	//UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnEndRender;
		//}

		//private UnityRenderSettings originalSettings;

		//private void OnBeginRender(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
		//{
		//	//if (cam != GetComponent<Camera>()) return;

		//	// Save current global render settings
		//	originalSettings = UnityRenderSettings.Clone();

		//	//// Apply the override values
		//	//RenderSettings.ambientMode = overrideSettings.ambientMode;
		//	////RenderSettings.ambientLight = overrideSettings.ambientLight;
		//	//RenderSettings.ambientIntensity = overrideSettings.ambientIntensity;
		//	//RenderSettings.skybox = overrideSettings.skybox;
		//	//RenderSettings.ambientProbe = overrideSettings.ambientProbe;
		//	//RenderSettings.subtractiveShadowColor = overrideSettings.subtractiveShadowColor;

		//	RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
		//	RenderSettings.ambientLight = Color.white;
		//	RenderSettings.ambientIntensity = 10;
		//	RenderSettings.skybox = null;
		//}

		//private void OnEndRender(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
		//{
		//	if (cam != GetComponent<Camera>()) return;

		//	// Restore original settings
		//	UnityRenderSettings.Restore(originalSettings);
		//}
	}
}