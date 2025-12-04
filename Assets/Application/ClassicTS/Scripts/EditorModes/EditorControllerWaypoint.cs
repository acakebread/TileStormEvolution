using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class EditorControllerWaypoint : EditorControllerMovement
	{
		public int SelectedWaypointIndex { get; private set; } = -1;
		private bool isInPlacementMode = false;  // Are we currently placing a new waypoint?
		private int draggingIndex = -1;
		private int originalTile = -1;

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
			// Toggle placement mode
			isInPlacementMode = !isInPlacementMode;

			if (isInPlacementMode)
			{
				SelectWaypoint(-1); // Deselect current
				RebuildMarkers();
			}
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
			var snapped = editorController.iMapManager.SnappedMapPosition(worldPos);
			int tileUnderMouse = editorController.iMapManager.WorldToMapIndex(snapped);

			bool hasSelection = SelectedWaypointIndex >= 0;

			// === YELLOW CURSOR: Only show in placement mode (not when just hovering normally) ===
			if (isInPlacementMode && tileUnderMouse >= 0)
			{
				EditorUtil.UpdateWaypointCursor(camera, editorController.iMapManager, worldPos);
			}
			else
			{
				EditorUtil.HideWaypointCursor();
			}

			// === LEFT CLICK ===
			if (Input.GetMouseButtonDown(0))
			{
				Ray ray = camera.ScreenPointToRay(Input.mousePosition);

				// 1. Clicked on an existing waypoint?
				if (Physics.Raycast(ray, out RaycastHit hit))
				{
					if (hit.collider && hit.collider.gameObject.name.StartsWith("WP"))
					{
						if (int.TryParse(hit.collider.gameObject.name.Substring(2), out int index))
						{
							// If in placement mode → cancel it
							if (isInPlacementMode)
							{
								isInPlacementMode = false;
							}

							SelectWaypoint(index);
							draggingIndex = index;
							originalTile = editorController.iMapManager.CurrentMap.waypoints[index].tile;
							return;
						}
					}
				}

				// 2. In placement mode and clicked valid tile → CREATE NEW
				if (isInPlacementMode && tileUnderMouse >= 0)
				{
					AddWaypointAtTile(tileUnderMouse);
					isInPlacementMode = false; // Done placing
					return;
				}

				// 3. Clicked empty space while not placing → do nothing (or optionally deselect)
				if (!hasSelection && !isInPlacementMode)
				{
					SelectWaypoint(-1); // Optional: deselect all
				}
			}

			// === DRAG EXISTING WAYPOINT ===
			if (draggingIndex >= 0 && Input.GetMouseButton(0))
			{
				if (tileUnderMouse >= 0)
				{
					editorController.iMapManager.CurrentMap.waypoints[draggingIndex].tile = tileUnderMouse;
					RebuildMarkers();
				}
			}

			// === DROP (validate or revert) ===
			if (draggingIndex >= 0 && Input.GetMouseButtonUp(0))
			{
				if (tileUnderMouse < 0)
				{
					editorController.iMapManager.CurrentMap.waypoints[draggingIndex].tile = originalTile;
					RebuildMarkers();
				}
				draggingIndex = -1;
				originalTile = -1;
			}

			// === ESCAPE cancels placement mode ===
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				isInPlacementMode = false;
			}
		}
	}
}