using UnityEngine;
using System.Collections.Generic;

public static class CinemaController
{
	// Environment constants
	public const float TargetFPS = 60f;
	private const int MaxFocusPoints = 50;
	private const float MinDistanceForNewFocusPoint = 3f;

	// Shared state - world data
	public static Transform playerTransform;
	public static List<Vector3> focusPoints = new();
	private static Bounds mapBounds;
    private static CinemaCameraBase cameraSystem;

	static CinemaController() => Reset();

	public static void Reset() // Called when a new map is loaded
	{
		cameraSystem = null;
		playerTransform = null;
		focusPoints.Clear();
		mapBounds = new Bounds(Vector3.zero, Vector3.zero); // Initialize empty bounds
		SpatialBucketSystem.Initialize(MinDistanceForNewFocusPoint); // Initialize bucket system
	}

	public static void SetFocusPoints(List<Vector3> points)
	{
		focusPoints = points ?? new List<Vector3>();
		SpatialBucketSystem.Clear(); // Clear buckets when focus points change
		if (focusPoints.Count <= 0) return;
		mapBounds = new Bounds(focusPoints[0], Vector3.zero); // Initialize bounds
		foreach (var point in focusPoints)
		{
			mapBounds.Encapsulate(point); // Expand bounds
			if (SpatialBucketSystem.CanAddPoint(point))
				SpatialBucketSystem.AddPoint(point);
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
		switch (Random.Range(0, 7))
		{
			case 0: case 1: case 2: cameraSystem = new CinemaCameraPath(); break;
			case 3: case 4: case 5: cameraSystem = new CinemaCameraOrbit(); break;
			case 6: cameraSystem = new CinemaCameraDollyZoom(); break;
		}
		//cameraSystem = new CinemaCameraDollyZoom();
	}

	public static void StartCinemaSequence() { if (cameraSystem != null) cameraSystem.StartSequence(); }

	public static bool UpdateCinemaMode() => cameraSystem != null && cameraSystem.Update();

	public static CameraController.CameraData GetCameraData() => cameraSystem.cameraData;

	public static Bounds GetMapBounds() => mapBounds;
}