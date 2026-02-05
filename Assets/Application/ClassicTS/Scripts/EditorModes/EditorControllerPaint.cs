using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private HashId selectedHashId;// hashid — placement & ghost
		public int SelectedHashId => selectedHashId;

		private static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1f, animDur: 0.3f);
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

		private float previewAngle = 0f;
		private float previewDelta = 0f;

		private void UpdateGhostMesh(Camera camera, IMapEdit iMap, Definition definition)
		{
			var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			var mapIndex = iMap.WorldToMapIndex(snapped);

			// ───────────────────────────────────────────────────────────────
			// Early hide + bail if no definition or we're just showing nothing
			// ───────────────────────────────────────────────────────────────
			if (definition == null)
			{
				EditorMeshUtil.HideGhostMesh();
				return;
			}

			// ───────────────────────────────────────────────────────────────
			// Determine which angle/delta to PREVIEW (ghost)
			// ───────────────────────────────────────────────────────────────
			float previewAngle = 0f;
			float previewDelta = 0f;

			if (mapIndex != -1)   // only cycle when hovering valid map cell
			{
				var currentVariant = iMap.GetVariantAt(mapIndex);
				var currentHash = currentVariant.hash;

				var selectedDef = ResourceManager.GetDefinition(selectedHashId);
				bool isDefaultSelected = selectedDef?.IsDefault() ?? false;

				bool shouldResetToZero =
					currentHash == 0 ||                 // empty cell
					isDefaultSelected ||                // user picked the default tile
					currentHash != selectedHashId;      // different tile type

				if (shouldResetToZero)
				{
					// Start fresh: angle=0, delta=0
					previewAngle = 0f;
					previewDelta = 0f;
				}
				else
				{
					// Same tile type → show the NEXT rotation/height variant

					float[] angles = { 0f, 90f, 180f, 270f };
					float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

					int angleIdx = Array.IndexOf(angles, currentVariant.angle);
					if (angleIdx == -1) angleIdx = 0;

					int deltaIdx = Array.IndexOf(deltas, currentVariant.delta);
					if (deltaIdx == -1) deltaIdx = 0;

					// Cycle angle first (inner loop)
					int nextAngleIdx = (angleIdx + 1) % angles.Length;

					// If angle wrapped around → advance delta (outer loop)
					int nextDeltaIdx = deltaIdx;
					if (nextAngleIdx == 0)
					{
						nextDeltaIdx = (deltaIdx + 1) % deltas.Length;
					}

					previewAngle = angles[nextAngleIdx];
					previewDelta = deltas[nextDeltaIdx];
				}
			}
			// else: off-map → just use 0/0 (already set above)

			// ───────────────────────────────────────────────────────────────
			// Finally update ghost
			// ───────────────────────────────────────────────────────────────
			var ghostPosition = snapped + new Vector3(0f, previewDelta, 0f);
			bool outOfBounds = mapIndex == -1;

			EditorMeshUtil.UpdateGhostMesh(definition, ghostPosition, previewAngle, outOfBounds);

			// Keep these for placement on click
			this.previewAngle = previewAngle;
			this.previewDelta = previewDelta;
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

			if (mapIndex == -1)
			{
				// Off-map → just place default/selected (no cycling or ghost)
				var hashToPlace = erase ? ResourceManager.FindOrCreateDefaultTile().HashID : selectedHashId;
				iMap.UpdateTileAt(placeX, placeZ, hashToPlace, 0f, 0f);
				return;
			}

			if (erase)
			{
				var defaultHash = ResourceManager.FindOrCreateDefaultTile().HashID;
				iMap.UpdateTileAt(placeX, placeZ, defaultHash, 0f, 0f);
				return;
			}

			iMap.UpdateTileAt(placeX, placeZ, selectedHashId, previewDelta, previewAngle);
		}

		// Called from panel — takes hashid directly
		private void SetSelectedDefinitionByHash(int hashId)
		{
			selectedHashId = 0 == hashId ? ResourceManager.FindOrCreateDefaultTile().HashID : hashId;
			UpdateGhostMesh(camera, iMap, ResourceManager.GetDefinition(selectedHashId));
		}

		private void DrawSidePanel()
		{
			var items = new List<ListViewItem>();

			foreach (var def in ResourceManager.Definitions)
			{
				bool isSelected = def.HashID == selectedHashId;

				string label = def.name;
				if (def.IsDefault())
					label = "[Default] " + label;

				items.Add(new ListViewItem(
					$"{label} ({def.texture ?? "none"})",
					_ => SetSelectedDefinitionByHash(def.HashID),  // pass hashid
					isSelected
				));
			}

			sidePanel.List.SetItems(items);
			sidePanel.Draw();
		}
	}
}