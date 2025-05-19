using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CinemaCameraController
{
	public const float TargetFPS = 60f;
	private const int MaxPlayerPositions = 50;
	private const float MinDistanceForNewFocusPoint = 3f;

	// Shared state - world data
	public List<Vector3> focusPoints = new();
	public List<Vector3> playerPositions = new();
	public Vector2 mapExtentsMin;
	public Vector2 mapExtentsMax;
	public Transform playerTransform;
	public Vector3 lastPlayerPos;

	private CinemaCameraBase cameraSystem;

	// Shared state - camera data
	public CameraController.CameraData cameraData;
	protected Vector3 originSrc { get => cameraData.originSrc; set => cameraData.originSrc = value; }
	protected Vector3 originDst { get => cameraData.originDst; set => cameraData.originDst = value; }
	protected Vector3 targetSrc { get => cameraData.targetSrc; set => cameraData.targetSrc = value; }
	protected Vector3 targetDst { get => cameraData.targetDst; set => cameraData.targetDst = value; }

	public static bool forceDollyZoomMode = false;// for debugging

	public void Reset()// we need this for when a new map is loaded
	{
		cameraSystem = null;

		mapExtentsMin = new Vector2(float.MaxValue, float.MaxValue);
		mapExtentsMax = new Vector2(float.MinValue, float.MinValue);
		playerPositions.Clear();
		focusPoints.Clear();

		cameraData = new CameraController.CameraData
		{
			smoothing = 64f,//default smoothing rate
			originSrc = Vector3.zero,
			originDst = Vector3.zero,
			targetSrc = Vector3.zero,
			targetDst = Vector3.zero,
			fieldOfView = 45f
		};

		playerTransform = null;
		lastPlayerPos = Vector3.zero;
	}

	public void SetFocusPoints(List<Vector3> points)
	{
		focusPoints = points;
		if (focusPoints.Count > 0)
		{
			mapExtentsMin.x = focusPoints.Min(p => p.x);
			mapExtentsMin.y = focusPoints.Min(p => p.z);
			mapExtentsMax.x = focusPoints.Max(p => p.x);
			mapExtentsMax.y = focusPoints.Max(p => p.z);
		}
	}

	public void UpdatePlayerTransform(Transform transform)
	{
		if (null == playerTransform) lastPlayerPos = null == transform ? Vector3.zero : transform.position;// todo get rid of this by initilalising properly
		playerTransform = transform;

		if (playerTransform != null && IsFarEnough(playerTransform.position))
		{
			var pos = playerTransform.position;
			mapExtentsMin.x = Mathf.Min(mapExtentsMin.x, pos.x);
			mapExtentsMin.y = Mathf.Min(mapExtentsMin.y, pos.z);
			mapExtentsMax.x = Mathf.Max(mapExtentsMax.x, pos.x);
			mapExtentsMax.y = Mathf.Max(mapExtentsMax.y, pos.z);
			playerPositions.Add(pos);
			if (playerPositions.Count > MaxPlayerPositions)
				playerPositions.RemoveAt(0);
		}

		bool IsFarEnough(Vector3 position)
		{
			foreach (var fp in focusPoints)
			{
				if (Vector3.Distance(position, fp) < MinDistanceForNewFocusPoint)
					return false;
			}
			foreach (var pp in playerPositions)
			{
				if (Vector3.Distance(position, pp) < MinDistanceForNewFocusPoint)
					return false;
			}
			return true;
		}
	}

	public CameraController.CameraData CameraData => cameraData;

	public CameraController.CameraData UpdateCameraData(Vector3 originDst, Vector3 targetDst, float FOV)
	{
		var interpolate = SmoothingUtils.Smooth(0f, 1f, cameraData.smoothing, Time.deltaTime, TargetFPS);
		cameraData.originSrc = Vector3.Lerp(cameraData.originSrc, originDst, interpolate);
		cameraData.targetSrc = Vector3.Lerp(cameraData.targetSrc, targetDst, interpolate);
		cameraData.fieldOfView = FOV;// Mathf.Lerp(cameraData.FOV, FOV, interpolate); ToDo initialise FOV in StartSequence
		return cameraData;
	}

	//public void CreateCinemaSequence() => cameraSystem = forceDollyZoomMode ? new CinemaCameraDollyZoom() : Random.value < 0.33f ? new CinemaCameraOrbit() : new CinemaCameraPath();
	public void CreateCinemaSequence() => cameraSystem = Random.value < 0.5f ? new CinemaCameraOrbit() : new CinemaCameraPath();
	//public void CreateCinemaSequence() => cameraSystem = new CinemaCameraPath();
	//public void CreateCinemaSequence() => cameraSystem = new CinemaCameraOrbit();
	
	public void StartCinemaSequence(Transform transform, List<Vector3> points) { playerTransform = null; UpdatePlayerTransform(transform); SetFocusPoints(points); cameraSystem.StartSequence(this); }

	public bool UpdateCinemaMode() => cameraSystem.UpdateSequence();//returns false when complete
}
