using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private HashId selectedHashId;
		private float previewAngle = 0f;
		private float previewDelta = 0f;

		private float panelY;                      // distance from BOTTOM of screen
		private float panelTargetY;
		private float hideTimer = 0f;

		private bool allowHideDespiteMouseOverPanel = false;

		private readonly float hideDelay = 0.25f;
		private readonly float animSpeed = 3000f;
		private readonly float triggerZoneHeight = 40f;   // distance from bottom to trigger show
		private float panelHeight = 100f;
		private const int COLUMNS = 28;
		private int ROWS = 0;
		private HashId[] gridHashIds;
		private Rect gridScreenRect;               // in normal screen coords (bottom-left origin)
		private bool mouseWasOverGridLastFrame = false;

		// ─── New hover logic fields ─────────────────────────────────────
		private bool mouseInTriggerZoneLastFrame = false;
		private bool panelWasShownByValidHover = false;

		private static readonly Color semiTransparentBg = new Color(0.08f, 0.09f, 0.11f, 0.95f);

		private const int ICON_SIZE = 128;
		private const int PANEL_BORDER = 32;

		private HashId defaultHash => ResourceManager.FindOrCreateDefaultTile().HashID;

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
				ROWS = 0;
				gridHashIds = Array.Empty<HashId>();
				panelHeight = 0f;
				gridScreenRect = Rect.zero;
				return;
			}

			ROWS = Mathf.CeilToInt((float)defs.Count / COLUMNS);

			CalculatePanelPosition();
		}

		private void CalculatePanelPosition()
		{
			if (ROWS == 0) return;

			const float cellSize = ICON_SIZE;
			float totalWidth = COLUMNS * cellSize;
			float totalHeight = ROWS * cellSize;

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

		private Rect ToGUIRect(Rect screenRect)
		{
			return new Rect(
				screenRect.x,
				Screen.height - screenRect.yMax,  // flip Y for GUI space
				screenRect.width,
				screenRect.height
			);
		}

		public override void OnEnable()
		{
			base.OnEnable();

			CalculatePanelGrid();

			// Atlas generation
			var defs = ResourceManager.Definitions;
			int width = COLUMNS * ICON_SIZE;
			int height = ROWS * ICON_SIZE;

			var atlas = new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "DefinitionIconAtlas"
			};

			Color[] blank = new Color[width * height];
			Array.Fill(blank, new Color(0, 0, 0, 0));
			atlas.SetPixels(blank);

			gridHashIds = new HashId[defs.Count];

			for (int i = 0; i < defs.Count; i++)
			{
				var def = defs[i];
				gridHashIds[i] = def.HashID;
				if (def == null || string.IsNullOrEmpty(def.model)) continue;

				Texture2D icon = DefinitionIconRenderUtil.GenerateIcon(def, ICON_SIZE);
				if (icon == null) continue;

				int col = i % COLUMNS;
				int row = i / COLUMNS;
				int x = col * ICON_SIZE;
				int y = (ROWS - 1 - row) * ICON_SIZE;  // flip Y for texture

				atlas.SetPixels(x, y, ICON_SIZE, ICON_SIZE, icon.GetPixels());

				UnityEngine.Object.DestroyImmediate(icon);
			}

			atlas.Apply(true, false);

			ScreenSpaceUtil.SetTexture(atlas);
			Debug.Log($"Icon atlas: {width}×{height}, {defs.Count} icons, panelHeight={panelHeight}");

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
			}

			mouseWasOverGridLastFrame = mouseOverGridThisFrame;

			// Reset force-hide flag once panel is fully off-screen
			if (allowHideDespiteMouseOverPanel && panelY <= -panelHeight + 1f)
			{
				allowHideDespiteMouseOverPanel = false;
			}

			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			if (Input.GetMouseButtonUp(0) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
			{
				if (selectedHashId == defaultHash)
				{
					var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
					var snapped = Map.SnappedMapPosition(worldPos);
					var mapIndex = iMap.WorldToMapIndex(snapped);

					if (mapIndex != -1)
					{
						var current = iMap.GetVariantAt(mapIndex);
						selectedHashId = current.hash;
						previewAngle = current.angle;
						previewDelta = current.delta.y;
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

			var def = ResourceManager.GetDefinition(selectedHashId);
			UpdateGhostMesh(camera, iMap, def);
		}

		private void TrySelectTileFromGridClick(Vector2 mousePos)
		{
			if (ROWS <= 0 || gridHashIds == null || gridHashIds.Length == 0) return;

			Vector2 uv = gridScreenRect.NormalisedPoint(mousePos);  // assumes extension method

			int col = Mathf.Clamp(Mathf.FloorToInt(uv.x * COLUMNS), 0, COLUMNS - 1);
			int row = Mathf.Clamp(Mathf.FloorToInt((1f - uv.y) * ROWS), 0, ROWS - 1);
			int index = row * COLUMNS + col;

			if (index < 0 || index >= gridHashIds.Length) return;

			var newHash = gridHashIds[index];
			if (newHash != default)
			{
				selectedHashId = newHash;
			}
		}

		public override void OnGUI()
		{
			if (ROWS <= 0) return;
			if (panelY <= -panelHeight) return;

			CalculatePanelLayout();

			Rect guiPanelRect = new Rect(0, panelY, Screen.width, panelHeight);
			GUI.Box(guiPanelRect.ToGUIRect(), GUIContent.none,
				new GUIStyle { normal = { background = TextureUtils.MakeTex(1, 1, semiTransparentBg) } });

			if (panelY > -panelHeight + 1f)
			{
				Rect guiGridRect = ToGUIRect(gridScreenRect);
				Vector2 mouseUV = gridScreenRect.NormalisedPoint(Input.mousePosition);
				ScreenSpaceUtil.OnGUI(gridScreenRect, COLUMNS, ROWS, mouseUV);
			}
		}

		protected override bool IsMouseOverGUI()
		{
			var mp = Input.mousePosition;
			return base.IsMouseOverGUI() || mp.y <= (panelY + panelHeight);
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
			previewDelta = 0f;

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
					previewDelta = deltas[dIdx];
				}
			}

			bool oob = mapIndex == -1;
			var variant = new Variant(selectedHashId, previewAngle, new Vector3(0f, previewDelta, 0f));
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

			iMap.UpdateTileAt(px, pz, selectedHashId, new Vector3(0f, previewDelta, 0f), previewAngle);
		}

		public override void OnDisable() => EditorMeshUtil.HideGhostMesh();
		public override void OnDestroy() => EditorMeshUtil.DestroyGhostMesh();
	}
}