using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public class EditorControllerWaypoint : EditorControllerMovement
	{
		private static readonly AutoHidePanel sidePanel = new(120f, 340f, 1.5f, 0.25f);

		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public override void OnMapLoaded()
		{
			AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			AttachmentEditing.OnEnableShared(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);
		}

		public override void OnDisable()
		{
			base.OnDisable();
			AttachmentEditing.OnDisableShared();
		}

		public override void Update()
		{
			base.Update();

			AttachmentEditing.UpdateSharedInput(
				camera: camera,
				iMapManager: iMapManager,
				markerType: EditorMarkerUtil.MarkerType.Waypoint,
				isMouseOverGUI: IsMouseOverGUI,
				hitTileDelegate: () => HitTile(Input.mousePosition)
			);
		}

		public override void OnGUI()
		{
			DrawSidePanel(); // your waypoint-specific panel

			if (AttachmentEditing.pendingAction == AttachmentEditing.PendingAction.None) return;

			// Waypoint-specific popups
			switch (AttachmentEditing.pendingAction)
			{
				case AttachmentEditing.PendingAction.Add:
					if (DrawAddPopup()) return;
					break;
				case AttachmentEditing.PendingAction.Delete:
					if (DrawDeletePopup()) return;
					break;
			}

			AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None;
		}

		private bool DrawAddPopup()
		{
			var wasCancelled = true;
			var items = new List<PopupItem>
					{
						new ($"WP{currentMap.waypoints.Length:00} at tile {AttachmentEditing.CurrentPendingTile}", null, null, spacerHeight: 0),
						PopupItem.Spacer(6),
						new ("Add", () => { AttachmentWaypointEditing.CreateWaypoint(iMapManager, AttachmentEditing.CurrentPendingTile) ; AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None; }, colorOverride: Color.cyan),
						PopupItem.Spacer(4),
						new ("Cancel", () => { }, Color.yellow)
					};
			var result = PopupMenu.Show(AttachmentEditing.mouseDownPos, "Add Waypoint?", items);
			if (!result && wasCancelled)
				AttachmentEditing.Select(null, iMapManager, camera);
			return result;
		}

		private bool DrawDeletePopup()
		{
			var wasCancelled = true;
			var items = new List<PopupItem>
					{
						new ($"WP{selectedWaypoint.waypointIndex:00} at tile {AttachmentEditing.CurrentPendingTile}", null, null, spacerHeight: 0),
						PopupItem.Spacer(6),
						new ("Delete", () => {iMapManager.RemoveAttachment(selectedWaypoint); AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None;}, colorOverride: Color.red),
						PopupItem.Spacer(4),
						new ("Cancel", () => { }, Color.yellow )
					};
			var result = PopupMenu.Show(AttachmentEditing.mouseDownPos, "Delete Waypoint?", items);
			if (!result && wasCancelled)
				AttachmentEditing.Select(null, iMapManager, camera);
			return result;
		}

		private void DrawSidePanel()
		{
			var wpArray = currentMap.waypoints ?? Array.Empty<int>();
			var items = new List<ListViewItem>();

			var waypointAttachments = iMapManager.waypointAttachments; // This gives real Waypoint objects with correct indices

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
						if (null != waypoint)
							AttachmentEditing.Select(new[] { waypoint }, iMapManager, camera);
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

		private Waypoint selectedWaypoint => null != AttachmentEditing.selectedAttachments && AttachmentEditing.selectedAttachments.Length > 0 ? AttachmentEditing.selectedAttachments[0] as Waypoint : null;

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

		private void RebuildMarkers() => AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);
	}
}


//using System;
//using System.Linq;
//using UnityEngine;
//using System.Collections.Generic;
//using static MassiveHadronLtd.GuiUtils;

//namespace ClassicTilestorm
//{
//	public class EditorControllerWaypoint : EditorControllerMovement
//	{
//		private Vector3 mouseDownPos;
//		private bool mouseMovedBeyondThreshold;
//		private const float CLICK_THRESHOLD = 8f;
//		private bool rmbDownInPreview = false;
//		private bool supressInput = true;

//		private static readonly AutoHidePanel sidePanel = new(120f, 340f, 1.5f, 0.25f);

//		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

//		public override void OnMapLoaded() 
//		{
//			rmbDownInPreview = false;
//			RebuildMarkers(); 
//		}

//		private void RebuildMarkers() => AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);

//		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

//		public override void OnEnable()
//		{
//			base.OnEnable();
//			supressInput = true;
//			rmbDownInPreview = false;
//			AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None;
//			AttachmentEditing.HideAllGizmos();
//			RebuildMarkers();
//		}

//		public override void OnDisable()
//		{
//			base.OnDisable();
//			AttachmentEditing.selectedAttachments = null;
//			AttachmentEditing.HideAllGizmos();
//			AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None;
//		}

//		public override void Update()
//		{
//			base.Update();

//			// === VIEW PREVIEW INTERACTION ===
//			ViewPreviewUtil.SetInFocus(ViewPreviewUtil.IsMouseOverPreview());

//			if (Input.GetMouseButtonDown(1))
//				rmbDownInPreview = ViewPreviewUtil.IsInFocus;

//			if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
//				rmbDownInPreview = false;

//			ViewPreviewUtil.SetInUse(rmbDownInPreview);

//			var previewControlsActive = rmbDownInPreview || (!Input.GetMouseButton(1) && ViewPreviewUtil.IsInFocus);

//			if (previewControlsActive)
//			{
//				EditorCameraMovement.UpdateCamera(ViewPreviewUtil.PreviewCamera.transform);
//				AttachmentViewEditing.HandlePreviewCameraSync(iMapManager, camera);
//				supressInput = true;
//				return;
//			}

//			ViewPreviewUtil.Update();

//			if (IsMouseOverGUI() || IsGuiControlActive()) return;

//			if (EditorTransformUtil.HandleTransformGizmoInput(camera))
//			{
//				AttachmentEditing.HandleGizmoInput(iMapManager, camera);
//				supressInput = true;
//			}

//			if (supressInput)
//			{
//				supressInput = false;
//				return;
//			}

//			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
//			{
//				mouseDownPos = Input.mousePosition;
//				mouseMovedBeyondThreshold = false;
//			}

//			if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
//			{
//				if (Vector3.Distance(Input.mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
//					mouseMovedBeyondThreshold = true;
//			}

//			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
//				HandleMouseDown();

//			if (Input.GetMouseButton(0))
//				HandleDrag();

//			if (Input.GetMouseButtonUp(0) && !mouseMovedBeyondThreshold)
//				HandleLeftMouseUp();

//			if (Input.GetMouseButtonUp(1) && !mouseMovedBeyondThreshold)
//				HandleRightMouseUp();
//		}

//		public override void OnGUI()
//		{
//			DrawSidePanel();

//			if (AttachmentEditing.pendingAction == AttachmentEditing.PendingAction.None) return;
//			supressInput = true;

//			switch (AttachmentEditing.pendingAction)
//			{
//				case AttachmentEditing.PendingAction.Add: if (DrawAddPopup()) return; break;
//				case AttachmentEditing.PendingAction.Delete: if (DrawDeletePopup()) return; break;
//			}

//			AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None;
//		}

//		private void HandleMouseDown()
//		{
//			int hitTile = HitTile(Input.mousePosition);
//			AttachmentEditing.CurrentPendingTile = hitTile;

//			if (-1 != hitTile)
//			{
//				var alreadySelected = AttachmentEditing.selectedAttachments?.Length > 0 && AttachmentEditing.selectedAttachments[0].tile == hitTile;
//				if (!alreadySelected)
//				{
//					AttachmentEditing.selectedAttachments = AttachmentEditing.GetAttachmentsOnTile(iMapManager, hitTile);
//					AttachmentEditing.Select(AttachmentEditing.selectedAttachments, iMapManager, camera);
//					if (AttachmentEditing.selectedAttachments?.Length > 0) Debug.Log($"selected {AttachmentEditing.selectedAttachments?.Length}");
//				}
//				return;
//			}
//			AttachmentEditing.Select(null, iMapManager, camera);
//		}

//		private void HandleDrag() => AttachmentEditing.HandleDrag(iMapManager, camera, EditorMarkerUtil.MarkerType.Waypoint); 
		
//		private void HandleLeftMouseUp()
//		{
//			var attachmentsOnDownTile = AttachmentEditing.GetAttachmentsOnTile(iMapManager, AttachmentEditing.CurrentPendingTile);

//			if (attachmentsOnDownTile == null || attachmentsOnDownTile.Length == 0)
//			{
//				if (AttachmentEditing.CurrentPendingTile != -1)
//					AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.Add;
//			}
//			else if (attachmentsOnDownTile.Length > 1)
//			{
//				AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.Select;
//			}
//			else
//			{
//				AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None;
//				AttachmentEditing.Select(attachmentsOnDownTile, iMapManager, camera);
//			}
//		}

//		private void HandleRightMouseUp()
//		{
//			int hitTile = HitTile(mouseDownPos);
//			if (hitTile >= 0 && AttachmentEditing.GetAttachmentsOnTile(iMapManager, hitTile)?.Length > 0)
//			{
//				AttachmentEditing.CurrentPendingTile = hitTile;
//				AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.Delete;
//				return;
//			}
//			AttachmentEditing.Select(null, iMapManager, camera);
//		}

//		private void MoveWaypoint(Waypoint wp, int direction)
//		{
//			if (wp == null) return;

//			int oldIndex = wp.waypointIndex;
//			int newIndex = oldIndex + direction;

//			if (newIndex < 0 || newIndex >= currentMap.waypoints.Length) return;

//			// Swap in the underlying waypoints array
//			var list = currentMap.waypoints.ToList();
//			(list[oldIndex], list[newIndex]) = (list[newIndex], list[oldIndex]);
//			currentMap.waypoints = list.ToArray();

//			// Update the waypoint objects' indices (important for future selections)
//			// We need to refresh the virtual attachments so indices are correct
//			// But since we're in editor, easiest is to re-select the moved one
//			var movedWaypoint = new Waypoint(newIndex, list[newIndex]);
//			AttachmentEditing.Select(new[] { movedWaypoint }, iMapManager, camera);

//			// Rebuild markers to reflect new positions
//			RebuildMarkers();
//		}

//		private bool DrawAddPopup()
//		{
//			var wasCancelled = true;
//			var items = new List<PopupItem>
//			{
//				new ($"WP{currentMap.waypoints.Length:00} at tile {AttachmentEditing.CurrentPendingTile}", null, null, spacerHeight: 0),
//				PopupItem.Spacer(6),
//				new ("Add", () => { AttachmentWaypointEditing.CreateWaypoint(iMapManager, AttachmentEditing.CurrentPendingTile) ; AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None; }, colorOverride: Color.cyan),
//				PopupItem.Spacer(4),
//				new ("Cancel", () => { }, Color.yellow)
//			};
//			var result = PopupMenu.Show(mouseDownPos, "Add Waypoint?", items);
//			if (!result && wasCancelled)
//				AttachmentEditing.Select(null, iMapManager, camera);
//			return result;
//		}

//		private bool DrawDeletePopup()
//		{
//			var wasCancelled = true;
//			var items = new List<PopupItem>
//			{
//				new ($"WP{selectedWaypoint.waypointIndex:00} at tile {AttachmentEditing.CurrentPendingTile}", null, null, spacerHeight: 0),
//				PopupItem.Spacer(6),
//				new ("Delete", () => {iMapManager.RemoveAttachment(selectedWaypoint); AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None;}, colorOverride: Color.red),
//				PopupItem.Spacer(4),
//				new ("Cancel", () => { }, Color.yellow )
//			};
//			var result = PopupMenu.Show(mouseDownPos, "Delete Waypoint?", items);
//			if (!result && wasCancelled)
//				AttachmentEditing.Select(null, iMapManager, camera);
//			return result;
//		}

//		private void DrawSidePanel()
//		{
//			var wpArray = currentMap.waypoints ?? Array.Empty<int>();
//			var items = new List<ListViewItem>();

//			var waypointAttachments = iMapManager.waypointAttachments; // This gives real Waypoint objects with correct indices

//			for (int i = 0; i < wpArray.Length; i++)
//			{
//				int tile = wpArray[i];
//				var waypoint = waypointAttachments.FirstOrDefault(w => w.waypointIndex == i);

//				bool isSelected = selectedWaypoint?.waypointIndex == i;

//				items.Add(new ListViewItem(
//					label: $"WP{i:00} [tile {tile}]",
//					onClick: (x) =>
//					{
//						// Select this waypoint
//						if (null != waypoint)
//							AttachmentEditing.Select(new[] { waypoint }, iMapManager, camera);
//					},
//					selected: isSelected
//				));
//			}

//			sidePanel.List.SetItems(items);

//			// Buttons: Move Up / Down
//			sidePanel.Buttons.Clear();

//			bool canMoveUp = selectedWaypoint != null && selectedWaypoint.waypointIndex > 0;
//			bool canMoveDown = selectedWaypoint != null &&
//							   selectedWaypoint.waypointIndex >= 0 &&
//							   selectedWaypoint.waypointIndex < wpArray.Length - 1;

//			sidePanel.Buttons.Add(new ListViewButton("Move Up", () => MoveWaypoint(selectedWaypoint, -1), enabled: canMoveUp));
//			sidePanel.Buttons.Add(new ListViewButton("Move Down", () => MoveWaypoint(selectedWaypoint, +1), enabled: canMoveDown));

//			sidePanel.Draw();
//		}

//		private Waypoint selectedWaypoint => null != AttachmentEditing.selectedAttachments && AttachmentEditing.selectedAttachments.Length > 0 ? AttachmentEditing.selectedAttachments[0] as Waypoint : null;
//	}
//}