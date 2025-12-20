using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public abstract class AttachmentEditing
	{
		public static void HandleGizmoInput(EditorControllerAttachment editor) => GetEditorForSelection(editor.selectedAttachments)?.OnHandleGizmoInput(editor);
		protected virtual void OnHandleGizmoInput(EditorControllerAttachment editor) { }

		public static void HandleSelectionChanged(EditorControllerAttachment editor) => GetEditorForSelection(editor.selectedAttachments)?.OnHandleSelectionChanged(editor);
		protected virtual void OnHandleSelectionChanged(EditorControllerAttachment editor) { }

		protected virtual void OnRefreshDragVisuals(EditorControllerAttachment editor, MapAttachment attachment) { }
		protected virtual void OnUpdateDragGizmo(EditorControllerAttachment editor, MapAttachment attachment)
		{
			if (attachment is not ITransformableAttachment transformable)
				return;

			var worldPos = MapManager.WorldPosition(attachment.tile, transformable.Position);
			var worldRot = MapManager.WorldRotation(attachment.tile, transformable.Rotation);
			EditorTransformUtil.ShowAt(worldPos, worldRot, editor.camera);
		}

		public static void DrawAddPopup(EditorControllerAttachment editor, Vector2 position)
		{
			var items = new List<PopupItem>
			{
				new ("Emitter [flame]", () => AttachmentEmitterEditing.Instance.AddNewEmitter(editor, editor.PendingTile, "flame")),
				new ("Emitter [spark]", () => AttachmentEmitterEditing.Instance.AddNewEmitter(editor, editor.PendingTile, "spark")),
				new ("View", () => AttachmentViewEditing.Instance.AddNewView(editor, editor.PendingTile)),
				new ("Pickup", () => AttachmentPickupEditing.Instance.AddNewPickup(editor, editor.PendingTile)),
				PopupItem.Spacer(),
				new ("Cancel", colorOverride: Color.yellow)
			};

			var closed = PopupMenu.Show(position, "Add Attachment", items);
			if (closed)
				editor.ClearPendingAction();
		}

		public static void DrawDeletePopup(EditorControllerAttachment editor, Vector2 position)
		{
			var map = editor.currentMap;
			if (map == null) return;

			var attsOnTile = map.GetAttachmentsOnTile(editor.PendingTile);
			if (attsOnTile.Length == 0) return;

			var items = new List<PopupItem>();

			foreach (var att in attsOnTile)
			{
				string label = $"Delete {att.GetType().Name}";
				var localAtt = att;
				items.Add(new PopupItem(label, () =>
				{
					editor.iMapManager.RemoveAttachment(localAtt);
					editor.SelectAttachments(null);
				}));
			}

			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () =>
				{
					editor.iMapManager.RemoveAllAttachmentsOnTile(editor.PendingTile);
					editor.SelectAttachments(null);
				}, colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", colorOverride: Color.yellow));

			var closed = PopupMenu.Show(position, "Delete Attachment(s)", items);
			if (closed)
				editor.ClearPendingAction();
		}

		public static void DrawSelectPopup(EditorControllerAttachment editor, Vector2 position)
		{
			var map = editor.currentMap;
			var atts = map.GetAttachmentsOnTile(editor.PendingTile);
			if (atts == null || atts.Length == 0) return;
			var wasCancelled = true;//sentinel to clear selection when mouse off menu

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
					editor.SelectAttachments(new MapAttachment[] { att });
				}));
			}

			if (atts.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Select All", () =>
				{
					wasCancelled = false;
					editor.SelectAttachments(atts);
				}, colorOverride: Color.green));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", colorOverride: Color.yellow));

			var closed = PopupMenu.Show(position, $"Select ({atts.Length})", items);
			if (closed)
			{
				if (wasCancelled) editor.SelectAttachments(null);
				editor.ClearPendingAction();
			}
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

		public static void RefreshDragVisuals(EditorControllerAttachment editor)
		{
			if (null == editor?.selectedAttachments || 1 != editor.selectedAttachments.Length) return;
			var att = editor.selectedAttachments[0];
			var typeEditor = GetEditorFor(att);
			typeEditor?.OnRefreshDragVisuals(editor, att);
			typeEditor?.OnUpdateDragGizmo(editor, att);
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