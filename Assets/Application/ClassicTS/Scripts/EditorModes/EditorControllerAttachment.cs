using UnityEngine;
using System.Linq;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		private Vector3 mouseDownPos;
		private bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 8f;
		private bool rmbDragStartedInPreview = false;
		private bool supressInput = true;

		private int pendingTile = -1;
		private int lastDragTile = -1;
		private enum PendingAction { None, Add, Delete, Select }
		private PendingAction pendingAction = PendingAction.None;

		private static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		public override void OnEnable()
		{
			base.OnEnable();
			supressInput = true;
			rmbDragStartedInPreview = false;
			pendingAction = PendingAction.None;
			AttachmentEditing.HideAllGizmos();
			AttachmentEditing.RebuildMarkers(iMapManager);
		}

		public override void OnDisable()
		{
			base.OnDisable();
			pendingAction = PendingAction.None;
			AttachmentEditing.HideAllGizmos();
		}

		public override void Update()
		{
			base.Update();

			// === VIEW PREVIEW INTERACTION (clean, faithful to original) ===
			ViewPreviewUtil.SetInFocus(ViewPreviewUtil.IsMouseOverPreview());

			// Detect if RMB was pressed THIS FRAME while mouse was over preview
			if (Input.GetMouseButtonDown(1))
				rmbDragStartedInPreview = ViewPreviewUtil.IsInFocus;

			// Clear the drag flag when no mouse buttons are pressed
			if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
				rmbDragStartedInPreview = false;

			// "In use" = actively orbiting with RMB (strong border + "Preview Active")
			ViewPreviewUtil.SetInUse(rmbDragStartedInPreview);

			// Full preview camera control active if:
			// - We're in RMB orbit mode, OR
			// - Mouse is hovering over preview with no RMB held (soft focus)
			bool previewControlsActive = rmbDragStartedInPreview || (!Input.GetMouseButton(1) && ViewPreviewUtil.IsInFocus);

			if (previewControlsActive)
			{
				EditorCameraMovement.UpdateCamera(ViewPreviewUtil.PreviewCamera.transform);
				AttachmentViewEditing.HandlePreviewCameraSync(iMapManager, camera);

				supressInput = true;
				return; // Block normal map input while using preview
			}

			// Always update the preview render when visible
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

			if (IsGuiControlActive()) return;

			int tileUnderMouse = HitTile(Input.mousePosition);

			// LMB Down: select attachments
			if (!supressInput && Input.GetMouseButtonDown(0))
			{
				pendingTile = tileUnderMouse;
				HandleLeftMouseDown(pendingTile);
			}

			// LMB Drag: move attachments
			if (!supressInput && Input.GetMouseButton(0) && tileUnderMouse >= 0 && AttachmentEditing.selectedAttachments != null)
			{
				HandleDrag(tileUnderMouse);
				AttachmentEditing.RebuildMarkers(iMapManager);
			}

			bool wasClick = !mouseMovedBeyondThreshold;

			// LMB Up: popups (only on clean click)
			if (!supressInput && Input.GetMouseButtonUp(0) && wasClick)
			{
				lastDragTile = -1;
				HandleLeftMouseUpOnCleanClick();
			}

			// RMB Up: delete popup
			if (!supressInput && Input.GetMouseButtonUp(1) && wasClick)
				HandleRightMouseUp(wasClick);
		}

		public override void OnGUI()
		{
			DrawSidePanel();
			ViewPreviewUtil.OnGUI();

			if (PendingAction.None == pendingAction) return;

			var active = true;
			var context = new AttachmentEditing.AttachmentEditContext(iMapManager, camera, pendingTile);
			switch (pendingAction)
			{
				case PendingAction.Add: active = AttachmentEditing.DrawAddPopup(context, mouseDownPos); break;
				case PendingAction.Delete: active = AttachmentEditing.DrawDeletePopup(context, mouseDownPos); break;
				case PendingAction.Select: active = AttachmentEditing.DrawSelectPopup(context, mouseDownPos); break;
			}

			if (active) return;
			pendingAction = PendingAction.None;
			supressInput = true;
		}

		private void HandleDrag(int tileUnderMouse)
		{
			if (lastDragTile == tileUnderMouse)
				return;

			lastDragTile = tileUnderMouse;

			if (AttachmentEditing.selectedAttachments == null || AttachmentEditing.selectedAttachments.Length == 0)
				return;

			foreach (var att in AttachmentEditing.selectedAttachments)
			{
				att.tile = tileUnderMouse;
				iMapManager.RefreshAttachmentInstance(att);
			}

			var context = new AttachmentEditing.AttachmentEditContext(iMapManager, camera, pendingTile);
			AttachmentEditing.RefreshDragVisuals(context);
		}

		public override void OnMapLoaded()
		{
			supressInput = true;
			rmbDragStartedInPreview = false;
			AttachmentEditing.HideAllGizmos();
			AttachmentEditing.RebuildMarkers(iMapManager);
		}

		private void HandleLeftMouseDown(int tile)
		{
			if (-1 == tile)
				AttachmentEditing.Select(null, iMapManager, camera);
			else
			{
				var alreadySelected = AttachmentEditing.selectedAttachments?.Length > 0 && AttachmentEditing.selectedAttachments[0].tile == tile;
				if (!alreadySelected)
				{
					AttachmentEditing.selectedAttachments = GetAttachmentsOnTile(tile);
					AttachmentEditing.Select(AttachmentEditing.selectedAttachments, iMapManager, camera);
				}
			}
		}

		// New: Only called on clean click (no drag)
		private void HandleLeftMouseUpOnCleanClick()
		{
			var attachmentsOnDownTile = GetAttachmentsOnTile(pendingTile);

			if (attachmentsOnDownTile == null || attachmentsOnDownTile.Length == 0)
			{
				if (pendingTile != -1)
					pendingAction = PendingAction.Add;
			}
			else if (attachmentsOnDownTile.Length > 1)
			{
				// Only show multi-select if there were multiple on the original tile
				pendingAction = PendingAction.Select;
			}
			else
			{
				// Single attachment on original tile → ensure it's selected (may have been dragged back)
				pendingAction = PendingAction.None;
				AttachmentEditing.Select(attachmentsOnDownTile, iMapManager, camera);
			}
		}

		private void HandleRightMouseUp(bool wasClick)
		{
			if (!wasClick) return;
			int hitTile = HitTile(mouseDownPos);
			if (hitTile >= 0 && GetAttachmentsOnTile(hitTile)?.Length > 0)
			{
				pendingTile = hitTile;
				pendingAction = PendingAction.Delete;
				return;
			}
			AttachmentEditing.Select(null, iMapManager, camera);
		}

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			var map = currentMap;
			if (map == null || map.attachments == null || !map.IsValidTile(tileIndex)) return null;
			var result = map.attachments.Where(x => x.tile == tileIndex).ToArray();
			return result.Length > 0 ? result : null;
		}

		private void DrawSidePanel()
		{
			var atts = currentMap.attachments ?? System.Array.Empty<MapAttachment>();
			var items = new System.Collections.Generic.List<ListViewItem>(); 
			foreach (var att in atts)
				items.Add(new ListViewItem(GetAttachmentLabel(att),() => AttachmentEditing.Select(new[] { att }, iMapManager, camera), selected: null != AttachmentEditing.selectedAttachments && AttachmentEditing.selectedAttachments.Contains(att)));
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