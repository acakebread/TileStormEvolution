using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private MapManager mapManager;
		private int selectedTileDefIndex;
		private Vector3 mouseDownPos; // For RMB deletion
		private Vector3 mouseDownPosLMB; // For LMB placement
		private int mouseDownMapIndex = -1; // Map index on LMB down
		private List<int> tileDefCycleList; // List of TileDef indices for cycling
		private int cycleIndex = 0; // Current position in cycle list

		public EditorControllerPaint(Camera camera, MapManager map, int tileDefIndex) : base(camera)
		{
			mapManager = map;
			selectedTileDefIndex = tileDefIndex;
			tileDefCycleList = new List<int>();
		}

		public void SetTileDefIndex(int tileDefIndex, int globalIndex)
		{
			selectedTileDefIndex = tileDefIndex;
			UpdateTileCycleList(ResourceManager.TileDefs[globalIndex].szType);
			cycleIndex = tileDefCycleList.IndexOf(globalIndex);
			if (cycleIndex < 0) cycleIndex = 0;
		}

		public override void Update()
		{
			base.Update();
			if (!camera || PlaceholderEditorUI.Instance.IsGuiControlActive() || EventSystem.current.IsPointerOverGameObject()) return;

			var tempSelectedTileDefGlobalIndex = PlaceholderEditorUI.Instance.GetSelectedTileDefGlobalIndex();
			if (tempSelectedTileDefGlobalIndex >= 0 && tempSelectedTileDefGlobalIndex < ResourceManager.TileDefs.Count)
			{
				var tileDef = ResourceManager.TileDefs[tempSelectedTileDefGlobalIndex];
				GeometryUtil.UpdateGhostTile(camera, mapManager, tileDef);
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
					var emptyTileDefIndex = mapManager.GetOrAddMapDefIndex("tile_empty");
					if (emptyTileDefIndex >= 0)
					{
						selectedTileDefIndex = emptyTileDefIndex;
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
				HandleTilePlacement(tempSelectedTileDefGlobalIndex);
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

			mapManager.UpdateTileAt(x, z, selectedTileDefIndex);
		}

		private void HandleTilePlacement(int tempSelectedTileDefGlobalIndex)
		{
			var ray = camera.ScreenPointToRay(Input.mousePosition);
			var plane = new Plane(Vector3.up, Vector3.zero);
			if (!plane.Raycast(ray, out float enter)) return;

			var worldPos = ray.GetPoint(enter);
			var mapIndex = mapManager.WorldToMapIndex(worldPos);
			if (mapIndex < 0 || mapIndex >= mapManager.Width * mapManager.Height || mapIndex != mouseDownMapIndex) return;

			var selectedTileDef = ResourceManager.TileDefs[tempSelectedTileDefGlobalIndex];
			var selectedTileDefIndex = mapManager.GetOrAddMapDefIndex(selectedTileDef.szType);

			var tilesMatch = mapManager.GetTileDefAtIndex(mapIndex) == selectedTileDef.szType;
			if (tilesMatch && tileDefCycleList != null && tileDefCycleList.Count > 1)
			{
				cycleIndex = (cycleIndex + 1) % tileDefCycleList.Count;
				tempSelectedTileDefGlobalIndex = tileDefCycleList[cycleIndex];
				PlaceholderEditorUI.Instance.SetSelectedTileDefGlobalIndex(tempSelectedTileDefGlobalIndex);
				var newTileDef = ResourceManager.TileDefs[tempSelectedTileDefGlobalIndex];
				selectedTileDefIndex = mapManager.GetOrAddMapDefIndex(newTileDef.szType);
				this.selectedTileDefIndex = selectedTileDefIndex;
				GeometryUtil.DestroyGhostTile();
				GeometryUtil.UpdateGhostTile(camera, mapManager, newTileDef);
				PlaceTileAtMousePosition();
			}
			else
			{
				this.selectedTileDefIndex = selectedTileDefIndex;
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

			var derivedBaseTileType = currentTileType;
			foreach (var suffix in singleDirections.Concat(doubleLinear).Concat(doubleDiagonal))
			{
				if (currentTileType.EndsWith(suffix))
				{
					derivedBaseTileType = currentTileType.Substring(0, currentTileType.Length - suffix.Length);
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

			tileDefCycleList = new List<int>();

			for (var i = 0; i < ResourceManager.TileDefs.Count; i++)
			{
				if (ResourceManager.TileDefs[i].szType == derivedBaseTileType)
				{
					tileDefCycleList.Add(i);
					break;
				}
			}

			foreach (var suffix in selectedGroup)
			{
				for (var i = 0; i < ResourceManager.TileDefs.Count; i++)
				{
					if (ResourceManager.TileDefs[i].szType == derivedBaseTileType + suffix)
					{
						tileDefCycleList.Add(i);
						break;
					}
				}
			}
		}
	}
}