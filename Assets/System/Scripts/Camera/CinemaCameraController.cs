using UnityEngine;
using System.Collections.Generic;

public class CinemaCameraController
{
	private CinemaCameraBase method;
	public static bool forceDollyZoomMode = false;

	public void CreateCinemaSequence() => method = forceDollyZoomMode ? new CinemaCameraDollyZoom() : Random.value < 0.33f ? new CinemaCameraOrbit() : new CinemaCameraPath();
	//public void CreateCinemaSequence() => method = Random.value < 0.5f ? new CinemaCameraOrbit() : new CinemaCameraPath();
	//public void CreateCinemaSequence() => method = new CinemaCameraPath();

	public void StartCinemaSequence(Transform playerTransform, List<Vector3> points) => method.StartSequence(playerTransform, points);

	public void Reset() => method = null;

	public void SetFocusPoints(List<Vector3> points) => method.SetFocusPoints(points);

	public void UpdatePlayerTransform(Transform transform) => method.UpdatePlayerTransform(transform);

	public CameraController.CameraData GetCinemaData() => method.CameraData;

	public CameraController.CameraData UpdateCinemaMode(Camera camera, out bool shouldContinue) => method.UpdateSequence(camera, out shouldContinue);
}
