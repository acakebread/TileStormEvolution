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

		public override void OnGUI() => DrawSidePanel();

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