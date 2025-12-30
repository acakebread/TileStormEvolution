using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public static class AttachmentEditing
	{
		public enum PendingAction { None, Add, Delete, Select }
		public static PendingAction pendingAction = PendingAction.None;

		public static MapAttachment[] selectedAttachments = null;

		// Shared across both attachment and waypoint editing modes
		public static int CurrentPendingTile { get; set; } = -1;

		public static void RebuildMarkers(IMapManager iMapManager, EditorMarkerUtil.MarkerType type = EditorMarkerUtil.MarkerType.Undefined)
		{
			if (null == iMapManager) return;
			// Determine selected tile from current selection
			var selectedTile = (selectedAttachments != null && selectedAttachments.Length > 0) ? selectedAttachments[0].tile : -1;
			var tiles = iMapManager?.attachments?.Where(a => a.tile >= 0).Select(a => a.tile).Distinct().ToArray() ?? System.Array.Empty<int>();
			var selection = System.Array.IndexOf(tiles, selectedTile);
			UpdateMapMarkers(iMapManager, tiles, selection, type);
		}

		public static void HideAllGizmos()
		{
			EditorTransformUtil.Hide();
			EditorPrimitiveUtil.Hide();
			EditorFrustumUtil.Hide();
			ViewPreviewUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();
		}

		public static void Select(MapAttachment[] attachments, IMapManager mapManager, Camera camera)
		{
			selectedAttachments = null != attachments && null != attachments[0] ? attachments : null;
			HideAllGizmos();
			RebuildMarkers(mapManager, EditorMarkerUtil.MarkerType.Attachment);//ToDo get type of attachment[0] and see if it is a waypoint or not

			if (null == selectedAttachments || 1 != selectedAttachments.Length) return;
			HandleSelectionChanged(mapManager, camera);
			HandleGizmoInput(mapManager, camera);
		}

		public static bool DrawAddPopup(Vector2 position, IMapManager mapManager, Camera sceneCamera, int pendingTile)
		{
			var wasCancelled = true;
			var items = new List<PopupItem>
			{
				new ($"Waypoint [WP{mapManager.CurrentMap.waypoints.Length:00}]", () => { AttachmentWaypointEditing.CreateWaypoint(mapManager, CurrentPendingTile) ; AttachmentEditing.pendingAction = AttachmentEditing.PendingAction.None; }),
				new ("Emitter [flame]", () => Select(new[] { AttachmentEmitterEditing.CreateEmitter(mapManager, pendingTile, "flame") }, mapManager, sceneCamera)),
				new ("Emitter [spark]", () => Select(new[] { AttachmentEmitterEditing.CreateEmitter(mapManager, pendingTile, "spark") }, mapManager, sceneCamera)),
				new ("View", () => Select(new[] { AttachmentViewEditing.CreateView(mapManager, pendingTile) }, mapManager, sceneCamera)),
				new ("Pickup", () => Select(new[] { AttachmentPickupEditing.CreatePickup(mapManager, pendingTile) }, mapManager, sceneCamera)),
				PopupItem.Spacer(),
				new ("Cancel", () => { }, colorOverride: Color.yellow)
			};
			var result = PopupMenu.Show(position, $"Add Attachment at tile {CurrentPendingTile}", items);
			if (!result && wasCancelled)
				Select(null, mapManager, sceneCamera);
			return result;
		}

		public static bool DrawDeletePopup(Vector2 position, IMapManager mapManager, Camera sceneCamera, int pendingTile)
		{
			//var attsOnTile = mapManager.CurrentMap.GetAttachmentsOnTile(pendingTile);
			var attsOnTile = GetAttachmentsOnTile(mapManager, pendingTile);
			if (attsOnTile.Length == 0) return false;

			var wasCancelled = true;
			var items = new List<PopupItem>();
			foreach (var att in attsOnTile)
			{
				string label = $"Delete {att.GetType().Name}";
				if (att is Waypoint wp)
					label = $"WP{wp.waypointIndex:00} at tile {CurrentPendingTile}";

				var localAtt = att;
				items.Add(new PopupItem(label, () =>
				{
					mapManager.RemoveAttachment(localAtt);
					Select(null, mapManager, sceneCamera);
				}));
			}

			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () =>
				{
					mapManager.RemoveAllAttachmentsOnTile(pendingTile);
					Select(null, mapManager, sceneCamera);
				}, colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(position, "Delete Attachment(s)", items);
			if (!result && wasCancelled)
				Select(null, mapManager, sceneCamera);
			return result;
		}

		public static bool DrawSelectPopup(Vector2 position, IMapManager mapManager, Camera sceneCamera, int pendingTile)
		{
			//var atts = mapManager.CurrentMap.GetAttachmentsOnTile(pendingTile);
			var atts = GetAttachmentsOnTile(mapManager, pendingTile);
			if (atts == null || atts.Length == 0) return false;

			var wasCancelled = true;

			var items = new List<PopupItem>();

			foreach (var att in atts)
			{
				string label = att.GetType().Name;
				if (att is Emitter e && e.LookAt != null && e.LookAt != Vector3.up)
					label += $" to {e.LookAt.magnitude:F1}";
				label += $" [tile {att.tile}]";

				items.Add(new PopupItem(label, () =>
				{
					wasCancelled = false;
					Select(new[] { att }, mapManager, sceneCamera);
				}));
			}

			if (atts.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Select All", () =>
				{
					wasCancelled = false;
					Select(atts, mapManager, sceneCamera);
				}, colorOverride: Color.green));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(position, $"Select ({atts.Length})", items);
			if (!result && wasCancelled)
				Select(null, mapManager, sceneCamera);
			return result;
		}

		public static void RefreshAttachmentInstances(IMapManager mapManager, int tile)
		{
			foreach (var att in selectedAttachments)
			{
				att.tile = tile;
				mapManager.RefreshAttachmentInstance(att);
			}
		}

		public static MapAttachment[] GetAttachmentsOnTile(IMapManager mapManager, int tileIndex)
		{
			if (mapManager?.CurrentMap == null || mapManager?.attachments == null || !mapManager.CurrentMap.IsValidTile(tileIndex)) return null;
			var result = mapManager.attachments.Where(x => x.tile == tileIndex).ToArray();
			return result.Length > 0 ? result : null;
		}

		public static void UpdateMapMarkers(IMapManager mapManager, int[] tiles, int selectedIndex = -1, EditorMarkerUtil.MarkerType type = EditorMarkerUtil.MarkerType.Undefined)
		{
			if (null == tiles || 0 == tiles.Length)
			{
				EditorMarkerUtil.ClearMapMarkers();
				return;
			}

			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];

			for (var i = 0; i < tiles.Length; i++)
			{
				var tile = tiles[i];
				if (tile < 0 || tile >= mapManager.Count)
				{
					positions[i] = Vector3.zero;
					colors[i] = new Color(0f, 0.7f, 1f, 0.7f);
					continue;
				}

				positions[i] = mapManager.TileWorldPosition(tile);

				var hasView = type == EditorMarkerUtil.MarkerType.Waypoint && null != mapManager.GetView(tile);
				colors[i] = hasView ? new Color(0f, 1f, 1f, 0.5f) : new Color(0f, 0.7f, 1f, 0.7f);
			}

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		public static void HandleSelectionChanged(IMapManager mapManager, Camera camera)
		{
			if (null == selectedAttachments || 0 == selectedAttachments.Length) return;

			// We only support uniform selection for type-specific GUI
			var firstType = selectedAttachments[0].GetType();
			if (!selectedAttachments.All(a => a.GetType() == firstType)) return;

			switch (selectedAttachments[0])
			{
				case Waypoint:
					AttachmentWaypointEditing.OnSelectionChanged(mapManager, camera);
					break;
				case Emitter:
					AttachmentEmitterEditing.OnSelectionChanged(mapManager, camera);
					break;
				case View:
					AttachmentViewEditing.OnSelectionChanged(mapManager, camera);
					break;
				case Pickup:
					AttachmentPickupEditing.OnSelectionChanged(mapManager, camera);
					break;
			}
		}

		public static void HandleGizmoInput(IMapManager mapManager, Camera camera)
		{
			if (null == selectedAttachments || 0 == selectedAttachments.Length) return;

			var firstType = selectedAttachments[0].GetType();
			if (!selectedAttachments.All(a => a.GetType() == firstType)) return;

			switch (selectedAttachments[0])
			{
				case Waypoint:
					AttachmentWaypointEditing.OnGizmoInput(mapManager, camera);
					break;
				case Emitter:
					AttachmentEmitterEditing.OnGizmoInput(mapManager, camera);
					break;
				case View:
					AttachmentViewEditing.OnGizmoInput(mapManager, camera);
					break;
				case Pickup:
					AttachmentPickupEditing.OnGizmoInput(mapManager, camera);
					break;
			}
		}

		public static void HandleDragInput(IMapManager mapManager, Camera camera)
		{
			if (null == selectedAttachments || 1 != selectedAttachments.Length) return;

			var attachment = selectedAttachments[0];

			if (attachment is ITransformableAttachment transformable)
			{
				var worldPos = MapManager.WorldPosition(attachment.tile, transformable.Position);
				var worldRot = MapManager.WorldRotation(attachment.tile, transformable.Rotation);
				EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
			}

			switch (attachment)
			{
				case Waypoint:
					AttachmentWaypointEditing.OnDragInput(mapManager);
					break;
				case Emitter:
					AttachmentEmitterEditing.OnDragInput(mapManager);
					break;
				case View:
					AttachmentViewEditing.OnDragInput(mapManager);
					break;
				case Pickup:
					AttachmentPickupEditing.OnDragInput(mapManager);
					break;
			}
		}

		// Shared drag logic
		public static void HandleDrag(IMapManager mapManager, Camera camera, EditorMarkerUtil.MarkerType type)
		{
			var tileUnderMouse = mapManager.CameraHitTile(camera, Input.mousePosition);

			if (tileUnderMouse == -1 ||
				tileUnderMouse == CurrentPendingTile ||
				selectedAttachments == null ||
				selectedAttachments.Length == 0)
				return;

			CurrentPendingTile = tileUnderMouse;
			RefreshAttachmentInstances(mapManager, tileUnderMouse);
			HandleDragInput(mapManager, camera);
			RebuildMarkers(mapManager, type);
		}



		// === NEW: Shared input state for both controllers ===
		public static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));
		public static Vector3 mouseDownPos;
		private static bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 8f;
		private static bool rmbDownInPreview = false;
		public static bool supressInput = true;

		// === NEW: Core shared update logic ===
		public static void Update(Camera camera, IMapManager iMapManager, EditorMarkerUtil.MarkerType markerType, bool isMouseOverGUI)
		{
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

			if (isMouseOverGUI || IsGuiControlActive())
			{
				supressInput = false;
				return;
			}

			if (EditorTransformUtil.HandleTransformGizmoInput(camera))
			{
				HandleGizmoInput(iMapManager, camera);
				supressInput = true;
			}

			if (supressInput)
			{
				supressInput = false;
				return;
			}

			// Track click vs drag
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

			// Mouse events
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				HandleMouseDownShared(iMapManager, iMapManager.CameraHitTile(camera, Input.mousePosition), camera);

			if (Input.GetMouseButton(0))
				HandleDrag(iMapManager, camera, markerType);

			if (Input.GetMouseButtonUp(0) && !mouseMovedBeyondThreshold)
				HandleLeftMouseUpShared(iMapManager, camera, markerType);

			if (Input.GetMouseButtonUp(1) && !mouseMovedBeyondThreshold)
				HandleRightMouseUpShared(iMapManager, iMapManager.CameraHitTile(camera, Input.mousePosition), camera);
		}

		// === Shared mouse handlers ===
		private static void HandleMouseDownShared(IMapManager iMapManager, int tile, Camera camera)
		{
			CurrentPendingTile = tile;

			if (tile != -1)
			{
				var alreadySelected = selectedAttachments?.Length > 0 && selectedAttachments[0].tile == tile;
				if (!alreadySelected)
				{
					selectedAttachments = GetAttachmentsOnTile(iMapManager, tile);
					Select(selectedAttachments, iMapManager, camera);
				}
				return;
			}
			Select(null, iMapManager, camera);
		}

		private static void HandleLeftMouseUpShared(IMapManager iMapManager, Camera camera, EditorMarkerUtil.MarkerType markerType)
		{
			var attachmentsOnTile = GetAttachmentsOnTile(iMapManager, CurrentPendingTile);

			if (attachmentsOnTile == null || attachmentsOnTile.Length == 0)
			{
				if (CurrentPendingTile != -1)
					pendingAction = PendingAction.Add;
			}
			else if (attachmentsOnTile.Length > 1)
			{
				pendingAction = PendingAction.Select;
			}
			else
			{
				pendingAction = PendingAction.None;
				Select(attachmentsOnTile, iMapManager, camera);
			}

			RebuildMarkers(iMapManager, markerType);
		}

		private static void HandleRightMouseUpShared(IMapManager iMapManager, int tile, Camera camera)
		{
			if (tile >= 0 && GetAttachmentsOnTile(iMapManager, tile)?.Length > 0)
			{
				CurrentPendingTile = tile;
				pendingAction = PendingAction.Delete;
				return;
			}
			Select(null, iMapManager, camera);
		}

		// === Reset state helpers ===
		public static void ResetInputState()
		{
			supressInput = true;
			rmbDownInPreview = false;
			pendingAction = PendingAction.None;
			selectedAttachments = null;
			CurrentPendingTile = -1;//had to add this, not sure why but onmapchaged was invoking click events after unification
			HideAllGizmos();
		}

		public static void OnEnableShared(IMapManager iMapManager, EditorMarkerUtil.MarkerType markerType)
		{
			ResetInputState();
			RebuildMarkers(iMapManager, markerType);
		}

		public static void OnDisableShared()
		{
			ResetInputState();
		}

		public static bool IsGuiControlActive() => GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

		public static void OnGUI(IMapManager iMapManager, Camera camera)
		{
			ViewPreviewUtil.OnGUI();

			if (pendingAction == PendingAction.None) return;
			supressInput = true;

			// Use shared popups
			switch (pendingAction)
			{
				case PendingAction.Add:
					if (DrawAddPopup(mouseDownPos, iMapManager, camera, CurrentPendingTile)) return;
					break;
				case PendingAction.Delete:
					if (DrawDeletePopup(mouseDownPos, iMapManager, camera, CurrentPendingTile)) return;
					break;
				case PendingAction.Select:
					if (DrawSelectPopup(mouseDownPos, iMapManager, camera, CurrentPendingTile)) return;
					break;
			}

			pendingAction = PendingAction.None;
		}
	}
}