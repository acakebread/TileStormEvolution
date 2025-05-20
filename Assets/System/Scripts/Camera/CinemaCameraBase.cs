using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CinemaCameraBase
	{
		// Shared constants
		protected const float PauseDuration = 1.5f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float ProjectionSmoothingRate = 8f;//16f;
		protected Vector3 predictedPlayerPosition = Vector3.zero;

		// Shared state - sequence data
		protected float pauseTimer;
		protected float sequenceTimer;
		protected float currentSequenceDuration;

		//temporary workarounds
		protected Vector3 lastPlayerPos;
		protected Transform playerTransform => CinemaController.playerTransform;
		protected List<Vector3> focusPoints => CinemaController.focusPoints;

		protected Vector3 originSrc { get => cameraData.originSrc; set => cameraData.originSrc = value; }
		protected Vector3 originDst { get => cameraData.originDst; set => cameraData.originDst = value; }
		protected Vector3 targetSrc { get => cameraData.targetSrc; set => cameraData.targetSrc = value; }
		protected Vector3 targetDst { get => cameraData.targetDst; set => cameraData.targetDst = value; }
		protected float fieldOfView { get => cameraData.fieldOfView; set => cameraData.fieldOfView = value; }
		protected float smoothing { get => cameraData.smoothing; set => cameraData.smoothing = value; }
		protected float shake { get => cameraData.shake; set => cameraData.shake = value; }

		public CameraData cameraData;

		public virtual void StartSequence()
		{
			sequenceTimer = pauseTimer = 0f;//disable sequence by default
			if (null == CinemaController.playerTransform) return;

			cameraData = new CameraData
			{
				smoothing = 64f, // Default smoothing rate
				originSrc = Vector3.zero,
				originDst = Vector3.zero,
				targetSrc = Vector3.zero,
				targetDst = Vector3.zero,
				fieldOfView = 45f
			};

			currentSequenceDuration = DefaultSequenceDuration + Random.Range(-2, 2);
			sequenceTimer = currentSequenceDuration;
			pauseTimer = PauseDuration;
			lastPlayerPos = predictedPlayerPosition = playerTransform.position;
			Start();
		}

		protected abstract void Start();

		public virtual bool Update()
		{
			// Update sequence timer
			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer <= 0f)
			{
				// Handle pause state
				if (pauseTimer <= 0f) return false;
				pauseTimer -= Time.deltaTime;
			}
			else
			{
				// Smooth projected player position
				var posDelta = playerTransform.position - lastPlayerPos;
				predictedPlayerPosition = SmoothingUtils.SmoothVector(predictedPlayerPosition, playerTransform.position + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CinemaController.TargetFPS);

				// Compute eased time
				var easedSequenceTimer = SmoothingUtils.Ease(currentSequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f);

				// Compute mode-specific positions and FOV
				UpdateSequence(easedSequenceTimer);

				lastPlayerPos = playerTransform.position;
			}

			var interpolate = SmoothingUtils.Smooth(0f, 1f, smoothing, Time.deltaTime, CinemaController.TargetFPS);
			originSrc = Vector3.Lerp(originSrc, originDst, interpolate);
			targetSrc = Vector3.Lerp(targetSrc, targetDst, interpolate);
			//fovSrc = Mathf.Lerp(fovSrc, fovDst, interpolate); ToDo initialise FOV in StartSequence and lerp
			return true;
		}

		protected abstract void UpdateSequence(float easedSequenceTimer);
	}
}