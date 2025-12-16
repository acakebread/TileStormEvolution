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

		private readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1f, animDur: 0.3f);

		private GUIStyle leftButtonStyle;

		public EditorControllerPaint(EditorController editorController) : base(editorController) { }

		public override bool IsMouseOverGUI()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Paint) return false;

			if (base.IsMouseOverGUI()) return true;
			Rect panelRect = sidePanel.GetPanelRect();
			Vector2 mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;
			return panelRect.Contains(mouse);
		}

		public override void Update()
		{
			base.Update();
			if (!editorCamera || IsMouseOverGUI() || IsGuiControlActive()) return;

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			if (Input.GetMouseButtonUp(0) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile(selectedDefinitionId);

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				EditMapTile();

			var selectedDefinition = ResourceManager.GetDefinition(selectedDefinitionId);
			if (selectedDefinition != null)
				EditorMeshUtil.UpdateGhostMesh(editorCamera, editorController.iMapManager, selectedDefinition);
		}

		public override void OnDisable() => EditorMeshUtil.HideGhostMesh();

		private void EditMapTile(string defID = null)
		{
			var worldPos = MapManager.ScreenToWorld(editorCamera, Input.mousePosition);

			if (defID != null)
			{
				var mapIndex = editorController.iMapManager.WorldToMapIndex(worldPos);
				if (mapIndex != -1)
				{
					var currentId = editorController.iMapManager.GetDefinitionAtIndex(mapIndex);
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

			var snappedPos = editorController.iMapManager.SnappedMapPosition(worldPos);

			editorController.iMapManager.UpdateTileAt(
				Mathf.FloorToInt(snappedPos.x),
				Mathf.FloorToInt(snappedPos.z),
				defID,
				expand: true,
				onEdited: editorController.OnMapChanged
			);
		}

		public void SetSelectedDefinitionById(string id)
		{
			selectedDefinitionId = id ?? "tile_empty";

			definitionCycleList = DefinitionNavGroup(selectedDefinitionId);
			cycleIndex = definitionCycleList.IndexOf(selectedDefinitionId);

			EditorMeshUtil.DestroyGhostMesh();
			var def = ResourceManager.GetDefinition(selectedDefinitionId);
			if (def != null)
				EditorMeshUtil.UpdateGhostMesh(editorCamera, editorController.iMapManager, def);
			else
				EditorMeshUtil.HideGhostMesh();
		}

		public override void OnGUI()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Paint || editorCamera == null) return;

			if (leftButtonStyle == null)
			{
				leftButtonStyle = new GUIStyle(GUI.skin.button)
				{
					alignment = TextAnchor.MiddleLeft,
					padding = new RectOffset(12, 4, 4, 4)
				};
			}

			sidePanel.Update();

			// Clear old items and populate ListView
			sidePanel.List.Clear();
			foreach (var def in ResourceManager.Definitions)
			{
				string label = $"{def.id} ({def.texture})";
				sidePanel.List.AddItem(new ListViewItem(
					label,
					() => SetSelectedDefinitionById(def.id),
					def.id == selectedDefinitionId
				));
			}

			// Draw the panel (background + list)
			sidePanel.Draw();
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
	}
}
