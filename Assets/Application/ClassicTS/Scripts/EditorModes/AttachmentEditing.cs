using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public abstract class AttachmentEditing
	{
		public readonly struct AttachmentEditContext
		{
			public readonly IMapManager MapManager;
			public readonly Camera SceneCamera;
			public readonly int PendingTile;
			public readonly Map CurrentMap;
			public readonly System.Action ClearPendingAction;

			public AttachmentEditContext(IMapManager mapManager, Camera sceneCamera, int pendingTile, Map currentMap, System.Action clearPendingAction)
			{
				MapManager = mapManager;
				SceneCamera = sceneCamera;
				PendingTile = pendingTile;
				CurrentMap = currentMap;
				ClearPendingAction = clearPendingAction;
			}
		}

		public static MapAttachment[] selectedAttachments = null;

		public static void RebuildMarkers(IMapManager iMapManager)
		{
			if (null == iMapManager?.CurrentMap) return;
			var tiles = iMapManager?.CurrentMap.attachments?.Where(a => a.tile >= 0).Select(a => a.tile).Distinct().ToArray() ?? System.Array.Empty<int>();

			// Determine selected tile from current selection
			var selectedTile = (selectedAttachments != null && selectedAttachments.Length > 0) ? selectedAttachments[0].tile : -1;
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
			selectedAttachments = attachments;
			HideAllGizmos();
			RebuildMarkers(mapManager);

			if (attachments == null || attachments.Length != 1) return;

			HandleSelectionChanged(mapManager, camera);
			HandleGizmoInput(mapManager, camera);
		}

		public static void HandleGizmoInput(IMapManager mapManager, Camera camera)
			=> GetEditorForSelection(selectedAttachments)?.OnHandleGizmoInput(mapManager, camera);

		protected virtual void OnHandleGizmoInput(IMapManager mapManager, Camera camera) { }

		public static void HandleSelectionChanged(IMapManager mapManager, Camera camera)
			=> GetEditorForSelection(selectedAttachments)?.OnHandleSelectionChanged(mapManager, camera);

		protected virtual void OnHandleSelectionChanged(IMapManager mapManager, Camera camera) { }

		protected virtual void OnRefreshDragVisuals(IMapManager mapManager, MapAttachment attachment) { }
		protected virtual void OnUpdateDragGizmo(MapAttachment attachment, Camera camera)
		{
			if (attachment is not ITransformableAttachment transformable)
				return;

			var worldPos = MapManager.WorldPosition(attachment.tile, transformable.Position);
			var worldRot = MapManager.WorldRotation(attachment.tile, transformable.Rotation);
			EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
		}

		public static void DrawAddPopup(AttachmentEditContext context, Vector2 position)
		{
			var items = new List<PopupItem>
			{
				new ("Emitter [flame]", () =>
				{
					var e = AttachmentEmitterEditing.Instance.CreateEmitter(context.MapManager, context.PendingTile, "flame");
					if (e != null)
						Select(new[] { e }, context.MapManager, context.SceneCamera);
				}),
				new ("Emitter [spark]", () =>
				{
					var e = AttachmentEmitterEditing.Instance.CreateEmitter(context.MapManager, context.PendingTile, "spark");
					if (e != null)
						Select(new[] { e }, context.MapManager, context.SceneCamera);
				}),
				new ("View", () =>
				{
					var v = AttachmentViewEditing.Instance.CreateView(context.MapManager, context.PendingTile);
					if (v != null)
						Select(new[] { v }, context.MapManager, context.SceneCamera);
				}),
				new ("Pickup", () =>
				{
					var p = AttachmentPickupEditing.Instance.CreatePickup(context.MapManager, context.PendingTile);
					if (p != null)
						Select(new[] { p }, context.MapManager, context.SceneCamera);
				}),
				PopupItem.Spacer(),
				new ("Cancel", () => { }, colorOverride: Color.yellow)
			};

			if (!PopupMenu.Show(position, "Add Attachment", items))
				context.ClearPendingAction();
		}

		public static void DrawDeletePopup(AttachmentEditContext context, Vector2 position)
		{
			if (context.CurrentMap == null) return;
			var attsOnTile = context.CurrentMap.GetAttachmentsOnTile(context.PendingTile);
			if (attsOnTile.Length == 0) return;

			var items = new List<PopupItem>();

			foreach (var att in attsOnTile)
			{
				string label = $"Delete {att.GetType().Name}";
				var localAtt = att;
				items.Add(new PopupItem(label, () =>
				{
					context.MapManager.RemoveAttachment(localAtt);
					Select(null, context.MapManager, context.SceneCamera);
				}));
			}

			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () =>
				{
					context.MapManager.RemoveAllAttachmentsOnTile(context.PendingTile);
					Select(null, context.MapManager, context.SceneCamera);
				}, colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			if (!PopupMenu.Show(position, "Delete Attachment(s)", items))
				context.ClearPendingAction();
		}

		public static void DrawSelectPopup(AttachmentEditContext context, Vector2 position)
		{
			if (context.CurrentMap == null) return;
			var atts = context.CurrentMap.GetAttachmentsOnTile(context.PendingTile);
			if (atts == null || atts.Length == 0) return;

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
					Select(new[] { att }, context.MapManager, context.SceneCamera);
				}));
			}

			if (atts.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Select All", () =>
				{
					wasCancelled = false;
					Select(atts, context.MapManager, context.SceneCamera);
				}, colorOverride: Color.green));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			if (!PopupMenu.Show(position, $"Select ({atts.Length})", items))
			{
				if (wasCancelled)
					Select(null, context.MapManager, context.SceneCamera);
				context.ClearPendingAction();
			}
		}

		public static void RefreshDragVisuals(AttachmentEditContext context)
		{
			if (selectedAttachments == null || selectedAttachments.Length != 1) return;
			var att = selectedAttachments[0];
			var typeEditor = GetEditorFor(att);
			typeEditor?.OnRefreshDragVisuals(context.MapManager, att);
			typeEditor?.OnUpdateDragGizmo(att, context.SceneCamera);
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