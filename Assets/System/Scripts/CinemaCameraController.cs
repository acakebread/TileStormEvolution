using UnityEngine;
using System.Collections.Generic;

public class CinemaCameraController
{
	private CinemaCameraBase controller;
	public static bool forceDollyZoomMode = false;

	public CinemaCameraController()
	{
		// Initialize with appropriate subclass based on mode preference
		//if (forceDollyZoomMode)
		//	controller = new CinemaCameraDollyZoom();
		//else
		//	controller = Random.value < 0.25f ? new CinemaCameraOrbit() : new CinemaCameraPath();

		controller = new CinemaCameraPath();
	}

	public void Reset() => controller.Reset();

	public void SetFocusPoints(List<Vector3> points) => controller.SetFocusPoints(points);

	public void UpdatePlayerTransform(Transform transform) => controller.UpdatePlayerTransform(transform);

	public void StartNewCinemaSequence() => controller.StartSequence();

	public CameraController.CameraData GetCinemaData(CameraController.CameraData data) => controller.CreateCameraData(data);

	public CameraController.CameraData UpdateCinemaMode(CameraController.CameraData data, Camera camera) => controller.UpdateSequence(data, camera);
}
