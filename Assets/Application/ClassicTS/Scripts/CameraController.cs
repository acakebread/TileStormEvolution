using UnityEngine;
using ClassicTilestorm; // Only for legacy section
using static ClassicTilestorm.DatabaseLoader; // Only for legacy section

public class CameraController : MonoBehaviour
{
	public enum CameraState
	{
		Static,
		Follow,
		Preset
	}

	// Configuration constants (matched to C++ GameCamera, 60 FPS)
	private float CurrentRate = 64f; // 1/64 default
	//private const float DefaultSmoothingN = 256f; // 1/256 (C++: nFollow)
	private const float FollowSmoothingNa = 8f; // 1/8 (C++: nFollow)
	private const float FollowSmoothingNb = 64f; // 1/64 (C++: nFollow)
	private const float PresetSmoothingN = 32; // 1/32 for fast transition
	private const float IdealDistance = 14f; // C++: fIdeal = 14.0f
	private const float IdealDistanceHorizontalScale = 1.4f; // C++: fIdeal * 1.4f
	private const float TargetFPS = 60f; // C++ camera update rate

	// Internal state
	private CameraState state = CameraState.Static;
	private Vector3 originSrc; // Current camera position
	private Vector3 originDst; // Target camera position
	private Vector3 targetSrc; // Current look-at point
	private Vector3 targetDst; // Target look-at point
	private Camera mainCamera;

	private void InternalInitialize()
	{
		mainCamera = Camera.main;
		if (mainCamera == null) return;

		originSrc = Vector3.zero;
		targetSrc = Vector3.zero;
		originDst = Vector3.forward;
		targetDst = Vector3.forward;
	}

	public void SetMode(CameraState value) => state = value;

	public void SetOrigin(Vector3 value) { originDst = value; if (CameraState.Static == state) originSrc = value; }

	public void SetTarget(Vector3 value) { targetDst = value; if (CameraState.Static == state) targetSrc = value; }

	private void InternalUpdate()
	{
		if (mainCamera == null) return;

		switch (state)
		{
			case CameraState.Static:
				break;

			case CameraState.Follow:
				CurrentRate = SmoothingUtils.Smooth(CurrentRate, FollowSmoothingNa, FollowSmoothingNb, Time.deltaTime, TargetFPS);
				var followLerp = SmoothingUtils.Smooth(0f, 1f, CurrentRate, Time.deltaTime, TargetFPS);
				targetSrc = Vector3.Lerp(targetSrc, targetDst, followLerp);

				var delta = targetSrc - originSrc;
				var deltaHorizontal = new Vector3(delta.x, 0, delta.z);
				if (deltaHorizontal.sqrMagnitude < 0.01f)
					deltaHorizontal = mainCamera.transform.forward;
				deltaHorizontal.Normalize();
				var idealPos = targetSrc - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
				idealPos.y = targetSrc.y + IdealDistance;

				originSrc = Vector3.Lerp(originSrc, idealPos, followLerp);
				break;

			case CameraState.Preset:
				CurrentRate = PresetSmoothingN;
				var presetLerp = SmoothingUtils.Smooth(0f, 1f, CurrentRate, Time.deltaTime, TargetFPS);
				originSrc = Vector3.Lerp(originSrc, originDst, presetLerp);
				targetSrc = Vector3.Lerp(targetSrc, targetDst, presetLerp);
				break;
		}

		UpdateCameraTransform();
	}

	private void UpdateCameraTransform()
	{
		mainCamera.transform.position = originSrc;

		var direction = targetSrc - originSrc;
		if (direction.sqrMagnitude < 0.01f) return;

		var adjustedTarget = targetSrc;
		if (adjustedTarget.y > originSrc.y - 0.5f)
			adjustedTarget.y = Mathf.Min(adjustedTarget.y, originSrc.y - 1f);

		mainCamera.transform.rotation = Quaternion.LookRotation(adjustedTarget - originSrc, Vector3.up);
	}

	// Legacy Compatibility (remove after GameController integration)
	#region Legacy Compatibility
	public void Initialize()
	{
		InternalInitialize();

		SetMode(CameraState.Static);

		var mapManager = GamePreview.mapManager;
		var eggbotController = GamePreview.eggbotController;

		if (mapManager == null || mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
		{
			if (eggbotController?.eggbotRoot != null)
			{
				SetMode(CameraState.Follow);
				SetTarget(eggbotController.eggbotRoot.position);
			}
			else
			{
				SetOrigin(new Vector3(0f, 14f, -14f));
				SetTarget(Vector3.zero);
				originSrc = new Vector3(0f, 14f, -14f);
				targetSrc = Vector3.zero;
			}
			UpdateCameraTransform();
			return;
		}


		var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
		var srcPos = dstPos + new Vector3(0f, 14f, -14f);

		var firstWaypoint = mapManager.Waypoints[0];
		if (true == firstWaypoint.bCamera)
		{
			if (true == CameraUtils.IsValidVector(firstWaypoint.vSrc)) srcPos = new Vector3(firstWaypoint.vSrc.fX, firstWaypoint.vSrc.fY, firstWaypoint.vSrc.fZ);
			if (true == CameraUtils.IsValidVector(firstWaypoint.vDst)) dstPos = new Vector3(firstWaypoint.vDst.fX, firstWaypoint.vDst.fY, firstWaypoint.vDst.fZ);
		}

		SetOrigin(srcPos);
		SetTarget(dstPos);
		SetMode(CameraState.Follow);

		UpdateCameraTransform();

		if (eggbotController != null)
		{
			eggbotController.OnWaypointReached += OnWaypointReached;
			eggbotController.OnPuzzleSolved += OnPuzzleSolved;
			eggbotController.OnLevelCompleted += OnLevelCompleted;
		}
	}

	public void UpdateCamera()
	{
		var eggbotController = GamePreview.eggbotController;
		if (eggbotController == null || eggbotController.eggbotRoot == null)
		{
			InternalUpdate();
			return;
		}

		if (state == CameraState.Follow)
			SetTarget(eggbotController.eggbotRoot.position);

		InternalUpdate();
	}

	public void OnWaypointReached(int waypointIndex)
	{
		var mapManager = GamePreview.mapManager;
		var eggbotController = GamePreview.eggbotController;
		if (mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
		{
			if (eggbotController?.eggbotRoot != null)
			{
				SetMode(CameraState.Follow);
				SetTarget(eggbotController.eggbotRoot.position);
			}
			return;
		}

		var waypoint = mapManager.Waypoints[waypointIndex];
		Vector3 srcPos = new Vector3(waypoint.vSrc.fX, waypoint.vSrc.fY, waypoint.vSrc.fZ);
		if (srcPos == Vector3.zero) srcPos = new Vector3(0f, 14f, -14f);

		Vector3 lookAtPos = waypoint.vDst != null && CameraUtils.IsValidVector(waypoint.vDst)
			? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
			: mapManager.GetTilePosition(waypoint.nTile) + new Vector3(0f, 0.5f, 0f);

		if (waypointIndex == mapManager.Waypoints.Count - 1)
		{
			SetMode(CameraState.Follow);
			SetTarget(lookAtPos);
			return;
		}

		if (!CameraUtils.IsValidVector(waypoint.vSrc))
		{
			SetMode(CameraState.Follow);
			SetTarget(eggbotController?.eggbotRoot.position ?? Vector3.zero);
			return;
		}

		SetMode(CameraState.Preset);
		SetOrigin(srcPos);
		SetTarget(lookAtPos);
	}

	public void OnPuzzleSolved(int waypointIndex)
	{
		var eggbotController = GamePreview.eggbotController;
		SetMode(CameraState.Follow);
		SetTarget(eggbotController?.eggbotRoot.position ?? Vector3.zero);
	}

	public void OnLevelCompleted() { }

	private void OnDestroy()
	{
		var eggbotController = GamePreview.eggbotController;
		if (eggbotController != null)
		{
			eggbotController.OnWaypointReached -= OnWaypointReached;
			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
			eggbotController.OnLevelCompleted -= OnLevelCompleted;
		}
	}
	#endregion
}