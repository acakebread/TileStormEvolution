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

		private enum PendingAction { None, Add, Delete }
		private PendingAction pendingAction = PendingAction.None;

		private static readonly AutoHidePanel sidePanel = new(120f, 340f, 1.5f, 0.25f);

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public override void OnMapLoaded() => RebuildMarkers();

		private void RebuildMarkers()
		{
			// Use current pendingWaypoint index for marker highlight
			AttachmentEditing.UpdateMapMarkers(iMapManager, iMapManager.Waypoints, pendingWaypoint, EditorMarkerUtil.MarkerType.Waypoint);
		}

		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();
			pendingWaypoint = -1; // This will clear shared selection
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

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				mouseDownPos = Input.mousePosition;
				mouseMovedBeyondThreshold = false;
				pendingTile = tileUnderMouse;

				// Update pendingWaypoint based on what's under mouse
				if (currentMap?.waypoints != null && currentMap.waypoints.Contains(pendingTile))
				{
					pendingWaypoint = Array.IndexOf(currentMap.waypoints, pendingTile);
				}
				else
				{
					pendingWaypoint = -1;
				}
			}

			if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
			{
				if (Vector3.Distance(Input.mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
					mouseMovedBeyondThreshold = true;
			}

			var wasClick = !mouseMovedBeyondThreshold;

			if (Input.GetMouseButtonUp(0))
			{
				if (wasClick && pendingTile >= 0 && currentMap?.waypoints != null && !currentMap.waypoints.Contains(pendingTile))
					pendingAction = PendingAction.Add;
			}

			if (Input.GetMouseButtonUp(1))
			{
				if (wasClick && pendingWaypoint >= 0)
				{
					var map = iMapManager.CurrentMap;
					if (map?.waypoints != null && pendingWaypoint < map.waypoints.Length)
					{
						pendingTile = iMapManager.Waypoints[pendingWaypoint];
						pendingAction = PendingAction.Delete;
					}
				}
			}

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

			if (Input.GetMouseButtonDown(0) && pendingWaypoint >= 0)
			{
				pendingAction = PendingAction.None;
			}
		}

		public override void OnGUI()
		{
			DrawSidePanel();

			if (pendingAction == PendingAction.None) return;
			supressInput = true;

			switch (pendingAction)
			{
				case PendingAction.Add: if (DrawAddPopup()) return; break;
				case PendingAction.Delete: if (DrawDeletePopup()) return; break;
			}
		}

		// ===================================================================
		// pendingWaypoint is now a PROPERTY — no backing field
		// It transparently converts to/from WaypointAttachment.waypointIndex
		// ===================================================================
		private int pendingWaypoint
		{
			get
			{
				var selected = AttachmentEditing.selectedAttachments;
				if (selected == null || selected.Length == 0) return -1;
				if (selected[0] is Waypoint wa)
					return wa.waypointIndex;
				return -1;
			}
			set
			{
				if (value < 0)
				{
					AttachmentEditing.Select(null, iMapManager, camera);
					RebuildMarkers();
					return;
				}

				if (currentMap?.waypoints == null || value >= currentMap.waypoints.Length)
				{
					AttachmentEditing.Select(null, iMapManager, camera);
					RebuildMarkers();
					return;
				}

				var att = new Waypoint(value, currentMap.waypoints[value]);
				AttachmentEditing.Select(new[] { att }, iMapManager, camera);
				RebuildMarkers();
			}
		}

		private void AddWaypointAtTile(int tile)
		{
			if (null == iMapManager) return;

			var list = iMapManager.Waypoints?.ToList() ?? new List<int>();
			list.Add(tile);
			iMapManager.Waypoints = list.ToArray();

			pendingWaypoint = list.Count - 1; // Uses setter → creates proxy and syncs
		}

		private void DeleteWaypoint(int index)
		{
			if (null == currentMap || null == currentMap.waypoints || index < 0 || index >= currentMap.waypoints.Length) return;
			var list = currentMap.waypoints.ToList();
			list.RemoveAt(index);
			currentMap.waypoints = list.ToArray();

			pendingWaypoint = -1; // Uses setter → clears selection
		}

		private void MoveWaypoint(int index, int direction)
		{
			var map = iMapManager.CurrentMap;
			var list = map.waypoints.ToList();
			var temp = list[index];
			list[index] = list[index + direction];
			list[index + direction] = temp;
			map.waypoints = list.ToArray();

			pendingWaypoint = index + direction; // Uses setter → updates proxy
		}

		private bool DrawAddPopup()
		{
			var items = new List<PopupItem>
			{
				new ($"WP{iMapManager.Waypoints.Length:00} at tile {pendingTile}", null, null, spacerHeight: 0),
				PopupItem.Spacer(6),
				new ("Add", () => AddWaypointAtTile(pendingTile), colorOverride: Color.cyan),
				PopupItem.Spacer(4),
				new ("Cancel", () => { }, Color.yellow)
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
				new ($"WP{pendingWaypoint:00} at tile {pendingTile}", null, null, spacerHeight: 0),
				PopupItem.Spacer(6),
				new ("Delete", () => DeleteWaypoint(pendingWaypoint), colorOverride: Color.red),
				PopupItem.Spacer(4),
				new ("Cancel", () => { }, Color.yellow )
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
			var wp = iMapManager.Waypoints ?? Array.Empty<int>();
			var items = new List<ListViewItem>();

			for (int i = 0; i < wp.Length; i++)
			{
				items.Add(new ListViewItem(
					label: $"WP{i:00} [{wp[i]}]",
					onClick: (x) => pendingWaypoint = x, // Uses setter → creates proxy
					selected: i == pendingWaypoint)); // Uses getter
			}

			sidePanel.List.SetItems(items);

			sidePanel.Buttons.Clear();
			sidePanel.Buttons.Add(new ListViewButton("Move Up", () => MoveWaypoint(pendingWaypoint, -1), enabled: pendingWaypoint > 0));
			sidePanel.Buttons.Add(new ListViewButton("Move Down", () => MoveWaypoint(pendingWaypoint, +1), enabled: pendingWaypoint >= 0 && pendingWaypoint < wp.Length - 1));

			sidePanel.Draw();
		}
	}
}