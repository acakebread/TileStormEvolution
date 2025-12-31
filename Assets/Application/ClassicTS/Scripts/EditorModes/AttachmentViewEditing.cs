using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	internal class ViewAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly ViewAttachmentHandler Instance = new();

		public void OnSelectionChanged(IMapManager mapManager, Camera camera, MapAttachment[] selection)
		{
			var view = (View)selection[0];
			var worldPos = MapManager.WorldPosition(view.tile, view.Position);
			EditorTransformUtil.ShowAt(worldPos, view.Rotation, camera);
			OnDragInput(mapManager, selection);
		}

		public void OnGizmoInput(IMapManager mapManager, Camera camera, MapAttachment[] selection)
		{
			var view = (View)selection[0];

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

		public void OnDragInput(IMapManager mapManager, MapAttachment[] selection)
		{
			var view = (View)selection[0];
			ViewPreviewUtil.Show(view, mapManager);
			UpdateViewFrustumMarker(view);
		}

		public static View Create(IMapManager mapManager, int tile)
		{
			if (mapManager == null) return null;

			var view = new View
			{
				tile = tile,
				Position = (Vector3.up + Vector3.back) * 8f,
				LookAt = (Vector3.forward + Vector3.down) * 4f
			};

			SnapViewDistanceToGround(view);
			mapManager.AddAttachment(view);
			return view;
		}

		public static void HandlePreviewCameraSync(IMapManager mapManager, Camera camera, MapAttachment[] selection)
		{
			if (selection?.FirstOrDefault() is not View view) return;
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
			if (view == null) return;
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
			up = up.sqrMagnitude < 0.01f ? Vector3.up : up.normalized;

			Quaternion targetRotation = Quaternion.LookRotation(forward, up);
			EditorFrustumUtil.UpdateFrustum(worldPos, targetRotation, view.Distance, view.FOV);
		}
	}
}