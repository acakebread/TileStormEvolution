using UnityEngine;
using System.Collections.Generic;

public class CinemaCameraController
{
	private CinemaCameraBase controller;
	public static bool forceDollyZoomMode = false;

	public CinemaCameraController()
	{
		// Initialize with appropriate subclass based on mode preference
		if (forceDollyZoomMode)
		{
			controller = new CinemaDollyZoom();
		}
		else
		{
			controller = new CinemaMultiMode();
		}
	}

	public void Reset()
	{
		controller.Reset();
	}

	public void SetFocusPoints(List<Vector3> points)
	{
		controller.SetFocusPoints(points);
	}

	public void UpdatePlayerTransform(Transform transform)
	{
		controller.UpdatePlayerTransform(transform);
	}

	public void StartNewCinemaSequence()
	{
		if (forceDollyZoomMode)
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
		controller.StartSequence();
	}

	public CameraController.CameraData GetCinemaData(CameraController.CameraData data)
	{
		return controller.CreateCameraData(data);
	}

	public CameraController.CameraData UpdateCinemaMode(CameraController.CameraData data, Camera camera)
	{
		return controller.UpdateSequence(data, camera);
	}
}
