using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class CameraController
{
	public enum CameraState
	{
		Static,
		Follow,
		Preset,
		Cinema
	}

	public struct CameraData
	{
		public Vector3 origin;
		public Vector3 target;
		public Vector3 originDst;
		public Vector3 targetDst;
		public float smoothingRate;
		public float fov;
	}

	// Public properties
	public static bool CinemaEnabled => allowAutoCinema;
	public static bool CinemaActive => CameraState.Cinema == currentState;

	// Common behavior constants
	private const float DefaultSmoothingRate = 64f;
	private const float TargetFPS = 60f;

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
	private static bool allowAutoCinema;
	private static float lastRefreshTime;
	private static readonly CinemaCameraController cinemaController = new();
	private static List<Vector3> waypoints = new();

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
				origin = Vector3.zero,
				target = Vector3.zero,
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
	}

	// Cinema controls
	public static void SetAutoCinema(bool allow = true)  => allowAutoCinema = allow;

	// State management
	public static void Refresh(float time)
	{
		lastRefreshTime = time;
		if (currentState == CameraState.Cinema)
			SetMode(previousState);
	}

	public static void SetMode(CameraState value)
	{
		if (value == CameraState.Cinema && currentState != CameraState.Cinema)
		{
			previousState = currentState;
			previousData = currentData;
			cinemaController.StartNewCinemaSequence(playerPos, waypoints);
		}
		else if (value != CameraState.Cinema && currentState == CameraState.Cinema)
		{
			previousState = currentState;
			currentData = previousData;
			mainCamera.fieldOfView = currentData.fov;
		}
		currentState = value;
	}

	// Position setters
	public static void SetOrigin(Vector3 value)
	{
		currentData.originDst = value;
		if (currentState == CameraState.Static) currentData.origin = value;
	}

	public static void SetTarget(Vector3 value)
	{
		currentData.targetDst = value;
		if (currentState == CameraState.Static) currentData.target = value;
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

		if (allowAutoCinema && currentState != CameraState.Cinema && Time.time - lastRefreshTime > cinemaController.CinemaTimeoutDuration)
		{
			Debug.Log("Auto-switching to Cinema mode");
			SetMode(CameraState.Cinema);
		}

		switch (currentState)
		{
			case CameraState.Static:
				break;

			case CameraState.Follow:
				UpdateFollowMode();
				break;

			case CameraState.Preset:
				UpdatePresetMode();
				break;

			case CameraState.Cinema:
				currentData = cinemaController.UpdateCinemaMode(currentData, mainCamera);
				break;
		}

		UpdateCameraTransform();
	}

	private static void UpdateFollowMode()
	{
		currentData.smoothingRate = SmoothingUtils.Smooth(currentData.smoothingRate, FollowConfig.SmoothingNa, FollowConfig.SmoothingNb, Time.deltaTime, TargetFPS);
		float followLerp = SmoothingUtils.Smooth(0f, 1f, currentData.smoothingRate, Time.deltaTime, TargetFPS);
		currentData.target = Vector3.Lerp(currentData.target, currentData.targetDst, followLerp);
		Vector3 delta = currentData.target - currentData.origin;
		Vector3 deltaHorizontal = new Vector3(delta.x, 0, delta.z);
		if (deltaHorizontal.sqrMagnitude < 0.01f)
			deltaHorizontal = mainCamera.transform.forward;
		deltaHorizontal.Normalize();
		Vector3 idealPos = currentData.target - deltaHorizontal * (FollowConfig.IdealDistance * FollowConfig.IdealDistanceHorizontalScale);
		idealPos.y = currentData.target.y + FollowConfig.IdealDistance;
		currentData.origin = Vector3.Lerp(currentData.origin, idealPos, followLerp);
	}

	private static void UpdatePresetMode()
	{
		currentData.smoothingRate = SmoothingUtils.Smooth(currentData.smoothingRate, PresetConfig.SmoothingN, Time.deltaTime, TargetFPS);
		float presetLerp = SmoothingUtils.Smooth(0f, 1f, currentData.smoothingRate, Time.deltaTime, TargetFPS);
		currentData.origin = Vector3.Lerp(currentData.origin, currentData.originDst, presetLerp);
		currentData.target = Vector3.Lerp(currentData.target, currentData.targetDst, presetLerp);
	}

	public static void UpdateCameraTransform()
	{
		mainCamera.transform.position = currentData.origin;
		Vector3 direction = currentData.target - currentData.origin;
		if (direction.sqrMagnitude < 0.01f)
		{
			Debug.LogWarning("Direction vector too small, skipping rotation update.");
			return;
		}
		mainCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
	}
}