// AttachmentViewEditing.cs
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace ClassicTilestorm
{
	public class AttachmentViewEditing : AttachmentEditing
	{
		public static readonly AttachmentViewEditing Instance = new();

		private static View GetSelectedView(EditorControllerAttachment editor)
		{
			if (editor.selectedAttachments != null && editor.selectedAttachments.Length > 0)
				return editor.selectedAttachments.OfType<View>().FirstOrDefault();

			if (editor.SelectedAttachmentIndex < 0) return null;
			var map = editor.editorController.iMapManager.CurrentMap;
			if (map?.attachments == null || editor.SelectedAttachmentIndex >= map.attachments.Length) return null;
			return map.attachments[editor.SelectedAttachmentIndex] as View;
		}

		public override void HandleSelectionChanged(EditorControllerAttachment editor)
		{
			var view = GetSelectedView(editor);
			if (view == null) return;

			SnapViewDistanceToGround(view, editor.editorController.iMapManager);
			UpdateVisuals(editor, view);
			ShowGizmoAndPreview(editor, view);
		}

		public override void HandleDrag(EditorControllerAttachment editor, MapAttachment attachment)
		{
			if (attachment is View view)
			{
				SnapViewDistanceToGround(view, editor.editorController.iMapManager);
				UpdateVisuals(editor, view);
				ShowGizmoAndPreview(editor, view);
			}
		}

		protected override void DrawTypeSpecificGUI(EditorControllerAttachment editor)
		{
			// Future: View-specific inspector panel here
		}

		// Static methods used directly from main controller (preview & gizmo input)
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

		private static void SnapViewDistanceToGround(View view, IMapManager mapManager)
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
	}
}