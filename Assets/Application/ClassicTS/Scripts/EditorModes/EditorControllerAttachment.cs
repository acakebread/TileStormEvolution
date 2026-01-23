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
		private Vector3 mouseDownPos;
		private bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 8f;
		private bool rmbDownInPreview = false;

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
				ViewAttachmentHandler.HandlePreviewCameraSync(iMapManager, camera, selection);
				return;
			}

			ViewPreviewUtil.Update();

			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			if (EditorTransformUtil.HandleTransformGizmoInput(camera))
			{
				HandleGizmoInput();
				return;
			}

			if (WasGuiActiveLastFrame)
			{
				return; // Skip input this frame — GUI consumed it last frame
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
				HandleMouseDown();

			if (Input.GetMouseButton(0))
				HandleDrag();

			if (Input.GetMouseButtonUp(0) && !mouseMovedBeyondThreshold)
				HandleLeftMouseUp();

			if (Input.GetMouseButtonUp(1) && !mouseMovedBeyondThreshold)
				HandleRightMouseUp();
		}

		// ===================================================================
		// GUI — currently only draws attachment panel (waypoint panel ready below)
		// ===================================================================
		public override void OnGUI()
		{
			ViewPreviewUtil.OnGUI();

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
		private void HandleMouseDown()
		{
			pendingTile = iMapManager.CameraHitTile(camera, Input.mousePosition);

			if (-1 != pendingTile)
			{
				var alreadySelected = selection?.Length > 0 && selection[0].tile == pendingTile;
				if (!alreadySelected) Select(GetAttachmentsOnTile(pendingTile));
				return;
			}
			Select();
		}

		private void HandleLeftMouseUp()
		{
			var attachmentsOnTile = GetAttachmentsOnTile(pendingTile);

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
			var tile = iMapManager.CameraHitTile(camera, Input.mousePosition);
			if (tile >= 0 && GetAttachmentsOnTile(tile)?.Length > 0)
			{
				pendingTile = tile;
				pendingAction = PendingAction.Delete;
				return;
			}
			Select();
		}

		private void HandleDrag()
		{
			var tile = iMapManager.CameraHitTile(camera, Input.mousePosition);
			if (tile == pendingTile || -1 == tile || null == selection || 0 == selection.Length)
				return;

			pendingTile = tile;
			if (null == selection) return;
			foreach (var att in selection)
			{
				att.tile = pendingTile;
				iMapManager.RefreshAttachmentInstance(att);
			}
			HandleDragInput();
			RebuildMarkers();

			void HandleDragInput()
			{
				if (null == selection || 1 != selection.Length) return;
				if (selection[0] is ITransformableAttachment transformable)
				{
					var worldPos = MapManager.WorldPosition(selection[0].tile, transformable.Position);
					var worldRot = MapManager.WorldRotation(selection[0].tile, transformable.Rotation);
					EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
				}
				selection[0].OnDragInput(iMapManager, selection);
			}
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

			HideAllGizmos();
			RebuildMarkers();

			if (null == selection || 1 != selection.Length) return;
			HandleSelectionChanged();
			HandleGizmoInput();

			void HandleSelectionChanged()
			{
				if (null == selection || 0 == selection.Length) return;
				var firstType = selection[0].GetType();
				if (!selection.All(a => a.GetType() == firstType)) return;
				selection[0].OnSelectionChanged(iMapManager, camera, selection);
			}
		}

		private void HandleGizmoInput()
		{
			if (null == selection || 0 == selection.Length) return;
			var firstType = selection[0].GetType();
			if (!selection.All(a => a.GetType() == firstType)) return;
			selection[0].OnGizmoInput(iMapManager, camera, selection);
		}

		// ===================================================================
		// Helpers
		// ===================================================================
		private void ResetInputState()
		{
			rmbDownInPreview = false;
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
			ViewPreviewUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();
		}

		private MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			if (null == currentMap || !currentMap.IsValidTile(tileIndex)) return Array.Empty<MapAttachment>();
			return currentMap.Attachments?.Where(x => x.tile == tileIndex).ToArray() ?? Array.Empty<MapAttachment>();
		}

		private void RebuildMarkers()
		{
			var tiles = iMapManager?.GetAllAttachments()?.Where(a => a.tile >= 0).Select(a => a.tile).Distinct().ToArray() ?? null;
			if (null == tiles)
			{
				EditorMarkerUtil.ClearMapMarkers();
				return;
			}

			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];

			for (var i = 0; i < tiles.Length; i++)
			{
				var tile = tiles[i];
				positions[i] = tile < 0 || tile >= iMapManager.Count ? Vector3.zero : iMapManager.TileWorldPosition(tile);

				var hasView = currentMode == Mode.Waypoint && null != iMapManager.GetView(tile);
				colors[i] = hasView ? new Color(0f, 1f, 1f, 0.5f) : new Color(0f, 0.7f, 1f, 0.7f);
			}

			var selectedTile = (null != selection && selection.Length > 0) ? selection[0].tile : -1;
			var selectedIndex = Array.IndexOf(tiles, selectedTile);
			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		// ===================================================================
		// Side Panels
		// ===================================================================
		private void DrawSidePanelAttachment()
		{
			var atts = iMapManager?.GetAllAttachments() ?? Array.Empty<MapAttachment>();
			var items = new List<ListViewItem>();

			foreach (var att in atts)
				items.Add(new(GetAttachmentLabel(att), (_) => Select(att), selected: null != selection && selection.Contains(att)));

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

			var items = new List<ListViewItem>();
			var waypointAttachments = iMapManager.waypointAttachments;//cached

			for (var i = 0; i < waypointAttachments.Length; i++)
			{
				var waypoint = waypointAttachments.FirstOrDefault(w => w.waypointIndex == i);
				items.Add(new(label: $"WP{i:00} [tile {waypointAttachments[i].tile}]", onClick: (_) =>
				{
					if (null != waypoint) Select(waypoint);
				}, selected: selectedWaypoint?.waypointIndex == i));
			}

			sidePanel.List.SetItems(items);

			sidePanel.Buttons.Clear();

			var canMoveUp = null != selectedWaypoint && selectedWaypoint.waypointIndex > 0;
			var canMoveDown = null != selectedWaypoint && selectedWaypoint.waypointIndex >= 0 && selectedWaypoint.waypointIndex < waypointAttachments.Length - 1;

			sidePanel.Buttons.Add(new("Move Up", () => MoveWaypoint(selectedWaypoint, -1), enabled: canMoveUp));
			sidePanel.Buttons.Add(new("Move Down", () => MoveWaypoint(selectedWaypoint, +1), enabled: canMoveDown));

			sidePanel.Draw();

			void MoveWaypoint(Waypoint wp, int direction)
			{
				if (null == wp) return;

				var oldIndex = wp.waypointIndex;
				var newIndex = oldIndex + direction;
				if (newIndex < 0 || newIndex >= waypointAttachments.Length) return;

				var list = currentMap.Waypoints.ToList();
				(list[oldIndex], list[newIndex]) = (list[newIndex], list[oldIndex]);
				currentMap.Waypoints = list.ToArray();

				var movedWaypoint = new Waypoint(newIndex, list[newIndex]);
				Select(movedWaypoint);
				RebuildMarkers();
			}
		}

		// ===================================================================
		// Popups
		// ===================================================================
		private bool DrawAddPopup()
		{
			var items = new List<PopupItem>
			{
				new($"Waypoint [WP{currentMap.Waypoints.Length:00}]", () => Select(WaypointAttachmentHandler.Create(iMapManager, pendingTile)), colorOverride: Color.lightSteelBlue),
				new("Emitter [flame]", () => Select(EmitterAttachmentHandler.Create(iMapManager, pendingTile, "flame")), colorOverride: Color.cyan),
				new("Emitter [spark]", () => Select(EmitterAttachmentHandler.Create(iMapManager, pendingTile, "spark")), colorOverride: Color.cyan),
				new("View", () => Select(ViewAttachmentHandler.Create(iMapManager, pendingTile)), colorOverride: Color.cyan),
				new("Pickup", () => Select(PickupAttachmentHandler.Create(iMapManager, pendingTile)), colorOverride: Color.cyan),
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
			var attsOnTile = GetAttachmentsOnTile(pendingTile);
			if (0 == attsOnTile.Length) return false;

			var items = new List<PopupItem>();

			foreach (var att in attsOnTile)
			{
				var localAtt = att;
				string label = att is Waypoint wp ? $"Delete WP{wp.waypointIndex:00} [{pendingTile}]" : $"Delete {att.GetType().Name} [{pendingTile}]";
				items.Add(new PopupItem(label, () => iMapManager.RemoveAttachment(localAtt), colorOverride: Color.softRed));
			}

			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () => iMapManager.RemoveAttachments(attsOnTile), colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(mouseDownPos, "Delete Attachment" + (attsOnTile.Length > 1 ? "(s)" : ""), items);
			if (result != PopupResult.StillOpen) Select();
			return result == PopupResult.StillOpen;
		}

		private bool DrawSelectPopup()
		{
			var atts = GetAttachmentsOnTile(pendingTile);
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