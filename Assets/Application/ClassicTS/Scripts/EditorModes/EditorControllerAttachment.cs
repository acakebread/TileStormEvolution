using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		public MapAttachment[] selectedAttachments = null;

		private Vector3 mouseDownPos;
		private bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 8f;
		private bool rmbDragStartedInPreview = false;
		private bool supressInput = true;

		private int pendingTile = -1;
		private int lastDragTile = -1;
		private enum PendingAction { None, Wait, Add, Delete, Select }
		private PendingAction pendingAction = PendingAction.None;
		private Vector2 pendingPopupScreenPos;

		public override bool IsMouseOverGUI()
		{
			if (base.IsMouseOverGUI()) return true;
			if (AttachmentEditing.IsMouseOverSidePanel()) return true;
			return false;
		}

		protected override bool IsMouseOverPreview() => ViewPreviewUtil.IsMouseOverPreview();

		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		public override void OnEnable()
		{
			base.OnEnable();
			supressInput = true;
			rmbDragStartedInPreview = false;
			pendingAction = PendingAction.None;
			HideAllGizmos();
			RebuildMarkers();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			pendingAction = PendingAction.None;
			HideAllGizmos();
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
				AttachmentViewEditing.HandlePreviewCameraSync(this);

				supressInput = true;
				return; // Block normal map input while using preview
			}

			// Always update the preview render when visible
			ViewPreviewUtil.Update();

			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			if (EditorTransformUtil.HandleTransformGizmoInput(camera))
			{
				AttachmentEditing.HandleGizmoInput(this);
				supressInput = true;
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

			bool wasClick = !mouseMovedBeyondThreshold;

			if (IsGuiControlActive()) return;

			int tileUnderMouse = GetTileUnderMouse();

			// LMB Down: select attachments
			if (!supressInput && Input.GetMouseButtonDown(0))
			{
				pendingTile = HitTile(Input.mousePosition);
				HandleLeftMouseDown(pendingTile);
			}

			// LMB Drag: move attachments
			if (!supressInput && Input.GetMouseButton(0) && tileUnderMouse >= 0 && selectedAttachments != null)
			{
				HandleDrag(tileUnderMouse);
				RebuildMarkers();
			}

			supressInput |= pendingAction == PendingAction.Wait;

			// LMB Up: popups (only on clean click)
			if (!supressInput && Input.GetMouseButtonUp(0) && wasClick)
			{
				lastDragTile = -1;
				HandleLeftMouseUpOnCleanClick();
			}

			// RMB Up: delete popup
			if (!supressInput && Input.GetMouseButtonUp(1) && wasClick)
				HandleRightMouseUp(wasClick);

			if (pendingAction == PendingAction.Wait) pendingAction = PendingAction.None;
			supressInput = false;
		}

		public override void OnGUI()
		{
			switch (pendingAction)
			{
				case PendingAction.Add: AttachmentEditing.DrawAddPopup(this, pendingPopupScreenPos); break;
				case PendingAction.Delete: AttachmentEditing.DrawDeletePopup(this, pendingPopupScreenPos); break;
				case PendingAction.Select: AttachmentEditing.DrawSelectPopup(this, pendingPopupScreenPos); break;
			}

			AttachmentEditing.DrawSidePanel(this);
			ViewPreviewUtil.OnGUI();
		}

		private void HandleDrag(int tileUnderMouse)
		{
			if (lastDragTile == tileUnderMouse)
				return;

			lastDragTile = tileUnderMouse;

			if (null == selectedAttachments || 0 == selectedAttachments.Length)
				return;

			// refresh runtime GameObjects (particles, etc.)
			foreach (var att in selectedAttachments)
			{
				att.tile = tileUnderMouse;
				iMapManager.RefreshAttachmentInstance(att);
			}
			AttachmentEditing.RefreshDragVisuals(this);
		}

		public override void OnMapLoaded()
		{
			if (enabled)
			{
				supressInput = true;
				rmbDragStartedInPreview = false;
				HideAllGizmos();
				RebuildMarkers();
			}
			else
				Debug.LogError("EditorControllerAttachment::OnMapLoaded");
		}

		public void RebuildMarkers()
		{
			if (null == currentMap) return;
			var tiles = currentMap.attachments?.Where(a => a.tile >= 0).Select(a => a.tile).Distinct().ToArray() ?? System.Array.Empty<int>();

			// Determine selected tile from current selection
			var selectedTile = (selectedAttachments != null && selectedAttachments.Length > 0) ? selectedAttachments[0].tile : -1;
			var selection = System.Array.IndexOf(tiles, selectedTile);
			AttachmentEditing.UpdateMapMarkers(iMapManager, tiles, selection, EditorMarkerUtil.MarkerType.Attachment);
		}

		public void SelectAttachments(MapAttachment[] attachments)
		{
			selectedAttachments = attachments;
			HideAllGizmos();
			RebuildMarkers();

			if (null == attachments || 1 != attachments.Length) return;// Only show editing helpers if exactly ONE attachment selected
			AttachmentEditing.HandleSelectionChanged(this);
			AttachmentEditing.HandleGizmoInput(this); // if needed on select
		}

		private int GetTileUnderMouse()
		{
			if (!camera) return -1;
			return iMapManager.WorldToMapIndex(MapManager.ScreenToWorld(camera, Input.mousePosition));
		}

		private void HandleLeftMouseDown(int tile)
		{
			if (-1 == tile)
				SelectAttachments(null);
			else
			{
				var alreadySelected = selectedAttachments?.Length > 0 && selectedAttachments[0].tile == tile;
				if (!alreadySelected)
				{
					selectedAttachments = GetAttachmentsOnTile(tile);
					SelectAttachments(selectedAttachments);
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
				{
					pendingAction = PendingAction.Add;
					SetPopupPosition(pendingTile);
				}
			}
			else if (attachmentsOnDownTile.Length > 1)
			{
				// Only show multi-select if there were multiple on the original tile
				pendingAction = PendingAction.Select;
				SetPopupPosition(pendingTile);
			}
			else
			{
				// Single attachment on original tile → ensure it's selected (may have been dragged back)
				pendingAction = PendingAction.None;
				SelectAttachments(attachmentsOnDownTile);
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
				SetPopupPosition(hitTile);
				return;
			}
			SelectAttachments(null);
		}

		private void SetPopupPosition(int tile)
		{
			var wp = iMapManager.TileWorldPosition(tile) + Vector3.up * 0.6f;
			var sp = camera.WorldToScreenPoint(wp);
			sp.y = Screen.height - sp.y;
			pendingPopupScreenPos = sp;
		}

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			var map = currentMap;
			if (map == null || map.attachments == null || !map.IsValidTile(tileIndex)) return null;
			var result = map.attachments.Where(x => x.tile == tileIndex).ToArray();
			return result.Length > 0 ? result : null;
		}

		public void ClearPendingAction(bool clearSelection = true)
		{
			pendingAction = PendingAction.Wait;
			if (clearSelection)
			{
				selectedAttachments = null;
				pendingTile = -1;
			}
		}
		public int PendingTile => pendingTile;

		private void HideAllGizmos()
		{
			EditorPrimitiveUtil.HideCone();
			EditorFrustumUtil.Hide();
			EditorTransformUtil.HideTransformGizmo();
			ViewPreviewUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();
		}
	}
}