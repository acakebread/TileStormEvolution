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

		private float panelY;                      // top edge in GUI coords (0 = top of screen)
		private float panelTargetY;
		private float hideTimer = 0f;

		// NEW: flag to force-allow hiding even when mouse is still over panel
		private bool allowHideDespiteMouseOverPanel = false;

		private readonly float hideDelay = 0.25f;
		private readonly float animSpeed = 3000f;
		private readonly float triggerZoneHeight = 40f;   // distance from bottom to trigger show
		private readonly float panelHeight = 500f;

		private const int COLUMNS = 32;
		private int ROWS = 0;
		private HashId[] gridHashIds;
		private Rect gridScreenRect;
		private bool mouseWasOverGridLastFrame = false;

		private static readonly Color semiTransparentBg = new Color(0.08f, 0.09f, 0.11f, 0.92f);

		public EditorControllerPaint(EditorController editorController) : base(editorController)
		{
			var defaultDef = ResourceManager.FindOrCreateDefaultTile();
			selectedHashId = defaultDef.HashID;
		}

		public override void OnEnable()
		{
			base.OnEnable();

			float screenH = Screen.height;
			panelY = screenH + panelHeight + 40f;
			panelTargetY = screenH + panelHeight + 40f;

			const int ICON_SIZE = 128;

			var defs = ResourceManager.Definitions;
			if (defs == null || defs.Count == 0)
			{
				Debug.LogWarning("No definitions → no grid icons");
				ROWS = 0;
				gridHashIds = Array.Empty<HashId>();
				return;
			}

			ROWS = Mathf.CeilToInt((float)defs.Count / COLUMNS);
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
			atlas.Apply(false);

			gridHashIds = new HashId[defs.Count];

			for (int i = 0; i < defs.Count; i++)
			{
				var def = defs[i];
				if (def == null || string.IsNullOrEmpty(def.model)) continue;

				gridHashIds[i] = def.HashID;

				Texture2D icon = DefinitionIconRenderUtil.GenerateIcon(def, ICON_SIZE);
				if (icon == null) continue;

				int col = i % COLUMNS;
				int row = i / COLUMNS;
				int x = col * ICON_SIZE;
				int y = (ROWS - 1 - row) * ICON_SIZE;

				Color[] pixels = icon.GetPixels();
				atlas.SetPixels(x, y, ICON_SIZE, ICON_SIZE, pixels);

				UnityEngine.Object.DestroyImmediate(icon);
			}

			atlas.Apply(true, false);
			ScreenSpaceUtil.SetTexture(atlas);

			Debug.Log($"Icon atlas: {width}×{height}, {defs.Count} icons");
		}

		public override void Update()
		{
			base.Update();
			if (!camera) return;

			float screenH = Screen.height;

			// ───────────────────────────────────────
			// Panel visibility
			// ───────────────────────────────────────
			bool nearBottom = Input.mousePosition.y <= triggerZoneHeight;

			// ← Only check overPanel if we are NOT forcing the hide after click
			bool overPanel = !allowHideDespiteMouseOverPanel && Input.mousePosition.y <= screenH - panelY;

			bool wantsVisible = nearBottom || overPanel;

			if (wantsVisible)
			{
				panelTargetY = screenH - panelHeight;
				hideTimer = 0f;
			}
			else
			{
				hideTimer += Time.deltaTime;
				if (hideTimer >= hideDelay)
				{
					panelTargetY = screenH + 40f;
				}
			}

			panelY = Mathf.MoveTowards(panelY, panelTargetY, animSpeed * Time.deltaTime);

			// ───────────────────────────────────────
			var invertedMosue = Input.mousePosition;
			invertedMosue.y = Screen.height - invertedMosue.y;

			bool mouseOverGridThisFrame = gridScreenRect.Contains(invertedMosue);

			if (Input.GetMouseButtonDown(0))
			{
				if (mouseOverGridThisFrame)
				{
					TrySelectTileFromGridClick(Input.mousePosition);

					// INSTANT CLOSE: force hide immediately, no delay at all
					panelTargetY = screenH + 40f;
					hideTimer = hideDelay;  // make sure hide condition is true right away
					allowHideDespiteMouseOverPanel = true;
				}
				else if (mouseWasOverGridLastFrame)
				{
					hideTimer = hideDelay - 0.4f;
				}
			}

			mouseWasOverGridLastFrame = mouseOverGridThisFrame;

			// Reset flag once panel is fully hidden (so normal mouse-over behavior returns)
			if (allowHideDespiteMouseOverPanel && panelY >= screenH)
			{
				allowHideDespiteMouseOverPanel = false;
			}

			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			if (Input.GetMouseButtonUp(0) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile();

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile(erase: true);

			var def = ResourceManager.GetDefinition(selectedHashId);
			UpdateGhostMesh(camera, iMap, def);
		}

		private void TrySelectTileFromGridClick(Vector2 mousePos)
		{
			if (ROWS <= 0 || gridHashIds == null || gridHashIds.Length == 0) return;

			float uvx = (mousePos.x - gridScreenRect.xMin) / gridScreenRect.width;
			float uvy = ((Screen.height - mousePos.y) - gridScreenRect.yMin) / gridScreenRect.height;

			int col = Mathf.Clamp(Mathf.FloorToInt(uvx * COLUMNS), 0, COLUMNS - 1);
			int row = Mathf.Clamp(Mathf.FloorToInt(uvy * ROWS), 0, ROWS - 1);

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

			float screenH = Screen.height;
			if (panelY >= screenH) return;

			var bgRect = new Rect(0, panelY, Screen.width, panelHeight);
			GUI.Box(bgRect, GUIContent.none, new GUIStyle { normal = { background = TextureUtils.MakeTex(1, 1, semiTransparentBg) } });

			const float margin = 12f;
			const float cellSize = 64f;

			float totalWidth = COLUMNS * cellSize;
			float totalHeight = ROWS * cellSize;

			float availW = Screen.width - 2 * margin;
			float scale = Mathf.Min(1f, availW / totalWidth);
			float drawW = totalWidth * scale;
			float drawH = totalHeight * scale;

			float x = (Screen.width - drawW) * 0.5f;
			float y = panelY + margin;

			var panelBottom = Screen.height - (y + drawH);

			gridScreenRect = new Rect(x, y, drawW, drawH);

			if (panelY < screenH - 20f)
			{
				Vector2 mouseUV = new Vector2(
					(Input.mousePosition.x - x) / drawW,
					(Input.mousePosition.y - panelBottom) / drawH
				);

				var rt = ScreenSpaceUtil.GetRenderTexture(COLUMNS, ROWS, mouseUV);
				GUI.DrawTexture(gridScreenRect, rt, ScaleMode.StretchToFill, true);
			}
		}

		protected override bool IsMouseOverGUI()
		{
			var mp = Input.mousePosition;
			float screenH = Screen.height;
			float guiMouseY = screenH - mp.y;

			return base.IsMouseOverGUI() || guiMouseY >= panelY;
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
					int dIdx = Array.IndexOf(deltas, current.delta); if (dIdx < 0) dIdx = 0;

					aIdx = (aIdx + 1) % angles.Length;
					if (aIdx == 0) dIdx = (dIdx + 1) % deltas.Length;

					previewAngle = angles[aIdx];
					previewDelta = deltas[dIdx];
				}
			}

			bool oob = mapIndex == -1;
			var variant = new Variant(selectedHashId, previewAngle, previewDelta);
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
				iMap.UpdateTileAt(px, pz, hash, 0f, 0f);
				return;
			}

			iMap.UpdateTileAt(px, pz, selectedHashId, previewDelta, previewAngle);
		}

		public override void OnDisable() => EditorMeshUtil.HideGhostMesh();
		public override void OnDestroy() => EditorMeshUtil.DestroyGhostMesh();
	}
}