using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	partial class View : ISelectable, ITransformableAttachment
	{
		public void OnSelect(IMapEdit map, Camera camera)
		{
			var worldPos = map.WorldPosition(tile, Position);
			EditorTransformUtil.ShowAt(worldPos, Rotation, camera);
			OnUpdate(map, camera);
		}

		public void OnDeselect(IMapEdit iMap, Camera camera)
		{
			EditorTransformUtil.Hide();
			EditorFrustumUtil.Hide();
			ViewPreviewUtil.Hide();
		}

		public bool OnGizmoInput(IMapEdit map, Camera camera)
		{
			//OnUpdate(map, camera, selection);
			HandlePreviewCameraSync(map, camera, this);
			if (!EditorTransformUtil.HandleInput(camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
				return false;
			EditorTransformUtil.UpdateTransformGizmoVisuals(camera);

			Position = map.LocalPosition(tile, newWorldPos);
			Rotation = map.LocalRotation(tile, newWorldRot);
			SnapViewDistanceToGround(map, this);

			var previewTransform = ViewPreviewUtil.PreviewCameraTransform;
			if (previewTransform != null)
			{
				previewTransform.position = map.WorldPosition(tile, Position);
				previewTransform.rotation = map.WorldRotation(tile, Rotation);
			}

			ViewPreviewUtil.Update();
			UpdateViewFrustumMarker(map, this);
			return true;
		}

		public void OnUpdate(IMapEdit map, Camera camera)
		{
			ViewPreviewUtil.Show(map, this);
			UpdateViewFrustumMarker(map, this);
		}

		public static View Create(IMapEdit map, int tile)
		{
			if (map == null) return null;

			var view = new View
			{
				tile = tile,
				Position = (Vector3.up + Vector3.back) * 8f,
				LookAt = (Vector3.forward + Vector3.down) * 4f
			};

			SnapViewDistanceToGround(map, view);
			map.AddAttachment(view);
			return view;
		}

		public static void HandlePreviewCameraSync(IMapEdit map, Camera camera, View view)
		{
			var previewTransform = ViewPreviewUtil.PreviewCameraTransform;
			if (previewTransform == null) return;

			view.Position = map.LocalPosition(view.tile, previewTransform.position);
			view.Rotation = map.LocalRotation(view.tile, previewTransform.rotation);
			SnapViewDistanceToGround(map, view);

			previewTransform.position = map.WorldPosition(view.tile, view.Position);
			previewTransform.rotation = map.WorldRotation(view.tile, view.Rotation);

			ViewPreviewUtil.Update();
			UpdateViewFrustumMarker(map, view);
			EditorTransformUtil.UpdateTransform(previewTransform.position, view.Rotation, camera);
		}

		private static void SnapViewDistanceToGround(IMapEdit map, View view)
		{
			if (view == null) return;
			var origin = map.WorldPosition(view.tile, view.Position);
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

		private static bool UpdateViewFrustumMarker(IMapEdit map, View view)
		{
			if (view == null || view.data == null || view.data.Length < 7 || view.Distance < 0.02f)
			{
				EditorFrustumUtil.Hide();
				return false;
			}

			Vector3 worldPos = map.WorldPosition(view.tile, view.Position);
			Vector3 forward = (view.LookAt - view.Position).normalized;
			if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

			Quaternion rot = view.Rotation;
			Vector3 up = Vector3.ProjectOnPlane(rot * Vector3.up, forward);
			up = up.sqrMagnitude < 0.01f ? Vector3.up : up.normalized;

			Quaternion targetRotation = Quaternion.LookRotation(forward, up);
			EditorFrustumUtil.UpdateFrustum(worldPos, targetRotation, view.Distance, view.FOV);
			return true;
		}
	}
}