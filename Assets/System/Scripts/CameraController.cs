using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class CameraController
{
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
	private static CameraData previousData;
	private static Camera mainCamera;
	private static Vector3 playerPos;
	private static bool enableAutoCinema;
	private static float lastRefreshTime;
	private static readonly CinemaCameraController cinemaController = new();
	private static List<Vector3> waypoints = new();
	private static bool isCameraShakeEnabled; // Tracks if camera shake is active
	private static float shakeSeed; // Seed for Perlin noise

	// Initialization
	public static void Initialize()
	{
		mainCamera = Camera.main;
		if (mainCamera == null) return;

		if (currentState == CameraState.Cinema)
			currentData = previousData;
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
		Reset();
	}

	public static void Reset()
	{
		playerPos = Vector3.zero;
		waypoints.Clear();
		lastRefreshTime = Time.time;
		cinemaController.Reset();
		isCameraShakeEnabled = false;
		shakeSeed = 0f;
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
			previousData = currentData;
			cinemaController.StartNewCinemaSequence(playerPos, waypoints);
			currentData = cinemaController.GetCinemaData(currentData);
			isCameraShakeEnabled = Random.value < ShakeChance; // 33% chance for shake in Cinema mode
			shakeSeed = Random.value * 100f; // New seed for shake
		}
		else if (value != CameraState.Cinema && currentState == CameraState.Cinema)
		{
			previousState = currentState;
			currentData = previousData;
			mainCamera.fieldOfView = currentData.fov;
			isCameraShakeEnabled = false; // Disable shake when exiting Cinema mode
		}
		//isCameraShakeEnabled = true;
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

	public static void SetPlayer(Vector3 position)
	{
		playerPos = position;
		if (currentState == CameraState.Follow) currentData.targetDst = position;
		cinemaController.UpdatePlayerPosition(position);
	}

	public static void SetWaypoints(List<Vector3> newWaypoints)
	{
		waypoints = newWaypoints?.Where(p => p != Vector3.zero && Vector3.Distance(p, Vector3.zero) > 0.1f).ToList() ?? new List<Vector3>();
		cinemaController.SetWaypoints(waypoints);
	}

	// Update logic
	public static void Update()
	{
		if (mainCamera == null) return;

		if (enableAutoCinema && currentState != CameraState.Cinema && Time.time - lastRefreshTime > cinemaController.CinemaTimeoutDuration)
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
				currentData = cinemaController.UpdateCinemaMode(currentData, mainCamera);
				break;
		}

		UpdateCameraTransform();
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
		CameraUtils.ApplyCameraShake(mainCamera, isCameraShakeEnabled, shakeSeed);
	}
}