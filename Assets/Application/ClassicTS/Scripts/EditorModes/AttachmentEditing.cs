using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public static class AttachmentEditing
	{
		public static MapAttachment[] selectedAttachments = null;

		// Shared across both attachment and waypoint editing modes
		public static int CurrentPendingTile { get; set; } = -1;

		public static void RebuildMarkers(IMapManager iMapManager, EditorMarkerUtil.MarkerType type = EditorMarkerUtil.MarkerType.Undefined)
		{
			if (null == iMapManager?.CurrentMap) return;
			// Determine selected tile from current selection
			var selectedTile = (selectedAttachments != null && selectedAttachments.Length > 0) ? selectedAttachments[0].tile : -1;
			var tiles = type switch
			{
				EditorMarkerUtil.MarkerType.Waypoint => iMapManager?.CurrentMap.waypoints,
				EditorMarkerUtil.MarkerType.Attachment => iMapManager?.CurrentMap.attachments?.Where(a => a.tile >= 0).Select(a => a.tile).Distinct().ToArray() ?? System.Array.Empty<int>(),
				_ => null
			};
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
				new ("Emitter [flame]", () => Select(new[] { AttachmentEmitterEditing.CreateEmitter(mapManager, pendingTile, "flame") }, mapManager, sceneCamera)),
				new ("Emitter [spark]", () => Select(new[] { AttachmentEmitterEditing.CreateEmitter(mapManager, pendingTile, "spark") }, mapManager, sceneCamera)),
				new ("View", () => Select(new[] { AttachmentViewEditing.CreateView(mapManager, pendingTile) }, mapManager, sceneCamera)),
				new ("Pickup", () => Select(new[] { AttachmentPickupEditing.CreatePickup(mapManager, pendingTile) }, mapManager, sceneCamera)),
				PopupItem.Spacer(),
				new ("Cancel", () => { }, colorOverride: Color.yellow)
			};
			var result = PopupMenu.Show(position, "Add Attachment", items);
			if (!result && wasCancelled)
				Select(null, mapManager, sceneCamera);
			return result;
		}

		public static bool DrawDeletePopup(Vector2 position, IMapManager mapManager, Camera sceneCamera, int pendingTile)
		{
			var attsOnTile = mapManager.CurrentMap.GetAttachmentsOnTile(pendingTile);
			if (attsOnTile.Length == 0) return false;

			var wasCancelled = true;
			var items = new List<PopupItem>();
			foreach (var att in attsOnTile)
			{
				string label = $"Delete {att.GetType().Name}";
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
			var atts = mapManager.CurrentMap.GetAttachmentsOnTile(pendingTile);
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
				switch (att)//this will move to IMapManager when is supports Waypoint classes directly
				{
					case Waypoint:
						mapManager.CurrentMap.waypoints[(att as Waypoint).waypointIndex] = tile;
						break;
					case Emitter:
					case Pickup:
					case View:
						mapManager.RefreshAttachmentInstance(att);
						break;
				};
			}
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
	}
}