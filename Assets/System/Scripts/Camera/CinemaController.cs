using UnityEngine;
using System.Collections.Generic;

public static class CinemaController
{
	// Environment constants
	public const float TargetFPS = 60f;
	private const int MaxFocusPoints = 50;
	private const float MinDistanceForNewFocusPoint = 3f;

	// Shared state - world data
	public static List<Vector3> focusPoints = new();
	private static Bounds mapBounds; // Replaces mapExtentsMin and mapExtentsMax
	public static Transform playerTransform;

    private static CinemaCameraBase cameraSystem;

	// Shared state - camera data
	public static CameraController.CameraData cameraData;

	static CinemaController() => Reset();

	public static void Reset() // Called when a new map is loaded
	{
		cameraSystem = null;

		mapBounds = new Bounds(Vector3.zero, Vector3.zero); // Initialize empty bounds
		focusPoints.Clear();
		SpatialBucketSystem.Initialize(MinDistanceForNewFocusPoint); // Initialize bucket system

		cameraData = new CameraController.CameraData
		{
			smoothing = 64f, // Default smoothing rate
			originSrc = Vector3.zero,
			originDst = Vector3.zero,
			targetSrc = Vector3.zero,
			targetDst = Vector3.zero,
			fieldOfView = 45f
		};

		playerTransform = null;
	}

	public static void SetFocusPoints(List<Vector3> points)
	{
		focusPoints = points ?? new List<Vector3>();
		SpatialBucketSystem.Clear(); // Clear buckets when focus points change

		if (focusPoints.Count > 0)
		{
			mapBounds = new Bounds(focusPoints[0], Vector3.zero); // Initialize bounds
			foreach (var point in focusPoints)
			{
				mapBounds.Encapsulate(point); // Expand bounds
				if (SpatialBucketSystem.CanAddPoint(point))
					SpatialBucketSystem.AddPoint(point);
			}
		}
	}

	public static void UpdatePlayerTransform(Transform transform)
	{
		playerTransform = transform;
		if (playerTransform == null || !SpatialBucketSystem.CanAddPoint(playerTransform.position)) return;

		var pos = playerTransform.position;
		mapBounds.Encapsulate(pos); // Expand bounds
		focusPoints.Add(pos);
		SpatialBucketSystem.AddPoint(pos); // Add to bucket system
		if (focusPoints.Count <= MaxFocusPoints) return;
		SpatialBucketSystem.RemovePoint(focusPoints[0]); // Remove oldest from bucket system
		focusPoints.RemoveAt(0);
	}

	public static void CreateCinemaSequence()
	{
		//cameraSystem = Random.value < 0.33f ? new CinemaCameraDollyZoom() : Random.value < 0.5f ? new CinemaCameraOrbit() : new CinemaCameraPath();
		cameraSystem = Random.value < 0.5f ? new CinemaCameraOrbit() : new CinemaCameraPath();
		//cameraSystem = new CinemaCameraDollyZoom();
		//cameraSystem = new CinemaCameraPath();
		//cameraSystem = new CinemaCameraOrbit();
	}

	public static void StartCinemaSequence()
	{
		if (cameraSystem != null) cameraSystem.StartSequence();
	}

	public static bool UpdateCinemaMode() => cameraSystem != null && cameraSystem.Update();

	public static CameraController.CameraData GetCameraData() => cameraData;

	public static Bounds GetMapBounds() => mapBounds;
}