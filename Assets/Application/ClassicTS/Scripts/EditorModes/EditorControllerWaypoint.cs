using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public class EditorControllerWaypoint : EditorControllerMovement
	{
		public int SelectedWaypointIndex { get; private set; } = -1;

		private int draggingIndex = -1;
		private int originalTile = -1;

		private int pendingTile = -1;
		private int pendingWaypoint = -1;
		private enum PendingAction { None, Add, Delete }
		private PendingAction pendingAction = PendingAction.None;
		private Vector2 pendingPopupScreenPos = Vector2.zero;

		private Vector3 clickStartPos;
		private int clickStartTile = -1;
		private int potentialWaypointHit = -1;

		private static readonly AutoHidePanel sidePanel = new(120f, 340f, 1.5f, 0.25f);

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public override void OnMapLoaded() => RebuildMarkers();

		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedWaypointIndex = -1;
			EditorMarkerUtil.ClearMapMarkers();
			RebuildMarkers();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			EditorMarkerUtil.ClearMapMarkers();
			pendingAction = PendingAction.None;
		}

		public override void Update()
		{
			base.Update();
			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			int tileUnderMouse = GetTileUnderMouse();

			// === CLICK DETECTION (shared for left & right) ===
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				clickStartPos = Input.mousePosition;
				clickStartTile = tileUnderMouse;
				potentialWaypointHit = IndexOfWaypoint(HitTile(Input.mousePosition));
			}

			// === LEFT CLICK RELEASE - ADD or DRAG ===
			if (Input.GetMouseButtonUp(0))
			{
				bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;

				if (draggingIndex >= 0)
				{
					// Was dragging a waypoint
					if (!wasClick && tileUnderMouse < 0)
					{
						// Revert if dropped outside map
						var map = iMapManager.CurrentMap;
						if (map?.waypoints != null && draggingIndex < map.waypoints.Length)
							iMapManager.Waypoints[draggingIndex] = originalTile;
						RebuildMarkers();
					}
					draggingIndex = -1;
					originalTile = -1;
				}
				else if (wasClick && clickStartTile >= 0 && potentialWaypointHit < 0)
				{
					// True click on empty tile → prepare to add
					pendingTile = clickStartTile;
					pendingAction = PendingAction.Add;
				}
			}

			// === RIGHT CLICK RELEASE - DELETE ===
			if (Input.GetMouseButtonUp(1))
			{
				bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;

				if (wasClick && potentialWaypointHit >= 0)
				{
					SelectWaypoint(potentialWaypointHit);

					var map = iMapManager.CurrentMap;
					if (map?.waypoints != null && potentialWaypointHit < map.waypoints.Length)
					{
						pendingTile = iMapManager.Waypoints[potentialWaypointHit];
						pendingWaypoint = potentialWaypointHit;
						pendingAction = PendingAction.Delete;
					}
				}
			}

			// === DRAG EXISTING WAYPOINT (left button held) ===
			if (Input.GetMouseButton(0) && draggingIndex >= 0 && tileUnderMouse >= 0)
			{
				var map = iMapManager.CurrentMap;
				if (map?.waypoints != null && draggingIndex < map.waypoints.Length)
				{
					if (map.waypoints[draggingIndex] != tileUnderMouse)
					{
						map.waypoints[draggingIndex] = tileUnderMouse;
						RebuildMarkers();
					}
				}
			}

			// Start drag only if we clicked a waypoint and are now moving
			if (Input.GetMouseButtonDown(0) && potentialWaypointHit >= 0)
			{
				SelectWaypoint(potentialWaypointHit);
				draggingIndex = potentialWaypointHit;
				originalTile = iMapManager.Waypoints[potentialWaypointHit];
				pendingAction = PendingAction.None; // cancel any pending add
			}

			int IndexOfWaypoint(int tileIndex) => null != currentMap && null != currentMap.waypoints && currentMap.waypoints.Contains(tileIndex) ? Array.IndexOf(currentMap.waypoints, tileIndex) : -1;
		}

		public override void OnGUI()
		{
			DrawSidePanel();
			// Popups
			if (pendingAction == PendingAction.Add) DrawAddPopup();
			if (pendingAction == PendingAction.Delete) DrawDeletePopup();
		}

		private void RebuildMarkers() => AttachmentEditing.UpdateMapMarkers(iMapManager, iMapManager.Waypoints, SelectedWaypointIndex, EditorMarkerUtil.MarkerType.Waypoint);

		private void SelectWaypoint(int index)
		{
			SelectedWaypointIndex = index;
			RebuildMarkers();
		}

		private void AddWaypointAtTile(int tile)
		{
			if (null == iMapManager) return;

			var list = iMapManager.Waypoints?.ToList() ?? new List<int>();
			list.Add(tile);
			iMapManager.Waypoints = list.ToArray();

			SelectWaypoint(list.Count - 1);
		}

		private void DeleteWaypoint(int index)
		{
			if (null == currentMap || null == currentMap.waypoints || index < 0 || index >= currentMap.waypoints.Length) return;
			var list = currentMap.waypoints.ToList();
			list.RemoveAt(index);
			currentMap.waypoints = list.ToArray();
			SelectWaypoint(-1);
		}

		private int GetTileUnderMouse()
		{
			if (!camera) return -1;
			Vector3 mouseWorld = MapManager.ScreenToWorld(camera, Input.mousePosition);
			Vector3 snapped = MapManager.SnappedMapPosition(mouseWorld);
			return iMapManager.WorldToMapIndex(snapped);
		}

		private void MoveWaypoint(int index, int direction)
		{
			var map = iMapManager.CurrentMap;
			var list = map.waypoints.ToList();
			var temp = list[index];
			list[index] = list[index + direction];
			list[index + direction] = temp;
			map.waypoints = list.ToArray();
			SelectedWaypointIndex += direction;
			RebuildMarkers();
		}

		private void DrawAddPopup()
		{
			var items = new List<PopupItem>
			{
				// Info line (non-clickable)
				new ($"WP{iMapManager.Waypoints.Length:00} at tile {pendingTile}", null, null, spacerHeight: 0),
				PopupItem.Spacer(6),
				new ("Add", () => AddWaypointAtTile(pendingTile), colorOverride: Color.cyan),// Add waypoint
				PopupItem.Spacer(4),
				new ("Cancel", () => { }, Color.yellow)// Cancel
			};

			if (false == PopupMenu.Show(clickStartPos, "Add Waypoint?", items))
				pendingAction = PendingAction.None;
		}

		private void DrawDeletePopup()
		{
			var items = new List<PopupItem>
			{
				// Info line (non-clickable)
				new ($"WP{pendingWaypoint:00} at tile {pendingTile}", null, null, spacerHeight: 0),
				PopupItem.Spacer(6),
				new ("Delete", () => DeleteWaypoint(pendingWaypoint), colorOverride: Color.red), // Delete waypoint
				PopupItem.Spacer(4),
				new ("Cancel", () => { }, Color.yellow )// Cancel
			};

			if (false == PopupMenu.Show(pendingPopupScreenPos, "Delete Waypoint?", items))
				pendingAction = PendingAction.None;
		}

		private void DrawSidePanel()
		{
			// Build ListView items
			var wp = iMapManager.Waypoints ?? Array.Empty<int>();
			var items = new List<ListViewItem>();

			for (int i = 0; i < wp.Length; i++)
				items.Add(new ListViewItem(label: $"WP{i:00} [{wp[i]}]", onClick: () => SelectWaypoint(i), selected: i == SelectedWaypointIndex));

			sidePanel.List.SetItems(items);

			// Build dynamic Move buttons
			sidePanel.Buttons.Clear();

			// Up button always visible
			sidePanel.Buttons.Add(new ListViewButton("Move Up", () => MoveWaypoint(SelectedWaypointIndex, -1), enabled: SelectedWaypointIndex > 0));

			// Down button always visible
			sidePanel.Buttons.Add(new ListViewButton("Move Down", () => MoveWaypoint(SelectedWaypointIndex, +1), enabled: SelectedWaypointIndex >= 0 && SelectedWaypointIndex < wp.Length - 1));

			// Draw entire panel
			sidePanel.Draw();
		}
	}
}