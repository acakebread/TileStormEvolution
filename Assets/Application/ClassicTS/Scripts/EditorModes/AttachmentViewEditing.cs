using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System.Linq;

namespace ClassicTilestorm
{
	public static class AttachmentViewEditing
	{
		private static View GetSelectedView(EditorControllerAttachment editor)
		{
			// First priority: if there are selected attachments, look for a View among them
			if (editor.selectedAttachments != null && editor.selectedAttachments.Length > 0)
			{
				return editor.selectedAttachments.OfType<View>().FirstOrDefault();
			}

			// Fallback to old behavior: use the indexed one (used when nothing is multi-selected)
			if (editor.SelectedAttachmentIndex < 0) return null;
			var map = editor.editorController.iMapManager.CurrentMap;
			if (map?.attachments == null || editor.SelectedAttachmentIndex >= map.attachments.Length) return null;
			return map.attachments[editor.SelectedAttachmentIndex] as View;
		}

		public static void HandlePreviewCameraSync(EditorControllerAttachment editor, ViewPreview viewPreview)
		{
			var view = GetSelectedView(editor);
			if (view == null) return;

			SyncPreviewToView(editor, viewPreview, view);
			SnapViewDistanceToGround(view, editor.editorController.iMapManager);
			SyncPreviewToView(editor, viewPreview, view);
		}

		public static void HandleGizmoInput(EditorControllerAttachment editor)
		{
			var view = GetSelectedView(editor);
			if (view == null) return;

			SnapViewDistanceToGround(view, editor.editorController.iMapManager);
			UpdateVisuals(editor, view);
		}

		public static void HandleDrag(EditorControllerAttachment editor, View view)
		{
			SnapViewDistanceToGround(view, editor.editorController.iMapManager);
			UpdateVisuals(editor, view);
			ShowGizmoAndPreview(editor, view);
		}

		public static void HandleSelectionChanged(EditorControllerAttachment editor)
		{
			var view = GetSelectedView(editor);
			if (view == null) return;

			SnapViewDistanceToGround(view, editor.editorController.iMapManager);
			UpdateVisuals(editor, view);
			ShowGizmoAndPreview(editor, view);
		}

		private static void SyncPreviewToView(EditorControllerAttachment editor, ViewPreview viewPreview, View view)
		{
			Vector3 wp = viewPreview.previewCam.transform.position;
			view.Position = wp - editor.editorController.iMapManager.TileWorldPosition(view.tile);
			view.Rotation = viewPreview.previewCam.transform.rotation;
			UpdateVisuals(editor, view);
		}

		private static void UpdateVisuals(EditorControllerAttachment editor, View view)
		{
			EditorUtil.UpdateViewFrustumMarker(view, editor.editorController.iMapManager);
			EditorTransformUtil.UpdateTransformGizmoVisuals(editor.editorCamera);
		}

		private static void ShowGizmoAndPreview(EditorControllerAttachment editor, View view)
		{
			EditorTransformUtil.ShowTransformGizmo(view, editor.editorController.iMapManager, editor.editorCamera);
			editor.viewPreview.Show(view, editor.editorController.iMapManager);
		}

		public static void SnapViewDistanceToGround(View view, IMapManager mapManager)
		{
			if (view == null || mapManager == null) return;

			var origin = mapManager.TileWorldPosition(view.tile) + view.Position;
			var forward = view.Rotation * Vector3.forward;
			var ray = new Ray(origin, forward);

			if (MapManager.RayToWorld(ray, out Vector3 result))
			{
				float distance = (result - origin).magnitude;
				if (distance > 0.1f)
				{
					view.Distance = Mathf.Min(distance, View.MAX_DISTANCE);
					return;
				}
			}

			view.Distance = View.MAX_DISTANCE;
		}

		public static void DrawGUI(EditorControllerAttachment editor)
		{
			if (editor.CurrentPendingAction == EditorControllerAttachment.PendingAction.Add)
				DrawAddPopup(editor);
			if (editor.CurrentPendingAction == EditorControllerAttachment.PendingAction.Delete)
				DrawDeletePopup(editor);
			if (editor.CurrentPendingAction == EditorControllerAttachment.PendingAction.Select)
				DrawSelectPopup(editor);
		}

		private static void DrawAddPopup(EditorControllerAttachment editor)
		{
			var items = new List<PopupItem>
			{
				new PopupItem("Emitter", () => editor.AddNewAttachment(editor.PendingTile, typeof(Emitter))),
				new PopupItem("View", () => editor.AddNewAttachment(editor.PendingTile, typeof(View))),
				new PopupItem("Pickup", () => editor.AddNewAttachment(editor.PendingTile, typeof(Pickup))),
				PopupItem.Spacer(),
				new PopupItem("Cancel", () => editor.SelectAttachments(null), colorOverride: Color.yellow)
			};

			bool closed = PopupMenu.Show(editor.PendingPopupScreenPos, "Add Attachment", items);
			if (closed) editor.ClearPendingAction();
		}

		private static void DrawDeletePopup(EditorControllerAttachment editor)
		{
			var map = editor.editorController?.iMapManager?.CurrentMap;
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
					map.RemoveAttachment(localAtt);
					editor.SelectAttachments(null);
					EditorUtil.DestroyViewFrustumMarker();
					EditorTransformUtil.HideTransformGizmo();
					editor.RebuildMarkers();
					editor.viewPreview.Hide();
					editor.editorController.OnMapChanged();
				}));
			}

			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () =>
				{
					map.RemoveAllAttachmentsOnTile(editor.PendingTile);
					editor.SelectAttachments(null);
					EditorUtil.DestroyViewFrustumMarker();
					EditorTransformUtil.HideTransformGizmo();
					editor.RebuildMarkers();
					editor.viewPreview.Hide();
					editor.editorController.OnMapChanged();
				}, colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", colorOverride: Color.yellow));

			bool closed = PopupMenu.Show(editor.PendingPopupScreenPos, "Delete Attachment(s)", items);
			if (closed) editor.ClearPendingAction();
		}

		private static void DrawSelectPopup(EditorControllerAttachment editor)
		{
			var map = editor.editorController.iMapManager.CurrentMap;
			var atts = map.GetAttachmentsOnTile(editor.PendingTile);
			if (atts == null || atts.Length == 0) return;

			var items = new List<PopupItem>();

			foreach (var att in atts)
			{
				string label = att.GetType().Name;
				if (att is Emitter e && e.LookAt != null && e.LookAt != Vector3.up)
					label += $" to {e.LookAt.magnitude:F1}";
				label += $" [tile {att.tile}]";

				items.Add(new PopupItem(label, () =>
				{
					editor.pendingAction = EditorControllerAttachment.PendingAction.Drag;
					editor.SelectAttachments(new MapAttachment[] { att });
				}));
			}

			if (atts.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Select All", () =>
				{
					editor.pendingAction = EditorControllerAttachment.PendingAction.Drag;
					editor.SelectAttachments(atts);
				}, colorOverride: Color.green));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", colorOverride: Color.yellow));

			bool closed = PopupMenu.Show(editor.PendingPopupScreenPos, $"Select ({atts.Length})", items);
			if (closed)
			{
				if (editor.pendingAction != EditorControllerAttachment.PendingAction.Drag)
					editor.SelectAttachments(null);
				editor.ClearPendingAction();
			}
		}
	}
}