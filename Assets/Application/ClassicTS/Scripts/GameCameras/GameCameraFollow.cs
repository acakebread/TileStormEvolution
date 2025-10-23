using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameCameraFollow : CameraBase
	{
		public Func<Vector3> targetFn;
		
		private const float SmoothingNa = 8f;
		private const float SmoothingNb = 64f;
		private const float IdealDistance = 14f;
		private const float IdealDistanceHorizontalScale = 1.4f;

		public GameCameraFollow(Camera camera) : base(camera) { }

		public override void Start()
		{
			base.Start();
			smoothing = 64f;
			fieldOfView = 20f;
			postProcessingEnabled = true;
		}

		public override void Update()
		{
			base.Update();
			var target = targetFn?.Invoke() ?? Vector3.forward;
			smoothing = SmoothingUtils.Smooth(smoothing, SmoothingNa, SmoothingNb, Time.deltaTime, TargetFPS);
			var followLerp = SmoothingUtils.Smooth(0f, 1f, smoothing, Time.deltaTime, TargetFPS);
			itarget = Vector3.Lerp(itarget, target, followLerp); // Use helper property 'target'
			var delta = itarget - iorigin;
			var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? Vector3.zero : new Vector3(delta.x, 0, delta.z).normalized;
			var origin = itarget - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
			origin.y = itarget.y + IdealDistance;
			iorigin = Vector3.Lerp(iorigin, origin, followLerp);
			OnRender();
		}

		private void OnRender()
		{
			if (null == camera) return;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = fieldOfView;
		}
	}
}