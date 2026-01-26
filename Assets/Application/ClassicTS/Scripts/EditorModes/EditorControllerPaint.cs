using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System.Linq;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private Vector3 mouseDownPos;

		private int selectedHashId;// hashid — placement & ghost
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
			int hashToPlace = erase ? ResourceManager.FindOrCreateDefaultTile().HashID : selectedHashId;

			// Cycle check using snapped world pos (we'll reuse the same pos for placement)
			if (!erase)
			{
				var snapped = Map.SnappedMapPosition(worldPos);
				int mapIndex = iMap.WorldToMapIndex(snapped);  // reuse snapped pos for accuracy

				if (mapIndex != -1)
				{
					var currentHash = iMap.GetTileID(mapIndex);
					if (currentHash == selectedHashId && cycleDefinitions.Count > 1)
					{
						cycleIndex = (cycleIndex + 1) % cycleDefinitions.Count;
						var nextDef = cycleDefinitions[cycleIndex];

						selectedHashId = nextDef.HashID;
						hashToPlace = selectedHashId;

						EditorMeshUtil.DestroyGhostMesh();
						EditorMeshUtil.UpdateGhostMesh(camera, iMap, nextDef);
					}
				}
			}

			// Placement uses the **same snapped world position**
			var snappedPos = Map.SnappedMapPosition(worldPos);
			int placeX = Mathf.FloorToInt(snappedPos.x);
			int placeZ = Mathf.FloorToInt(snappedPos.z);

			iMap.UpdateTileAt(placeX, placeZ, hashToPlace);
		}

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