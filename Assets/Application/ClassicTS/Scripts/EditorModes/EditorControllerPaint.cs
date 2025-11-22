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
		private Vector3 mouseDownPos;
		private Vector3 mouseDownPosLMB;
		private int mouseDownMapIndex = -1;
		private List<string> definitionCycleList;
		private int cycleIndex = 0;

		public EditorControllerPaint(Camera camera, MapManager map, string definitionId = "tile_empty") : base(camera)
		{
			mapManager = map;
			selectedDefinitionId = definitionId;
			definitionCycleList = new List<string>();
			UpdateTileCycleList(definitionId);
		}

		// Called from UI - we ignore the int index, only use globalIndex to find the definition
		public void SetDeinitionfIndex(int definitionIndex, int globalIndex)
		{
			if (globalIndex >= 0 && globalIndex < ResourceManager.Definitions.Count)
			{
				selectedDefinitionId = ResourceManager.Definitions[globalIndex].id;
				UpdateTileCycleList(selectedDefinitionId);
				cycleIndex = definitionCycleList.IndexOf(selectedDefinitionId);
				if (cycleIndex < 0) cycleIndex = 0;
			}
		}

		public override void Update()
		{
			base.Update();
			if (!camera || PlaceholderEditorUI.Instance.IsGuiControlActive() || EventSystem.current.IsPointerOverGameObject()) return;

			var tempSelectedDefinitionGlobalIndex = PlaceholderEditorUI.Instance.GetSelectedDefinitionGlobalIndex();
			if (tempSelectedDefinitionGlobalIndex >= 0 && tempSelectedDefinitionGlobalIndex < ResourceManager.Definitions.Count)
			{
				var definition = ResourceManager.Definitions[tempSelectedDefinitionGlobalIndex];
				GeometryUtil.UpdateGhostTile(camera, mapManager, definition);
			}
			else
			{
				GeometryUtil.HideGhostTile();
			}

			// Right-click = erase
			if (Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			if (Input.GetMouseButtonUp(1))
			{
				if (Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
				{
					selectedDefinitionId = "tile_empty";
					PlaceTileAtMousePosition();
				}
			}

			// Left-click placement + cycling
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
				HandleTilePlacement(tempSelectedDefinitionGlobalIndex);
			}
		}

		public void PlaceTileAtMousePosition()
		{
			if (PlaceholderEditorUI.Instance.IsGuiControlActive()) return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			var mapIndex = mapManager.WorldToMapIndex(worldPos);
			if (mapIndex == -1) return;

			var x = mapIndex % mapManager.Width;
			var z = mapIndex / mapManager.Width;

			mapManager.UpdateTileAt(x, z, selectedDefinitionId); // STRING ONLY
		}

		private void HandleTilePlacement(int tempSelectedGlobalIndex)
		{
			var ray = camera.ScreenPointToRay(Input.mousePosition);
			var plane = new Plane(Vector3.up, Vector3.zero);
			if (!plane.Raycast(ray, out float enter)) return;

			var worldPos = ray.GetPoint(enter);
			var mapIndex = mapManager.WorldToMapIndex(worldPos);
			if (mapIndex < 0 || mapIndex != mouseDownMapIndex) return;

			var selectedDef = ResourceManager.Definitions[tempSelectedGlobalIndex];
			string targetId = selectedDef.id;
			string currentId = mapManager.GetDefinitionAtIndex(mapIndex);

			if (currentId == targetId && definitionCycleList.Count > 1)
			{
				cycleIndex = (cycleIndex + 1) % definitionCycleList.Count;
				targetId = definitionCycleList[cycleIndex];

				var nextDef = ResourceManager.Definitions.First(d => d.id == targetId);
				int nextGlobalIndex = ResourceManager.Definitions.IndexOf(nextDef);

				PlaceholderEditorUI.Instance.SetSelectedDefinitionGlobalIndex(nextGlobalIndex);
				GeometryUtil.DestroyGhostTile();
				GeometryUtil.UpdateGhostTile(camera, mapManager, nextDef);
			}

			selectedDefinitionId = targetId;
			PlaceTileAtMousePosition();
			mouseDownMapIndex = -1;
		}

		private void UpdateTileCycleList(string currentTileType)
		{
			var singleDirections = new[] { " n", " e", " s", " w" };
			var doubleLinear = new[] { " we", " ns", " ew", " sn" };
			var doubleDiagonal = new[] { " nw", " ne", " se", " sw" };
			string[] selectedGroup = null;

			var derivedBaseTileId = currentTileType;
			foreach (var suffix in singleDirections.Concat(doubleLinear).Concat(doubleDiagonal))
			{
				if (currentTileType.EndsWith(suffix))
				{
					derivedBaseTileId = currentTileType.Substring(0, currentTileType.Length - suffix.Length);
					break;
				}
			}

			if (singleDirections.Any(suffix => currentTileType.EndsWith(suffix)))
				selectedGroup = singleDirections;
			else if (doubleLinear.Any(suffix => currentTileType.EndsWith(suffix)))
				selectedGroup = doubleLinear;
			else if (doubleDiagonal.Any(suffix => currentTileType.EndsWith(suffix)))
				selectedGroup = doubleDiagonal;
			else
				selectedGroup = singleDirections;

			definitionCycleList = new List<string>();

			// Always add the base (no suffix) first — but only if it actually exists
			if (ResourceManager.Definitions.Any(d => d.id == derivedBaseTileId))
				definitionCycleList.Add(derivedBaseTileId);

			// Then add directional variants in the correct group
			foreach (var suffix in selectedGroup)
			{
				string candidate = derivedBaseTileId + suffix;
				if (ResourceManager.Definitions.Any(d => d.id == candidate))
					definitionCycleList.Add(candidate);
			}

			// Fallback: if nothing was added, just use the current tile
			if (definitionCycleList.Count == 0)
				definitionCycleList.Add(currentTileType);
		}
	}
}
