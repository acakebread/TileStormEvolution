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
		private bool rmbDownInPreview = false;
		private bool supressInput = true;

		private enum PendingAction { None, Add, Delete, Select }
		private PendingAction pendingAction = PendingAction.None;

		private static readonly AutoHidePanel sidePanel = new(120f, 340f, 1.5f, 0.25f);

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public override void OnMapLoaded() 
		{
			rmbDownInPreview = false;
			RebuildMarkers(); 
		}

		private void RebuildMarkers() => AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);

		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();
			supressInput = true;
			rmbDownInPreview = false;
			pendingAction = PendingAction.None;
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

			// === VIEW PREVIEW INTERACTION ===
			ViewPreviewUtil.SetInFocus(ViewPreviewUtil.IsMouseOverPreview());

			if (Input.GetMouseButtonDown(1))
				rmbDownInPreview = ViewPreviewUtil.IsInFocus;

			if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
				rmbDownInPreview = false;

			ViewPreviewUtil.SetInUse(rmbDownInPreview);

			var previewControlsActive = rmbDownInPreview || (!Input.GetMouseButton(1) && ViewPreviewUtil.IsInFocus);

			if (previewControlsActive)
			{
				EditorCameraMovement.UpdateCamera(ViewPreviewUtil.PreviewCamera.transform);
				AttachmentViewEditing.HandlePreviewCameraSync(iMapManager, camera);
				supressInput = true;
				return;
			}

			ViewPreviewUtil.Update();

			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			if (EditorTransformUtil.HandleTransformGizmoInput(camera))
			{
				AttachmentEditing.HandleGizmoInput(iMapManager, camera);
				supressInput = true;
			}

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

		//private void HandleMouseDown()
		//{
		//	var tileUnderMouse = HitTile(Input.mousePosition);
		//	AttachmentEditing.CurrentPendingTile = tileUnderMouse;

		//	if (currentMap?.waypoints != null && currentMap.waypoints.Contains(tileUnderMouse))
		//		pendingWaypoint = Array.IndexOf(currentMap.waypoints, tileUnderMouse);
		//	else
		//		pendingWaypoint = -1;
		//}

		//private void HandleLeftMouseUp()
		//{
		//	var tileUnderMouse = HitTile(Input.mousePosition);
		//	AttachmentEditing.CurrentPendingTile = tileUnderMouse;

		//	if (currentMap?.waypoints != null && currentMap.waypoints.Contains(tileUnderMouse))
		//		pendingWaypoint = Array.IndexOf(currentMap.waypoints, tileUnderMouse);
		//	else
		//		pendingWaypoint = -1;

		//	if (tileUnderMouse >= 0 && currentMap?.waypoints != null && !currentMap.waypoints.Contains(tileUnderMouse))
		//		pendingAction = PendingAction.Add;
		//}

		//private void HandleRightMouseUp()
		//{
		//	var tileUnderMouse = HitTile(Input.mousePosition);
		//	AttachmentEditing.CurrentPendingTile = tileUnderMouse;

		//	if (currentMap?.waypoints != null && currentMap.waypoints.Contains(tileUnderMouse))
		//		pendingWaypoint = Array.IndexOf(currentMap.waypoints, tileUnderMouse);
		//	else
		//		pendingWaypoint = -1;

		//	if (pendingWaypoint >= 0 && currentMap?.waypoints != null && pendingWaypoint < currentMap.waypoints.Length)
		//	{
		//		AttachmentEditing.CurrentPendingTile = currentMap.waypoints[pendingWaypoint];
		//		pendingAction = PendingAction.Delete;
		//	}
		//}

		private void HandleMouseDown()
		{
			int hitTile = HitTile(Input.mousePosition);
			AttachmentEditing.CurrentPendingTile = hitTile;

			if (-1 != hitTile)
			{
				var alreadySelected = AttachmentEditing.selectedAttachments?.Length > 0 && AttachmentEditing.selectedAttachments[0].tile == hitTile;
				if (!alreadySelected)
				{
					AttachmentEditing.selectedAttachments = GetAttachmentsOnTile(hitTile);
					AttachmentEditing.Select(AttachmentEditing.selectedAttachments, iMapManager, camera);
					if (AttachmentEditing.selectedAttachments?.Length > 0) Debug.Log($"selected {AttachmentEditing.selectedAttachments?.Length}");
				}
				return;
			}
			AttachmentEditing.Select(null, iMapManager, camera);
		}

		private void HandleDrag() => AttachmentEditing.HandleDrag(iMapManager, camera, EditorMarkerUtil.MarkerType.Waypoint); 
		
		private void HandleLeftMouseUp()
		{
			var attachmentsOnDownTile = GetAttachmentsOnTile(AttachmentEditing.CurrentPendingTile);

			if (attachmentsOnDownTile == null || attachmentsOnDownTile.Length == 0)
			{
				if (AttachmentEditing.CurrentPendingTile != -1)
					pendingAction = PendingAction.Add;
			}
			else if (attachmentsOnDownTile.Length > 1)
			{
				pendingAction = PendingAction.Select;
			}
			else
			{
				pendingAction = PendingAction.None;
				AttachmentEditing.Select(attachmentsOnDownTile, iMapManager, camera);
			}
		}

		private void HandleRightMouseUp()
		{
			int hitTile = HitTile(mouseDownPos);
			if (hitTile >= 0 && GetAttachmentsOnTile(hitTile)?.Length > 0)
			{
				AttachmentEditing.CurrentPendingTile = hitTile;
				pendingAction = PendingAction.Delete;
				return;
			}
			AttachmentEditing.Select(null, iMapManager, camera);
		}

		//private void AddWaypointAtTile(int tile)
		//{
		//	//if (null == currentMap.waypoints) return;
		//	//var newIndex = currentMap.waypoints?.Length ?? 0;
		//	//var newWp = new Waypoint(newIndex, tile);
		//	var newWp = AttachmentWaypointEditing.CreateWaypoint(iMapManager, tile);
		//	//iMapManager.attachments = iMapManager.attachments.Append(newWp).ToArray();//add the new waypoint
		//	AttachmentEditing.Select(new[] { newWp }, iMapManager, camera);
		//}

		//private void DeleteWaypoint(int index)
		//{
		//	//if (null == currentMap || null == currentMap.waypoints || index < 0 || index >= currentMap.waypoints.Length) return;
		//	//var list = currentMap.waypoints.ToList();
		//	//list.RemoveAt(index);
		//	//currentMap.waypoints = list.ToArray();
		//	//pendingWaypoint = -1;

		//	var atts = GetAttachmentsOnTile(index);
		//	iMapManager.RemoveAttachment(atts[0]);
		//	AttachmentEditing.Select(null, iMapManager, camera);
		//}

		//private void DeleteWaypoint(Waypoint waypoint)
		//{
		//	iMapManager.RemoveAttachment(waypoint);
		//	AttachmentEditing.Select(null, iMapManager, camera);
		//}

		//private void MoveWaypoint(Waypoint wp, int direction)
		//{
		//	var index = wp.waypointIndex;
		//	var list = currentMap.waypoints.ToList();
		//	(list[index + direction], list[index]) = (list[index], list[index + direction]);
		//	currentMap.waypoints = list.ToArray();
		//	//pendingWaypoint = index + direction;
		//}


		private void MoveWaypoint(Waypoint wp, int direction)
		{
			if (wp == null) return;

			int oldIndex = wp.waypointIndex;
			int newIndex = oldIndex + direction;

			if (newIndex < 0 || newIndex >= currentMap.waypoints.Length) return;

			// Swap in the underlying waypoints array
			var list = currentMap.waypoints.ToList();
			(list[oldIndex], list[newIndex]) = (list[newIndex], list[oldIndex]);
			currentMap.waypoints = list.ToArray();

			// Update the waypoint objects' indices (important for future selections)
			// We need to refresh the virtual attachments so indices are correct
			// But since we're in editor, easiest is to re-select the moved one
			var movedWaypoint = new Waypoint(newIndex, list[newIndex]);
			AttachmentEditing.Select(new[] { movedWaypoint }, iMapManager, camera);

			// Rebuild markers to reflect new positions
			RebuildMarkers();
		}

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			if (currentMap == null || currentMap.attachments == null || !currentMap.IsValidTile(tileIndex)) return null;
			//var result = currentMap.attachments.Where(x => x.tile == tileIndex).ToArray();
			var result = iMapManager.attachments.Where(x => x.tile == tileIndex).ToArray();
			return result.Length > 0 ? result : null;
		}

		private bool DrawAddPopup()
		{
			var wasCancelled = true;
			var items = new List<PopupItem>
			{
				new ($"WP{currentMap.waypoints.Length:00} at tile {AttachmentEditing.CurrentPendingTile}", null, null, spacerHeight: 0),
				PopupItem.Spacer(6),
				//new ("Add", () => { AddWaypointAtTile(AttachmentEditing.CurrentPendingTile); pendingAction = PendingAction.None; }, colorOverride: Color.cyan),
				new ("Add", () => { AttachmentWaypointEditing.CreateWaypoint(iMapManager, AttachmentEditing.CurrentPendingTile) ; pendingAction = PendingAction.None; }, colorOverride: Color.cyan),
				PopupItem.Spacer(4),
				new ("Cancel", () => { }, Color.yellow)
			};
			var result = PopupMenu.Show(mouseDownPos, "Add Waypoint?", items);
			if (!result && wasCancelled)
				AttachmentEditing.Select(null, iMapManager, camera); //pendingWaypoint = -1;
			return result;
		}

		private bool DrawDeletePopup()
		{
			var wasCancelled = true;
			var items = new List<PopupItem>
			{
				//new ($"WP{pendingWaypoint:00} at tile {AttachmentEditing.CurrentPendingTile}", null, null, spacerHeight: 0),
				new ($"WP{selectedWaypoint.waypointIndex:00} at tile {AttachmentEditing.CurrentPendingTile}", null, null, spacerHeight: 0),
				PopupItem.Spacer(6),
				//new ("Delete", () => {DeleteWaypoint(pendingWaypoint); pendingAction = PendingAction.None;}, colorOverride: Color.red),
				//new ("Delete", () => {DeleteWaypoint(selectedWaypoint); pendingAction = PendingAction.None;}, colorOverride: Color.red),
				new ("Delete", () => {iMapManager.RemoveAttachment(selectedWaypoint); pendingAction = PendingAction.None;}, colorOverride: Color.red),
				PopupItem.Spacer(4),
				new ("Cancel", () => { }, Color.yellow )
			};
			var result = PopupMenu.Show(mouseDownPos, "Delete Waypoint?", items);
			if (!result && wasCancelled)
				AttachmentEditing.Select(null, iMapManager, camera); //pendingWaypoint = -1;
			return result;
		}

		//private void DrawSidePanel()
		//{
		//	var wp = currentMap.waypoints ?? Array.Empty<int>();
		//	var items = new List<ListViewItem>();

		//	for (int i = 0; i < wp.Length; i++)
		//		//items.Add(new ListViewItem(label: $"WP{i:00} [{wp[i]}]", onClick: (x) => pendingWaypoint = x, selected: i == selectedWaypoint?.waypointIndex));
		//		items.Add(new ListViewItem(label: $"WP{i:00} [{wp[i]}]", selected: i == selectedWaypoint?.waypointIndex));

		//	sidePanel.List.SetItems(items);

		//	sidePanel.Buttons.Clear();
		//	sidePanel.Buttons.Add(new ListViewButton("Move Up", () => MoveWaypoint(selectedWaypoint, -1), enabled: selectedWaypoint?.waypointIndex > 0));
		//	sidePanel.Buttons.Add(new ListViewButton("Move Down", () => MoveWaypoint(selectedWaypoint, +1), enabled: selectedWaypoint?.waypointIndex >= 0 && selectedWaypoint?.waypointIndex < wp.Length - 1));
		//	sidePanel.Draw();
		//}

		private void DrawSidePanel()
		{
			var wpArray = currentMap.waypoints ?? Array.Empty<int>();
			var items = new List<ListViewItem>();

			var waypointAttachments = iMapManager.GetWaypointAttachments(); // This gives real Waypoint objects with correct indices

			for (int i = 0; i < wpArray.Length; i++)
			{
				int tile = wpArray[i];
				var waypoint = waypointAttachments.FirstOrDefault(w => w.waypointIndex == i);

				bool isSelected = selectedWaypoint?.waypointIndex == i;

				items.Add(new ListViewItem(
					label: $"WP{i:00} [tile {tile}]",
					onClick: (x) =>
					{
						// Select this waypoint
						if (waypoint != null)
						{
							AttachmentEditing.Select(new[] { waypoint }, iMapManager, camera);
						}
					},
					selected: isSelected
				));
			}

			sidePanel.List.SetItems(items);

			// Buttons: Move Up / Down
			sidePanel.Buttons.Clear();

			bool canMoveUp = selectedWaypoint != null && selectedWaypoint.waypointIndex > 0;
			bool canMoveDown = selectedWaypoint != null &&
							   selectedWaypoint.waypointIndex >= 0 &&
							   selectedWaypoint.waypointIndex < wpArray.Length - 1;

			sidePanel.Buttons.Add(new ListViewButton("Move Up", () => MoveWaypoint(selectedWaypoint, -1), enabled: canMoveUp));
			sidePanel.Buttons.Add(new ListViewButton("Move Down", () => MoveWaypoint(selectedWaypoint, +1), enabled: canMoveDown));

			sidePanel.Draw();
		}

		private Waypoint selectedWaypoint
		{
			get => null != AttachmentEditing.selectedAttachments && AttachmentEditing.selectedAttachments.Length > 0 ? AttachmentEditing.selectedAttachments[0] as Waypoint : null;
		}

		//private int pendingWaypoint
		//{
		//	get
		//	{
		//		var selected = AttachmentEditing.selectedAttachments;
		//		if (null != selected && 0 != selected.Length && selected[0] is Waypoint wa)
		//			return wa.waypointIndex;
		//		return -1;
		//	}
		//	set
		//	{
		//		if (value < 0 || currentMap?.waypoints == null || value >= currentMap.waypoints.Length)
		//		{
		//			AttachmentEditing.Select(null, iMapManager, camera);
		//			RebuildMarkers();
		//			return;
		//		}

		//		var att = new Waypoint(value, currentMap.waypoints[value]);
		//		AttachmentEditing.Select(new[] { att }, iMapManager, camera);
		//		RebuildMarkers();
		//	}
		//}
	}
}