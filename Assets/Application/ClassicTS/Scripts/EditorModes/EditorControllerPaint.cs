using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System.Linq;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private Vector3 mouseDownPos;

		private string selectedHashId;              // hashid — placement & ghost
		public string SelectedHashId => selectedHashId;

		private List<Definition> cycleDefinitions = new();  // list of full definitions for cycling
		private int cycleIndex = 0;

		private static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1f, animDur: 0.3f);
		protected override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public EditorControllerPaint(EditorController editorController) : base(editorController)
		{
			var defaultDef = ResourceManager.FindOrCreateDefaultTile();
			selectedHashId = defaultDef.hashid;

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
				EditorMeshUtil.UpdateGhostMesh(camera, iMapManager, selectedDef);
			else
				EditorMeshUtil.HideGhostMesh();
		}

		public override void OnGUI() => DrawSidePanel();

		public override void OnDisable() => EditorMeshUtil.HideGhostMesh();

		public override void OnDestroy() => EditorMeshUtil.DestroyGhostMesh();

		private void EditMapTile(bool erase = false)
		{
			var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
			string hashToPlace = erase
				? ResourceManager.FindOrCreateDefaultTile().hashid
				: selectedHashId;

			// Cycle on left-click if on the current tile
			if (!erase)
			{
				var mapIndex = iMapManager.CurrentMap.WorldToMapIndex(worldPos);
				if (mapIndex != -1)
				{
					var currentHash = iMapManager.CurrentMap.GetDefinitionAtIndex(mapIndex);
					if (currentHash == selectedHashId && cycleDefinitions.Count > 1)
					{
						cycleIndex = (cycleIndex + 1) % cycleDefinitions.Count;
						var nextDef = cycleDefinitions[cycleIndex];

						selectedHashId = nextDef.hashid;
						hashToPlace = selectedHashId;

						EditorMeshUtil.DestroyGhostMesh();
						EditorMeshUtil.UpdateGhostMesh(camera, iMapManager, nextDef);
					}
				}
			}

			var snapped = Map.SnappedMapPosition(worldPos);
			iMapManager.CurrentMap.UpdateTileAt(
				Mathf.FloorToInt(snapped.x),
				Mathf.FloorToInt(snapped.z),
				hashToPlace,
				expand: true
			);
		}

		// Called from panel — takes hashid directly
		private void SetSelectedDefinitionByHash(string hashId)
		{
			if (string.IsNullOrEmpty(hashId))
			{
				hashId = ResourceManager.FindOrCreateDefaultTile().hashid;
			}

			selectedHashId = hashId;

			var def = ResourceManager.GetDefinition(hashId);

			RefreshCycleList();

			EditorMeshUtil.DestroyGhostMesh();
			if (def != null)
				EditorMeshUtil.UpdateGhostMesh(camera, iMapManager, def);
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
			string referenceName = referenceDef.id;

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

			var baseDef = ResourceManager.Definitions.FirstOrDefault(d => d.id == baseName);
			if (baseDef != null)
				variants.Add(baseDef);

			foreach (var suffix in group)
			{
				var candidate = baseName + suffix;
				var candidateDef = ResourceManager.Definitions.FirstOrDefault(d => d.id == candidate);
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
				bool isSelected = def.hashid == selectedHashId;

				string label = def.id;
				if (def.IsDefault())
					label = "[Default] " + label;

				items.Add(new ListViewItem(
					$"{label} ({def.texture ?? "none"})",
					_ => SetSelectedDefinitionByHash(def.hashid),  // pass hashid
					isSelected
				));
			}

			sidePanel.List.SetItems(items);
			sidePanel.Draw();
		}
	}
}