using UnityEngine;
using System.Linq;
using static MassiveHadronLtd.GuiUtils;
using static ClassicTilestorm.EditorController;
using UnityEditor;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		private Vector3 mouseDownPos;
		private bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 8f;

		public MapAttachment[] selectedAttachments = System.Array.Empty<MapAttachment>();

		private int pendingTile = -1;
		public enum PendingAction { None, Wait, Add, Delete, Select, Drag }
		public PendingAction pendingAction = PendingAction.None;
		private Vector2 pendingPopupScreenPos = Vector2.zero;

		public ViewPreview viewPreview;
		private bool isControllingPreviewWithRMB = false;
		private bool rmbDragStartedInPreview = false;

		private readonly AutoHidePanel sidePanel = new AutoHidePanel(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));

		private bool supressInput = true;

		public override bool IsMouseOverGUI()
		{
			if (base.IsMouseOverGUI()) return true;

			Rect panelRect = sidePanel.GetPanelRect();
			Vector2 mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;
			return panelRect.Contains(mouse);
		}

		private bool IsMouseOverPreview()
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

			sidePanel.List.Clear();
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

			isControllingPreviewWithRMB = false;
			rmbDragStartedInPreview = false;
		}

		public override void Update()
		{
			if (IsMouseOverGUI() || IsGuiControlActive()) return;
			var isMouseOverPreview = IsMouseOverPreview();

			if (EditorTransformUtil.HandleTransformGizmoInput(editorCamera))
			{
				var typeEditor = AttachmentEditing.GetCurrentEditor(this);
				typeEditor?.HandleGizmoInput(this);
				supressInput = true;
			}

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				mouseDownPos = Input.mousePosition;
				mouseMovedBeyondThreshold = false;
				rmbDragStartedInPreview = isMouseOverPreview;
			}

			if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
			{
				if (Vector3.Distance(Input.mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
					mouseMovedBeyondThreshold = true;
			}

			bool wasClick = !mouseMovedBeyondThreshold;

			if (rmbDragStartedInPreview && Input.GetMouseButton(1))
				isControllingPreviewWithRMB = true;

			if (Input.GetMouseButtonUp(1))
			{
				isControllingPreviewWithRMB = false;
				rmbDragStartedInPreview = false;
			}

			viewPreview.isInFocus = isMouseOverPreview;
			viewPreview.inInUse = isControllingPreviewWithRMB;

			if (isControllingPreviewWithRMB || (!Input.GetMouseButton(1) && isMouseOverPreview))
			{
				float scroll = Input.GetAxis("Mouse ScrollWheel");
				if (isControllingPreviewWithRMB || scroll != 0f)
				{
					EditorCameraMovement.UpdateCamera(viewPreview.previewCam.transform);
					AttachmentViewEditing.HandlePreviewCameraSync(this, viewPreview);
				}
				return;
			}

			if (viewPreview.inInUse) return;

			base.Update();

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
			if (Input.GetMouseButtonUp(0) && !supressInput && wasClick)
			{
				HandleLeftMouseUpOnCleanClick();
			}

			// RMB Up: delete popup
			if (Input.GetMouseButtonUp(1) && !supressInput && wasClick)
				HandleRightMouseUp(wasClick);

			if (pendingAction == PendingAction.Wait) pendingAction = PendingAction.None;
			supressInput = false;
		}

		public override void OnGUI()
		{
			if (null == editorCamera) return;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			sidePanel.Update();
			sidePanel.List.Clear();

			var attachments = map.attachments ?? System.Array.Empty<MapAttachment>();
			for (int i = 0; i < attachments.Length; i++)
			{
				var att = attachments[i];
				string label = AttachmentSidePanelLabel(att);

				sidePanel.List.AddItem(new ListViewItem(
					label,
					() => SelectAttachments(new MapAttachment[] { att }),
					selected: selectedAttachments != null && selectedAttachments.Contains(att)
				));
			}

			sidePanel.SetFootnote("Hold RMB on preview to orbit • Scroll to zoom • LMB: place/move in/move • RMB on tile: delete");
			sidePanel.Draw();

			AttachmentEditing.DrawGUI(this);
		}

		public void OnMapChanged()
		{
			if (editorController.CurrentMode != EditorMode.Attachment) return;
			RebuildMarkers();
			EditorPrimitiveUtil.HideCone();
			EditorFrustumUtil.Hide();
			EditorTransformUtil.HideTransformGizmo();
			viewPreview.Hide();
			isControllingPreviewWithRMB = false;
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
			int selectedTile = (selectedAttachments != null && selectedAttachments.Length > 0)
				? selectedAttachments[0].tile
				: -1;

			int selection = System.Array.IndexOf(tiles, selectedTile);

			UpdateMapMarkers(editorController.iMapManager, tiles, selection, EditorMarkerUtil.MarkerType.Attachment);
		}

		public void SelectAttachments(MapAttachment[] attachments)
		{
			selectedAttachments = attachments;

			RebuildMarkers();
			EditorPrimitiveUtil.HideCone();
			EditorFrustumUtil.Hide();
			EditorTransformUtil.HideTransformGizmo();
			viewPreview.Hide();

			var typeEditor = AttachmentEditing.GetCurrentEditor(this);
			typeEditor?.HandleSelectionChanged(this);
		}

		private int GetTileUnderMouse()
		{
			if (!editorCamera) return -1;
			Vector3 mouseWorld = MapManager.ScreenToWorld(editorCamera, Input.mousePosition);
			Vector3 snapped = editorController.iMapManager.SnappedMapPosition(mouseWorld);
			return editorController.iMapManager.WorldToMapIndex(snapped);
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

		private void HandleDrag(int tileUnderMouse)
		{
			foreach (var att in selectedAttachments)
			{
				att.tile = tileUnderMouse;
				var typeEditor = AttachmentEditing.GetCurrentEditor(this);
				typeEditor?.HandleDrag(this, att);
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
			}
			else
			{
				SelectAttachments(null);
			}
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

		private string AttachmentSidePanelLabel(MapAttachment att)
		{
			return att switch
			{
				Emitter e => $"Emitter [{att.tile}]" + (e.LookAt != null && e.LookAt != Vector3.up ? $" to {e.LookAt.magnitude:F1}" : ""),
				View => $"View [{att.tile}]",
				Pickup => $"Pickup [{att.tile}]",
				_ => $"{att.GetType().Name} [{att.tile}]"
			};
		}

		public MapAttachment AddNewAttachment(int tile, System.Type type)
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return null;

			MapAttachment newAtt = type.Name switch
			{
				"Emitter" => new Emitter { tile = tile, Position = Vector3.up, LookAt = Vector3.up },
				"View" => new View { tile = tile, Position = (Vector3.up + Vector3.back) * 8, LookAt = (Vector3.forward + Vector3.down) * 4 },
				"Pickup" => new Pickup { tile = tile },
				_ => null
			};

			if (newAtt == null) return null;

			// Ensure correct tile
			newAtt.tile = tile;

			map.AddAttachment(newAtt);

			editorController.OnMapChanged();
			SelectAttachments(new MapAttachment[] { newAtt });

			return newAtt;
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

		private static void UpdateMapMarkers(IMapManager mapManager, int[] tiles, int selectedIndex = -1, EditorMarkerUtil.MarkerType type = EditorMarkerUtil.MarkerType.Undefined)
		{
			if (tiles == null || tiles.Length == 0 || EditorMarkerUtil.SphereMesh == null)
			{
				EditorMarkerUtil.ClearMapMarkers();
				return;
			}

			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];

			for (int i = 0; i < tiles.Length; i++)
			{
				int tile = tiles[i];
				if (tile < 0 || tile >= mapManager.Count)
				{
					positions[i] = Vector3.zero;
					colors[i] = new Color(0f, 0.7f, 1f, 0.7f);
					continue;
				}

				positions[i] = mapManager.TileWorldPosition(tile);

				bool hasView = type == EditorMarkerUtil.MarkerType.Waypoint && mapManager.GetView(tile) != null;
				colors[i] = hasView ? new Color(0f, 1f, 1f, 0.5f) : new Color(0f, 0.7f, 1f, 0.7f);
			}

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}
	}
}