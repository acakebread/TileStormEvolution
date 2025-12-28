using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public abstract class AttachmentEditing
	{
		public static MapAttachment[] selectedAttachments = null;

		public static void RebuildMarkers(IMapManager iMapManager)
		{
			if (null == iMapManager?.CurrentMap) return;
			// Determine selected tile from current selection
			var selectedTile = (selectedAttachments != null && selectedAttachments.Length > 0) ? selectedAttachments[0].tile : -1;
			var tiles = iMapManager?.CurrentMap.attachments?.Where(a => a.tile >= 0).Select(a => a.tile).Distinct().ToArray() ?? System.Array.Empty<int>();
			var selection = System.Array.IndexOf(tiles, selectedTile);
			UpdateMapMarkers(iMapManager, tiles, selection, EditorMarkerUtil.MarkerType.Attachment);
		}

		public static void HideAllGizmos()
		{
			EditorPrimitiveUtil.HideCone();
			EditorFrustumUtil.Hide();
			EditorTransformUtil.HideTransformGizmo();
			ViewPreviewUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();
		}

		public static void Select(MapAttachment[] attachments, IMapManager mapManager, Camera camera)
		{
			selectedAttachments = null != attachments && null != attachments[0] ? attachments : null;
			HideAllGizmos();
			RebuildMarkers(mapManager);

			if (null == selectedAttachments || 1 != selectedAttachments.Length) return;
			HandleSelectionChanged(mapManager, camera);
			HandleGizmoInput(mapManager, camera);
		}

		public static bool DrawAddPopup(Vector2 position, IMapManager mapManager, Camera sceneCamera, int pendingTile)
		{
			var items = new List<PopupItem>
			{
				new ("Emitter [flame]", () => Select(new[] { AttachmentEmitterEditing.CreateEmitter(mapManager, pendingTile, "flame") }, mapManager, sceneCamera)),
				new ("Emitter [spark]", () => Select(new[] { AttachmentEmitterEditing.CreateEmitter(mapManager, pendingTile, "spark") }, mapManager, sceneCamera)),
				new ("View", () => Select(new[] { AttachmentViewEditing.CreateView(mapManager, pendingTile) }, mapManager, sceneCamera)),
				new ("Pickup", () => Select(new[] { AttachmentPickupEditing.CreatePickup(mapManager, pendingTile) }, mapManager, sceneCamera)),
				PopupItem.Spacer(),
				new ("Cancel", () => { }, colorOverride: Color.yellow)
			};
			return PopupMenu.Show(position, "Add Attachment", items);
		}

		public static bool DrawDeletePopup(Vector2 position, IMapManager mapManager, Camera sceneCamera, int pendingTile)
		{
			var attsOnTile = mapManager.CurrentMap.GetAttachmentsOnTile(pendingTile);
			if (attsOnTile.Length == 0) return false;

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
			return PopupMenu.Show(position, "Delete Attachment(s)", items);
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

		private static AttachmentEditing GetEditorFor(MapAttachment attachment) => attachment switch
		{
			null => null,
			Emitter => AttachmentEmitterEditing.Instance,
			View => AttachmentViewEditing.Instance,
			Pickup => AttachmentPickupEditing.Instance,
			_ => null // unknown attachment type
		};

		// return null unless uniform selections (e.g. type-specific GUI when all same type)
		private static AttachmentEditing GetEditorForSelection(MapAttachment[] selectedAttachments)
		{
			if (null == selectedAttachments || 0 == selectedAttachments.Length) return null;

			var firstType = selectedAttachments[0].GetType();
			if (selectedAttachments.All(att => att.GetType() == firstType))
				return GetEditorFor(selectedAttachments[0]);

			return null;
		}

		public static void UpdateMapMarkers(IMapManager mapManager, int[] tiles, int selectedIndex = -1, EditorMarkerUtil.MarkerType type = EditorMarkerUtil.MarkerType.Undefined)
		{
			if (tiles == null || tiles.Length == 0 || EditorMarkerUtil.SphereMesh == null)
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

		public static void HandleSelectionChanged(IMapManager mapManager, Camera camera) => GetEditorForSelection(selectedAttachments)?.OnHandleSelectionChanged(mapManager, camera);
		public static void HandleGizmoInput(IMapManager mapManager, Camera camera) => GetEditorForSelection(selectedAttachments)?.OnHandleGizmoInput(mapManager, camera);
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
			GetEditorFor(attachment)?.OnHandleDragInput(mapManager, attachment);
		}

		protected virtual void OnHandleSelectionChanged(IMapManager mapManager, Camera camera) { }
		protected virtual void OnHandleGizmoInput(IMapManager mapManager, Camera camera) { }
		protected virtual void OnHandleDragInput(IMapManager mapManager, MapAttachment attachment) { }
	}
}