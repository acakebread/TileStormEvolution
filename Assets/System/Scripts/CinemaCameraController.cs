using UnityEngine;
using System.Collections.Generic;

public class CinemaCameraController
{
	// Enum for cinematic modes
	public enum CinemaMode
	{
		Orbit = 0,
		PoiStandard = 1,
		PoiVariation1 = 2,
		PoiVariation2 = 3,
		ChiefBrody = 4
	}

	private CinemaCameraBase controller;
	private const bool forceChiefBrodyMode = false;

	public CinemaCameraController()
	{
		// Initialize with appropriate subclass based on mode preference
		if (forceChiefBrodyMode)
		{
			controller = new CinemaDollyZoom();
		}
		else
		{
			controller = new CinemaMultiMode();
		}
	}

	public float CinemaTimeoutDuration => controller.CinemaTimeoutDuration;

	public void Reset()
	{
		controller.Reset();
	}

	public void SetWaypoints(List<Vector3> newWaypoints)
	{
		controller.SetWaypoints(newWaypoints);
	}

	public void UpdatePlayerTransform(Transform transform)
	{
		controller.UpdatePlayerTransform(transform);
	}

	public void StartNewCinemaSequence(Vector3 playerPos, List<Vector3> waypoints)
	{
		if (forceChiefBrodyMode)
		{
			if (!(controller is CinemaDollyZoom))
			{
				controller = new CinemaDollyZoom();
			}
		}
		else
		{
			if (!(controller is CinemaMultiMode))
			{
				controller = new CinemaMultiMode();
			}
		}
		controller.StartNewCinemaSequence(playerPos, waypoints);
	}

	public CameraController.CameraData GetCinemaData(CameraController.CameraData data)
	{
		return controller.GetCinemaData(data);
	}

	public CameraController.CameraData UpdateCinemaMode(CameraController.CameraData data, Camera camera)
	{
		return controller.UpdateCinemaMode(data, camera);
	}
}
