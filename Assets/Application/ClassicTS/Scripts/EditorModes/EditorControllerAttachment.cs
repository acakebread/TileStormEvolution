using UnityEngine;
using System.Linq;
using static MassiveHadronLtd.GuiUtils;
using static ClassicTilestorm.EditorController;
using UnityEditor;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		public int SelectedAttachmentIndex { get; private set; } = -1;

		private Vector3 mouseDownPos;

		private int draggingTile = -1;
		private MapAttachment[] draggedAttachments = System.Array.Empty<MapAttachment>();

		private int pendingTile = -1;
		private enum PendingAction { None, Wait, Add, Delete, Select }
		private PendingAction pendingAction = PendingAction.None;
		private Vector2 pendingPopupScreenPos = Vector2.zero;

		private ViewPreview viewPreview;
		private bool isControllingPreviewWithRMB = false;
		private bool rmbDragStartedInPreview = false;
		private bool isMouseOverPreview = false;

		private readonly AutoHidePanel sidePanel = new AutoHidePanel( collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f) );

		private bool supressPopup = true;

		public override bool IsMouseOverGUI()
		{
			// Check preview first
			isMouseOverPreview = false;
			if (viewPreview != null && viewPreview.gameObject.activeSelf && viewPreview.previewRect is Rect r && r.width > 0)
			{
				Rect hitRect = new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16);
				Vector2 mp = Input.mousePosition;
				mp.y = Screen.height - mp.y;

				if (hitRect.Contains(mp))
				{
					isMouseOverPreview = true;
					return true; // block main camera scroll AND mouse wheel
				}
			}

			// Only relevant in Attachment mode
			if (editorController.CurrentMode != EditorMode.Attachment) return false;

			// Use the panel's actual rect
			Rect panelRect = sidePanel.GetPanelRect();
			Vector2 mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;

			return panelRect.Contains(mouse);
		}

		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedAttachmentIndex = -1;
			EditorUtil.DestroyMarkerVisuals();
			EditorUtil.DestroyViewFrustumMarker();
			RebuildMarkers();

			viewPreview = ViewPreview.Create();
			viewPreview.Hide();

			sidePanel.List.Clear(); // clear old items
		}

		public override void OnDisable()
		{
			base.OnDisable();
			EditorUtil.DestroyMarkerVisuals();
			pendingAction = PendingAction.None;
			EditorUtil.DestroyViewFrustumMarker();
			EditorTransformUtil.HideTransformGizmo();

			viewPreview?.Hide();
			if (viewPreview != null) Object.Destroy(viewPreview.gameObject);

			isControllingPreviewWithRMB = false;
			rmbDragStartedInPreview = false;
		}

		public override void Update()
		{
			IsMouseOverGUI();

			// === 1. TRACK MOUSE DOWN POSITION FOR BOTH BUTTONS ===
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				mouseDownPos = Input.mousePosition;

			// === 2. PREVIEW CAMERA CONTROL (RMB orbit in preview window) ===
			if (Input.GetMouseButtonDown(1))
				rmbDragStartedInPreview = isMouseOverPreview;

			if (rmbDragStartedInPreview && Input.GetMouseButton(1))
				isControllingPreviewWithRMB = true;

			if (Input.GetMouseButtonUp(1))
			{
				isControllingPreviewWithRMB = false;
				rmbDragStartedInPreview = false; // reset for next time
			}

			viewPreview.isInFocus = isMouseOverPreview;
			viewPreview.inInUse = isControllingPreviewWithRMB;

			if (isControllingPreviewWithRMB || (!Input.GetMouseButton(1) && isMouseOverPreview))
			{
				if (viewPreview?.previewCam != null)
				{
					EditorCameraMovement.UpdateCamera(viewPreview.previewCam.transform);
					if (SelectedAttachmentIndex >= 0 && SelectedAttachmentIndex < editorController.iMapManager.CurrentMap.attachments.Length && editorController.currentMap?.attachments?[SelectedAttachmentIndex] is View view)
						SnapViewDistanceToGround(view, editorController.iMapManager);
					SyncPreviewToSelectedView();
				}
				return;
			}

			// === 3. NORMAL EDITOR INPUT ===
			if (!editorCamera || viewPreview.inInUse || IsMouseOverGUI() || IsGuiControlActive())
				return;

			base.Update();

			// Gizmo handling
			if (EditorTransformUtil.HandleTransformGizmoInput(editorCamera))
			{
				if (SelectedAttachmentIndex >= 0 && SelectedAttachmentIndex < editorController.iMapManager.CurrentMap.attachments.Length && editorController.currentMap?.attachments?[SelectedAttachmentIndex] is View view)
					SnapViewDistanceToGround(view, editorController.iMapManager);
				supressPopup = true;
			}

			if (editorCamera == null || IsGuiControlActive()) return;

			// Tile picking
			Vector3 mouseWorld = MapManager.ScreenToWorld(editorCamera, Input.mousePosition);
			Vector3 snapped = editorController.iMapManager.SnappedMapPosition(mouseWorld);
			int tileUnderMouse = editorController.iMapManager.WorldToMapIndex(snapped);
			bool wasClick = Vector3.Distance(Input.mousePosition, mouseDownPos) < 8f; // slightly more forgiving than 6

			// === 4. LMB: START DRAGGING EXISTING ATTACHMENTS ===
			if (Input.GetMouseButtonDown(0))
			{
				int hitTile = HitTile(Input.mousePosition);
				var atts = GetAttachmentsOnTile(hitTile);

				if (atts != null && atts.Length > 0)
				{
					draggingTile = hitTile;
					draggedAttachments = atts;

					if (draggedAttachments.Length > 0)
						SelectAttachments(draggedAttachments);
				}
				// If no attachments → draggingTile stays -1 → we'll treat it as a potential "add" click
			}

			// === 5. LMB DRAG: MOVE ATTACHMENTS ===
			if (Input.GetMouseButton(0) && draggingTile >= 0 && tileUnderMouse >= 0 && tileUnderMouse != draggingTile)
			{
				bool moved = false;
				foreach (var att in draggedAttachments)
				{
					if (att.tile == draggingTile)
					{
						att.tile = tileUnderMouse;
						if (att is View v)
						{
							SnapViewDistanceToGround(v, editorController.iMapManager);
							EditorUtil.UpdateViewFrustumMarker(v, editorController.iMapManager);
							EditorTransformUtil.ShowTransformGizmo(v, editorController.iMapManager, editorCamera);
							viewPreview.Show(v, editorController.iMapManager);
						}
						moved = true;
					}
				}
				if (moved)
				{
					draggingTile = tileUnderMouse;
					RebuildMarkers();
				}
			}

			if (supressPopup) pendingAction = PendingAction.Wait;

			// === 6. LMB UP: ADD NEW ATTACHMENT IF IT WAS A CLICK ON EMPTY TILE ===
			if (Input.GetMouseButtonUp(0) && pendingAction == PendingAction.None)
			{
				if (draggingTile == -1) // ← We didn't drag anything → it was a click
				{
					int hitTile = HitTile(Input.mousePosition);
					if (hitTile >= 0 && GetAttachmentsOnTile(hitTile) == null)
					{
						pendingTile = hitTile;
						pendingAction = PendingAction.Add;

						var wp = editorController.iMapManager.TileWorldPosition(hitTile) + Vector3.up * 0.6f;
						var sp = editorCamera.WorldToScreenPoint(wp);
						sp.y = Screen.height - sp.y;
						pendingPopupScreenPos = sp;
					}
				}

				// Always clean up
				draggingTile = -1;
				draggedAttachments = System.Array.Empty<MapAttachment>();
			}

			// === 7. RMB UP: DELETE IF IT WAS A SHORT CLICK (not a drag/orbit) ===
			if (Input.GetMouseButtonUp(1) && pendingAction == PendingAction.None)
			{
				if (wasClick)
				{
					int hitTile = HitTile(mouseDownPos); // use down pos = more reliable
					if (hitTile >= 0 && GetAttachmentsOnTile(hitTile)?.Length > 0)
					{
						pendingTile = hitTile;
						pendingAction = PendingAction.Delete;

						var wp = editorController.iMapManager.TileWorldPosition(hitTile) + Vector3.up * 0.6f;
						var sp = editorCamera.WorldToScreenPoint(wp);
						sp.y = Screen.height - sp.y;
						pendingPopupScreenPos = sp;
					}
				}
			}

			if (pendingAction == PendingAction.Wait) pendingAction = PendingAction.None;
			supressPopup = false;
		}

		public override void OnGUI()
		{
			if (editorController.CurrentMode != EditorMode.Attachment || editorCamera == null) return;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			sidePanel.Update();

			// Clear previous list
			sidePanel.List.Clear();

			// Populate ListView
			var attachments = map.attachments ?? System.Array.Empty<MapAttachment>();
			for (int i = 0; i < attachments.Length; i++)
			{
				var att = attachments[i];
				string extra = att is Emitter e && e.LookAt != null ? $" to {e.LookAt:F1}" : "";
				string label = $"{att.GetType().Name} [{att.tile}]{extra}";

				int index = i; // capture for closure
				sidePanel.List.AddItem(new ListViewItem(
					label,
					() => SelectAttachments(new MapAttachment[] { att }),
					selected: index == SelectedAttachmentIndex
				));
			}

			// Optional footnote
			sidePanel.SetFootnote("Hold RMB on preview to orbit • Scroll to zoom • LMB: place/move • RMB on tile: delete");

			// Draw the panel
			sidePanel.Draw();

			// Draw popups
			if (pendingAction == PendingAction.Add) DrawAddPopup();
			if (pendingAction == PendingAction.Delete) DrawDeletePopup();
			if (pendingAction == PendingAction.Select) DrawSelectPopup();
		}

		public void OnMapChanged()
		{
			if (editorController.CurrentMode != EditorMode.Attachment) return;
			RebuildMarkers();
			EditorUtil.DestroyViewFrustumMarker();
			EditorTransformUtil.HideTransformGizmo();
		}

		private void RebuildMarkers()
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			var tiles = map.attachments?
				.Where(a => a.tile >= 0)
				.Select(a => a.tile)
				.Distinct()
				.ToArray() ?? System.Array.Empty<int>();

			var markerIndexTile = SelectedAttachmentIndex >= 0 && SelectedAttachmentIndex < editorController.iMapManager.CurrentMap.attachments.Length ? editorController.iMapManager.CurrentMap.attachments[SelectedAttachmentIndex].tile : -1;
			var selection = System.Array.IndexOf(tiles, markerIndexTile);
			EditorUtil.UpdateMapMarkers(editorController.iMapManager, tiles, selection, EditorUtil.MarkerType.Attachment);
		}

		private void SelectAttachments(MapAttachment[] attachments)
		{
			var index = SelectedAttachmentIndex = System.Array.IndexOf(editorController.iMapManager.CurrentMap.attachments, attachments[0]);
			RebuildMarkers();
			EditorUtil.DestroyViewFrustumMarker();
			EditorTransformUtil.HideTransformGizmo();
			viewPreview.Hide();

			var map = editorController?.iMapManager?.CurrentMap;
			if (map?.attachments == null || index < 0 || index >= map.attachments.Length) return;

			if (map.attachments[index] is View view)
			{
				SnapViewDistanceToGround(view, editorController.iMapManager);
				EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
				EditorTransformUtil.ShowTransformGizmo(view, editorController.iMapManager, editorCamera);
				viewPreview.Show(view, editorController.iMapManager);
			}
		}

		private void SyncPreviewToSelectedView()
		{
			if (SelectedAttachmentIndex < 0) return;
			var map = editorController.iMapManager.CurrentMap;
			if (map?.attachments == null || SelectedAttachmentIndex >= map.attachments.Length) return;

			if (map.attachments[SelectedAttachmentIndex] is View view)
			{
				Vector3 wp = viewPreview.previewCam.transform.position;
				view.Position = wp - editorController.iMapManager.TileWorldPosition(view.tile);
				view.Rotation = viewPreview.previewCam.transform.rotation;

				EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
				EditorTransformUtil.UpdateTransformGizmoVisuals(editorCamera);
			}
		}

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			var map = editorController.currentMap;
			if (map == null || map.attachments == null || !map.IsValidTile(tileIndex)) return null;
			var result = map.attachments.Where(x => x.tile == tileIndex).ToArray();
			return result.Length > 0 ? result : null;
		}

		private void AddAttachmentAtTileWithType(int tile, System.Type type)
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			MapAttachment newAtt = type.Name switch
			{
				"Emitter" => new Emitter { tile = tile, Position = Vector3.up, LookAt = Vector3.up },
				"View" => new View { tile = tile, Position = (Vector3.up + Vector3.back) * 8, LookAt = (Vector3.forward + Vector3.down) * 4 },
				"Pickup" => new Pickup { tile = tile },
				_ => null
			};

			if (newAtt == null) return;

			map.AddAttachment(newAtt);
			editorController.OnMapChanged();

			if (newAtt is View view)
			{
				EditorUtil.UpdateViewFrustumMarker(view, editorController.iMapManager);
				EditorTransformUtil.ShowTransformGizmo(view, editorController.iMapManager, editorCamera);
				viewPreview.Show(view, editorController.iMapManager);
			}
			SelectAttachments(new MapAttachment[] { newAtt });
		}

		private void DrawAddPopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 120;
			sp.y -= 140;

			var items = new List<PopupItem>
			{
				new PopupItem("Emitter", () => AddAttachmentAtTileWithType(pendingTile, typeof(Emitter))),
				new PopupItem("View", () => AddAttachmentAtTileWithType(pendingTile, typeof(View))),
				new PopupItem("Pickup", () => AddAttachmentAtTileWithType(pendingTile, typeof(Pickup))),
				PopupItem.Spacer(),
				new PopupItem("Cancel", colorOverride: Color.yellow)
			};

			bool closed = PopupMenu.Show(sp, "Add Attachment", items);
			if (closed) pendingAction = PendingAction.Wait;
		}

		private void DrawDeletePopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 140;
			sp.y -= 120;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			var attsOnTile = map.GetAttachmentsOnTile(pendingTile);
			if (attsOnTile.Length == 0) return;

			var items = new List<PopupItem>();

			// Individual delete buttons
			foreach (var att in attsOnTile)
			{
				string label = $"Delete {att.GetType().Name}";
				var localAtt = att;
				items.Add(new PopupItem(label, () =>
				{
					map.RemoveAttachment(localAtt);
					EditorUtil.DestroyViewFrustumMarker();
					EditorTransformUtil.HideTransformGizmo();
					RebuildMarkers();
					viewPreview.Hide(); 
					editorController.OnMapChanged();
				}));
			}

			// Only show "Delete All" if more than one
			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () =>
				{
					map.RemoveAllAttachmentsOnTile(pendingTile);
					EditorUtil.DestroyViewFrustumMarker();
					EditorTransformUtil.HideTransformGizmo();
					RebuildMarkers();
					viewPreview.Hide(); 
					editorController.OnMapChanged();
				}, colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", colorOverride: Color.yellow));

			bool closed = PopupMenu.Show(sp, "Delete Attachment(s)", items);
			if (closed) pendingAction = PendingAction.Wait;
		}

		private void DrawSelectPopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 130;
			sp.y -= 100;

			var map = editorController.iMapManager.CurrentMap;
			var atts = map.GetAttachmentsOnTile(pendingTile);
			if (atts == null || atts.Length == 0)
			{
				pendingAction = PendingAction.None;
				return;
			}

			var items = new List<PopupItem>();

			foreach (var att in atts)
			{
				string label = att.GetType().Name;

				// Only add extra info for Emitter if LookAt is not the default up vector
				if (att is Emitter e && e.LookAt != null && e.LookAt != Vector3.up)
				{
					float distance = e.LookAt.magnitude;
					label += $" to {distance:F1}";
				}

				label += $" [tile {att.tile}]";

				items.Add(new PopupItem(label, () =>
				{
					SelectAttachments(new MapAttachment[] { att });
				}));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", colorOverride: Color.yellow));

			bool closed = PopupMenu.Show(sp, $"Select ({atts.Length})", items);
			if (closed)
				pendingAction = PendingAction.Wait;
		}

		private static void SnapViewDistanceToGround(View view, IMapManager mapManager)
		{
			if (view == null || mapManager == null) return;

			var origin = mapManager.TileWorldPosition(view.tile) + view.Position;
			var forward = view.Rotation * Vector3.forward;
			var ray = new Ray(origin, forward);

			if (MapManager.RayToWorld(ray, out Vector3 result))
			{
				float distance = (result - origin).magnitude;
				if (distance > 0.1f)
				{
					view.Distance = Mathf.Min(distance, View.MAX_DISTANCE);
					return;
				}
			}

			view.Distance = View.MAX_DISTANCE;
		}
	}
}
