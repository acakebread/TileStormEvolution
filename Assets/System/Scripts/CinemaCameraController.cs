using UnityEngine;
using System.Collections.Generic;

public class CinemaCameraController
{
	private CinemaCameraBase controller;
	public static bool forceDollyZoomMode = false;

	public void CreateCinemaSequence() => controller = forceDollyZoomMode ? new CinemaCameraDollyZoom() : Random.value < 0.25f ? new CinemaCameraOrbit() : new CinemaCameraPath();

	public void StartCinemaSequence(Transform playerTransform, List<Vector3> points) => controller.StartSequence(playerTransform, points);

	public void Reset() => controller?.Reset();// todo get initialisation order sorted oout

	public void SetFocusPoints(List<Vector3> points) => controller.SetFocusPoints(points);

	public void UpdatePlayerTransform(Transform transform) => controller.UpdatePlayerTransform(transform);

	public CameraController.CameraData GetCinemaData(CameraController.CameraData data) => controller.CreateCameraData(data);

	public CameraController.CameraData UpdateCinemaMode(CameraController.CameraData data, Camera camera, out bool shouldContinue) => controller.UpdateSequence(data, camera, out shouldContinue);
}
