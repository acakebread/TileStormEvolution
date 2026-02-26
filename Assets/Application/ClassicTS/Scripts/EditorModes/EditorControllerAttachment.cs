using System;
using UnityEngine;
using System.Linq;
using static MassiveHadronLtd.GuiUtils;
using MassiveHadronLtd;

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
		private Vector3 popupPos;

		// ===================================================================
		// Shared input state
		// ===================================================================
		private readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));

		// ===================================================================
		// Current mode - used for panel and marker styling
		// ===================================================================

		public enum Mode { Undefined, Waypoint, Attachment }//there will be several new modes based on context
		private Mode currentMode => null == selection ? Mode.Undefined : selection.Length > 1 ? Mode.Attachment : selection[0] is Waypoint ? Mode.Waypoint : Mode.Attachment;

		// ===================================================================
		// Constructor
		// ===================================================================
		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		protected override bool IsMouseOverGUI() => base.IsMouseOverGUI() || (sidePanel.IsMouseOver && null != selection);

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
			//currentMode = Mode.Attachment; // default to attachment mode
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

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);

			if (InputX.GetMouseButtonDown(0))
				HandleLeftMouseDown();

			if (InputX.GetMouseButtonDown(1))
				HandleRightMouseDown();

			if (InputX.GetMouseButton(0))
				HandleLeftMouseDrag();

			if (InputX.GetMouseButton(1))
				HandleRightMouseDrag();

			if (!staticClick)
				return;

			if (InputX.GetMouseButtonUp(0))
				HandleLeftMouseUp();

			if (InputX.GetMouseButtonUp(1))
				HandleRightMouseUp();
		}

		// ===================================================================
		// Input Handlers — now using currentMode
		// ===================================================================

		private void HandleLeftMouseDown()
		{
			pendingTile = iMap.CameraHitTile(camera, InputX.mousePosition);

			bool noAttachmentHere = pendingTile < 0 || iMap.GetAttachments(tileIndex: pendingTile).Length == 0;
			if (noAttachmentHere && ShouldStartPanningOnLeftClick())
				StartPanning();

			if (-1 != pendingTile)
			{
				var alreadySelected = selection?.Length > 0 && selection[0].tile == pendingTile;
				if (!alreadySelected) Select(iMap.GetAttachments(tileIndex: pendingTile));
				return;
			}
			Select();
		}

		private void HandleRightMouseDown() => pendingTile = iMap.CameraHitTile(camera, InputX.mousePosition);

		private void HandleLeftMouseDrag()
		{
			var tile = iMap.CameraHitTile(camera, InputX.mousePosition);
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

		protected override void HandleGizmoInput()
		{
			if (null == selection || 0 == selection.Length) return;
			var firstType = selection[0].GetType();
			if (!selection.All(a => a.GetType() == firstType)) return;
			selection[0].OnGizmoInput(iMap, camera, selection);
		}

		private void HandleRightMouseDrag() { }

		private void HandleLeftMouseUp()
		{
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
			var tile = iMap.CameraHitTile(camera, InputX.mousePosition);
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
			popupPos = InputX.mousePosition;

			selection = attachments?.Length > 0 ? attachments : null;

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

			var isWaypointMode = currentMode == Mode.Waypoint;

			for (int i = 0; i < tiles.Length; i++)
			{
				int tile = tiles[i];
				positions[i] = iMap.TileRenderPosition(tile);

				colors[i] = isWaypointMode && iMap.HasAttachmentOfType<View>(tile)
					? new Color(0f, 1f, 1f, 0.5f)
					: new Color(0f, 0.7f, 1f, 0.7f);
			}

			var selectedTile = (selection != null && selection.Length > 0) ? selection[0].tile : -1;
			var selectedIndex = Array.IndexOf(tiles, selectedTile);

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
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

		public override void OnGUI()
		{
			base.OnGUI();

			if (selection != null && selection.Length >= 1)
			{
				if (currentMode == Mode.Waypoint)
				{
					EditorAttachmentUI.DrawSidePanelWaypoint(
						sidePanel,
						iMap,
						selection,
						(wp, dir) => MoveWaypoint(wp, dir)
					);
				}
				else
				{
					EditorAttachmentUI.DrawSidePanelAttachment(
						sidePanel,
						iMap,
						selection,
						att => Select(att)
					);
				}
			}
			else
			{
				sidePanel.Update();
			}

			if (pendingAction == PendingAction.None) return;

			switch (pendingAction)
			{
				case PendingAction.Add:
					if (EditorAttachmentUI.DrawAddPopup(
							popupPos, iMap, pendingTile,
							created => Select(created)))
						return;
					break;

				case PendingAction.Delete:
					if (EditorAttachmentUI.DrawDeletePopup(
							popupPos, iMap, pendingTile,
							() => Select()))
						return;
					break;

				case PendingAction.Select:
					if (EditorAttachmentUI.DrawSelectPopup(
							popupPos, iMap, pendingTile,
							att => Select(att),
							atts => Select(atts),
							() => Select()))
						return;
					break;
			}

			pendingAction = PendingAction.None;
		}
	}
}