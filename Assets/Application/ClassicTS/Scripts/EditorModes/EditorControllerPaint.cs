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

		private float panelY;                      // distance from BOTTOM of screen now
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
		private Rect gridScreenRect;               // ← now in normal screen coords (bottom-left origin)
		private bool mouseWasOverGridLastFrame = false;

		private static readonly Color semiTransparentBg = new Color(0.08f, 0.09f, 0.11f, 0.95f);

		public EditorControllerPaint(EditorController editorController) : base(editorController)
		{
			var defaultDef = ResourceManager.FindOrCreateDefaultTile();
			selectedHashId = defaultDef.HashID;
		}

		private const int ICON_SIZE = 128;
		private const int BORDER = 32;

		private void CalculatePanelLayout()
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

			const float cellSize = ICON_SIZE;
			float totalWidth = COLUMNS * cellSize;
			float totalHeight = ROWS * cellSize;

			float margin = BORDER;
			float availWidth = Screen.width - 2f * margin;
			float scale = Mathf.Min(1f, availWidth / totalWidth);

			float drawWidth = totalWidth * scale;
			float drawHeight = totalHeight * scale;

			panelHeight = drawHeight + 2f * margin;

			float x = (Screen.width - drawWidth) * 0.5f;
			float gridBottomFromScreenBottom = panelY + margin;
			float gridY = gridBottomFromScreenBottom;  // bottom of grid content

			gridScreenRect = new Rect(x, gridY, drawWidth, drawHeight);
		}

		private Rect ToGUIRect(Rect screenRect)
		{
			return new Rect(
				screenRect.x,
				Screen.height - screenRect.yMax,  // flip: top edge in GUI space
				screenRect.width,
				screenRect.height
			);
		}

		public override void OnEnable()
		{
			base.OnEnable();

			CalculatePanelLayout();

			if (ROWS <= 0)
			{
				Debug.LogWarning("No definitions → no grid icons");
				return;
			}

			panelY = -panelHeight;           // start hidden below screen
			panelTargetY = -panelHeight;

			// Atlas generation (y-flip preserved for texture)
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
				if (def == null || string.IsNullOrEmpty(def.model)) continue;

				gridHashIds[i] = def.HashID;

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
		}

		public override void Update()
		{
			base.Update();
			if (!camera) return;

			// Panel visibility
			bool nearBottom = Input.mousePosition.y <= triggerZoneHeight;
			bool overPanel = !allowHideDespiteMouseOverPanel && Input.mousePosition.y <= (panelY + panelHeight);

			bool wantsVisible = nearBottom || overPanel;

			if (wantsVisible)
			{
				panelTargetY = 0f;
				hideTimer = 0f;
			}
			else
			{
				hideTimer += Time.deltaTime;
				if (hideTimer >= hideDelay)
				{
					panelTargetY = -panelHeight;
				}
			}

			panelY = Mathf.MoveTowards(panelY, panelTargetY, animSpeed * Time.deltaTime);

			// Mouse testing — now direct in screen space
			bool mouseOverGridThisFrame = gridScreenRect.Contains(Input.mousePosition);

			if (Input.GetMouseButtonDown(0))
			{
				if (mouseOverGridThisFrame)
				{
					TrySelectTileFromGridClick(Input.mousePosition);

					// INSTANT CLOSE
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
				EditMapTile();

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile(erase: true);

			var def = ResourceManager.GetDefinition(selectedHashId);
			UpdateGhostMesh(camera, iMap, def);
		}

		private void TrySelectTileFromGridClick(Vector2 mousePos)
		{
			if (ROWS <= 0 || gridHashIds == null || gridHashIds.Length == 0) return;

			Vector2 uv = gridScreenRect.NormalisedPoint(mousePos);  // assumes you have this extension

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
			GUI.Box(guiPanelRect.ToGUIRect(), GUIContent.none, new GUIStyle { normal = { background = TextureUtils.MakeTex(1, 1, semiTransparentBg) } });

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