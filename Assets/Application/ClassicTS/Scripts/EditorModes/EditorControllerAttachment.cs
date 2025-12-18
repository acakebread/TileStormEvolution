using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		private Vector3 mouseDownPos;
		private bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 8f;
		private bool rmbDragStartedInPreview = false;
		private int lastDragTile = -1;  // Tracks the last tile we dragged to
		private bool supressInput = true;

		private int pendingTile = -1;
		public MapAttachment[] selectedAttachments = System.Array.Empty<MapAttachment>();
		public enum PendingAction { None, Wait, Add, Delete, Select, Drag }
		public PendingAction pendingAction = PendingAction.None;
		private Vector2 pendingPopupScreenPos = Vector2.zero;
		public ViewPreview viewPreview;

		public override bool IsMouseOverGUI()
		{
			if (base.IsMouseOverGUI()) return true;
			if (AttachmentEditing.IsMouseOverSidePanel()) return true;
			return false;
		}

		protected override bool IsMouseOverPreview()
		{
			if (viewPreview != null && viewPreview.gameObject.activeSelf && viewPreview.previewRect is Rect r && r.width > 0)
			{
				Rect hitRect = new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16);
				Vector2 mp = Input.mousePosition;
				mp.y = Screen.height - mp.y;
				return hitRect.Contains(mp);
			}
			return false;
		}

		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		public override void OnEnable()
		{
			base.OnEnable();
			EditorMarkerUtil.ClearMapMarkers();
			EditorPrimitiveUtil.HideCone();
			EditorFrustumUtil.Hide();
			RebuildMarkers();

			viewPreview = ViewPreview.Create();
			viewPreview.Hide();

			supressInput = true;
		}

		public override void OnDisable()
		{
			base.OnDisable();
			EditorMarkerUtil.ClearMapMarkers();
			pendingAction = PendingAction.None;
			EditorPrimitiveUtil.HideCone();
			EditorFrustumUtil.Hide();
			EditorTransformUtil.HideTransformGizmo();

			viewPreview?.Hide();
			if (viewPreview != null) Object.Destroy(viewPreview.gameObject);

			viewPreview.inInUse = false;
			rmbDragStartedInPreview = false;
		}

		public override void Update()
		{
			base.Update();

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				rmbDragStartedInPreview = IsMouseOverPreview();

			var touch = Input.GetMouseButton(0) || Input.GetMouseButton(1);
			if (!touch) rmbDragStartedInPreview = false;

			viewPreview.inInUse = rmbDragStartedInPreview || (!touch && IsMouseOverPreview());
			if (viewPreview.inInUse) supressInput = true;

			if (rmbDragStartedInPreview || (!Input.GetMouseButton(1) && IsMouseOverPreview()))
			{
				EditorCameraMovement.UpdateCamera(viewPreview.previewCam.transform);
				AttachmentViewEditing.HandlePreviewCameraSync(this, viewPreview);
				return;
			}

			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			if (EditorTransformUtil.HandleTransformGizmoInput(editorCamera))
			{
				AttachmentEditing.HandleGizmoInput(this);
				supressInput = true;
			}

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				mouseDownPos = Input.mousePosition;
				mouseMovedBeyondThreshold = false;
				//rmbDragStartedInPreview = isMouseOverPreview;
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

		public override void OnGUI() => AttachmentEditing.DrawGUI(this);

		private void HandleDrag(int tileUnderMouse)
		{
			bool tileChanged = tileUnderMouse != lastDragTile;

			foreach (var att in selectedAttachments)
				att.tile = tileUnderMouse;

			if (tileChanged)
			{
				lastDragTile = tileUnderMouse;
				AttachmentEditing.RefreshDragVisuals(this);
			}
		}

		public void OnMapChanged()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Attachment) return;
			RebuildMarkers();
			EditorPrimitiveUtil.HideCone();
			EditorFrustumUtil.Hide();
			EditorTransformUtil.HideTransformGizmo();
			viewPreview.Hide();
			viewPreview.inInUse = false;
			rmbDragStartedInPreview = false;
			supressInput = true;
		}

		public void RebuildMarkers()
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			var tiles = map.attachments?
				.Where(a => a.tile >= 0)
				.Select(a => a.tile)
				.Distinct()
				.ToArray() ?? System.Array.Empty<int>();

			// Determine selected tile from current selection
			int selectedTile = (selectedAttachments != null && selectedAttachments.Length > 0) ? selectedAttachments[0].tile : -1;

			int selection = System.Array.IndexOf(tiles, selectedTile);

			AttachmentEditing.UpdateMapMarkers(editorController.iMapManager, tiles, selection, EditorMarkerUtil.MarkerType.Attachment);
		}

		public void SelectAttachments(MapAttachment[] attachments)
		{
			selectedAttachments = attachments;

			RebuildMarkers();
			EditorPrimitiveUtil.HideCone();
			EditorFrustumUtil.Hide();
			EditorTransformUtil.HideTransformGizmo();
			viewPreview.Hide();

			// Only show editing helpers if exactly ONE attachment selected
			if (attachments != null && attachments.Length == 1)
			{
				AttachmentEditing.HandleSelectionChanged(this);
				AttachmentEditing.HandleGizmoInput(this); // if needed on select
			}
		}

		private int GetTileUnderMouse()
		{
			if (!editorCamera) return -1;
			return editorController.iMapManager.WorldToMapIndex(MapManager.ScreenToWorld(editorCamera, Input.mousePosition));
		}

		private void HandleLeftMouseDown(int tile)
		{
			if (tile == -1)
				SelectAttachments(null);
			else
			{
				bool alreadySelected = selectedAttachments?.Length > 0 && selectedAttachments[0].tile == tile;
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
			var wp = editorController.iMapManager.TileWorldPosition(tile) + Vector3.up * 0.6f;
			var sp = editorCamera.WorldToScreenPoint(wp);
			sp.y = Screen.height - sp.y;
			pendingPopupScreenPos = sp;
		}

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			var map = editorController.currentMap;
			if (map == null || map.attachments == null || !map.IsValidTile(tileIndex)) return null;
			var result = map.attachments.Where(x => x.tile == tileIndex).ToArray();
			return result.Length > 0 ? result : null;
		}

		public PendingAction CurrentPendingAction => pendingAction;
		public void ClearPendingAction(bool clearSelection = true)
		{
			pendingAction = PendingAction.Wait;
			if (clearSelection)
			{
				selectedAttachments = null;
				pendingTile = -1;
			}
		}
		public Vector2 PendingPopupScreenPos => pendingPopupScreenPos;
		public int PendingTile => pendingTile;
	}
}