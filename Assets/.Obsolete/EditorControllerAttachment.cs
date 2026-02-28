using System;
using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		// ===================================================================
		// Pending state — only tile stays here
		// ===================================================================
		private int pendingTile = -1;
		private MapAttachment[] selection = null;

		// ===================================================================
		// Constructor
		// ===================================================================
		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		protected override bool IsMouseOverGUI() => base.IsMouseOverGUI() || EditorAttachmentUI.sidePanel.IsMouseOver;

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
			if (!camera) return;

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
		// Input Handlers
		// ===================================================================
		private void HandleLeftMouseDown()
		{
			pendingTile = iMap.CameraHitTile(camera, InputX.mousePosition);

			if (pendingTile < 0 || iMap.GetAttachments(tileIndex: pendingTile).Length == 0)//no attachment here
				StartPanning();

			if (-1 != pendingTile)
			{
				if (null == selection || selection.Length == 0 || selection[0].tile != pendingTile)
					Select(iMap.GetAttachments(tileIndex: pendingTile));
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
					EditorAttachmentUI.RequestAdd();
			}
			else if (attachmentsOnTile.Length > 1)
			{
				EditorAttachmentUI.RequestSelect();
			}
			else
			{
				EditorAttachmentUI.ClearPending();
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
				EditorAttachmentUI.RequestDelete();
				Select(iMap.GetAttachments(tileIndex: pendingTile));
				return;
			}
			Select();
		}

		// ===================================================================
		// Selection & Gizmos
		// ===================================================================

		private void Select(MapAttachment[] attachments = null)
		{
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
			pendingTile = -1;
			EditorAttachmentUI.ClearPending();
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
			var tiles = iMap?.GetAttachments()?.Select(a => a.tile)?.Distinct()?.ToArray() ?? Array.Empty<int>();

			if (tiles.Length == 0)
			{
				EditorMarkerUtil.ClearMapMarkers();
				return;
			}

			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];

			var isWaypointMode = null != selection && selection.Length == 1 && selection[0] is Waypoint;

			for (var i = 0; i < tiles.Length; i++)
			{
				var tile = tiles[i];
				positions[i] = iMap.TileRenderPosition(tile);

				colors[i] = isWaypointMode && iMap.HasAttachmentOfType<View>(tile)
					? new Color(0f, 1f, 1f, 0.5f)
					: new Color(0f, 0.7f, 1f, 0.7f);
			}

			var selectedTile = (selection != null && selection.Length > 0) ? selection[0].tile : -1;
			var selectedIndex = Array.IndexOf(tiles, selectedTile);

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		public override void OnGUI()
		{
			base.OnGUI();
			EditorAttachmentUI.UpdateGUI(iMap, selection, atts => Select(atts), pendingTile);
		}
	}
}