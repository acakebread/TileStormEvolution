using UnityEngine;
using System;
using System.Collections.Generic;

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
		public float AutoRotateSpeed { get; set; } = -15f;
		public float AutoRotateTimeout { get; set; } = 3f;

		// Inertia (disabled by default)
		public bool EnableInertia { get; set; } = false;
		public float InertiaDecay { get; set; } = 7f;              // 8–16 range, higher = quicker stop
		public float InertiaMultiplier { get; set; } = 0.125f;       // Higher for noticeable coast

		// Velocity history (average last few deltas on release for linear feel)
		private readonly Queue<Vector2> velocityHistory = new Queue<Vector2>();
		private const int VelocityHistoryLength = 5;  // last 5 frames
		private Vector2 frameVel;

		[Header("Pivot")]
		public Vector3 PivotOffset = Vector3.zero;           // ← new field, default (0,0,0)

		// Optional: expose a setter if you want to change it at runtime
		public void SetPivot(Vector3 worldPivot)
		{
			PivotOffset = worldPivot;
		}

		// ── New method: update framing without resetting angles ───────────────────────────────
		public void Reframe(Bounds newBounds, float distanceMultiplier = 1f, float? overridePivotY = null)
		{
			currentModelBounds = newBounds;

			// Distance calculation (with optional closer zoom)
			float diag = newBounds.size.magnitude;
			float target = diag * SizeToDistanceFactor * distanceMultiplier;
			CurrentDistance = Mathf.Clamp(target, MinDistance, MaxDistance);
			CurrentTiltAngle = Mathf.Clamp(CurrentTiltAngle, MinTiltAngle, MaxTiltAngle);

			// Pivot Y override (for maps: set to 0 or center.y)
			if (overridePivotY.HasValue)
			{
				PivotOffset = new Vector3(0, overridePivotY.Value, 0);
			}

			lastInputTime = Time.unscaledTime - 999f;
			OnTransformChanged?.Invoke();
		}

		// State
		public float CurrentOrbitAngle { get; private set; }
		public float CurrentTiltAngle { get; private set; }
		public float CurrentDistance { get; private set; }

		private Bounds currentModelBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
		private float lastInputTime = -999f;

		// Inertia
		private Vector2 currentVelocity;

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
			float defaultTilt = 30f,
			bool enableInertia = false)
		{
			DragOrbitSensitivity = dragOrbitSens;
			DragTiltSensitivity = dragTiltSens;
			ScrollZoomSensitivity = scrollZoomSens;
			MinTiltAngle = minTilt;
			MaxTiltAngle = maxTilt;
			MinDistance = minDist;
			MaxDistance = maxDist;
			SizeToDistanceFactor = sizeToDistFactor;
			CurrentTiltAngle = DefaultTiltAngle = defaultTilt;
			EnableInertia = enableInertia;
		}

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

			currentVelocity = Vector2.zero;
			velocityHistory.Clear();

			lastInputTime = Time.unscaledTime - AutoRotateTimeout + 1f;

			CurrentTiltAngle = Mathf.Clamp(CurrentTiltAngle, MinTiltAngle, MaxTiltAngle);

			OnTransformChanged?.Invoke();
		}

		public void ProcessDrag(Vector2 delta)
		{
			// Direct 1:1 during drag
			CurrentOrbitAngle += delta.x * DragOrbitSensitivity;
			CurrentTiltAngle -= delta.y * DragTiltSensitivity;
			CurrentTiltAngle = Mathf.Clamp(CurrentTiltAngle, MinTiltAngle, MaxTiltAngle);

			if (EnableInertia)
				frameVel = new Vector2(delta.x * InertiaMultiplier, -delta.y * InertiaMultiplier);// Accumulate per-frame delta for release

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

		public void EndDrag()
		{
			// On release: average the accumulated deltas for smooth velocity
			if (EnableInertia && velocityHistory.Count > 0)
			{
				Vector2 avgVel = Vector2.zero;
				foreach (var v in velocityHistory)
					avgVel += v;

				currentVelocity = avgVel / velocityHistory.Count;
				velocityHistory.Clear();
			}
		}

		public void Update()
		{
			bool transformChanged = false;

			// Inertia only after release (EndDrag has been called)
			if (EnableInertia)
			{
				// Accumulate per-frame delta for release
				velocityHistory.Enqueue(frameVel);
				frameVel = Vector3.zero;

				// Keep only last few frames
				if (velocityHistory.Count > VelocityHistoryLength)
					velocityHistory.Dequeue();


				float decay = Mathf.Exp(-InertiaDecay * Time.deltaTime);
				currentVelocity *= decay;

				if (currentVelocity.sqrMagnitude > 0.0001f)
				{
					CurrentOrbitAngle += currentVelocity.x;
					CurrentTiltAngle += currentVelocity.y;
					CurrentTiltAngle = Mathf.Clamp(CurrentTiltAngle, MinTiltAngle, MaxTiltAngle);

					transformChanged = true;
				}
			}

			// Auto-rotate
			if (AutoRotateSpeed != 0f && AutoRotateTimeout > 0f)
			{
				if (Time.unscaledTime - lastInputTime > AutoRotateTimeout)
				{
					if (Vector3.Dot(AutoRotateAxis.normalized, Vector3.up) > 0.99f)
					{
						CurrentOrbitAngle += AutoRotateSpeed * Time.deltaTime;
						transformChanged = true;
					}
				}
			}

			if (transformChanged)
			{
				OnTransformChanged?.Invoke();
			}
		}

		// ── Updated GetCameraTransform to use PivotYOffset ────────────────────────────────
		public (Vector3 position, Quaternion rotation) GetCameraTransform()
		{
			Vector3 gimbalPosition = currentModelBounds.center + PivotOffset;

			Quaternion rotation = Quaternion.Euler(CurrentTiltAngle, CurrentOrbitAngle, 0f);
			Vector3 forward = rotation * Vector3.forward;
			Vector3 cameraPosition = gimbalPosition - forward * CurrentDistance;

			return (cameraPosition, rotation);
		}
	}
}
