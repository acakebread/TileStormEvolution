using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected CameraData data;
		public CameraData Data { get => data; set => data = value; }
		public Func<Transform> playerTransform;
		public Func<List<Vector3>> focusPoints;

		// === Shared cinema constants ===
		protected const float ProjectionSmoothingRate = 8f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float PauseDurationDefault = 1.5f;

		// === Shared cinema state ===
		protected Vector3 lastPlayerPos = Vector3.zero;
		protected Vector3 nextPlayerPos = Vector3.zero;//prediction
		protected float sequenceDuration = DefaultSequenceDuration;
		protected float sequenceTimer = DefaultSequenceDuration;
		protected float pauseTimer = PauseDurationDefault;

		public virtual void Awake(CameraController controller)
		{
			Awake();
			if (null != data.postProcessingCameraController)
				data.postProcessingCameraController.enabled = data.enablePostProcessing;
		}

		protected virtual void Awake() { }

		public virtual void Start(CameraController controller) { Start(); }

		protected virtual void Start() { }

		public virtual void Update(CameraController controller)
		{
			Update();
			ApplyProjection();
		}

		protected virtual void Update() { }

		protected virtual void ApplyProjection()
		{
			if (data.camera == null) return;
			data.camera.transform.position = data.lerpedOrigin;
			var direction = data.lerpedTarget - data.lerpedOrigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			data.camera.fieldOfView = data.fieldOfView;
			CameraUtils.ApplyCameraShake(data.camera, data.shake);
		}

		// === Common helpers ===
		public virtual void SetOrigin(Vector3 value, bool immediate = false)
		{
			data.origin = value;
			if (immediate) data.lerpedOrigin = value;
		}

		public virtual void SetTarget(Vector3 value, bool immediate = false)
		{
			data.target = value;
			if (immediate) data.lerpedTarget = value;
		}

		// === Shared cinema utilities ===
		protected virtual void InitializeCinemaSequence()
		{
			var playerTransform = this.playerTransform?.Invoke();
			sequenceTimer = pauseTimer = 0f;
			if (playerTransform == null) return;

			sequenceDuration = DefaultSequenceDuration + UnityEngine.Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = PauseDurationDefault;

			lastPlayerPos = nextPlayerPos = playerTransform.position;
		}

		protected virtual bool UpdateCinemaSequence()
		{
			var playerTransform = this.playerTransform?.Invoke();
			if (data.camera == null || playerTransform == null) return false;

			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer > 0f)
			{
				var posDelta = playerTransform.position - lastPlayerPos;
				nextPlayerPos = SmoothingUtils.SmoothVector(
					nextPlayerPos,
					playerTransform.position + posDelta * 2f,
					ProjectionSmoothingRate,
					Time.deltaTime,
					CameraData.TargetFPS);

				lastPlayerPos = playerTransform.position;
			}
			else
			{
				pauseTimer -= Time.deltaTime;
			}

			// Update camera lerping
			var interpolate = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.lerpedOrigin = Vector3.Lerp(data.lerpedOrigin, data.origin, interpolate);
			data.lerpedTarget = Vector3.Lerp(data.lerpedTarget, data.target, interpolate);

			return sequenceTimer > 0f || pauseTimer > 0f;
		}

		public virtual bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;
	}
}
