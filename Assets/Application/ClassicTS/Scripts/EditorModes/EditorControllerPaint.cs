using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private MapManager mapManager;
		private int selectedDefinitionIndex;
		private Vector3 mouseDownPos; // For RMB deletion
		private Vector3 mouseDownPosLMB; // For LMB placement
		private int mouseDownMapIndex = -1; // Map index on LMB down
		private List<int> definitionCycleList; // List of definition indices for cycling
		private int cycleIndex = 0; // Current position in cycle list

		public EditorControllerPaint(Camera camera, MapManager map, int definitionIndex) : base(camera)
		{
			mapManager = map;
			selectedDefinitionIndex = definitionIndex;
			definitionCycleList = new List<int>();
		}

		public void SetDeinitionfIndex(int definitionIndex, int globalIndex)
		{
			selectedDefinitionIndex = definitionIndex;
			UpdateTileCycleList(ResourceManager.Definitions[globalIndex].id);
			cycleIndex = definitionCycleList.IndexOf(globalIndex);
			if (cycleIndex < 0) cycleIndex = 0;
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

			// Handle deletion (RMB)
			if (Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			if (Input.GetMouseButtonUp(1))
			{
				var mouseMoveDistance = Vector3.Distance(Input.mousePosition, mouseDownPos);
				if (mouseMoveDistance < 5f) // Threshold: 5 pixels
				{
					var emptyDefinitionIndex = mapManager.GetOrAddMapDefIndex("tile_empty");
					if (emptyDefinitionIndex >= 0)
					{
						selectedDefinitionIndex = emptyDefinitionIndex;
						PlaceTileAtMousePosition();
					}
				}
			}

			// Handle placement and cycling (LMB)
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
			if (mapIndex == -1)
			{
				Debug.LogWarning("Mouse position is outside map bounds");
				return;
			}

			var x = mapIndex % mapManager.Width;
			var z = mapIndex / mapManager.Width;

			mapManager.UpdateTileAt(x, z, selectedDefinitionIndex);
		}

		private void HandleTilePlacement(int tempSelectedDefinitionGlobalIndex)
		{
			var ray = camera.ScreenPointToRay(Input.mousePosition);
			var plane = new Plane(Vector3.up, Vector3.zero);
			if (!plane.Raycast(ray, out float enter)) return;

			var worldPos = ray.GetPoint(enter);
			var mapIndex = mapManager.WorldToMapIndex(worldPos);
			if (mapIndex < 0 || mapIndex >= mapManager.Width * mapManager.Height || mapIndex != mouseDownMapIndex) return;

			var selectedDefinition = ResourceManager.Definitions[tempSelectedDefinitionGlobalIndex];
			var selectedDefinitionIndex = mapManager.GetOrAddMapDefIndex(selectedDefinition.id);

			var tilesMatch = mapManager.GetDefinitionAtIndex(mapIndex) == selectedDefinition.id;
			if (tilesMatch && definitionCycleList != null && definitionCycleList.Count > 1)
			{
				cycleIndex = (cycleIndex + 1) % definitionCycleList.Count;
				tempSelectedDefinitionGlobalIndex = definitionCycleList[cycleIndex];
				PlaceholderEditorUI.Instance.SetSelectedDefinitionGlobalIndex(tempSelectedDefinitionGlobalIndex);
				var newDefinition = ResourceManager.Definitions[tempSelectedDefinitionGlobalIndex];
				selectedDefinitionIndex = mapManager.GetOrAddMapDefIndex(newDefinition.id);
				this.selectedDefinitionIndex = selectedDefinitionIndex;
				GeometryUtil.DestroyGhostTile();
				GeometryUtil.UpdateGhostTile(camera, mapManager, newDefinition);
				PlaceTileAtMousePosition();
			}
			else
			{
				this.selectedDefinitionIndex = selectedDefinitionIndex;
				PlaceTileAtMousePosition();
			}

			mouseDownMapIndex = -1; // Reset after mouse up
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

			definitionCycleList = new List<int>();

			for (var i = 0; i < ResourceManager.Definitions.Count; i++)
			{
				if (ResourceManager.Definitions[i].id == derivedBaseTileId)
				{
					definitionCycleList.Add(i);
					break;
				}
			}

			foreach (var suffix in selectedGroup)
			{
				for (var i = 0; i < ResourceManager.Definitions.Count; i++)
				{
					if (ResourceManager.Definitions[i].id == derivedBaseTileId + suffix)
					{
						definitionCycleList.Add(i);
						break;
					}
				}
			}
		}
	}
}