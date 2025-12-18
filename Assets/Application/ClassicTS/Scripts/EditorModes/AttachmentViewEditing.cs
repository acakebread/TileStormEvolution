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

			// No runtime GO needed — just editor visuals
			editor.editorController.iMapManager.RefreshAttachmentInstance(view); // safe no-op

			editor.editorController.OnMapChanged();
			editor.SelectAttachments(new[] { view });

			OnHandleSelectionChanged(editor); // shows frustum, gizmo, preview

			return view;
		}

		protected override void OnHandleSelectionChanged(EditorControllerAttachment editor)
		{
			var view = editor.selectedAttachments?.OfType<View>().FirstOrDefault();
			if (view == null) return;

			Vector3 worldPos = editor.editorController.iMapManager.TileWorldPosition(view.tile) + view.Position;
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
				UpdateViewFrustumMarker(view, editor.editorController.iMapManager); // your existing method
			}
		}

		protected override void OnHandleGizmoInput(EditorControllerAttachment editor)
		{
			var view = editor.selectedAttachments?.OfType<View>().FirstOrDefault();
			if (view == null) return;

			if (EditorTransformUtil.HandleInput(editor.editorCamera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				view.Position = newWorldPos - editor.editorController.iMapManager.TileWorldPosition(view.tile);
				view.Rotation = newWorldRot;

				SnapViewDistanceToGround(view, editor.editorController.iMapManager);
				UpdateViewFrustumMarker(view, editor.editorController.iMapManager);

				// Also update preview window to stay in sync
				editor.viewPreview.Show(view, editor.editorController.iMapManager);
			}
		}

		// ===================================================================
		// PREVIEW CAMERA SYNC — NOW FULLY FIXED
		// ===================================================================

		public static void HandlePreviewCameraSync(EditorControllerAttachment editor, ViewPreview viewPreview)
		{
			var view = editor.selectedAttachments?.OfType<View>().FirstOrDefault();
			if (view == null) return;

			// First: sync preview cam → View properties
			Vector3 wp = viewPreview.previewCam.transform.position;
			view.Position = wp - editor.editorController.iMapManager.TileWorldPosition(view.tile);
			view.Rotation = viewPreview.previewCam.transform.rotation;

			// Apply ground snap
			SnapViewDistanceToGround(view, editor.editorController.iMapManager);

			// Second: sync back View → preview cam (ensures perfect consistency)
			SyncPreviewToView(editor, viewPreview, view);

			// CRITICAL: Update main scene gizmo to match new View transform
			Vector3 worldPos = editor.editorController.iMapManager.TileWorldPosition(view.tile) + view.Position;
			EditorTransformUtil.UpdateTransform(worldPos, view.Rotation, editor.editorCamera);

			// Update frustum marker
			UpdateViewFrustumMarker(view, editor.editorController.iMapManager);
		}

		private static void SyncPreviewToView(EditorControllerAttachment editor, ViewPreview viewPreview, View view)
		{
			Vector3 tileWorld = editor.editorController.iMapManager.TileWorldPosition(view.tile);
			viewPreview.previewCam.transform.position = tileWorld + view.Position;
			viewPreview.previewCam.transform.rotation = view.Rotation;
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

		private static void UpdateViewFrustumMarker(View view, IMapManager mapManager)
		{
			if (view == null || view.data == null || view.data.Length < 7 || view.Distance < 0.02f)
			{
				EditorFrustumUtil.Hide();
				return;
			}

			Vector3 worldPos = mapManager.TileWorldPosition(view.tile) + view.Position;

			Vector3 forward = (view.LookAt - view.Position).normalized;
			if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

			Quaternion rot = view.Rotation;
			Vector3 up = Vector3.ProjectOnPlane(rot * Vector3.up, forward);
			if (up.sqrMagnitude < 0.01f) up = Vector3.up;
			else up = up.normalized;

			Quaternion targetRotation = Quaternion.LookRotation(forward, up);

			// Use UpdateFrustum for efficiency (handles mesh regeneration on FOV/distance change)
			EditorFrustumUtil.UpdateFrustum(worldPos, targetRotation, view.Distance, view.FOV);
		}

		// Future: View-specific inspector panel here
		protected override void DrawTypeSpecificGUI(EditorControllerAttachment editor) { }
	}
}