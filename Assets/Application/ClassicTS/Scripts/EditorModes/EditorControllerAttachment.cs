using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		// ===================================================================
		// Pending state
		// ===================================================================
		private MapAttachment[] selection = null;
		private enum PendingAction { None, Add, Delete, Select }
		private PendingAction pendingAction = PendingAction.None;
		private int pendingTile = -1;

		// ===================================================================
		// Shared input state
		// ===================================================================
		private readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));

		// ===================================================================
		// Current mode - used for panel and marker styling
		// ===================================================================

		public enum Mode { Undefined, Mixed, Waypoint, Attachment }//there will be several new modes based on context
		private Mode currentMode = Mode.Undefined;

		// ===================================================================
		// Constructor
		// ===================================================================
		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		protected override bool IsMouseOverGUI() => base.IsMouseOverGUI() || sidePanel.IsMouseOver;

		// ===================================================================
		// Lifecycle
		// ===================================================================
		public override void OnMapLoaded()
		{
			base.OnMapLoaded();
			ResetInputState();
			RebuildMarkers();
		}

		public override void OnEnable()
		{
			base.OnEnable();
			currentMode = Mode.Attachment; // default to attachment mode
			ResetInputState();
			RebuildMarkers();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			ResetInputState();
		}

		// ===================================================================
		// Core Update
		// ===================================================================

		public override void Update()
		{
			base.Update();
			EditorTransformUtil.UpdateTransformGizmoVisuals(camera);
			ViewAttachmentHandler.HandlePreviewCameraSync(iMap, camera, selection);
		}

		public override void OnControl()
		{
			base.OnControl();

			if (Input.GetMouseButtonDown(0))
				HandleLeftMouseDown();

			if (Input.GetMouseButtonDown(1))
				HandleRightMouseDown();

			if (Input.GetMouseButton(0))
				HandleLeftMouseDrag();

			if (Input.GetMouseButton(1))
				HandleRightMouseDrag();

			if (Input.GetMouseButtonUp(0))
				HandleLeftMouseUp();

			if (Input.GetMouseButtonUp(1))
				HandleRightMouseUp();
		}

		// ===================================================================
		// GUI — currently only draws attachment panel (waypoint panel ready below)
		// ===================================================================
		public override void OnGUI()
		{
			base.OnGUI();

			// Draw correct panel based on currentMode
			if (currentMode == Mode.Waypoint)
				DrawSidePanelWaypoint();
			else
				DrawSidePanelAttachment(); // includes Undefined and Attachment

			if (pendingAction == PendingAction.None) return;

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
		// Input Handlers — now using currentMode
		// ===================================================================

		private void ThresholdCheck()
		{
			if (Vector3.Distance(Input.mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
				mouseMovedBeyondThreshold = true;
		}

		private void HandleLeftMouseDown()
		{
			if (EditorTransformUtil.MouseOverGizmo(camera)) 
				return;

			mouseDownPos = Input.mousePosition;
			mouseMovedBeyondThreshold = false;
			pendingTile = iMap.CameraHitTile(camera, Input.mousePosition);

			bool noAttachmentHere = pendingTile < 0 || iMap.GetAttachments(tileIndex: pendingTile).Length == 0;
			if (noAttachmentHere && ShouldStartPanningOnLeftClick())
				StartPanning();
			else
				isPanning = false;

			if (-1 != pendingTile)
			{
				var alreadySelected = selection?.Length > 0 && selection[0].tile == pendingTile;
				if (!alreadySelected) Select(iMap.GetAttachments(tileIndex: pendingTile));
				return;
			}
			Select();
		}

		private void HandleRightMouseDown()
		{
			mouseDownPos = Input.mousePosition;
			mouseMovedBeyondThreshold = false;
			pendingTile = iMap.CameraHitTile(camera, Input.mousePosition);
		}

		private void HandleLeftMouseDrag()
		{
			if (EditorTransformUtil.HandleTransformGizmoInput(camera))
			{
				HandleGizmoInput();
				return;
			}

			ThresholdCheck();

			if (isPanning)
				UpdatePan();

			var tile = iMap.CameraHitTile(camera, Input.mousePosition);
			if (tile == pendingTile || -1 == tile || null == selection || 0 == selection.Length)
				return;

			pendingTile = tile;
			if (null == selection) return;
			foreach (var att in selection)
			{
				att.tile = pendingTile;
				iMap.RefreshAttachment(att);
			}
			HandleDragInput();
			RebuildMarkers();

			void HandleGizmoInput()
			{
				if (null == selection || 0 == selection.Length) return;
				var firstType = selection[0].GetType();
				if (!selection.All(a => a.GetType() == firstType)) return;
				selection[0].OnGizmoInput(iMap, camera, selection);
			}

			void HandleDragInput()
			{
				if (null == selection || 1 != selection.Length) return;
				if (selection[0] is ITransformableAttachment transformable)
				{
					var worldPos = iMap.WorldPosition(selection[0].tile, transformable.Position);
					var worldRot = iMap.WorldRotation(selection[0].tile, transformable.Rotation);
					EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
				}
				selection[0].OnDragInput(iMap, selection);
			}
		}

		private void HandleRightMouseDrag() => ThresholdCheck();

		private void HandleLeftMouseUp()
		{
			isPanning = false;

			if (mouseMovedBeyondThreshold)
				return;

			var attachmentsOnTile = iMap.GetAttachments(tileIndex: pendingTile);

			if (null == attachmentsOnTile || 0 == attachmentsOnTile.Length)
			{
				if (-1 != pendingTile)
					pendingAction = PendingAction.Add;
			}
			else if (attachmentsOnTile.Length > 1)
			{
				pendingAction = PendingAction.Select;
			}
			else
			{
				pendingAction = PendingAction.None;
				Select(attachmentsOnTile);
			}

			RebuildMarkers();
		}

		private void HandleRightMouseUp()
		{
			if (mouseMovedBeyondThreshold)
				return;

			var tile = iMap.CameraHitTile(camera, Input.mousePosition);
			if (tile >= 0 && iMap.GetAttachments(tileIndex: tile).Length > 0)
			{
				pendingTile = tile;
				pendingAction = PendingAction.Delete;
				Select(iMap.GetAttachments(tileIndex: pendingTile));
				return;
			}
			Select();
		}

		// ===================================================================
		// Selection & Gizmos
		// ===================================================================

		private void Select(MapAttachment attachment) => Select(attachment == null ? null : new[] { attachment });

		private void Select(MapAttachment[] attachments = null)
		{
			selection = attachments?.Length > 0 ? attachments : null;

			if (null != selection && selection.Length > 0)
			{
				if (selection == null || selection.Length == 0)
				{
					// Do nothing — keep currentMode as is (panel stays)
				}
				else if (selection.Length == 1)
				{
					currentMode = selection[0] is Waypoint ? Mode.Waypoint : Mode.Attachment;
				}
				else // Length > 1
				{
					currentMode = Mode.Attachment;
				}
			}

			ViewPreviewUtil.Hide();
			HideAllGizmos();
			RebuildMarkers();

			if (null == selection || 1 != selection.Length) return;
			HandleSelectionChanged();

			void HandleSelectionChanged()
			{
				if (null == selection || 0 == selection.Length) return;
				var firstType = selection[0].GetType();
				if (!selection.All(a => a.GetType() == firstType)) return;
				selection[0].OnSelectionChanged(iMap, camera, selection);
			}
		}

		// ===================================================================
		// Helpers
		// ===================================================================
		private void ResetInputState()
		{
			currentMode = Mode.Undefined;
			selection = null;
			pendingAction = PendingAction.None;
			pendingTile = -1;
			HideAllGizmos();
		}

		private void HideAllGizmos()
		{
			EditorTransformUtil.Hide();
			EditorPrimitiveUtil.Hide();
			EditorFrustumUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();
		}

		private void RebuildMarkers()
		{
			// GetAttachments() already returns only valid attachments (tile >= 0)
			var tiles = iMap?.GetAttachments()
							?.Select(a => a.tile)
							?.Distinct()
							?.ToArray()
							?? Array.Empty<int>();

			if (tiles.Length == 0)
			{
				EditorMarkerUtil.ClearMapMarkers();
				return;
			}

			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];

			bool isWaypointMode = currentMode == Mode.Waypoint;

			for (int i = 0; i < tiles.Length; i++)
			{
				int tile = tiles[i];
				positions[i] = iMap.TileWorldPosition(tile);

				colors[i] = isWaypointMode && iMap.HasAttachmentOfType<View>(tile)
					? new Color(0f, 1f, 1f, 0.5f)
					: new Color(0f, 0.7f, 1f, 0.7f);
			}

			var selectedTile = (selection != null && selection.Length > 0) ? selection[0].tile : -1;
			var selectedIndex = Array.IndexOf(tiles, selectedTile);

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		// ===================================================================
		// Side Panels
		// ===================================================================
		private void DrawSidePanelAttachment()
		{
			// Use GetAttachments() — already clean and valid
			var atts = iMap?.GetAttachments() ?? Array.Empty<MapAttachment>();

			var items = new List<ListViewItem>();

			foreach (var att in atts)
			{
				items.Add(new(
					GetAttachmentLabel(att),
					(_) => Select(att),
					selected: selection != null && selection.Contains(att)
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

		private void DrawSidePanelWaypoint()
		{
			var selectedWaypoint = selection?.Length > 0 ? selection[0] as Waypoint : null;

			// Get only waypoints, sorted by their waypointIndex
			var waypointAttachments = iMap.GetAttachments(filterTypes: new[] { typeof(Waypoint) })
										  .Cast<Waypoint>()
										  .OrderBy(wp => wp.waypointIndex)
										  .ToArray();

			var items = new List<ListViewItem>();

			for (var i = 0; i < waypointAttachments.Length; i++)
			{
				var waypoint = waypointAttachments[i];

				items.Add(new ListViewItem(
					label: $"WP{waypoint.waypointIndex:00} [tile {waypoint.tile}]",
					onClick: (_) => Select(waypoint),
					selected: selectedWaypoint?.waypointIndex == waypoint.waypointIndex
				));
			}

			sidePanel.List.SetItems(items);

			sidePanel.Buttons.Clear();

			var canMoveUp = selectedWaypoint != null && selectedWaypoint.waypointIndex > 0;
			var canMoveDown = selectedWaypoint != null && selectedWaypoint.waypointIndex < waypointAttachments.Length - 1;

			sidePanel.Buttons.Add(new("Move Up", () => MoveWaypoint(selectedWaypoint, -1), enabled: canMoveUp));
			sidePanel.Buttons.Add(new("Move Down", () => MoveWaypoint(selectedWaypoint, +1), enabled: canMoveDown));

			sidePanel.Draw();
		}

		private void MoveWaypoint(Waypoint wp, int direction)
		{
			if (wp == null) return;

			var oldIndex = wp.waypointIndex;
			var newIndex = oldIndex + direction;

			// Get current sorted waypoints
			var currentWaypoints = iMap.GetWaypoints();  // using extension

			if (newIndex < 0 || newIndex >= currentWaypoints.Length) return;

			var targetWp = currentWaypoints[newIndex];

			// Swap waypointIndex values on the objects
			wp.waypointIndex = newIndex;
			targetWp.waypointIndex = oldIndex;

			var movedWaypoint = new Waypoint(newIndex, wp.tile);
			Select(movedWaypoint);

			RebuildMarkers();
		}

		// ===================================================================
		// Popups
		// ===================================================================
		private bool DrawAddPopup()
		{
			var waypoints = iMap.GetWaypoints();
			var items = new List<PopupItem>
			{
				new($"Waypoint [WP{waypoints?.Length:00}]", () => Select(WaypointAttachmentHandler.Create(iMap, pendingTile)), colorOverride: Color.lightSteelBlue),
				new("Emitter [flame]", () => Select(EmitterAttachmentHandler.Create(iMap, pendingTile, "flame")), colorOverride: Color.cyan),
				new("Emitter [spark]", () => Select(EmitterAttachmentHandler.Create(iMap, pendingTile, "spark")), colorOverride: Color.cyan),
				new("View", () => Select(ViewAttachmentHandler.Create(iMap, pendingTile)), colorOverride: Color.cyan),
				new("Pickup", () => Select(PickupAttachmentHandler.Create(iMap, pendingTile)), colorOverride: Color.cyan),
				PopupItem.Spacer(),
				new("Cancel", () => {}, colorOverride: Color.yellow)
			};

			var result = PopupMenu.Show(mouseDownPos, $"Add Attachment at tile {pendingTile}", items);

			if (result == PopupResult.ClosedByAction)
				return false; // action already invoked inside popup

			if (result == PopupResult.ClosedByClickOutside || result == PopupResult.ClosedByCancel)
				Select(); // explicit deselect

			return result == PopupResult.StillOpen;
		}

		private bool DrawDeletePopup()
		{
			var attsOnTile = iMap.GetAttachments(tileIndex: pendingTile);
			if (attsOnTile.Length == 0) return false;

			var items = new List<PopupItem>();

			foreach (var att in attsOnTile)
			{
				var localAtt = att;
				string label = att is Waypoint wp ? $"Delete WP{wp.waypointIndex:00} [{pendingTile}]" : $"Delete {att.GetType().Name} [{pendingTile}]";
				items.Add(new PopupItem(label, () => iMap.RemoveAttachment(localAtt), colorOverride: Color.softRed));
			}

			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () => iMap.RemoveAttachments(attsOnTile), colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(mouseDownPos, "Delete Attachment" + (attsOnTile.Length > 1 ? "(s)" : ""), items);
			if (result != PopupResult.StillOpen) Select();
			return result == PopupResult.StillOpen;
		}

		private bool DrawSelectPopup()
		{
			var atts = iMap.GetAttachments(tileIndex: pendingTile);
			if (atts.Length == 0) return false;

			var items = new List<PopupItem>();

			foreach (var att in atts)
			{
				string label = att.GetType().Name;
				if (att is Emitter e && e.LookAt != null && e.LookAt != Vector3.up)
					label += $" to {e.LookAt.magnitude:F1}";
				label += $" [tile {att.tile}]";

				items.Add(new (label, () => Select(att)));
			}

			if (atts.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new ("Select All", () => Select(atts), colorOverride: Color.green));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(mouseDownPos, $"Select ({atts.Length})", items);

			if (result == PopupResult.ClosedByAction)
				return false; // action already invoked inside popup

			if (result == PopupResult.ClosedByClickOutside || result == PopupResult.ClosedByCancel)
				Select(); // explicit deselect

			return result == PopupResult.StillOpen;
		}
	}
}