using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class CameraController
{
	const float CinemaTimeoutDuration = 5f;

	public enum CameraState
	{
		Static,
		Preset,
		Follow,
		Cinema
	}

	public struct CameraData
	{
		public Vector3 originSrc;
		public Vector3 originDst;
		public Vector3 targetSrc;
		public Vector3 targetDst;
		public float smoothingRate;
		public float fov;
	}

	// Public properties
	public static bool CinemaEnabled => enableAutoCinema;
	public static bool CinemaActive => CameraState.Cinema == currentState;

	// Common behavior constants
	private const float TargetFPS = 60f;
	private const float DefaultSmoothingRate = 64f;

	// Camera shake constants
	private const float ShakeChance = 1f;//0.33f; // 33% chance to enable shake in Cinema mode

	// State-specific constants
	private static class FollowConfig
	{
		public const float SmoothingNa = 8f;
		public const float SmoothingNb = 64f;
		public const float IdealDistance = 14f;
		public const float IdealDistanceHorizontalScale = 1.4f;
	}

	private static class PresetConfig
	{
		public const float SmoothingN = 32f;
	}

	// Internal state
	private static CameraState currentState = CameraState.Static;
	private static CameraState previousState = CameraState.Static;
	private static CameraData currentData;
	private static CameraData restoreData;
	private static Camera mainCamera;
	private static Transform playerTransform;
	private static bool enableAutoCinema;
	private static float lastRefreshTime;
	private static readonly CinemaCameraController cinemaController = new();
	private static List<Vector3> focusPoints = new();
	private static bool isCameraShakeEnabled; // Tracks if camera shake is active

	public static void Reset()
	{
		cinemaController.Reset();
		playerTransform = null;
		focusPoints.Clear();
		lastRefreshTime = Time.time;
		isCameraShakeEnabled = false;
	}

	// Initialization
	public static void Initialize()
	{
		mainCamera = Camera.main;
		if (mainCamera == null) return;

		if (currentState == CameraState.Cinema)
			currentData = restoreData;
		else
		{
			currentData = new CameraData
			{
				originSrc = Vector3.zero,
				targetSrc = Vector3.zero,
				originDst = Vector3.forward,
				targetDst = Vector3.forward,
				smoothingRate = DefaultSmoothingRate,
				fov = mainCamera.fieldOfView
			};
		}
	}

	// Cinema controls
	public static void SetAutoCinema(bool allow = true) => enableAutoCinema = allow;

	// State management
	public static void SetMode(CameraState value)
	{
		//value = CameraState.Static;
		if (value == CameraState.Cinema && currentState != CameraState.Cinema)
		{
			previousState = currentState;
			restoreData = currentData;

			cinemaController.CreateCinemaSequence();
			cinemaController.StartCinemaSequence(playerTransform, focusPoints);
			currentData = cinemaController.GetCinemaData();

			isCameraShakeEnabled = Random.value < ShakeChance;
		}
		else if (value != CameraState.Cinema && currentState == CameraState.Cinema)
		{
			previousState = currentState;
			currentData = restoreData;
			mainCamera.fieldOfView = currentData.fov;
			isCameraShakeEnabled = false; // Disable shake when exiting Cinema mode
		}
		currentState = value;
	}

	public static void Refresh(float time)
	{
		lastRefreshTime = time;
		if (currentState == CameraState.Cinema)
			SetMode(previousState);
	}

	// Position setters
	public static void SetOrigin(Vector3 value)
	{
		currentData.originDst = value;
		if (currentState == CameraState.Static) currentData.originSrc = value;
	}

	public static void SetTarget(Vector3 value)
	{
		currentData.targetDst = value;
		if (currentState == CameraState.Static) currentData.targetSrc = value;
	}

	public static void SetPlayer(Transform transform)
	{
		playerTransform = transform;
		if (currentState == CameraState.Follow) currentData.targetDst = transform.position;
		if (currentState == CameraState.Cinema) cinemaController.UpdatePlayerTransform(transform);
	}

	public static void SetFocusPoints(List<Vector3> newFocusPoints)
	{
		focusPoints = newFocusPoints?.Where(p => p != Vector3.zero && Vector3.Distance(p, Vector3.zero) > 0.1f).ToList() ?? new List<Vector3>();
		if (currentState == CameraState.Cinema) cinemaController.SetFocusPoints(focusPoints);
	}

	// Update logic
	public static void Update()
	{
		if (mainCamera == null) return;

		if (enableAutoCinema && currentState != CameraState.Cinema && Time.time - lastRefreshTime > CinemaTimeoutDuration)
		{
			Debug.Log("Auto-switching to Cinema mode");
			SetMode(CameraState.Cinema);
		}

		switch (currentState)
		{
			case CameraState.Static:
				break;

			case CameraState.Preset:
				UpdatePresetMode();
				break;

			case CameraState.Follow:
				UpdateFollowMode();
				break;

			case CameraState.Cinema:
				bool shouldContinue;
				currentData = cinemaController.UpdateCinemaMode(mainCamera, out shouldContinue);
				if (false == shouldContinue)
				{
					cinemaController.CreateCinemaSequence();
					cinemaController.StartCinemaSequence(playerTransform, focusPoints);
					currentData = cinemaController.GetCinemaData();
				}
				break;
		}

		UpdateCameraTransform();
		mainCamera.fieldOfView = currentData.fov;
	}

	private static void UpdatePresetMode()
	{
		currentData.smoothingRate = SmoothingUtils.Smooth(currentData.smoothingRate, PresetConfig.SmoothingN, Time.deltaTime, TargetFPS);
		var presetLerp = SmoothingUtils.Smooth(0f, 1f, currentData.smoothingRate, Time.deltaTime, TargetFPS);
		currentData.originSrc = Vector3.Lerp(currentData.originSrc, currentData.originDst, presetLerp);
		currentData.targetSrc = Vector3.Lerp(currentData.targetSrc, currentData.targetDst, presetLerp);
	}

	private static void UpdateFollowMode()
	{
		currentData.smoothingRate = SmoothingUtils.Smooth(currentData.smoothingRate, FollowConfig.SmoothingNa, FollowConfig.SmoothingNb, Time.deltaTime, TargetFPS);
		var followLerp = SmoothingUtils.Smooth(0f, 1f, currentData.smoothingRate, Time.deltaTime, TargetFPS);
		currentData.targetSrc = Vector3.Lerp(currentData.targetSrc, currentData.targetDst, followLerp);
		var delta = currentData.targetSrc - currentData.originSrc;
		var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? mainCamera.transform.forward : new Vector3(delta.x, 0, delta.z);
		deltaHorizontal.Normalize();
		var idealPos = currentData.targetSrc - deltaHorizontal * (FollowConfig.IdealDistance * FollowConfig.IdealDistanceHorizontalScale);
		idealPos.y = currentData.targetSrc.y + FollowConfig.IdealDistance;
		currentData.originSrc = Vector3.Lerp(currentData.originSrc, idealPos, followLerp);
	}

	public static void UpdateCameraTransform()
	{
		mainCamera.transform.position = currentData.originSrc;
		var direction = currentData.targetSrc - currentData.originSrc;
		if (direction.sqrMagnitude < 0.01f)
		{
			Debug.LogWarning("Direction vector too small, skipping rotation update.");
			return;
		}
		mainCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

		// Apply camera shake
		if (true == isCameraShakeEnabled) CameraUtils.ApplyCameraShake(mainCamera);
	}
}