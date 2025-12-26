using UnityEngine;
using System.Linq;
using static MassiveHadronLtd.GuiUtils;

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

		private static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		public static void DrawSidePanel(EditorControllerAttachment editor)
		{
			sidePanel.Update();
			sidePanel.List.Clear();

			var map = editor.currentMap;
			if (map != null && map.attachments != null)
			{
				foreach (var att in map.attachments)
				{
					var label = GetAttachmentLabel(att);

					sidePanel.List.AddItem(new ListViewItem(
						label,
						() => editor.SelectAttachments(new[] { att }),
						selected: null != editor.selectedAttachments && editor.selectedAttachments.Contains(att)
					));
				}
			}

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

			int tileUnderMouse = HitTile(Input.mousePosition);

			// LMB Down: select attachments
			if (!supressInput && Input.GetMouseButtonDown(0))
			{
				pendingTile = tileUnderMouse;
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
			DrawSidePanel(this);
			ViewPreviewUtil.OnGUI();

			switch (pendingAction)
			{
				case PendingAction.Add: AttachmentEditing.DrawAddPopup(this, pendingPopupScreenPos); break;
				case PendingAction.Delete: AttachmentEditing.DrawDeletePopup(this, pendingPopupScreenPos); break;
				case PendingAction.Select: AttachmentEditing.DrawSelectPopup(this, pendingPopupScreenPos); break;
			}
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
			supressInput = true;
			rmbDragStartedInPreview = false;
			HideAllGizmos();
			RebuildMarkers();
		}

		private void RebuildMarkers()
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
					SetPopupPosition();
				}
			}
			else if (attachmentsOnDownTile.Length > 1)
			{
				// Only show multi-select if there were multiple on the original tile
				pendingAction = PendingAction.Select;
				SetPopupPosition();
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
				SetPopupPosition();
				return;
			}
			SelectAttachments(null);
		}

		private void SetPopupPosition() => pendingPopupScreenPos = Input.mousePosition;

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			var map = currentMap;
			if (map == null || map.attachments == null || !map.IsValidTile(tileIndex)) return null;
			var result = map.attachments.Where(x => x.tile == tileIndex).ToArray();
			return result.Length > 0 ? result : null;
		}

		public void ClearPendingAction() => pendingAction = PendingAction.Wait;

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