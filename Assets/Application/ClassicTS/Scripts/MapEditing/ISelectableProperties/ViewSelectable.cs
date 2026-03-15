using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	partial class View : ISelectable, ITransformableAttachment
	{
		public void OnSelect(EditorController controller)
		{
			if (controller.IsMultiSelect)
			{
				OnDeselect(controller);
				return;
			}
			var worldPos = controller.iMap.WorldPosition(tile, Position);
				EditorTransformUtil.ShowAt(worldPos, Rotation, controller._camera);
			OnUpdate(controller);
		}

		public void OnDeselect(EditorController controller)
		{
			EditorTransformUtil.Hide();
			EditorFrustumUtil.Hide();
			ViewPreviewUtil.Hide();
		}

		public bool OnGizmoInput(EditorController controller)
		{
			HandlePreviewCameraSync(controller);

			if (!EditorTransformUtil.HandleInput(controller._camera, out Vector3 newWorldPos, out Quaternion newWorldRot))
				return false;

			EditorTransformUtil.UpdateTransformGizmoVisuals(controller._camera);

			Position = controller.iMap.LocalPosition(tile, newWorldPos);
			Rotation = controller.iMap.LocalRotation(tile, newWorldRot);
			SnapViewDistanceToGround(controller);

			var previewTransform = ViewPreviewUtil.PreviewCameraTransform;
			if (previewTransform != null)
			{
				previewTransform.position = controller.iMap.WorldPosition(tile, Position);
				previewTransform.rotation = controller.iMap.WorldRotation(tile, Rotation);
			}

			ViewPreviewUtil.Update();
			UpdateViewFrustumMarker(controller);
			return true;
		}

		public void OnUpdate(EditorController controller)
		{
			if (controller.IsMultiSelect)
			{
				OnDeselect(controller);
				return;
			}
			ViewPreviewUtil.Show(controller.iMap, this);
			UpdateViewFrustumMarker(controller);
		}

		// Helpers — now take controller
		private void HandlePreviewCameraSync(EditorController controller)
		{
			var previewTransform = ViewPreviewUtil.PreviewCameraTransform;
			if (previewTransform == null) return;

			Position = controller.iMap.LocalPosition(tile, previewTransform.position);
			Rotation = controller.iMap.LocalRotation(tile, previewTransform.rotation);
			SnapViewDistanceToGround(controller);

			previewTransform.position = controller.iMap.WorldPosition(tile, Position);
			previewTransform.rotation = controller.iMap.WorldRotation(tile, Rotation);

			ViewPreviewUtil.Update();
			UpdateViewFrustumMarker(controller);
			EditorTransformUtil.UpdateTransform(previewTransform.position, Rotation, controller._camera);
		}

		private void SnapViewDistanceToGround(EditorController controller)
		{
			var origin = controller.iMap.WorldPosition(tile, Position);
			var forward = Rotation * Vector3.forward;
			var ray = new Ray(origin, forward);

			if (Map.RayToWorld(ray, out Vector3 result))
			{
				float distance = (result - origin).magnitude;
				if (distance > 0.1f)
				{
					Distance = Mathf.Min(distance, MAX_DISTANCE);
					return;
				}
			}

			Distance = MAX_DISTANCE;
		}

		private bool UpdateViewFrustumMarker(EditorController controller)
		{
			if (data == null || data.Length < 7 || Distance < 0.02f)
			{
				EditorFrustumUtil.Hide();
				return false;
			}

			Vector3 worldPos = controller.iMap.WorldPosition(tile, Position);
			Vector3 forward = (LookAt - Position).normalized;
			if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

			Quaternion rot = Rotation;
			Vector3 up = Vector3.ProjectOnPlane(rot * Vector3.up, forward);
			up = up.sqrMagnitude < 0.01f ? Vector3.up : up.normalized;

			Quaternion targetRotation = Quaternion.LookRotation(forward, up);
			EditorFrustumUtil.UpdateFrustum(worldPos, targetRotation, Distance, FOV);
			return true;
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

			view.Distance = MAX_DISTANCE;
		}
	}
}