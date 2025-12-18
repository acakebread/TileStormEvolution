using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class AttachmentViewEditing : AttachmentEditing
	{
		public static readonly AttachmentViewEditing Instance = new();

		public View AddNewView(EditorControllerAttachment editor, int tile)
		{
			var map = editor.editorController?.iMapManager?.CurrentMap;
			if (map == null) return null;

			var view = new View
			{
				tile = tile,
				Position = (Vector3.up + Vector3.back) * 8f,
				LookAt = (Vector3.forward + Vector3.down) * 4f
			};

			map.AddAttachment(view);

			editor.editorController.iMapManager.RefreshAttachmentInstance(view);

			editor.editorController.OnMapChanged();
			editor.SelectAttachments(new[] { view });

			OnHandleSelectionChanged(editor);

			return view;
		}

		protected override void OnHandleSelectionChanged(EditorControllerAttachment editor)
		{
			var view = editor.selectedAttachments?.OfType<View>().FirstOrDefault();
			if (view == null) return;

			var worldPos = MapManager.WorldPosition(view.tile, view.Position);

			EditorTransformUtil.ShowAt(worldPos, view.Rotation, editor.editorCamera);

			SnapViewDistanceToGround(view, editor.editorController.iMapManager);
			UpdateViewFrustumMarker(view, editor.editorController.iMapManager);
			editor.viewPreview.Show(view, editor.editorController.iMapManager);
		}

		protected override void OnRefreshDragVisuals(EditorControllerAttachment editor, MapAttachment attachment)
		{
			if (attachment is View view)
			{
				editor.viewPreview.Show(view, editor.editorController.iMapManager);
				UpdateViewFrustumMarker(view, editor.editorController.iMapManager);
			}
		}

		protected override void OnHandleGizmoInput(EditorControllerAttachment editor)
		{
			var view = editor.selectedAttachments?.OfType<View>().FirstOrDefault();
			if (view == null) return;

			if (EditorTransformUtil.HandleInput(editor.editorCamera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				view.Position = MapManager.LocalPosition(view.tile, newWorldPos);
				view.Rotation = MapManager.LocalRotation(view.tile, newWorldRot);

				SnapViewDistanceToGround(view, editor.editorController.iMapManager);
				UpdateViewFrustumMarker(view, editor.editorController.iMapManager);

				editor.viewPreview.Show(view, editor.editorController.iMapManager);
			}
		}

		public static void HandlePreviewCameraSync(EditorControllerAttachment editor, ViewPreview viewPreview)
		{
			var view = editor.selectedAttachments?.OfType<View>().FirstOrDefault();
			if (view == null) return;

			// Sync preview cam → View
			view.Position = MapManager.LocalPosition(view.tile, viewPreview.previewCam.transform.position);
			view.Rotation = MapManager.LocalRotation(view.tile, viewPreview.previewCam.transform.rotation);

			SnapViewDistanceToGround(view, editor.editorController.iMapManager);

			// Sync back View → preview cam
			SyncPreviewToView(editor, viewPreview, view);

			// Update scene gizmo
			Vector3 worldPos = MapManager.WorldPosition(view.tile, view.Position);
			EditorTransformUtil.UpdateTransform(worldPos, view.Rotation, editor.editorCamera);

			UpdateViewFrustumMarker(view, editor.editorController.iMapManager);
		}

		private static void SyncPreviewToView(EditorControllerAttachment editor, ViewPreview viewPreview, View view)
		{
			Vector3 worldPos = MapManager.WorldPosition(view.tile, view.Position);
			viewPreview.previewCam.transform.position = worldPos;
			viewPreview.previewCam.transform.rotation = view.Rotation;
		}

		private static void SnapViewDistanceToGround(View view, IMapManager mapManager)
		{
			if (view == null || mapManager == null) return;

			var origin = MapManager.WorldPosition(view.tile, view.Position);
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

		private static void UpdateViewFrustumMarker(View view, IMapManager mapManager)
		{
			if (view == null || view.data == null || view.data.Length < 7 || view.Distance < 0.02f)
			{
				EditorFrustumUtil.Hide();
				return;
			}

			Vector3 worldPos = MapManager.WorldPosition(view.tile, view.Position);

			Vector3 forward = (view.LookAt - view.Position).normalized;
			if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

			Quaternion rot = view.Rotation;
			Vector3 up = Vector3.ProjectOnPlane(rot * Vector3.up, forward);
			if (up.sqrMagnitude < 0.01f) up = Vector3.up;
			else up = up.normalized;

			Quaternion targetRotation = Quaternion.LookRotation(forward, up);

			EditorFrustumUtil.UpdateFrustum(worldPos, targetRotation, view.Distance, view.FOV);
		}

		protected override void DrawTypeSpecificGUI(EditorControllerAttachment editor) { }
	}
}