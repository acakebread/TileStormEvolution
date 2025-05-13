using UnityEngine;

public static class CameraController
{
	public enum CameraState
	{
		Static,
		Follow,
		Preset
	}

	public static CameraState State => state;

	// Configuration constants (matched to C++ GameCamera, 60 FPS)
	private static float CurrentRate = 64f; // 1/64 default
											//private const float DefaultSmoothingN = 256f; // 1/256 (C++: nFollow) - currently unused
	private const float FollowSmoothingNa = 8f; // 1/8 (C++: nFollow)
	private const float FollowSmoothingNb = 64f; // 1/64 (C++: nFollow)
	private const float PresetSmoothingN = 32; // 1/32 for fast transition
	private const float IdealDistance = 14f; // C++: fIdeal = 14.0f
	private const float IdealDistanceHorizontalScale = 1.4f; // C++: fIdeal * 1.4f
	private const float TargetFPS = 60f; // C++ camera update rate

	// Internal state
	private static CameraState state = CameraState.Static;
	private static Vector3 originSrc; // Current camera position
	private static Vector3 originDst; // Target camera position
	private static Vector3 targetSrc; // Current look-at point
	private static Vector3 targetDst; // Target look-at point
	private static Camera mainCamera;

	public static void Initialize()
	{
		mainCamera = Camera.main;
		if (null == mainCamera) return;

		originSrc = Vector3.zero;
		targetSrc = Vector3.zero;
		originDst = Vector3.forward;
		targetDst = Vector3.forward;
	}

	public static void SetMode(CameraState value) => state = value;

	public static void SetOrigin(Vector3 value) { originDst = value; if (CameraState.Static == state) originSrc = value; }

	public static void SetTarget(Vector3 value) { targetDst = value; if (CameraState.Static == state) targetSrc = value; }

	public static void Update()
	{
		if (null == mainCamera) return;

		switch (state)
		{
			case CameraState.Static:
				break;

			case CameraState.Follow:
				CurrentRate = SmoothingUtils.Smooth(CurrentRate, FollowSmoothingNa, FollowSmoothingNb, Time.deltaTime, TargetFPS);
				var followLerp = SmoothingUtils.Smooth(0f, 1f, CurrentRate, Time.deltaTime, TargetFPS);
				targetSrc = Vector3.Lerp(targetSrc, targetDst, followLerp);

				var delta = targetSrc - originSrc;
				var deltaHorizontal = new Vector3(delta.x, 0, delta.z);
				if (deltaHorizontal.sqrMagnitude < 0.01f)
					deltaHorizontal = mainCamera.transform.forward;
				deltaHorizontal.Normalize();
				var idealPos = targetSrc - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
				idealPos.y = targetSrc.y + IdealDistance;

				originSrc = Vector3.Lerp(originSrc, idealPos, followLerp);
				break;

			case CameraState.Preset:
				CurrentRate = PresetSmoothingN;
				var presetLerp = SmoothingUtils.Smooth(0f, 1f, CurrentRate, Time.deltaTime, TargetFPS);
				originSrc = Vector3.Lerp(originSrc, originDst, presetLerp);
				targetSrc = Vector3.Lerp(targetSrc, targetDst, presetLerp);
				break;
		}

		UpdateCameraTransform();
	}

	public static void UpdateCameraTransform()
	{
		mainCamera.transform.position = originSrc;

		var direction = targetSrc - originSrc;
		if (direction.sqrMagnitude < 0.01f) return;

		var adjustedTarget = targetSrc;
		if (adjustedTarget.y > originSrc.y - 0.5f)
			adjustedTarget.y = Mathf.Min(adjustedTarget.y, originSrc.y - 1f);

		mainCamera.transform.rotation = Quaternion.LookRotation(adjustedTarget - originSrc, Vector3.up);
	}
}