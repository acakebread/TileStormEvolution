using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public class EditorControllerWaypoint : EditorControllerMovement
	{
		private Vector3 mouseDownPos;
		private bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 8f;
		private bool supressInput = true;

		private int pendingTile = -1;
		private int pendingWaypoint = -1;//this is effectively 'selectedAttachments'

		private enum PendingAction { None, Add, Delete }
		private PendingAction pendingAction = PendingAction.None;

		private static readonly AutoHidePanel sidePanel = new(120f, 340f, 1.5f, 0.25f);

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public override void OnMapLoaded() => RebuildMarkers();

		private void RebuildMarkers() => AttachmentEditing.UpdateMapMarkers(iMapManager, iMapManager.Waypoints, pendingWaypoint, EditorMarkerUtil.MarkerType.Waypoint);

		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();
			pendingWaypoint = -1;
			AttachmentEditing.HideAllGizmos();
			RebuildMarkers();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			AttachmentEditing.HideAllGizmos();
			pendingAction = PendingAction.None;
		}

		public override void Update()
		{
			base.Update();
			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			if (supressInput)
			{
				supressInput = false;
				return;
			}

			var tileUnderMouse = HitTile(Input.mousePosition);

			// === CLICK DETECTION (shared for left & right) ===
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				mouseDownPos = Input.mousePosition;
				mouseMovedBeyondThreshold = false;
				pendingTile = tileUnderMouse;
				pendingWaypoint = null != currentMap && null != currentMap.waypoints && currentMap.waypoints.Contains(pendingTile) ? Array.IndexOf(currentMap.waypoints, pendingTile) : -1;
			}

			if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
			{
				if (Vector3.Distance(Input.mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
					mouseMovedBeyondThreshold = true;
			}

			var wasClick = !mouseMovedBeyondThreshold;

			// === LEFT CLICK RELEASE - ADD or DRAG ===
			if (Input.GetMouseButtonUp(0))
			{
				if (wasClick && pendingTile >= 0)
					pendingAction = PendingAction.Add;
			}

			// === RIGHT CLICK RELEASE - DELETE ===
			if (Input.GetMouseButtonUp(1))
			{
				if (wasClick && pendingWaypoint >= 0)
				{
					SelectWaypoint(pendingWaypoint);

					var map = iMapManager.CurrentMap;
					if (map?.waypoints != null && pendingWaypoint < map.waypoints.Length)
					{
						pendingTile = iMapManager.Waypoints[pendingWaypoint];
						pendingAction = PendingAction.Delete;
					}
				}
			}

			// === DRAG EXISTING WAYPOINT (left button held) ===
			if (Input.GetMouseButton(0) && pendingWaypoint >= 0 && tileUnderMouse >= 0)
			{
				var map = iMapManager.CurrentMap;
				if (map?.waypoints != null && pendingWaypoint < map.waypoints.Length)
				{
					if (map.waypoints[pendingWaypoint] != tileUnderMouse)
					{
						map.waypoints[pendingWaypoint] = tileUnderMouse;
						RebuildMarkers();
					}
				}
			}

			// Start drag only if we clicked a waypoint and are now moving
			if (Input.GetMouseButtonDown(0) && pendingWaypoint >= 0)
			{
				SelectWaypoint(pendingWaypoint);
				pendingAction = PendingAction.None; // cancel any pending add
			}
		}

		public override void OnGUI()
		{
			DrawSidePanel();
			// Popups
			if (PendingAction.None == pendingAction) return;
			supressInput = true;

			switch (pendingAction)
			{
				case PendingAction.Add: if (DrawAddPopup()) return; break;
				case PendingAction.Delete: if (DrawDeletePopup()) return; break;
			}
		}

		private void SelectWaypoint(int index)
		{
			pendingWaypoint = index;
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

		private void MoveWaypoint(int index, int direction)
		{
			var map = iMapManager.CurrentMap;
			var list = map.waypoints.ToList();
			var temp = list[index];
			list[index] = list[index + direction];
			list[index + direction] = temp;
			map.waypoints = list.ToArray();
			pendingWaypoint += direction;
			RebuildMarkers();
		}

		private bool DrawAddPopup()
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

			if (false == PopupMenu.Show(mouseDownPos, "Add Waypoint?", items))
			{
				pendingAction = PendingAction.None;
				return false;
			}
			return true;
		}

		private bool DrawDeletePopup()
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

			if (false == PopupMenu.Show(mouseDownPos, "Delete Waypoint?", items))
			{
				pendingAction = PendingAction.None;
				return false;
			}
			return true;
		}

		private void DrawSidePanel()
		{
			// Build ListView items
			var wp = iMapManager.Waypoints ?? Array.Empty<int>();
			var items = new List<ListViewItem>();

			for (int i = 0; i < wp.Length; i++)
				items.Add(new ListViewItem(label: $"WP{i:00} [{wp[i]}]", onClick: (x) => SelectWaypoint(x), selected: i == pendingWaypoint));

			sidePanel.List.SetItems(items);

			// Build dynamic Move buttons
			sidePanel.Buttons.Clear();

			// Up button always visible
			sidePanel.Buttons.Add(new ListViewButton("Move Up", () => MoveWaypoint(pendingWaypoint, -1), enabled: pendingWaypoint > 0));

			// Down button always visible
			sidePanel.Buttons.Add(new ListViewButton("Move Down", () => MoveWaypoint(pendingWaypoint, +1), enabled: pendingWaypoint >= 0 && pendingWaypoint < wp.Length - 1));

			// Draw entire panel
			sidePanel.Draw();
		}
	}
}