using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System.Linq;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private Vector3 mouseDownPos;

		private string selectedDefinitionId = "tile_empty";
		public string SelectedDefinitionID => selectedDefinitionId;
		private List<string> definitionCycleList = new();
		private int cycleIndex = 0;

		private static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1f, animDur: 0.3f);
		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public EditorControllerPaint(EditorController editorController) : base(editorController) { }

		public override void Update()
		{
			base.Update();
			if (!camera || IsMouseOverGUI() || IsGuiControlActive()) return;

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			if (Input.GetMouseButtonUp(0) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile(selectedDefinitionId);

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile();

			var selectedDefinition = ResourceManager.GetDefinition(selectedDefinitionId);
			if (selectedDefinition != null)
				EditorMeshUtil.UpdateGhostMesh(camera, iMapManager, selectedDefinition);
		}

		public override void OnGUI() => DrawSidePanel();

		public override void OnDisable() => EditorMeshUtil.HideGhostMesh();

		public override void OnDestroy() => EditorMeshUtil.DestroyGhostMesh();

		private void EditMapTile(string defID = null)
		{
			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);

			if (defID != null)
			{
				var mapIndex = iMapManager.WorldToMapIndex(worldPos);
				if (mapIndex != -1)
				{
					var currentId = iMapManager.GetDefinitionAtIndex(mapIndex);
					if (currentId == selectedDefinitionId && definitionCycleList.Count > 1)
					{
						cycleIndex = (cycleIndex + 1) % definitionCycleList.Count;
						selectedDefinitionId = definitionCycleList[cycleIndex];
						EditorMeshUtil.DestroyGhostMesh();
						defID = selectedDefinitionId;
					}
				}
			}
			else
				defID = "tile_empty";

			var snappedPos = MapManager.SnappedMapPosition(worldPos);
			iMapManager.UpdateTileAt(Mathf.FloorToInt(snappedPos.x), Mathf.FloorToInt(snappedPos.z), defID, expand: true);
		}

		private void SetSelectedDefinitionById(string id)
		{
			selectedDefinitionId = id ?? "tile_empty";

			definitionCycleList = DefinitionNavGroup(selectedDefinitionId);
			cycleIndex = definitionCycleList.IndexOf(selectedDefinitionId);

			EditorMeshUtil.DestroyGhostMesh();
			var def = ResourceManager.GetDefinition(selectedDefinitionId);
			if (def != null)
				EditorMeshUtil.UpdateGhostMesh(camera, iMapManager, def);
			else
				EditorMeshUtil.HideGhostMesh();
		}

		private static List<string> DefinitionNavGroup(string referenceDef)
		{
			var singleDirections = new[] { " n", " e", " s", " w" };
			var doubleLinear = new[] { " we", " ns", " ew", " sn" };
			var doubleDiagonal = new[] { " nw", " ne", " se", " sw" };
			var selectedGroup = singleDirections;

			var baseId = referenceDef;
			foreach (var suffix in singleDirections.Concat(doubleLinear).Concat(doubleDiagonal))
			{
				if (referenceDef.EndsWith(suffix))
				{
					baseId = referenceDef.Substring(0, referenceDef.Length - suffix.Length);
					if (doubleLinear.Any(s => referenceDef.EndsWith(s)))
						selectedGroup = doubleLinear;
					else if (doubleDiagonal.Any(s => referenceDef.EndsWith(s)))
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

			if (0 == cycleList.Count)
				cycleList.Add(referenceDef);

			return cycleList;
		}

		private void DrawSidePanel()
		{
			// Clear old items and populate ListView
			var items = new List<ListViewItem>();

			foreach (var def in ResourceManager.Definitions)
				items.Add(new ListViewItem($"{def.id} ({def.texture})", () => SetSelectedDefinitionById(def.id), def.id == selectedDefinitionId));
			sidePanel.List.SetItems(items);

			// Draw the panel (background + list)
			sidePanel.Draw();
		}
	}
}
