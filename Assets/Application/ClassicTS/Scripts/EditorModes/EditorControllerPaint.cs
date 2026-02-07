using System;
using UnityEngine;
using System.Collections.Generic;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private HashId selectedHashId;// hashid — placement & ghost
		private float previewAngle = 0f;
		private float previewDelta = 0f;

		private static readonly GuiUtils.AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1f, animDur: 0.3f);
		protected override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public EditorControllerPaint(EditorController editorController) : base(editorController)
		{
			var defaultDef = ResourceManager.FindOrCreateDefaultTile();
			selectedHashId = defaultDef.HashID;
		}

		public override void Update()
		{
			base.Update();

			if (!camera || IsMouseOverGUI() || IsGuiControlActive()) return;

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			if (Input.GetMouseButtonUp(0) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile();

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile(erase: true);

			var selectedDef = ResourceManager.GetDefinition(selectedHashId);
			UpdateGhostMesh(camera, iMap, selectedDef);
		}

		private void UpdateGhostMesh(Camera camera, IMapEdit iMap, Definition definition)
		{
			// ───────────────────────────────────────────────────────────────
			// Early hide + bail if no definition or we're just showing nothing
			// ───────────────────────────────────────────────────────────────
			if (definition == null)
			{
				EditorMeshUtil.HideGhostMesh();
				return;
			}

			var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			var mapIndex = iMap.WorldToMapIndex(snapped);

			// ───────────────────────────────────────────────────────────────
			// Determine which angle/delta to PREVIEW (ghost)
			// ───────────────────────────────────────────────────────────────
			previewAngle = 0f;
			previewDelta = 0f;

			if (mapIndex != -1)   // only cycle when hovering valid map cell
			{
				var currentVariant = iMap.GetVariantAt(mapIndex);
				var currentHash = currentVariant.hash;
				var selectedDef = ResourceManager.GetDefinition(selectedHashId);
				var isDefaultSelected = selectedDef?.IsDefault() ?? false;

				if (currentHash != 0 && !isDefaultSelected && currentHash == selectedHashId)
				{
					// Same tile type → show the NEXT rotation/height variant
					float[] angles = { 0f, 90f, 180f, 270f };
					float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

					var angleIdx = Array.IndexOf(angles, currentVariant.angle);
					if (angleIdx == -1) angleIdx = 0;

					var deltaIdx = Array.IndexOf(deltas, currentVariant.delta);
					if (deltaIdx == -1) deltaIdx = 0;

					// Cycle angle first (inner loop)
					angleIdx = (angleIdx + 1) % angles.Length;

					// If angle wrapped around → advance delta (outer loop)
					deltaIdx = angleIdx != 0 ? deltaIdx : (deltaIdx + 1) % deltas.Length;

					previewAngle = angles[angleIdx];
					previewDelta = deltas[deltaIdx];
				}
			}

			// ───────────────────────────────────────────────────────────────
			// Finally update ghost
			// ───────────────────────────────────────────────────────────────
			var outOfBounds = mapIndex == -1;
			var previewVariant = new Variant(selectedHashId, previewAngle, previewDelta);
			EditorMeshUtil.UpdateGhostMesh(previewVariant, snapped, outOfBounds);
		}

		//public override void OnGUI() => DrawSidePanel();

		public static Texture2D testIcon;

		private int COLUMNS = 32;
		private int ROWS = 0;


		// Debug: draw the quad texture full-screen in GUI
		public override void OnGUI()
		{
			var rect = new Rect(0, Screen.height * 0.5f, Screen.width, Screen.height * 0.5f);
			//var rect = new Rect(0, 0, Screen.width, Screen.height);

			GUI.DrawTexture(
				rect,
				//ScreenSpaceUtil.GetRenderTexture(Screen.width / 64, Screen.height / 64, new Vector2((Input.mousePosition.x - rect.x) / rect.width, (Input.mousePosition.y - rect.y) / rect.height)),
				ScreenSpaceUtil.GetRenderTexture(COLUMNS, ROWS, new Vector2((Input.mousePosition.x - rect.x) / rect.width, (Input.mousePosition.y) / rect.height)),
				//ScreenSpaceUtil.GetRenderTexture(9, 5, new Vector2((Input.mousePosition.x - rect.x) / rect.width, (Input.mousePosition.y - rect.y) / rect.height)),
				//ScaleMode.StretchToFill,
				ScaleMode.ScaleToFit,
				true
			);

			//DrawSidePanel();
		}

		public override void OnEnable()
		{
			base.OnEnable();

			const int ICON_SIZE = 128;

			var defs = ResourceManager.Definitions;
			if (defs == null || defs.Count == 0)
			{
				Debug.LogWarning("No definitions to render icons for.");
				return;
			}

			// Calculate grid dimensions
			ROWS = Mathf.CeilToInt((float)defs.Count / COLUMNS);
			int width = COLUMNS * ICON_SIZE;
			int height = ROWS * ICON_SIZE;

			// Create output atlas texture
			var atlas = new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,   // sharp pixel edges for icons
				wrapMode = TextureWrapMode.Clamp,
				name = "DefinitionIconAtlas"
			};

			// Fill with transparent black by default (or change to desired bg)
			Color[] blank = new Color[width * height];
			for (int i = 0; i < blank.Length; i++) blank[i] = new Color(0, 0, 0, 0);
			atlas.SetPixels(blank);
			atlas.Apply(false);

			// Generate and blit each icon
			for (int i = 0; i < defs.Count; i++)
			{
				var def = defs[i];

				// Skip invalid entries
				if (def == null || string.IsNullOrEmpty(def.model))
					continue;

				// Generate small icon (64×64 is fine for atlas)
				Texture2D icon = DefinitionIconRenderUtil.GenerateIcon(def, ICON_SIZE);

				if (icon == null)
				{
					Debug.LogWarning($"Failed to generate icon for definition {def.name ?? "unnamed"}");
					continue;
				}

				// Calculate position in atlas
				int col = i % COLUMNS;
				int row = i / COLUMNS;
				int x = col * ICON_SIZE;
				int y = (ROWS - 1 - row) * ICON_SIZE;  // flip Y so row 0 is at top

				// Copy icon pixels into atlas
				Color[] pixels = icon.GetPixels();
				atlas.SetPixels(x, y, ICON_SIZE, ICON_SIZE, pixels);

				// Clean up single icon immediately
				UnityEngine.Object.DestroyImmediate(icon);
			}

			// Final upload
			atlas.Apply(true, false);  // true = generate mipmaps if you want, false = readable

			// Pass to ScreenSpaceUtil
			ScreenSpaceUtil.SetTexture(atlas);

			Debug.Log($"Generated definition icon atlas: {width}×{height}, {defs.Count} icons");
		}

		public override void OnDisable() => EditorMeshUtil.HideGhostMesh();

		public override void OnDestroy() => EditorMeshUtil.DestroyGhostMesh();

		private void EditMapTile(bool erase = false)
		{
			var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			int placeX = Mathf.FloorToInt(snapped.x);
			int placeZ = Mathf.FloorToInt(snapped.z);
			int mapIndex = iMap.WorldToMapIndex(snapped);

			if (mapIndex == -1 || erase)
			{
				// Off-map or erase just place or erase  with default/selected
				var hashToPlace = erase ? ResourceManager.FindOrCreateDefaultTile().HashID : selectedHashId;
				iMap.UpdateTileAt(placeX, placeZ, hashToPlace, 0f, 0f);
				return;
			}

			iMap.UpdateTileAt(placeX, placeZ, selectedHashId, previewDelta, previewAngle);
		}

		private void DrawSidePanel()
		{
			var items = new List<GuiUtils.ListViewItem>();

			foreach (var def in ResourceManager.Definitions)
			{
				var label = def.IsDefault() ? "[Default] " + def.name : def.name;
				var isSelected = def.HashID == selectedHashId;

				items.Add(new GuiUtils.ListViewItem(
					$"{label} ({def.texture ?? "none"})",
					_ =>
					{
						selectedHashId = 0 == def.HashID ? ResourceManager.FindOrCreateDefaultTile().HashID : def.HashID;
						UpdateGhostMesh(camera, iMap, ResourceManager.GetDefinition(selectedHashId));
					},
					isSelected
				));
			}

			sidePanel.List.SetItems(items);
			sidePanel.Draw();
		}
	}
}