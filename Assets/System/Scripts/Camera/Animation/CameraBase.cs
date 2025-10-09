using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected CameraData _data;
		private bool HasStarted { get; set; }

		// === Shared cinema constants ===
		protected const float ProjectionSmoothingRate = 8f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float PauseDurationDefault = 1.5f;

		// === Shared cinema state ===
		protected Vector3 predictedPlayerPosition = Vector3.zero;
		protected Vector3 lastPlayerPos = Vector3.zero;
		protected float currentSequenceDuration = DefaultSequenceDuration;
		protected float sequenceTimer = DefaultSequenceDuration;
		protected float pauseTimer = PauseDurationDefault;

		// === Common interface ===
		public virtual Transform playerTransform { get; set; }
		public virtual List<Vector3> focusPoints { get; set; }

		protected virtual void Start() { }

		public virtual void Start(ref CameraData data)
		{
			_data = data;
			Start();
			if (data.postProcessingCameraController != null)
				data.postProcessingCameraController.enabled = data.enablePostProcessing;
			HasStarted = true;
		}

		protected virtual void Update() { }

		public virtual void Update(ref CameraData data)
		{
			_data = data;
			if (!HasStarted) Start(ref _data);
			Update();
			ApplyProjection(_data);
		}

		protected virtual void ApplyProjection(CameraData data)
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
		public virtual void SetOrigin(ref CameraData data, Vector3 value, bool immediate = false)
		{
			_data = data;
			SetOrigin(value, immediate);
		}

		protected virtual void SetOrigin(Vector3 value, bool immediate = false)
		{
			_data.origin = value;
			if (immediate) _data.lerpedOrigin = value;
		}

		public virtual void SetTarget(ref CameraData data, Vector3 value, bool immediate = false)
		{
			_data = data;
			SetTarget(value, immediate);
		}

		protected virtual void SetTarget(Vector3 value, bool immediate = false)
		{
			_data.target = value;
			if (immediate) _data.lerpedTarget = value;
		}

		// === Shared cinema utilities ===
		protected virtual void InitializeCinemaSequence()
		{
			sequenceTimer = pauseTimer = 0f;
			if (playerTransform == null) return;

			_data.smoothing = CameraData.DefaultSmoothingRate;
			_data.fieldOfView = 45f;
			_data.shake = 0f;
			_data.enablePostProcessing = true;

			currentSequenceDuration = DefaultSequenceDuration + Random.Range(-2f, 2f);
			sequenceTimer = currentSequenceDuration;
			pauseTimer = PauseDurationDefault;

			lastPlayerPos = predictedPlayerPosition = playerTransform.position;
		}

		protected virtual bool UpdateCinemaSequence()
		{
			// returns true if the cinematic sequence is still active (including pause phase)
			if (_data.camera == null || playerTransform == null) return false;

			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer > 0f)//inSequence
			{
				var posDelta = playerTransform.position - lastPlayerPos;
				predictedPlayerPosition = SmoothingUtils.SmoothVector(
					predictedPlayerPosition,
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
			var interpolate = SmoothingUtils.Smooth(0f, 1f, _data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			_data.lerpedOrigin = Vector3.Lerp(_data.lerpedOrigin, _data.origin, interpolate);
			_data.lerpedTarget = Vector3.Lerp(_data.lerpedTarget, _data.target, interpolate);

			return sequenceTimer > 0f || pauseTimer > 0f;
		}

		public virtual bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;
	}
}