using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System.Linq;
using System;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private Vector3 mouseDownPos;

		private HashId selectedHashId;// hashid — placement & ghost
		public int SelectedHashId => selectedHashId;

		private List<Definition> cycleDefinitions = new();  // list of full definitions for cycling
		private int cycleIndex = 0;

		private static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1f, animDur: 0.3f);
		protected override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public EditorControllerPaint(EditorController editorController) : base(editorController)
		{
			var defaultDef = ResourceManager.FindOrCreateDefaultTile();
			selectedHashId = defaultDef.HashID;

			RefreshCycleList();
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
			if (selectedDef != null)
				EditorMeshUtil.UpdateGhostMesh(camera, iMap, selectedDef);
			else
				EditorMeshUtil.HideGhostMesh();
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
				EditorMeshUtil.DestroyGhostMesh();
				return;
			}

			if (erase)
			{
				var defaultHash = ResourceManager.FindOrCreateDefaultTile().HashID;
				iMap.UpdateTileAt(placeX, placeZ, defaultHash, 0f, 0f);
				EditorMeshUtil.DestroyGhostMesh();
				return;
			}

			// Get current variant at this map index
			var currentVariant = iMap.GetVariantAt(mapIndex);
			HashId currentHash = currentVariant.hash;

			var selectedDef = ResourceManager.GetDefinition(selectedHashId);

			// If different tile or default → place selected with 0/0, update ghost
			if (currentHash == 0 ||
				(selectedDef?.IsDefault() ?? false) ||
				currentHash != selectedHashId)
			{
				iMap.UpdateTileAt(placeX, placeZ, selectedHashId, 0f, 0f);
				EditorMeshUtil.DestroyGhostMesh();  // reset ghost
				EditorMeshUtil.UpdateGhostMesh(camera, iMap, selectedDef);  // show new selected
				return;
			}

			// Same hash → cycle delta/angle, update ghost to show NEXT state
			float[] angles = { 0f, 90f, 180f, 270f };
			float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

			int angleIdx = Array.IndexOf(angles, currentVariant.angle);
			if (angleIdx == -1) angleIdx = 0;

			int deltaIdx = Array.IndexOf(deltas, currentVariant.delta);
			if (deltaIdx == -1) deltaIdx = 0;

			// Advance inner (angle) first
			int nextAngleIdx = (angleIdx + 1) % angles.Length;
			int nextDeltaIdx = deltaIdx;

			// If angle wrapped → advance delta
			if (nextAngleIdx == 0)
			{
				nextDeltaIdx = (deltaIdx + 1) % deltas.Length;
			}

			float nextAngle = angles[nextAngleIdx];
			float nextDelta = deltas[nextDeltaIdx];

			// Apply the cycle
			iMap.UpdateTileAt(placeX, placeZ, selectedHashId, nextDelta, nextAngle);

			// Update ghost to preview the NEXT cycle state (after this place)
			int previewAngleIdx = (nextAngleIdx + 1) % angles.Length;
			int previewDeltaIdx = nextDeltaIdx;
			if (previewAngleIdx == 0)
				previewDeltaIdx = (nextDeltaIdx + 1) % deltas.Length;

			float previewAngle = angles[previewAngleIdx];
			float previewDelta = deltas[previewDeltaIdx];

			//only do this ehen slection changes!!!!!!!
			EditorMeshUtil.DestroyGhostMesh();
			EditorMeshUtil.UpdateGhostMesh(camera, iMap, selectedDef);

			//Debug.Log($"Cycled at ({placeX},{placeZ}): delta {currentVariant.delta:F2}→{nextDelta:F2}, angle {currentVariant.angle:F0}→{nextAngle:F0} (preview next: {previewDelta:F2}@{previewAngle:F0}°)");
		}

		//private void EditMapTile(bool erase = false)
		//{
		//	var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
		//	int hashToPlace = erase ? ResourceManager.FindOrCreateDefaultTile().HashID : selectedHashId;

		//	// Cycle check using snapped world pos (we'll reuse the same pos for placement)
		//	if (!erase)
		//	{
		//		var snapped = Map.SnappedMapPosition(worldPos);
		//		int mapIndex = iMap.WorldToMapIndex(snapped);  // reuse snapped pos for accuracy

		//		if (mapIndex != -1)
		//		{
		//			var currentHash = iMap.GetTileID(mapIndex);
		//			if (currentHash == selectedHashId && cycleDefinitions.Count > 1)
		//			{
		//				cycleIndex = (cycleIndex + 1) % cycleDefinitions.Count;
		//				var nextDef = cycleDefinitions[cycleIndex];

		//				selectedHashId = nextDef.HashID;
		//				hashToPlace = selectedHashId;

		//				EditorMeshUtil.DestroyGhostMesh();
		//				EditorMeshUtil.UpdateGhostMesh(camera, iMap, nextDef);
		//			}
		//		}
		//	}

		//	// Placement uses the **same snapped world position**
		//	var snappedPos = Map.SnappedMapPosition(worldPos);
		//	int placeX = Mathf.FloorToInt(snappedPos.x);
		//	int placeZ = Mathf.FloorToInt(snappedPos.z);

		//	//iMap.UpdateTileAt(placeX, placeZ, hashToPlace);

		//	// Example: random delta and angle when placing
		//	float randomAngle = new[] { 0f, 90f, 180f, 270f }[UnityEngine.Random.Range(0, 4)];
		//	float randomDelta = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f }[UnityEngine.Random.Range(0, 5)];

		//	iMap.UpdateTileAt(placeX, placeZ, hashToPlace, randomDelta, randomAngle);
		//}

		// Called from panel — takes hashid directly
		private void SetSelectedDefinitionByHash(int hashId)
		{
			if (0 == hashId)
			{
				hashId = ResourceManager.FindOrCreateDefaultTile().HashID;
			}

			selectedHashId = hashId;

			var def = ResourceManager.GetDefinition(hashId);

			RefreshCycleList();

			EditorMeshUtil.DestroyGhostMesh();
			if (def != null)
				EditorMeshUtil.UpdateGhostMesh(camera, iMap, def);
			else
				EditorMeshUtil.HideGhostMesh();
		}

		private void RefreshCycleList()
		{
			var currentDef = ResourceManager.GetDefinition(selectedHashId);
			if (currentDef == null)
			{
				cycleDefinitions.Clear();
				cycleIndex = 0;
				return;
			}

			cycleDefinitions = GetVariantGroup(currentDef);
			cycleIndex = cycleDefinitions.IndexOf(currentDef);
			if (cycleIndex < 0) cycleIndex = 0;
		}

		private static List<Definition> GetVariantGroup(Definition referenceDef)
		{
			string referenceName = referenceDef.name;

			var singles = new[] { " n", " e", " s", " w" };
			var doublesLinear = new[] { " we", " ns", " ew", " sn" };
			var doublesDiag = new[] { " nw", " ne", " se", " sw" };
			var group = singles;

			var baseName = referenceName;
			foreach (var suffix in singles.Concat(doublesLinear).Concat(doublesDiag))
			{
				if (referenceName.EndsWith(suffix))
				{
					baseName = referenceName.Substring(0, referenceName.Length - suffix.Length);
					if (doublesLinear.Any(s => referenceName.EndsWith(s))) group = doublesLinear;
					else if (doublesDiag.Any(s => referenceName.EndsWith(s))) group = doublesDiag;
					break;
				}
			}

			var variants = new List<Definition>();

			var baseDef = ResourceManager.Definitions.FirstOrDefault(d => d.name == baseName);
			if (baseDef != null)
				variants.Add(baseDef);

			foreach (var suffix in group)
			{
				var candidate = baseName + suffix;
				var candidateDef = ResourceManager.Definitions.FirstOrDefault(d => d.name == candidate);
				if (candidateDef != null)
					variants.Add(candidateDef);
			}

			if (variants.Count == 0)
				variants.Add(referenceDef);

			return variants;
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