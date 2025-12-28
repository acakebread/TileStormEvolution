using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public static class AttachmentViewEditing
	{
		public static void OnSelectionChanged(IMapManager mapManager, Camera camera)
		{
			var view = (View)AttachmentEditing.selectedAttachments![0];

			var worldPos = MapManager.WorldPosition(view.tile, view.Position);
			EditorTransformUtil.ShowAt(worldPos, view.Rotation, camera);
			OnDragInput(mapManager); // reuse
		}

		public static void OnGizmoInput(IMapManager mapManager, Camera camera)
		{
			var view = (View)AttachmentEditing.selectedAttachments![0];

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

		public static void OnDragInput(IMapManager mapManager)
		{
			var view = (View)AttachmentEditing.selectedAttachments![0];
			ViewPreviewUtil.Show(view, mapManager);
			UpdateViewFrustumMarker(view);
		}

		public static View CreateView(IMapManager mapManager, int tile)
		{
			if (null == mapManager) return null;

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

		public static void HandlePreviewCameraSync(IMapManager mapManager, Camera camera)
		{
			if (AttachmentEditing.selectedAttachments?.FirstOrDefault() is not View view)
				return;

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
