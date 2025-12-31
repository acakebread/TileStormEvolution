using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using UnityEngine.EventSystems;
using System;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		// ===================================================================
		// Public static selection — required by external code
		// ===================================================================
		public static MapAttachment[] selectedAttachments = null;

		// ===================================================================
		// Current mode - used for panel and marker styling
		// ===================================================================
		private EditorMarkerUtil.MarkerType currentMode = EditorMarkerUtil.MarkerType.Undefined;

		// ===================================================================
		// Pending state
		// ===================================================================
		private enum PendingAction { None, Add, Delete, Select }
		private PendingAction pendingAction = PendingAction.None;

		private int currentPendingTile = -1;

		// ===================================================================
		// Shared input state
		// ===================================================================
		private readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));
		private Vector3 mouseDownPos;
		private bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 8f;
		private bool rmbDownInPreview = false;
		private bool supressInput = true;

		// ===================================================================
		// Constructor
		// ===================================================================
		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		// ===================================================================
		// Lifecycle
		// ===================================================================
		public override void OnMapLoaded()
		{
			ResetInputState();
			RebuildMarkers(currentMode);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			currentMode = EditorMarkerUtil.MarkerType.Attachment; // default to attachment mode
			ResetInputState();
			RebuildMarkers(currentMode);
		}

		public override void OnDisable()
		{
			base.OnDisable();
			ResetInputState();
		}

		private void ResetInputState()
		{
			supressInput = true;
			rmbDownInPreview = false;
			pendingAction = PendingAction.None;
			selectedAttachments = null;
			currentPendingTile = -1;
			currentMode = EditorMarkerUtil.MarkerType.Undefined;
			HideAllGizmos();
		}

		// ===================================================================
		// Core Update
		// ===================================================================
		public override void Update()
		{
			base.Update();

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

			if (IsMouseOverGUI() || IsGuiControlActive())
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
				HandleMouseDownShared(iMapManager.CameraHitTile(camera, Input.mousePosition), camera);

			if (Input.GetMouseButton(0))
				HandleDrag(iMapManager, camera, currentMode);

			if (Input.GetMouseButtonUp(0) && !mouseMovedBeyondThreshold)
				HandleLeftMouseUpShared(camera, currentMode);

			if (Input.GetMouseButtonUp(1) && !mouseMovedBeyondThreshold)
				HandleRightMouseUpShared(iMapManager.CameraHitTile(camera, Input.mousePosition), camera);
		}

		// ===================================================================
		// Input Handlers — now using currentMode
		// ===================================================================
		private void HandleMouseDownShared(int tile, Camera camera)
		{
			currentPendingTile = tile;

			if (tile != -1)
			{
				var alreadySelected = selectedAttachments?.Length > 0 && selectedAttachments[0].tile == tile;
				if (!alreadySelected)
				{
					selectedAttachments = GetAttachmentsOnTile(tile);


					// === MODE SWITCHING LOGIC — EXACTLY AS YOU SPECIFIED ===
					if (selectedAttachments == null || selectedAttachments.Length == 0)
					{
						// Do nothing — keep currentMode as is (panel stays)
					}
					else if (selectedAttachments.Length == 1)
					{
						currentMode = selectedAttachments[0] is Waypoint
							? EditorMarkerUtil.MarkerType.Waypoint
							: EditorMarkerUtil.MarkerType.Attachment;
					}
					else // Length > 1
					{
						currentMode = EditorMarkerUtil.MarkerType.Attachment;
					}
					// === END OF MODE SWITCHING ===

					Select(selectedAttachments, camera);
				}
				return;
			}
			Select(null, camera);
		}

		private void HandleLeftMouseUpShared(Camera camera, EditorMarkerUtil.MarkerType markerType)
		{
			var attachmentsOnTile = GetAttachmentsOnTile(currentPendingTile);

			if (attachmentsOnTile == null || attachmentsOnTile.Length == 0)
			{
				if (currentPendingTile != -1)
					pendingAction = PendingAction.Add;
			}
			else if (attachmentsOnTile.Length > 1)
			{
				pendingAction = PendingAction.Select;
			}
			else
			{
				pendingAction = PendingAction.None;
				Select(attachmentsOnTile, camera);
			}

			RebuildMarkers(markerType);
		}

		private void HandleRightMouseUpShared(int tile, Camera camera)
		{
			if (tile >= 0 && GetAttachmentsOnTile(tile)?.Length > 0)
			{
				currentPendingTile = tile;
				pendingAction = PendingAction.Delete;
				return;
			}
			Select(null, camera);
		}

		private void HandleDrag(IMapManager mapManager, Camera camera, EditorMarkerUtil.MarkerType type)
		{
			var tileUnderMouse = mapManager.CameraHitTile(camera, Input.mousePosition);

			if (tileUnderMouse == -1 ||
				tileUnderMouse == currentPendingTile ||
				selectedAttachments == null ||
				selectedAttachments.Length == 0)
				return;

			currentPendingTile = tileUnderMouse;
			RefreshAttachmentInstances(mapManager, tileUnderMouse);
			HandleDragInput(mapManager, camera);
			RebuildMarkers(type);
		}

		// ===================================================================
		// Selection & Gizmos
		// ===================================================================
		private void Select(MapAttachment[] attachments, Camera camera)
		{
			selectedAttachments = attachments?.Length > 0 ? attachments : null;

			if (selectedAttachments != null && selectedAttachments.Length > 0)
			{
				if (selectedAttachments.Length == 1)
				{
					currentMode = selectedAttachments[0] is Waypoint
						? EditorMarkerUtil.MarkerType.Waypoint
						: EditorMarkerUtil.MarkerType.Attachment;
				}
				else
				{
					currentMode = EditorMarkerUtil.MarkerType.Attachment;
				}
			}

			HideAllGizmos();
			RebuildMarkers(currentMode);

			if (selectedAttachments == null || selectedAttachments.Length != 1) return;
			HandleSelectionChanged(iMapManager, camera);
			HandleGizmoInput(iMapManager, camera);
		}

		private void HandleSelectionChanged(IMapManager mapManager, Camera camera)
		{
			if (selectedAttachments == null || selectedAttachments.Length == 0) return;
			var firstType = selectedAttachments[0].GetType();
			if (!selectedAttachments.All(a => a.GetType() == firstType)) return;

			switch (selectedAttachments[0])
			{
				case Waypoint: AttachmentWaypointEditing.OnSelectionChanged(mapManager, camera); break;
				case Emitter: AttachmentEmitterEditing.OnSelectionChanged(mapManager, camera); break;
				case View: AttachmentViewEditing.OnSelectionChanged(mapManager, camera); break;
				case Pickup: AttachmentPickupEditing.OnSelectionChanged(mapManager, camera); break;
			}
		}

		private void HandleGizmoInput(IMapManager mapManager, Camera camera)
		{
			if (selectedAttachments == null || selectedAttachments.Length == 0) return;
			var firstType = selectedAttachments[0].GetType();
			if (!selectedAttachments.All(a => a.GetType() == firstType)) return;

			switch (selectedAttachments[0])
			{
				case Waypoint: AttachmentWaypointEditing.OnGizmoInput(mapManager, camera); break;
				case Emitter: AttachmentEmitterEditing.OnGizmoInput(mapManager, camera); break;
				case View: AttachmentViewEditing.OnGizmoInput(mapManager, camera); break;
				case Pickup: AttachmentPickupEditing.OnGizmoInput(mapManager, camera); break;
			}
		}

		private void HandleDragInput(IMapManager mapManager, Camera camera)
		{
			if (selectedAttachments == null || selectedAttachments.Length != 1) return;

			var attachment = selectedAttachments[0];

			if (attachment is ITransformableAttachment transformable)
			{
				var worldPos = MapManager.WorldPosition(attachment.tile, transformable.Position);
				var worldRot = MapManager.WorldRotation(attachment.tile, transformable.Rotation);
				EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
			}

			switch (attachment)
			{
				case Waypoint: AttachmentWaypointEditing.OnDragInput(mapManager); break;
				case Emitter: AttachmentEmitterEditing.OnDragInput(mapManager); break;
				case View: AttachmentViewEditing.OnDragInput(mapManager); break;
				case Pickup: AttachmentPickupEditing.OnDragInput(mapManager); break;
			}
		}

		// ===================================================================
		// Helpers
		// ===================================================================
		private void HideAllGizmos()
		{
			EditorTransformUtil.Hide();
			EditorPrimitiveUtil.Hide();
			EditorFrustumUtil.Hide();
			ViewPreviewUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();
		}

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			if (iMapManager?.CurrentMap == null || !iMapManager.CurrentMap.IsValidTile(tileIndex)) return Array.Empty<MapAttachment>();
			return iMapManager.attachments?.Where(x => x.tile == tileIndex).ToArray() ?? Array.Empty<MapAttachment>();
		}

		private void RefreshAttachmentInstances(IMapManager mapManager, int tile)
		{
			if (selectedAttachments == null) return;
			foreach (var att in selectedAttachments)
			{
				att.tile = tile;
				mapManager.RefreshAttachmentInstance(att);
			}
		}

		private void RebuildMarkers(EditorMarkerUtil.MarkerType type)
		{
			var selectedTile = (selectedAttachments != null && selectedAttachments.Length > 0) ? selectedAttachments[0].tile : -1;
			var tiles = iMapManager?.attachments?.Where(a => a.tile >= 0).Select(a => a.tile).Distinct().ToArray() ?? Array.Empty<int>();
			var selection = Array.IndexOf(tiles, selectedTile);
			UpdateMapMarkers(iMapManager, tiles, selection, type);
		}

		private void UpdateMapMarkers(IMapManager mapManager, int[] tiles, int selectedIndex = -1, EditorMarkerUtil.MarkerType type = EditorMarkerUtil.MarkerType.Undefined)
		{
			if (tiles == null || tiles.Length == 0)
			{
				EditorMarkerUtil.ClearMapMarkers();
				return;
			}

			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];

			for (var i = 0; i < tiles.Length; i++)
			{
				var tile = tiles[i];
				positions[i] = tile < 0 || tile >= mapManager.Count ? Vector3.zero : mapManager.TileWorldPosition(tile);

				bool hasView = type == EditorMarkerUtil.MarkerType.Waypoint && mapManager.GetView(tile) != null;
				colors[i] = hasView ? new Color(0f, 1f, 1f, 0.5f) : new Color(0f, 0.7f, 1f, 0.7f);
			}

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		private static bool IsGuiControlActive() => GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

		// ===================================================================
		// GUI — currently only draws attachment panel (waypoint panel ready below)
		// ===================================================================
		public override void OnGUI()
		{
			ViewPreviewUtil.OnGUI();

			// Draw correct panel based on currentMode
			if (currentMode == EditorMarkerUtil.MarkerType.Waypoint)
			{
				DrawSidePanelWaypoint();
			}
			else
			{
				DrawSidePanelAttachment(); // includes Undefined and Attachment
			}

			if (pendingAction == PendingAction.None) return;

			supressInput = true;

			switch (pendingAction)
			{
				case PendingAction.Add:
					if (DrawAddPopup()) return;
					break;
				case PendingAction.Delete:
					if (DrawDeletePopup()) return;
					break;
				case PendingAction.Select:
					if (DrawSelectPopup()) return;
					break;
			}

			pendingAction = PendingAction.None;
		}

		// ===================================================================
		// Side Panels
		// ===================================================================
		private void DrawSidePanelAttachment()
		{
			var atts = currentMap.attachments ?? Array.Empty<MapAttachment>();
			var items = new List<ListViewItem>();

			foreach (var att in atts)
			{
				items.Add(new ListViewItem(
					GetAttachmentLabel(att),
					(_) => Select(new[] { att }, camera),
					selected: selectedAttachments != null && selectedAttachments.Contains(att)
				));
			}

			sidePanel.List.SetItems(items);
			sidePanel.SetFootnote("Hold RMB on preview to orbit • Scroll to zoom • LMB: place/move • RMB on tile: delete");
			sidePanel.Draw();

			string GetAttachmentLabel(MapAttachment att) => att switch
			{
				Emitter e => $"Emitter [{att.tile}]" + (e.LookAt.sqrMagnitude > 0.01f && e.LookAt != Vector3.up ? $" → {e.LookAt.magnitude:F1}" : ""),
				View => $"View [{att.tile}]",
				Pickup p => $"Pickup [{att.tile}] ({p.amount})",
				_ => $"{att.TypeName} [{att.tile}]"
			};
		}

		// WAYPOINT PANEL — lifted and ready
		private void DrawSidePanelWaypoint()
		{
			Waypoint selectedWaypoint = selectedAttachments?.Length > 0 ? selectedAttachments[0] as Waypoint : null;

			var wpArray = currentMap.waypoints ?? Array.Empty<int>();
			var items = new List<ListViewItem>();

			var waypointAttachments = iMapManager.waypointAttachments;

			for (int i = 0; i < wpArray.Length; i++)
			{
				int tile = wpArray[i];
				var waypoint = waypointAttachments.FirstOrDefault(w => w.waypointIndex == i);

				items.Add(new ListViewItem(
					label: $"WP{i:00} [tile {tile}]",
					onClick: (_) =>
					{
						if (waypoint != null)
							Select(new[] { waypoint }, camera);
					},
					selected: selectedWaypoint?.waypointIndex == i
				));
			}

			sidePanel.List.SetItems(items);

			sidePanel.Buttons.Clear();

			bool canMoveUp = selectedWaypoint != null && selectedWaypoint.waypointIndex > 0;
			bool canMoveDown = selectedWaypoint != null &&
							   selectedWaypoint.waypointIndex >= 0 &&
							   selectedWaypoint.waypointIndex < wpArray.Length - 1;

			sidePanel.Buttons.Add(new ListViewButton("Move Up", () => MoveWaypoint(selectedWaypoint, -1), enabled: canMoveUp));
			sidePanel.Buttons.Add(new ListViewButton("Move Down", () => MoveWaypoint(selectedWaypoint, +1), enabled: canMoveDown));

			sidePanel.Draw();
		}

		private void MoveWaypoint(Waypoint wp, int direction)
		{
			if (wp == null) return;

			int oldIndex = wp.waypointIndex;
			int newIndex = oldIndex + direction;
			if (newIndex < 0 || newIndex >= currentMap.waypoints.Length) return;

			var list = currentMap.waypoints.ToList();
			(list[oldIndex], list[newIndex]) = (list[newIndex], list[oldIndex]);
			currentMap.waypoints = list.ToArray();

			var movedWaypoint = new Waypoint(newIndex, list[newIndex]);
			Select(new[] { movedWaypoint }, camera);
			RebuildMarkers(currentMode);
		}

		// ===================================================================
		// Popups (unchanged — wasCancelled pattern preserved)
		// ===================================================================
		private bool DrawAddPopup()
		{
			var wasCancelled = true;
			var items = new List<PopupItem>
			{
				new($"Waypoint [WP{currentMap.waypoints.Length:00}]", () =>
				{
					AttachmentWaypointEditing.CreateWaypoint(iMapManager, currentPendingTile);
					pendingAction = PendingAction.None;
				}),
				new("Emitter [flame]", () => Select(new[] { AttachmentEmitterEditing.CreateEmitter(iMapManager, currentPendingTile, "flame") }, camera)),
				new("Emitter [spark]", () => Select(new[] { AttachmentEmitterEditing.CreateEmitter(iMapManager, currentPendingTile, "spark") }, camera)),
				new("View", () => Select(new[] { AttachmentViewEditing.CreateView(iMapManager, currentPendingTile) }, camera)),
				new("Pickup", () => Select(new[] { AttachmentPickupEditing.CreatePickup(iMapManager, currentPendingTile) }, camera)),
				PopupItem.Spacer(),
				new("Cancel", () => {}, colorOverride: Color.yellow)
			};

			var result = PopupMenu.Show(mouseDownPos, $"Add Attachment at tile {currentPendingTile}", items);
			if (!result && wasCancelled)
				Select(null, camera);
			return result;
		}

		private bool DrawDeletePopup()
		{
			var attsOnTile = GetAttachmentsOnTile(currentPendingTile);
			if (attsOnTile.Length == 0) return false;

			var wasCancelled = true;
			var items = new List<PopupItem>();

			foreach (var att in attsOnTile)
			{
				var localAtt = att;
				string label = att is Waypoint wp ? $"WP{wp.waypointIndex:00} at tile {currentPendingTile}" : $"Delete {att.GetType().Name}";
				items.Add(new PopupItem(label, () =>
				{
					iMapManager.RemoveAttachment(localAtt);
					Select(null, camera);
				}));
			}

			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () =>
				{
					iMapManager.RemoveAllAttachmentsOnTile(currentPendingTile);
					Select(null, camera);
				}, colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(mouseDownPos, "Delete Attachment(s)", items);
			if (!result && wasCancelled)
				Select(null, camera);
			return result;
		}

		private bool DrawSelectPopup()
		{
			var atts = GetAttachmentsOnTile(currentPendingTile);
			if (atts.Length == 0) return false;

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
					Select(new[] { att }, camera);
				}));
			}

			if (atts.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Select All", () =>
				{
					wasCancelled = false;
					Select(atts, camera);
				}, colorOverride: Color.green));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(mouseDownPos, $"Select ({atts.Length})", items);
			if (!result && wasCancelled)
				Select(null, camera);
			return result;
		}
	}
}