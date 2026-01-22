using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System.Linq;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private Vector3 mouseDownPos;

		private string selectedDefinitionId;           // this is now the hashid
		private string selectedDisplayName;             // this is the legacy id / display name used for cycling
		public string SelectedDefinitionID => selectedDefinitionId;
		private List<string> definitionCycleList = new();  // list of display names (legacy ids)
		private int cycleIndex = 0;

		private static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1f, animDur: 0.3f);
		protected override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public EditorControllerPaint(EditorController editorController) : base(editorController)
		{
			// Initialize with the canonical default tile
			var defaultDef = ResourceManager.FindOrCreateDefaultTile();
			selectedDefinitionId = defaultDef.hashid;
			selectedDisplayName = defaultDef.id;  // "tile_empty" or whatever legacy name

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

			var selectedDefinition = ResourceManager.GetDefinition(selectedDefinitionId);
			if (selectedDefinition != null)
				EditorMeshUtil.UpdateGhostMesh(camera, iMapManager, selectedDefinition);
		}

		public override void OnGUI() => DrawSidePanel();

		public override void OnDisable() => EditorMeshUtil.HideGhostMesh();

		public override void OnDestroy() => EditorMeshUtil.DestroyGhostMesh();

		private void EditMapTile(bool erase = false)
		{
			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);

			string defIDToPlace;

			if (erase)
			{
				var defaultDef = ResourceManager.FindOrCreateDefaultTile();
				defIDToPlace = defaultDef.hashid;
			}
			else
			{
				// Left-click: try cycle if clicking same tile
				var mapIndex = iMapManager.WorldToMapIndex(worldPos);
				if (mapIndex != -1)
				{
					var currentId = iMapManager.GetDefinitionAtIndex(mapIndex); // this returns hashid presumably
					var currentDef = ResourceManager.GetDefinition(currentId);
					if (currentDef != null && currentDef.hashid == selectedDefinitionId && definitionCycleList.Count > 1)
					{
						cycleIndex = (cycleIndex + 1) % definitionCycleList.Count;
						selectedDisplayName = definitionCycleList[cycleIndex];

						// Resolve back to hashid
						var nextDef = ResourceManager.GetDefinition(selectedDisplayName);
						if (nextDef != null)
						{
							selectedDefinitionId = nextDef.hashid;
							EditorMeshUtil.DestroyGhostMesh();
						}
					}
				}

				defIDToPlace = selectedDefinitionId;
			}

			var snappedPos = MapManager.SnappedMapPosition(worldPos);
			iMapManager.UpdateTileAt(
				Mathf.FloorToInt(snappedPos.x),
				Mathf.FloorToInt(snappedPos.z),
				defIDToPlace,
				expand: true
			);
		}

		private void SetSelectedDefinitionById(string id)
		{
			// id here is expected to be legacy display name (from list click)
			if (string.IsNullOrEmpty(id))
			{
				var defaultDef = ResourceManager.FindOrCreateDefaultTile();
				id = defaultDef.id;  // use legacy name for cycling
			}

			selectedDisplayName = id;

			// Resolve to hashid for actual use
			var def = ResourceManager.GetDefinition(id);
			if (def != null)
			{
				selectedDefinitionId = def.hashid;
			}
			else
			{
				// Fallback if definition missing
				var defaultDef = ResourceManager.FindOrCreateDefaultTile();
				selectedDefinitionId = defaultDef.hashid;
				selectedDisplayName = defaultDef.id;
			}

			RefreshCycleList();

			EditorMeshUtil.DestroyGhostMesh();
			if (def != null)
				EditorMeshUtil.UpdateGhostMesh(camera, iMapManager, def);
			else
				EditorMeshUtil.HideGhostMesh();
		}

		private void RefreshCycleList()
		{
			definitionCycleList = DefinitionNavGroup(selectedDisplayName);
			cycleIndex = definitionCycleList.IndexOf(selectedDisplayName);
			if (cycleIndex < 0) cycleIndex = 0;
		}

		private static List<string> DefinitionNavGroup(string referenceDefName)
		{
			var singleDirections = new[] { " n", " e", " s", " w" };
			var doubleLinear = new[] { " we", " ns", " ew", " sn" };
			var doubleDiagonal = new[] { " nw", " ne", " se", " sw" };
			var selectedGroup = singleDirections;

			var baseId = referenceDefName;
			foreach (var suffix in singleDirections.Concat(doubleLinear).Concat(doubleDiagonal))
			{
				if (referenceDefName.EndsWith(suffix))
				{
					baseId = referenceDefName.Substring(0, referenceDefName.Length - suffix.Length);
					if (doubleLinear.Any(s => referenceDefName.EndsWith(s)))
						selectedGroup = doubleLinear;
					else if (doubleDiagonal.Any(s => referenceDefName.EndsWith(s)))
						selectedGroup = doubleDiagonal;
					break;
				}
			}

			var cycleList = new List<string>();

			if (ResourceManager.Definitions.Any(d => d.id == baseId))
				cycleList.Add(baseId);

			foreach (var suffix in selectedGroup)
			{
				var candidate = baseId + suffix;
				if (ResourceManager.Definitions.Any(d => d.id == candidate))
					cycleList.Add(candidate);
			}

			if (cycleList.Count == 0)
				cycleList.Add(referenceDefName);

			return cycleList;
		}

		private void DrawSidePanel()
		{
			var items = new List<ListViewItem>();

			var defaultDef = ResourceManager.FindOrCreateDefaultTile();

			foreach (var def in ResourceManager.Definitions)
			{
				bool isSelected = def.hashid == selectedDefinitionId ||
								  (string.IsNullOrEmpty(def.hashid) && def.id == selectedDisplayName);

				string displayLabel = def.id;
				if (def.IsDefault())
					displayLabel = "[Default] " + displayLabel;

				items.Add(new ListViewItem(
					$"{displayLabel} ({def.texture ?? "none"})",
					(x) => SetSelectedDefinitionById(def.id),  // pass legacy id for cycling
					isSelected
				));
			}

			sidePanel.List.SetItems(items);
			sidePanel.Draw();
		}
	}
}