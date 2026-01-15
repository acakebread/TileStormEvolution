using UnityEngine;
using System;

namespace MassiveHadronLtd
{
	public class GimbalOrbitController
	{
		// Configuration
		public float DragOrbitSensitivity { get; }
		public float DragTiltSensitivity { get; }
		public float ScrollZoomSensitivity { get; }

		public float MinTiltAngle { get; }
		public float MaxTiltAngle { get; }
		public float MinDistance { get; }
		public float MaxDistance { get; }
		public float SizeToDistanceFactor { get; }
		public float DefaultTiltAngle { get; }

		// Auto-rotate
		public Vector3 AutoRotateAxis { get; set; } = Vector3.up;
		public float AutoRotateSpeed { get; set; } = 15f;
		public float AutoRotateTimeout { get; set; } = 3f;

		// State
		public float CurrentOrbitAngle { get; private set; }
		public float CurrentTiltAngle { get; private set; }
		public float CurrentDistance { get; private set; }

		private Bounds currentModelBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
		private float lastInputTime = -999f;

		public event Action OnTransformChanged;

		public GimbalOrbitController(
			float dragOrbitSens = 0.2f,
			float dragTiltSens = 0.2f,
			float scrollZoomSens = 0.5f,
			float minTilt = 0f,
			float maxTilt = 90f,
			float minDist = 0.8f,
			float maxDist = 10f,
			float sizeToDistFactor = 1f,
			float defaultTilt = 30f)
		{
			DragOrbitSensitivity = dragOrbitSens;
			DragTiltSensitivity = dragTiltSens;
			ScrollZoomSensitivity = scrollZoomSens;
			MinTiltAngle = minTilt;
			MaxTiltAngle = maxTilt;
			MinDistance = minDist;
			MaxDistance = maxDist;
			SizeToDistanceFactor = sizeToDistFactor;
			DefaultTiltAngle = defaultTilt;

			// No reset here - lazy init
		}

		/// <summary>
		/// Unified reset: call with hasModel = false for no model, true for valid model + bounds
		/// </summary>
		public void ResetView(bool hasModel, Bounds modelBounds = default)
		{
			CurrentOrbitAngle = 0f;
			CurrentTiltAngle = DefaultTiltAngle;

			if (hasModel)
			{
				currentModelBounds = modelBounds;
				float modelSize = modelBounds.size.magnitude;
				CurrentDistance = Mathf.Clamp(modelSize * SizeToDistanceFactor, MinDistance, MaxDistance);
			}
			else
			{
				currentModelBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
				CurrentDistance = 5f;
			}

			// Delay auto-rotate by 1 second after any reset
			lastInputTime = Time.unscaledTime - AutoRotateTimeout + 1f;

			// Fire event (now safe since subscription happens before first call)
			OnTransformChanged?.Invoke();
		}

		public void ProcessDrag(Vector2 delta)
		{
			CurrentOrbitAngle += delta.x * DragOrbitSensitivity;
			CurrentTiltAngle -= delta.y * DragTiltSensitivity;
			CurrentTiltAngle = Mathf.Clamp(CurrentTiltAngle, MinTiltAngle, MaxTiltAngle);

			lastInputTime = Time.unscaledTime;
			OnTransformChanged?.Invoke();
		}

		public void ProcessScroll(float scrollDelta)
		{
			if (scrollDelta == 0f) return;

			CurrentDistance -= scrollDelta * ScrollZoomSensitivity;
			CurrentDistance = Mathf.Clamp(CurrentDistance, MinDistance, MaxDistance);

			lastInputTime = Time.unscaledTime;
			OnTransformChanged?.Invoke();
		}

		public void Update()
		{
			if (AutoRotateSpeed == 0f || AutoRotateTimeout <= 0f)
				return;

			if (Time.unscaledTime - lastInputTime > AutoRotateTimeout)
			{
				if (Vector3.Dot(AutoRotateAxis.normalized, Vector3.up) > 0.99f)
				{
					CurrentOrbitAngle += AutoRotateSpeed * Time.deltaTime;
					OnTransformChanged?.Invoke();
				}
			}
		}

		public (Vector3 position, Quaternion rotation) GetCameraTransform()
		{
			float gimbalY = currentModelBounds.max.y * 0.5f;
			Vector3 gimbalPosition = Vector3.up * gimbalY;

			Quaternion rotation = Quaternion.Euler(CurrentTiltAngle, CurrentOrbitAngle, 0f);
			Vector3 forward = rotation * Vector3.forward;
			Vector3 cameraPosition = gimbalPosition - forward * CurrentDistance;

			return (cameraPosition, rotation);
		}
	}
}