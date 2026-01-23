using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	internal class ViewAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly ViewAttachmentHandler Instance = new();

		public void OnSelectionChanged(IMap map, Camera camera, MapAttachment[] selection)
		{
			var view = (View)selection[0];
			var worldPos = Map.WorldPosition(view.tile, view.Position);
			EditorTransformUtil.ShowAt(worldPos, view.Rotation, camera);
			OnDragInput(map, selection);
		}

		public void OnGizmoInput(IMap map, Camera camera, MapAttachment[] selection)
		{
			var view = (View)selection[0];

			if (EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
			{
				view.Position = Map.LocalPosition(view.tile, newWorldPos);
				view.Rotation = Map.LocalRotation(view.tile, newWorldRot);
				SnapViewDistanceToGround(view);

				var previewTransform = ViewPreviewUtil.PreviewCameraTransform;
				if (previewTransform != null)
				{
					previewTransform.position = Map.WorldPosition(view.tile, view.Position);
					previewTransform.rotation = Map.WorldRotation(view.tile, view.Rotation);
				}

				ViewPreviewUtil.Update();
				UpdateViewFrustumMarker(view);
			}
		}

		public void OnDragInput(IMap map, MapAttachment[] selection)
		{
			var view = (View)selection[0];
			ViewPreviewUtil.Show(view, map);
			UpdateViewFrustumMarker(view);
		}

		public static View Create(IMap map, int tile)
		{
			if (map == null) return null;

			var view = new View
			{
				tile = tile,
				Position = (Vector3.up + Vector3.back) * 8f,
				LookAt = (Vector3.forward + Vector3.down) * 4f
			};

			SnapViewDistanceToGround(view);
			map.AddAttachment(view);
			return view;
		}

		public static void HandlePreviewCameraSync(IMap map, Camera camera, MapAttachment[] selection)
		{
			if (selection?.FirstOrDefault() is not View view) return;
			var previewTransform = ViewPreviewUtil.PreviewCameraTransform;
			if (previewTransform == null) return;

			view.Position = Map.LocalPosition(view.tile, previewTransform.position);
			view.Rotation = Map.LocalRotation(view.tile, previewTransform.rotation);
			SnapViewDistanceToGround(view);

			previewTransform.position = Map.WorldPosition(view.tile, view.Position);
			previewTransform.rotation = Map.WorldRotation(view.tile, view.Rotation);

			ViewPreviewUtil.Update();
			UpdateViewFrustumMarker(view);
			EditorTransformUtil.UpdateTransform(previewTransform.position, view.Rotation, camera);
		}

		private static void SnapViewDistanceToGround(View view)
		{
			if (view == null) return;
			var origin = Map.WorldPosition(view.tile, view.Position);
			var forward = view.Rotation * Vector3.forward;
			var ray = new Ray(origin, forward);

			if (Map.RayToWorld(ray, out Vector3 result))
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

			Vector3 worldPos = Map.WorldPosition(view.tile, view.Position);
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