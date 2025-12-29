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

		private void RebuildMarkers() => AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);

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
			AttachmentEditing.selectedAttachments = null;
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

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				mouseDownPos = Input.mousePosition;
				mouseMovedBeyondThreshold = false;
			}

			if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
			{
				if (Vector3.Distance(Input.mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
					mouseMovedBeyondThreshold = true;
			}

			// Mouse Down: select attachments
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				HandleMouseDown();

			if (Input.GetMouseButton(0))
				HandleDrag();//drag must come after down - tightly coupled order

			if (Input.GetMouseButtonUp(0) && !mouseMovedBeyondThreshold)//was click = !mouseMovedBeyondThreshold
				HandleLeftMouseUp();

			if (Input.GetMouseButtonUp(1) && !mouseMovedBeyondThreshold)//was click = !mouseMovedBeyondThreshold
				HandleRightMouseUp();
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

			pendingAction = PendingAction.None;
		}

		private void HandleMouseDown()
		{
			var tileUnderMouse = HitTile(Input.mousePosition);
			// Update pendingWaypoint based on what's under mouse
			if (currentMap?.waypoints != null && currentMap.waypoints.Contains(tileUnderMouse))
				pendingWaypoint = Array.IndexOf(currentMap.waypoints, tileUnderMouse);
			else
				pendingWaypoint = -1;
		}

		private void HandleDrag()
		{
			var tileUnderMouse = HitTile(Input.mousePosition);
			if (-1 == tileUnderMouse || pendingTile == tileUnderMouse || null == AttachmentEditing.selectedAttachments || 0 == AttachmentEditing.selectedAttachments.Length)
				return;

			pendingTile = tileUnderMouse;
			AttachmentEditing.RefreshAttachmentInstances(iMapManager, tileUnderMouse);
			AttachmentEditing.HandleDragInput(iMapManager, camera);
			AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);
		}

		private void HandleLeftMouseUp()
		{
			var tileUnderMouse = HitTile(Input.mousePosition);
			pendingTile = tileUnderMouse;

			// Update pendingWaypoint based on what's under mouse
			if (currentMap?.waypoints != null && currentMap.waypoints.Contains(pendingTile))
				pendingWaypoint = Array.IndexOf(currentMap.waypoints, pendingTile);
			else
				pendingWaypoint = -1;

			if (pendingTile >= 0 && currentMap?.waypoints != null && !currentMap.waypoints.Contains(pendingTile))
				pendingAction = PendingAction.Add;
		}

		private void HandleRightMouseUp()
		{
			var tileUnderMouse = HitTile(Input.mousePosition);
			pendingTile = tileUnderMouse;

			// Update pendingWaypoint based on what's under mouse
			if (currentMap?.waypoints != null && currentMap.waypoints.Contains(pendingTile))
				pendingWaypoint = Array.IndexOf(currentMap.waypoints, pendingTile);
			else
				pendingWaypoint = -1;

			if (pendingWaypoint >= 0 && currentMap?.waypoints != null && pendingWaypoint < currentMap.waypoints.Length)
			{
				pendingTile = currentMap.waypoints[pendingWaypoint];
				pendingAction = PendingAction.Delete;
			}
		}

		private void AddWaypointAtTile(int tile)
		{
			if (null == currentMap.waypoints) return;

			var list = iMapManager.Waypoints?.ToList() ?? new List<int>();
			list.Add(tile);
			currentMap.waypoints = list.ToArray();
			pendingWaypoint = list.Count - 1;
		}

		private void DeleteWaypoint(int index)
		{
			if (null == currentMap || null == currentMap.waypoints || index < 0 || index >= currentMap.waypoints.Length) return;
			var list = currentMap.waypoints.ToList();
			list.RemoveAt(index);
			currentMap.waypoints = list.ToArray();
			pendingWaypoint = -1;
		}

		private void MoveWaypoint(int index, int direction)
		{
			var list = currentMap.waypoints.ToList();
			(list[index + direction], list[index]) = (list[index], list[index + direction]);//swap index values
			currentMap.waypoints = list.ToArray();
			pendingWaypoint = index + direction;
		}

		private bool DrawAddPopup()
		{
			var wasCancelled = true;
			var items = new List<PopupItem>
			{
				new ($"WP{currentMap.waypoints.Length:00} at tile {pendingTile}", null, null, spacerHeight: 0),
				PopupItem.Spacer(6),
				new ("Add", () => { AddWaypointAtTile(pendingTile); pendingAction = PendingAction.None; }, colorOverride: Color.cyan),
				PopupItem.Spacer(4),
				new ("Cancel", () => { }, Color.yellow)
			};
			var result = PopupMenu.Show(mouseDownPos, "Add Waypoint?", items);
			if (!result && wasCancelled)
				pendingWaypoint = -1;
			return result;
		}

		private bool DrawDeletePopup()
		{
			var wasCancelled = true;
			var items = new List<PopupItem>
			{
				new ($"WP{pendingWaypoint:00} at tile {pendingTile}", null, null, spacerHeight: 0),
				PopupItem.Spacer(6),
				new ("Delete", () => {DeleteWaypoint(pendingWaypoint); pendingAction = PendingAction.None;}, colorOverride: Color.red),
				PopupItem.Spacer(4),
				new ("Cancel", () => { }, Color.yellow )
			};
			var result = PopupMenu.Show(mouseDownPos, "Delete Waypoint?", items);
			if (!result && wasCancelled)
				pendingWaypoint = -1;
			return result;
		}

		private void DrawSidePanel()
		{
			var wp = currentMap.waypoints ?? Array.Empty<int>();
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

		// ===================================================================
		// pendingWaypoint is now a PROPERTY — no backing field
		// It transparently converts to/from WaypointAttachment.waypointIndex
		// ===================================================================
		private int pendingWaypoint
		{
			get
			{
				var selected = AttachmentEditing.selectedAttachments;
				if (null != selected && 0 != selected.Length && selected[0] is Waypoint wa)
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

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			if (currentMap?.waypoints == null || !currentMap.IsValidTile(tileIndex))
				return null;

			var waypointsOnTile = currentMap.waypoints
				.Select((tile, index) => new { tile, index })
				.Where(x => x.tile == tileIndex)
				.Select(x => new Waypoint(x.index, tileIndex))
				.ToArray();

			return waypointsOnTile.Length > 0 ? waypointsOnTile : null;
		}
	}
}