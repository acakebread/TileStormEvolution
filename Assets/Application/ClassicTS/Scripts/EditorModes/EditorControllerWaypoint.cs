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
			supressInput = true;
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

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				HandleMouseDown();

			if (Input.GetMouseButton(0))
				HandleDrag();

			if (Input.GetMouseButtonUp(0) && !mouseMovedBeyondThreshold)
				HandleLeftMouseUp();

			if (Input.GetMouseButtonUp(1) && !mouseMovedBeyondThreshold)
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
			AttachmentEditing.CurrentPendingTile = tileUnderMouse;

			if (currentMap?.waypoints != null && currentMap.waypoints.Contains(tileUnderMouse))
				pendingWaypoint = Array.IndexOf(currentMap.waypoints, tileUnderMouse);
			else
				pendingWaypoint = -1;
		}

		private void HandleDrag() => AttachmentEditing.HandleDrag(iMapManager, camera, EditorMarkerUtil.MarkerType.Waypoint);

		private void HandleLeftMouseUp()
		{
			var tileUnderMouse = HitTile(Input.mousePosition);
			AttachmentEditing.CurrentPendingTile = tileUnderMouse;

			if (currentMap?.waypoints != null && currentMap.waypoints.Contains(tileUnderMouse))
				pendingWaypoint = Array.IndexOf(currentMap.waypoints, tileUnderMouse);
			else
				pendingWaypoint = -1;

			if (tileUnderMouse >= 0 && currentMap?.waypoints != null && !currentMap.waypoints.Contains(tileUnderMouse))
				pendingAction = PendingAction.Add;
		}

		private void HandleRightMouseUp()
		{
			var tileUnderMouse = HitTile(Input.mousePosition);
			AttachmentEditing.CurrentPendingTile = tileUnderMouse;

			if (currentMap?.waypoints != null && currentMap.waypoints.Contains(tileUnderMouse))
				pendingWaypoint = Array.IndexOf(currentMap.waypoints, tileUnderMouse);
			else
				pendingWaypoint = -1;

			if (pendingWaypoint >= 0 && currentMap?.waypoints != null && pendingWaypoint < currentMap.waypoints.Length)
			{
				AttachmentEditing.CurrentPendingTile = currentMap.waypoints[pendingWaypoint];
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

		private void MoveWaypoint(Waypoint wp, int direction)
		{
			var index = wp.waypointIndex;
			var list = currentMap.waypoints.ToList();
			(list[index + direction], list[index]) = (list[index], list[index + direction]);
			currentMap.waypoints = list.ToArray();
			pendingWaypoint = index + direction;
		}

		private bool DrawAddPopup()
		{
			var wasCancelled = true;
			var items = new List<PopupItem>
			{
				new ($"WP{currentMap.waypoints.Length:00} at tile {AttachmentEditing.CurrentPendingTile}", null, null, spacerHeight: 0),
				PopupItem.Spacer(6),
				new ("Add", () => { AddWaypointAtTile(AttachmentEditing.CurrentPendingTile); pendingAction = PendingAction.None; }, colorOverride: Color.cyan),
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
				new ($"WP{pendingWaypoint:00} at tile {AttachmentEditing.CurrentPendingTile}", null, null, spacerHeight: 0),
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
				items.Add(new ListViewItem(label: $"WP{i:00} [{wp[i]}]", onClick: (x) => pendingWaypoint = x, selected: i == selectedWaypoint?.waypointIndex));

			sidePanel.List.SetItems(items);

			sidePanel.Buttons.Clear();
			sidePanel.Buttons.Add(new ListViewButton("Move Up", () => MoveWaypoint(selectedWaypoint, -1), enabled: selectedWaypoint?.waypointIndex > 0));
			sidePanel.Buttons.Add(new ListViewButton("Move Down", () => MoveWaypoint(selectedWaypoint, +1), enabled: selectedWaypoint?.waypointIndex >= 0 && selectedWaypoint?.waypointIndex < wp.Length - 1));
			sidePanel.Draw();
		}

		private Waypoint selectedWaypoint
		{
			get => null != AttachmentEditing.selectedAttachments && AttachmentEditing.selectedAttachments.Length > 0 ? AttachmentEditing.selectedAttachments[0] as Waypoint : null;
		}

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
				if (value < 0 || currentMap?.waypoints == null || value >= currentMap.waypoints.Length)
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
	}
}