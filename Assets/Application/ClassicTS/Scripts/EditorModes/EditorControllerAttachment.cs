using UnityEngine;
using System.Linq;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		private static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));

		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public override void OnMapLoaded()
		{
			AttachmentEditing.ResetInputState();
			AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Attachment);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			AttachmentEditing.OnEnableShared(iMapManager, EditorMarkerUtil.MarkerType.Attachment);
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
				markerType: EditorMarkerUtil.MarkerType.Attachment,
				isMouseOverGUI: IsMouseOverGUI,
				hitTileDelegate: () => HitTile(Input.mousePosition)
			);
		}

		public override void OnGUI()
		{
			DrawSidePanel(); // your existing panel code
			ViewPreviewUtil.OnGUI();

			if (AttachmentEditing.pendingAction == AttachmentEditing.PendingAction.None) return;

			// Use shared popups
			switch (AttachmentEditing.pendingAction)
			{
				case AttachmentEditing.PendingAction.Add:
					if (AttachmentEditing.DrawAddPopup(AttachmentEditing.mouseDownPos, iMapManager, camera, AttachmentEditing.CurrentPendingTile)) return;
					break;
				case AttachmentEditing.PendingAction.Delete:
					if (AttachmentEditing.DrawDeletePopup(AttachmentEditing.mouseDownPos, iMapManager, camera, AttachmentEditing.CurrentPendingTile)) return;
					break;
				case AttachmentEditing.PendingAction.Select:
					if (AttachmentEditing.DrawSelectPopup(AttachmentEditing.mouseDownPos, iMapManager, camera, AttachmentEditing.CurrentPendingTile)) return;
					break;
			}

			AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None;
		}

		private void DrawSidePanel()
		{
			var atts = currentMap.attachments ?? System.Array.Empty<MapAttachment>();
			var items = new System.Collections.Generic.List<ListViewItem>();
			foreach (var att in atts)
				items.Add(new ListViewItem(GetAttachmentLabel(att), (x) => AttachmentEditing.Select(new[] { att }, iMapManager, camera), selected: null != AttachmentEditing.selectedAttachments && AttachmentEditing.selectedAttachments.Contains(att)));
			sidePanel.List.SetItems(items);
			sidePanel.SetFootnote("Hold RMB on preview to orbit • Scroll to zoom • LMB: place/move • RMB on tile: delete");
			sidePanel.Draw();

			static string GetAttachmentLabel(MapAttachment att) => att switch
			{
				Emitter e => $"Emitter [{att.tile}]" + (e.LookAt.sqrMagnitude > 0.01f && e.LookAt != Vector3.up ? $" → {e.LookAt.magnitude:F1}" : ""),
				View => $"View [{att.tile}]",
				Pickup p => $"Pickup [{att.tile}] ({p.amount})",
				_ => $"{att.TypeName} [{att.tile}]"
			};
		}
	}
}

//using UnityEngine;
//using System.Linq;
//using static MassiveHadronLtd.GuiUtils;

//namespace ClassicTilestorm
//{
//	public class EditorControllerAttachment : EditorControllerMovement
//	{
//		private Vector3 mouseDownPos;
//		private bool mouseMovedBeyondThreshold;
//		private const float CLICK_THRESHOLD = 8f;
//		private bool rmbDownInPreview = false;
//		private bool supressInput = true;

//		private static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));

//		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

//		public override void OnMapLoaded()
//		{
//			supressInput = true;
//			rmbDownInPreview = false;
//			AttachmentEditing.HideAllGizmos();
//			AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Attachment);
//		}

//		public EditorControllerAttachment(EditorController controller) : base(controller) { }

//		public override void OnEnable()
//		{
//			base.OnEnable();
//			supressInput = true;
//			rmbDownInPreview = false;
//			AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None;
//			AttachmentEditing.HideAllGizmos();
//			AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Attachment);
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
//			ViewPreviewUtil.OnGUI();

//			if (AttachmentEditing.PendingAction.None == AttachmentEditing.pendingAction) return;
//			supressInput = true;

//			switch (AttachmentEditing.pendingAction)
//			{
//				case AttachmentEditing.PendingAction.Add: if (AttachmentEditing.DrawAddPopup(mouseDownPos, iMapManager, camera, AttachmentEditing.CurrentPendingTile)) return; break;
//				case AttachmentEditing.PendingAction.Delete: if (AttachmentEditing.DrawDeletePopup(mouseDownPos, iMapManager, camera, AttachmentEditing.CurrentPendingTile)) return; break;
//				case AttachmentEditing.PendingAction.Select: if (AttachmentEditing.DrawSelectPopup(mouseDownPos, iMapManager, camera, AttachmentEditing.CurrentPendingTile)) return; break;
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
//				}
//				return;
//			}
//			AttachmentEditing.Select(null, iMapManager, camera);
//		}

//		private void HandleDrag() => AttachmentEditing.HandleDrag(iMapManager, camera, EditorMarkerUtil.MarkerType.Attachment);

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

//		private void DrawSidePanel()
//		{
//			var atts = currentMap.attachments ?? System.Array.Empty<MapAttachment>();
//			var items = new System.Collections.Generic.List<ListViewItem>();
//			foreach (var att in atts)
//				items.Add(new ListViewItem(GetAttachmentLabel(att), (x) => AttachmentEditing.Select(new[] { att }, iMapManager, camera), selected: null != AttachmentEditing.selectedAttachments && AttachmentEditing.selectedAttachments.Contains(att)));
//			sidePanel.List.SetItems(items);
//			sidePanel.SetFootnote("Hold RMB on preview to orbit • Scroll to zoom • LMB: place/move • RMB on tile: delete");
//			sidePanel.Draw();

//			static string GetAttachmentLabel(MapAttachment att) => att switch
//			{
//				Emitter e => $"Emitter [{att.tile}]" + (e.LookAt.sqrMagnitude > 0.01f && e.LookAt != Vector3.up ? $" → {e.LookAt.magnitude:F1}" : ""),
//				View => $"View [{att.tile}]",
//				Pickup p => $"Pickup [{att.tile}] ({p.amount})",
//				_ => $"{att.TypeName} [{att.tile}]"
//			};
//		}
//	}
//}