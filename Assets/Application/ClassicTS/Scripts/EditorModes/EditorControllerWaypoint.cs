using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class EditorControllerWaypoint : EditorControllerMovement
	{
		public int SelectedWaypointIndex { get; private set; } = -1;

		public EditorControllerWaypoint(EditorController editorController) : base(editorController)
		{
			var ui = editorController.GetEditorUI();
			ui.OnWaypointSelected += SelectWaypoint;
			ui.OnWaypointAddRequested += AddWaypointAtCursor;
			ui.OnWaypointDelete += DeleteWaypoint;
			ui.OnWaypointMoveUp += i => MoveWaypoint(i, -1);
			ui.OnWaypointMoveDown += i => MoveWaypoint(i, 1);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedWaypointIndex = -1;
			EditorUtil.DestroyWaypointVisuals();
			RebuildMarkers();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			EditorUtil.DestroyWaypointVisuals();
		}

		// Call this from wherever you change maps (e.g. MapManager, MainController)
		public void OnMapChanged()
		{
			if (editorController.CurrentMode == EditorController.EditorMode.Waypoint)
				RebuildMarkers();
		}

		private void RebuildMarkers()
		{
			var map = editorController.iMapManager.CurrentMap;
			EditorUtil.UpdateWaypointMarkers(
				editorController.iMapManager,
				map?.waypoints ?? System.Array.Empty<Waypoint>(),
				SelectedWaypointIndex
			);
		}

		private void SelectWaypoint(int index)
		{
			SelectedWaypointIndex = index;
			RebuildMarkers();
		}

		private void AddWaypointAtCursor()
		{
			var map = editorController.iMapManager.CurrentMap;
			if (map == null) return;

			var newWp = new Waypoint
			{
				name = $"Waypoint {map.waypoints?.Length ?? 0}",
				tile = -1  // Unplaced — user must click map
			};

			var list = map.waypoints?.ToList() ?? new List<Waypoint>();
			list.Add(newWp);
			map.waypoints = list.ToArray();

			SelectedWaypointIndex = list.Count - 1;
			RebuildMarkers();
		}

		private void AddWaypointAtTile(int tile)
		{
			var map = editorController.iMapManager.CurrentMap;
			var wp = new Waypoint
			{
				name = $"Waypoint {map.waypoints?.Length ?? 0}",
				tile = tile
			};

			var list = map.waypoints?.ToList() ?? new List<Waypoint>();
			list.Add(wp);
			map.waypoints = list.ToArray();

			SelectedWaypointIndex = list.Count - 1;
			RebuildMarkers();
		}

		private void DeleteWaypoint(int index)
		{
			var map = editorController.iMapManager.CurrentMap;
			var list = map.waypoints.ToList();
			list.RemoveAt(index);
			map.waypoints = list.ToArray();

			if (SelectedWaypointIndex >= list.Count)
				SelectedWaypointIndex = list.Count - 1;

			RebuildMarkers();
		}

		private void MoveWaypoint(int index, int direction)
		{
			var map = editorController.iMapManager.CurrentMap;
			var list = map.waypoints.ToList();
			int newIdx = index + direction;

			if (newIdx < 0 || newIdx >= list.Count) return;

			(list[index], list[newIdx]) = (list[newIdx], list[index]);
			map.waypoints = list.ToArray();
			SelectedWaypointIndex = newIdx;

			RebuildMarkers();
		}

		public override void Update()
		{
			base.Update();

			if (!camera ||
				editorController.GetEditorUI().IsGuiControlActive() ||
				EventSystem.current.IsPointerOverGameObject())
				return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);

			// Yellow pulsing cursor
			EditorUtil.UpdateWaypointCursor(camera, editorController.iMapManager, worldPos);

			// Left click = place/move selected waypoint
			if (Input.GetMouseButtonDown(0))
			{
				var snapped = editorController.iMapManager.SnappedMapPosition(worldPos);
				int tile = editorController.iMapManager.WorldToMapIndex(snapped);

				if (tile >= 0)
				{
					if (SelectedWaypointIndex >= 0)
					{
						editorController.iMapManager.CurrentMap.waypoints[SelectedWaypointIndex].tile = tile;
					}
					else
					{
						AddWaypointAtTile(tile);
						return;
					}

					RebuildMarkers();
				}
			}
		}
	}
}