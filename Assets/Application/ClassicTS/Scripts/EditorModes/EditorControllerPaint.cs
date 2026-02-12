using System;
using UnityEngine;
using MassiveHadronLtd;
using System.Linq;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private HashId selectedHashId;
		private float previewAngle = 0f;
		private Vector3 previewDelta = new ();

		private float panelHeight = 0f;
		private float panelY;
		private float panelTargetY;

		private float hideTimer = 0f;
		private bool allowHideDespiteMouseOverPanel = false;
		private readonly float hideDelay = 0.25f;
		private readonly float animSpeed = 3000f;
		private readonly float triggerZoneHeight = 40f;

		private IconAtlas _atlas;
		private System.Collections.Generic.List<Definition> filteredDefs;

		private const int MAXIMUM_RENDER_TEXTURE_SIZE = 8192;
		private const int ICON_SIZE = 192;
		private const int COLUMNS = (MAXIMUM_RENDER_TEXTURE_SIZE - ICON_SIZE * 4) / ICON_SIZE;

		private const int PANEL_BORDER = 16;

		private int ROWS
		{
			get
			{
				var defs = ResourceManager.Definitions;
				if (null == defs || 0 == defs.Count)
					return 0;
				return Mathf.CeilToInt((float)defs.Count / COLUMNS);
			}
		}

		private Rect gridScreenRect;
		private bool mouseWasOverGridLastFrame = false;

		// ─── New hover logic fields ─────────────────────────────────────
		private bool mouseInTriggerZoneLastFrame = false;
		private bool panelWasShownByValidHover = false;

		private static readonly Color semiTransparentBg = new Color(0.08f, 0.09f, 0.11f, 0.95f);

		private HashId defaultHash => ResourceManager.FindOrCreateDefaultTile().HashID;

		protected override bool IsMouseOverGUI() => base.IsMouseOverGUI() || Input.mousePosition.y <= (panelY + panelHeight);

		private bool defSelection = false;

		public EditorControllerPaint(EditorController editorController) : base(editorController)
		{
			selectedHashId = defaultHash;
		}

		private void CalculatePanelLayout()
		{
			CalculatePanelGrid();
			CalculatePanelPosition();
		}

		private void CalculatePanelGrid()
		{
			var defs = ResourceManager.Definitions;
			if (defs == null || defs.Count == 0)
			{
				panelHeight = 0f;
				gridScreenRect = Rect.zero;
				return;
			}

			CalculatePanelPosition();
		}

		private void CalculatePanelPosition()
		{
			var RT = _atlas?.Texture;
			if (null == RT) return;

			float totalWidth = RT.width;
			float totalHeight = RT.height;

			float margin = PANEL_BORDER;
			float availWidth = Screen.width - 2f * margin;
			float scale = Mathf.Min(1f, availWidth / totalWidth);

			float drawWidth = totalWidth * scale;
			float drawHeight = totalHeight * scale;

			panelHeight = drawHeight + 2f * margin;

			float x = (Screen.width - drawWidth) * 0.5f;
			float gridBottomFromScreenBottom = panelY + margin;
			float gridY = gridBottomFromScreenBottom;

			gridScreenRect = new Rect(x, gridY, drawWidth, drawHeight);
		}

		public override void OnEnable()
		{
			base.OnEnable();

			// One-time atlas creation

			filteredDefs = ResourceManager.Definitions.Where(d => !d.IsDefaultEquivalent()).ToList();

			_atlas = DefinitionIconRenderUtil.CreateIconAtlas(
				iconSize: ICON_SIZE,
				columns: COLUMNS,
				filteredDefs,
				includeGround: false,           // ← tune as needed
				background: null,
				yaw: 35f,
				pitch: 30f
			);

			if (null == _atlas)
				Debug.LogWarning("Failed to generate icon atlas — grid will be empty.");

			CalculatePanelGrid();

			CalculatePanelPosition();
			panelY = panelTargetY = -panelHeight;
			CalculatePanelPosition();
		}

		public override void Update()
		{
			base.Update();
			if (!camera) return;

			// ─── Improved panel visibility logic ────────────────────────────────
			bool mouseInTriggerZoneThisFrame =
				InputX.mouseInsideWindow &&
				Input.mousePosition.y <= triggerZoneHeight;

			bool justEnteredTriggerZoneCleanly =
				!mouseInTriggerZoneLastFrame &&
				mouseInTriggerZoneThisFrame &&
				selectedHashId == defaultHash &&
				!Input.GetMouseButton(0) &&
				!Input.GetMouseButton(1) &&
				!Input.GetMouseButton(2);

			bool mouseOverPanel =
				!allowHideDespiteMouseOverPanel &&
				Input.mousePosition.y <= (panelY + panelHeight);

			bool wantsVisible =
				justEnteredTriggerZoneCleanly ||
				(panelWasShownByValidHover && mouseOverPanel);

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

					// Only reset the "shown by hover" flag once panel is basically gone
					if (panelY <= -panelHeight + 1f)
					{
						panelWasShownByValidHover = false;
					}
				}
			}

			// Animate
			panelY = Mathf.MoveTowards(panelY, panelTargetY, animSpeed * Time.deltaTime);

			// Remember for next frame
			mouseInTriggerZoneLastFrame = mouseInTriggerZoneThisFrame;

			// ─── Existing grid / click handling ────────────────────────────────
			bool mouseOverGridThisFrame = gridScreenRect.Contains(Input.mousePosition);
			if (mouseOverGridThisFrame)
				defSelection = true;

			if (Input.GetMouseButtonDown(0))
			{
				if (mouseOverGridThisFrame)
				{
					TrySelectTileFromGridClick(Input.mousePosition);

					// INSTANT CLOSE after selection
					panelTargetY = -panelHeight;
					hideTimer = hideDelay;
					allowHideDespiteMouseOverPanel = true;
				}
				else if (mouseWasOverGridLastFrame)
				{
					hideTimer = hideDelay - 0.4f;
				}

				if (!mouseOverGridThisFrame)
				{
					defSelection = false;
					var variant = iMap.CameraHitVariant(camera, Input.mousePosition);
					var selDef = ResourceManager.GetDefinition(variant.hash);
					var isDefault = selDef?.IsDefaultEquivalent() ?? true;
					if (isDefault)
						StartPanning();
				}
			}

			mouseWasOverGridLastFrame = mouseOverGridThisFrame;

			// Reset force-hide flag once panel is fully off-screen
			if (allowHideDespiteMouseOverPanel && panelY <= -panelHeight + 1f)
			{
				allowHideDespiteMouseOverPanel = false;
			}

			UpdatePanel();

			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			var def = ResourceManager.GetDefinition(selectedHashId);
			UpdateGhostMesh(camera, iMap, def);
		}

		protected override void OnControl(bool staticClick) 
		{
			base.OnControl(staticClick);
			if (!staticClick)
				return;

			if (defSelection && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)))
			{
				defSelection = false;
				return;
			}

			if (Input.GetMouseButtonUp(0) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
			{
				if (selectedHashId == defaultHash)
				{
					var variant = iMap.CameraHitVariant(camera, Input.mousePosition);
					var selDef = ResourceManager.GetDefinition(variant.hash);
					var isDefault = selDef?.IsDefaultEquivalent() ?? true;
					if (!isDefault)
					{
						selectedHashId = variant.hash;
						previewAngle = variant.angle;
						previewDelta = variant.delta;
					}
				}
				else
				{
					EditMapTile();
				}
			}

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
			{
				if (selectedHashId == defaultHash)
					EditMapTile(erase: true);
				else
					selectedHashId = defaultHash;
			}
		}

		private void TrySelectTileFromGridClick(Vector2 mousePos)
		{
			if (_atlas == null || filteredDefs == null || filteredDefs.Count == 0)
				return;

			Vector2 uv = gridScreenRect.NormalisedPoint(mousePos);

			if (_atlas.TryGetIndex(uv, out int index))
			{
				if (index >= 0 && index < filteredDefs.Count)
				{
					var newHash = filteredDefs[index].HashID;
					if (newHash != default)
					{
						selectedHashId = newHash;
					}
				}
			}
		}

		//public override void OnGUI()
		//{
		//	if (ROWS <= 0) return;
		//	if (panelY <= -panelHeight) return;

		//	CalculatePanelLayout();

		//	Rect guiPanelRect = new Rect(0, panelY, Screen.width, panelHeight);
		//	GUI.Box(guiPanelRect.ToGUIRect(), GUIContent.none,
		//		new GUIStyle { normal = { background = TextureUtils.MakeTex(1, 1, semiTransparentBg) } });

		//	if (panelY > -panelHeight + 1f)
		//	{
		//		Vector2 mouseUV = gridScreenRect.NormalisedPoint(Input.mousePosition);
		//		ScreenSpaceUtil.OnGUI(_atlas, gridScreenRect, mouseUV);
		//	}

		//	//ApplicationSettings.TilePanelRect;[RectTransform]
		//	//ApplicationSettings.GridTarget[UnityEngine.UI.RawImage]
		//	//ApplicationSettings.FocusTarget[UnityEngine.UI.RawImage]
		//}

		public void UpdatePanel()
		{
			if (ROWS <= 0)
				return;

			CalculatePanelLayout();

			var panelImage = EditorScreen.PanelTarget;
			if (!panelImage)
				return;

			var canvas = panelImage.GetComponentInParent<Canvas>();
			if (!canvas)
				return;

			var scaler = canvas.GetComponent<CanvasScaler>();
			float scale = 1f;

			if (scaler && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
			{
				float sx = Screen.width / scaler.referenceResolution.x;
				float sy = Screen.height / scaler.referenceResolution.y;
				scale = Mathf.Lerp(sx, sy, scaler.matchWidthOrHeight);
			}

			float inv = 1f / scale;

			// ========================= PANEL =========================
			var panelRT = panelImage.rectTransform;

			panelRT.anchorMin = new Vector2(0, 0);
			panelRT.anchorMax = new Vector2(1, 0);
			panelRT.pivot = new Vector2(0.5f, 0);

			panelRT.anchoredPosition = new Vector2(0, panelY * inv);
			panelRT.sizeDelta = new Vector2(0, panelHeight * inv);

			// Determine visibility AFTER applying position
			bool panelFullyHidden = Mathf.Approximately(panelY, panelTargetY) && panelTargetY < 0f;

			panelImage.enabled = !panelFullyHidden;

			if (panelFullyHidden || _atlas == null)
			{
				if (EditorScreen.GridTarget)
					EditorScreen.GridTarget.enabled = false;

				if (EditorScreen.FocusTarget)
					EditorScreen.FocusTarget.enabled = false;

				return;
			}

			// ========================= GRID / FOCUS =========================

			Vector2 mouseUV = gridScreenRect.NormalisedPoint(Input.mousePosition);

			var gridInfo = ScreenSpaceUtil.GetGridRenderInfo(_atlas, gridScreenRect, mouseUV);
			var focusInfo = ScreenSpaceUtil.GetFocusRenderInfo(_atlas, gridScreenRect, mouseUV);

			// ---------------- GRID ----------------
			if (EditorScreen.GridTarget)
			{
				var rt = EditorScreen.GridTarget.rectTransform;

				rt.anchorMin = new Vector2(0, 0);
				rt.anchorMax = new Vector2(0, 0);
				rt.pivot = new Vector2(0, 0);

				if (gridInfo.IsValid)
				{
					rt.anchoredPosition = new Vector2(
						gridInfo.ScreenRect.x * inv,
						gridInfo.ScreenRect.y * inv
					);

					rt.sizeDelta = new Vector2(
						gridInfo.ScreenRect.width * inv,
						gridInfo.ScreenRect.height * inv
					);

					EditorScreen.GridTarget.texture = gridInfo.Texture;
					EditorScreen.GridTarget.enabled = true;
				}
				else
				{
					EditorScreen.GridTarget.enabled = false;
				}
			}

			// ---------------- FOCUS ----------------
			if (EditorScreen.FocusTarget)
			{
				var rt = EditorScreen.FocusTarget.rectTransform;

				rt.anchorMin = new Vector2(0, 0);
				rt.anchorMax = new Vector2(0, 0);
				rt.pivot = new Vector2(0, 0);

				if (focusInfo.IsValid)
				{
					rt.anchoredPosition = new Vector2(
						focusInfo.ScreenRect.x * inv,
						focusInfo.ScreenRect.y * inv
					);

					rt.sizeDelta = new Vector2(
						focusInfo.ScreenRect.width * inv,
						focusInfo.ScreenRect.height * inv
					);

					EditorScreen.FocusTarget.texture = focusInfo.Texture;
					EditorScreen.FocusTarget.enabled = true;
				}
				else
				{
					EditorScreen.FocusTarget.enabled = false;
				}
			}
		}

		private void UpdateGhostMesh(Camera cam, IMapEdit map, Definition def)
		{
			if (def == null)
			{
				EditorMeshUtil.HideGhostMesh();
				return;
			}

			var worldPos = Map.ScreenToWorld(cam, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			var mapIndex = map.WorldToMapIndex(snapped);

			previewAngle = 0f;
			previewDelta = Vector3.zero;

			if (mapIndex != -1)
			{
				var current = map.GetVariantAt(mapIndex);
				var selDef = ResourceManager.GetDefinition(selectedHashId);
				bool isDefault = selDef?.IsDefault() ?? false;

				if (current.hash != 0 && !isDefault && current.hash == selectedHashId)
				{
					float[] angles = { 0f, 90f, 180f, 270f };
					float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

					int aIdx = Array.IndexOf(angles, current.angle); if (aIdx < 0) aIdx = 0;
					int dIdx = Array.IndexOf(deltas, current.delta.y); if (dIdx < 0) dIdx = 0;

					aIdx = (aIdx + 1) % angles.Length;
					if (aIdx == 0) dIdx = (dIdx + 1) % deltas.Length;

					previewAngle = angles[aIdx];
					previewDelta = new Vector3(current.delta.x, deltas[dIdx], current.delta.z);
				}
			}

			bool oob = mapIndex == -1;
			var variant = new Variant(selectedHashId, previewDelta, previewAngle);
			EditorMeshUtil.UpdateGhostMesh(variant, snapped, oob);
		}

		private void EditMapTile(bool erase = false)
		{
			var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			int px = Mathf.FloorToInt(snapped.x);
			int pz = Mathf.FloorToInt(snapped.z);
			int idx = iMap.WorldToMapIndex(snapped);

			if (idx == -1 || erase)
			{
				var hash = erase ? ResourceManager.FindOrCreateDefaultTile().HashID : selectedHashId;
				iMap.UpdateTileAt(px, pz, hash, Vector3.zero, 0f);
				return;
			}

			iMap.UpdateTileAt(px, pz, selectedHashId, previewDelta, previewAngle);
		}

		public override void OnDisable() => EditorMeshUtil.HideGhostMesh();
		public override void OnDestroy() => EditorMeshUtil.DestroyGhostMesh();
	}
}