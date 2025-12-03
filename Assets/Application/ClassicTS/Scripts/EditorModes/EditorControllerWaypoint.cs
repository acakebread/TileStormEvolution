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
			ui.OnWaypointSelected += i => SelectWaypoint(i);
			ui.OnWaypointAddRequested += AddWaypointAtCursor;
			ui.OnWaypointDelete += DeleteWaypoint;
			ui.OnWaypointMoveUp += i => MoveWaypoint(i, -1);
			ui.OnWaypointMoveDown += i => MoveWaypoint(i, 1);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedWaypointIndex = -1;  // Critical!
			EditorUtil.HideGhostTile();
			EditorUtil.DestroyWaypointVisuals();
			RefreshVisuals();  // This will show all as green/cyan
			Debug.Log("Waypoint Editor Active");
		}

		public override void OnDisable()
		{
			base.OnDisable();
			EditorUtil.HideGhostTile();
			EditorUtil.DestroyWaypointVisuals();  // This was missing or not called correctly
		}

		private Vector3 lastValidMouseWorldPos = Vector3.zero;
		private bool hasValidMousePos = false;

		private void AddWaypointAtCursor()
		{
			var map = editorController.iMapManager.CurrentMap;
			if (map == null) return;

			var newWp = new Waypoint
			{
				name = $"Waypoint {map.waypoints?.Length ?? 0}",
				tile = -1  // Invalid on purpose — user must click to place
			};

			var list = map.waypoints?.ToList() ?? new System.Collections.Generic.List<Waypoint>();
			list.Add(newWp);
			map.waypoints = list.ToArray();

			SelectedWaypointIndex = list.Count - 1;
			RefreshVisuals();

			Debug.Log($"Added new waypoint #{SelectedWaypointIndex} — click map to place it");
		}

		private void AddWaypointAtTile(int tile)
		{
			var map = editorController.iMapManager.CurrentMap;
			var wp = new Waypoint
			{
				name = $"Waypoint {map.waypoints?.Length ?? 0}",
				tile = tile
			};

			var list = map.waypoints?.ToList() ?? new();
			list.Add(wp);
			map.waypoints = list.ToArray();

			SelectedWaypointIndex = list.Count - 1;
			RefreshVisuals();
		}

		// Fix marker colors — only selected is blue
		private void RefreshVisuals()
		{
			var map = editorController.iMapManager.CurrentMap;
			EditorUtil.UpdateWaypointMarkers(
				editorController.iMapManager,
				map.waypoints,
				SelectedWaypointIndex  // Only this one is blue
			);
		}

		private void SelectWaypoint(int index)
		{
			SelectedWaypointIndex = index;
			RefreshVisuals();
		}

		private void DeleteWaypoint(int index)
		{
			var map = editorController.iMapManager.CurrentMap;
			var list = map.waypoints.ToList();
			list.RemoveAt(index);
			map.waypoints = list.ToArray();
			if (SelectedWaypointIndex >= map.waypoints.Length)
				SelectedWaypointIndex = map.waypoints.Length - 1;
			RefreshVisuals();
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
			RefreshVisuals();
		}

		public override void Update()
		{
			base.Update();

			if (!camera || editorController.GetEditorUI().IsGuiControlActive() || EventSystem.current.IsPointerOverGameObject())
				return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			var snapped = editorController.iMapManager.SnappedMapPosition(worldPos);
			int tileIndex = editorController.iMapManager.WorldToMapIndex(snapped);

			if (tileIndex >= 0)
			{
				lastValidMouseWorldPos = snapped;
				hasValidMousePos = true;
			}

			EditorUtil.UpdateWaypointGhost(camera, editorController.iMapManager, worldPos);

			if (Input.GetMouseButtonDown(0))
			{
				if (tileIndex >= 0)
				{
					if (SelectedWaypointIndex >= 0)
					{
						editorController.iMapManager.CurrentMap.waypoints[SelectedWaypointIndex].tile = tileIndex;
					}
					else
					{
						AddWaypointAtTile(tileIndex);
					}
					RefreshVisuals();
				}
			}

			RefreshVisuals();
		}
	}
}