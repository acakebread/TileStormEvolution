using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private MapManager mapManager;
		private string selectedDefinitionId = "tile_empty";
		private Definition selectedDefinition;
		private Vector3 mouseDownPos;
		private Vector3 mouseDownPosLMB;
		private int mouseDownMapIndex = -1;
		private List<string> definitionCycleList;
		private int cycleIndex = 0;

		public EditorControllerPaint(Camera camera, MapManager map, string definitionId = "tile_empty") : base(camera)
		{
			mapManager = map;
			SetSelectedDefinitionById(definitionId);
			definitionCycleList = new List<string>();
			UpdateTileCycleList(selectedDefinitionId);
		}

		public void SetSelectedDefinition(Definition def)
		{
			if (def == null)
			{
				selectedDefinition = ResourceManager.GetDefinition("tile_empty");
				selectedDefinitionId = "tile_empty";
			}
			else
			{
				selectedDefinition = def;
				selectedDefinitionId = def.id;
			}

			UpdateTileCycleList(selectedDefinitionId);
			cycleIndex = definitionCycleList.IndexOf(selectedDefinitionId);
			if (cycleIndex < 0) cycleIndex = 0;
		}

		private void SetSelectedDefinitionById(string id)
		{
			var def = ResourceManager.GetDefinition(id);
			SetSelectedDefinition(def ?? ResourceManager.GetDefinition("tile_empty"));
		}

		public override void Update()
		{
			base.Update();
			if (!camera || PlaceholderEditorUI.Instance.IsGuiControlActive() || EventSystem.current.IsPointerOverGameObject()) return;

			// Ghost tile follows selectedDefinition (object)
			if (selectedDefinition != null)
				GeometryUtil.UpdateGhostTile(camera, mapManager, selectedDefinition);
			else
				GeometryUtil.HideGhostTile();

			// Right-click = erase
			if (Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			if (Input.GetMouseButtonUp(1))
			{
				if (Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
					PlaceTileAtMousePosition("tile_empty");
			}

			if (Input.GetMouseButtonDown(0))
			{
				mouseDownPosLMB = Input.mousePosition;
				var ray = camera.ScreenPointToRay(mouseDownPosLMB);
				var plane = new Plane(Vector3.up, Vector3.zero);
				if (plane.Raycast(ray, out float enter))
				{
					Vector3 worldPos = ray.GetPoint(enter);
					mouseDownMapIndex = mapManager.WorldToMapIndex(worldPos);
				}
				else
				{
					mouseDownMapIndex = -1;
				}
			}

			if (Input.GetMouseButtonUp(0))
			{
				HandleTilePlacement();
			}
		}

		private void PlaceTileAtMousePosition(string defID)
		{
			if (PlaceholderEditorUI.Instance.IsGuiControlActive()) return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			var mapIndex = mapManager.WorldToMapIndex(worldPos);
			if (mapIndex == -1) return;

			var x = mapIndex % mapManager.Width;
			var z = mapIndex / mapManager.Width;

			mapManager.UpdateTileAt(x, z, defID);
		}

		private void HandleTilePlacement()
		{
			var ray = camera.ScreenPointToRay(Input.mousePosition);
			var plane = new Plane(Vector3.up, Vector3.zero);
			if (!plane.Raycast(ray, out float enter)) return;

			var worldPos = ray.GetPoint(enter);
			var mapIndex = mapManager.WorldToMapIndex(worldPos);
			if (mapIndex < 0 || mapIndex != mouseDownMapIndex) return;

			string currentId = mapManager.GetDefinitionAtIndex(mapIndex);

			if (currentId == selectedDefinitionId && definitionCycleList.Count > 1)
			{
				cycleIndex = (cycleIndex + 1) % definitionCycleList.Count;
				string nextId = definitionCycleList[cycleIndex];
				var nextDef = ResourceManager.GetDefinition(nextId);

				if (nextDef != null)
				{
					selectedDefinition = nextDef;
					selectedDefinitionId = nextId;

					// Tell UI to highlight this tile — using string only
					PlaceholderEditorUI.Instance.SetSelectedDefinitionId(nextId);

					GeometryUtil.DestroyGhostTile();
					GeometryUtil.UpdateGhostTile(camera, mapManager, nextDef);
				}
			}

			PlaceTileAtMousePosition(selectedDefinitionId);
			mouseDownMapIndex = -1;
		}

		private void UpdateTileCycleList(string currentTileType)
		{
			var singleDirections = new[] { " n", " e", " s", " w" };
			var doubleLinear = new[] { " we", " ns", " ew", " sn" };
			var doubleDiagonal = new[] { " nw", " ne", " se", " sw" };
			string[] selectedGroup = singleDirections;

			string baseId = currentTileType;
			foreach (var suffix in singleDirections.Concat(doubleLinear).Concat(doubleDiagonal))
			{
				if (currentTileType.EndsWith(suffix))
				{
					baseId = currentTileType.Substring(0, currentTileType.Length - suffix.Length);
					if (doubleLinear.Any(s => currentTileType.EndsWith(s)))
						selectedGroup = doubleLinear;
					else if (doubleDiagonal.Any(s => currentTileType.EndsWith(s)))
						selectedGroup = doubleDiagonal;
					break;
				}
			}

			definitionCycleList = new List<string>();

			if (ResourceManager.Definitions.Any(d => d.id == baseId))
				definitionCycleList.Add(baseId);

			foreach (var suffix in selectedGroup)
			{
				string candidate = baseId + suffix;
				if (ResourceManager.Definitions.Any(d => d.id == candidate))
					definitionCycleList.Add(candidate);
			}

			if (definitionCycleList.Count == 0)
				definitionCycleList.Add(currentTileType);
		}
	}
}