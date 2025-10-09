using System;
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

		public Func<Transform> OnUpdatePlayer;
		public Func<List<Vector3>> OnUpdateFocusPoints;

		public virtual void Start(CameraController controller)
		{
			Start();
			if (null != _data.postProcessingCameraController)
				_data.postProcessingCameraController.enabled = _data.enablePostProcessing;
			HasStarted = true;
		}

		protected virtual void Start() { }

		public virtual void Update(CameraController controller)
		{
			if (!HasStarted) Start(controller);
			Update();
			ApplyProjection();
		}

		protected virtual void Update() { }

		protected virtual void ApplyProjection()
		{
			if (_data.camera == null) return;
			_data.camera.transform.position = _data.lerpedOrigin;
			var direction = _data.lerpedTarget - _data.lerpedOrigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				_data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			_data.camera.fieldOfView = _data.fieldOfView;
			CameraUtils.ApplyCameraShake(_data.camera, _data.shake);
		}

		// === Common helpers ===
		public virtual void SetOrigin(Vector3 value, bool immediate = false)
		{
			_data.origin = value;
			if (immediate) _data.lerpedOrigin = value;
		}

		public virtual void SetTarget(Vector3 value, bool immediate = false)
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

			currentSequenceDuration = DefaultSequenceDuration + UnityEngine.Random.Range(-2f, 2f);
			sequenceTimer = currentSequenceDuration;
			pauseTimer = PauseDurationDefault;

			lastPlayerPos = predictedPlayerPosition = playerTransform.position;
		}

		protected virtual bool UpdateCinemaSequence()
		{
			if (_data.camera == null || playerTransform == null) return false;

			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer > 0f)
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

		public CameraData GetData() => _data;
		public void SetData(CameraData data) => _data = data;
	}
}
