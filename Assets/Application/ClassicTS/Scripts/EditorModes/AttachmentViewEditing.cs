using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class AttachmentViewEditing : AttachmentEditing
	{
		public static readonly AttachmentViewEditing Instance = new();

		public View CreateView(IMapManager mapManager, int tile)
		{
			if (mapManager == null) return null;

			var view = new View
			{
				tile = tile,
				Position = (Vector3.up + Vector3.back) * 8f,
				LookAt = (Vector3.forward + Vector3.down) * 4f
			};

			SnapViewDistanceToGround(view); // unchanged, uses static MapManager
			mapManager.AddAttachment(view);
			return view;
		}

		protected override void OnRefreshDragVisuals(IMapManager mapManager, MapAttachment attachment)
		{
			if (attachment is View view)
			{
				ViewPreviewUtil.Show(view, mapManager);
				UpdateViewFrustumMarker(view);
			}
		}

		protected override void OnHandleSelectionChanged(IMapManager mapManager, Camera camera)
		{
			var view = selectedAttachments?.OfType<View>().FirstOrDefault();
			if (view == null) return;

			var worldPos = MapManager.WorldPosition(view.tile, view.Position);
			EditorTransformUtil.ShowAt(worldPos, view.Rotation, camera);
			OnRefreshDragVisuals(mapManager, view); // already decoupled
		}

		protected override void OnHandleGizmoInput(IMapManager mapManager, Camera camera)
		{
			var view = selectedAttachments?.OfType<View>().FirstOrDefault();
			if (view == null) return;

			if (EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				view.Position = MapManager.LocalPosition(view.tile, newWorldPos);
				view.Rotation = MapManager.LocalRotation(view.tile, newWorldRot);

				SnapViewDistanceToGround(view);

				var previewTransform = ViewPreviewUtil.PreviewCameraTransform;
				if (previewTransform != null)
				{
					previewTransform.position = MapManager.WorldPosition(view.tile, view.Position);
					previewTransform.rotation = MapManager.WorldRotation(view.tile, view.Rotation);
				}

				ViewPreviewUtil.Update();
				UpdateViewFrustumMarker(view);
			}
		}

		public static void HandlePreviewCameraSync(IMapManager mapManager, Camera camera)
		{
			var view = selectedAttachments?.OfType<View>().FirstOrDefault();
			if (view == null) return;

			var previewTransform = ViewPreviewUtil.PreviewCameraTransform;
			if (previewTransform == null) return;

			view.Position = MapManager.LocalPosition(view.tile, previewTransform.position);
			view.Rotation = MapManager.LocalRotation(view.tile, previewTransform.rotation);

			SnapViewDistanceToGround(view);

			previewTransform.position = MapManager.WorldPosition(view.tile, view.Position);
			previewTransform.rotation = MapManager.WorldRotation(view.tile, view.Rotation);

			ViewPreviewUtil.Update();
			UpdateViewFrustumMarker(view);

			EditorTransformUtil.UpdateTransform(previewTransform.position, view.Rotation, camera);
		}

		private static void SnapViewDistanceToGround(View view)
		{
			if (null == view) return;

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

		private static void UpdateViewFrustumMarker(View view)
		{
			if (null == view || null == view.data || view.data.Length < 7 || view.Distance < 0.02f)
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
	}
}
