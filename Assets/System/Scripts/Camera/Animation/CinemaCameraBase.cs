using UnityEngine;

namespace MassiveHadronLtd
{
	public abstract class CinemaCameraBase : CameraBase
	{
		// Shared constants
		protected const float PauseDuration = 1.5f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float ProjectionSmoothingRate = 8f;//16f;
		protected Vector3 predictedPlayerPosition = Vector3.zero;    // Shared state - sequence data
		protected float pauseTimer;
		protected float sequenceTimer;
		protected float currentSequenceDuration;

		//cinema specific properties
		protected Vector3 lastPlayerPos;

		public override void Start(ref CameraData data)
		{
			base.Start(ref data);
			sequenceTimer = pauseTimer = 0f;//disable sequence by default
			if (null == playerTransform) return;

			data = new CameraData
			{
				smoothing = 64f, // Default smoothing rate
				originSrc = Vector3.zero,
				originDst = Vector3.zero,
				targetSrc = Vector3.zero,
				targetDst = Vector3.zero,
				fieldOfView = 45f,
				shake = 0f,
				enablePostProcessing = true
			};

			currentSequenceDuration = DefaultSequenceDuration + Random.Range(-2, 2);
			sequenceTimer = currentSequenceDuration;
			pauseTimer = PauseDuration;
			lastPlayerPos = predictedPlayerPosition = playerTransform.position;
			StartCinemaSequence(ref data);
			UpdateCinemaSequence(ref data, 0f);
		}

		protected abstract void StartCinemaSequence(ref CameraData data);

		public override void Update(ref CameraData data)
		{
			base.Update(ref data);
			// Update sequence timer
			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer <= 0f)
			{
				// Handle pause state
				if (pauseTimer <= 0f) return;
				pauseTimer -= Time.deltaTime;
			}
			else
			{
				// Smooth projected player position
				var posDelta = playerTransform.position - lastPlayerPos;
				predictedPlayerPosition = SmoothingUtils.SmoothVector(predictedPlayerPosition, playerTransform.position + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CameraData.TargetFPS);

				// Compute eased time
				var easedSequenceTimer = SmoothingUtils.Ease(currentSequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f);

				// Compute mode-specific positions and FOV
				UpdateCinemaSequence(ref data, easedSequenceTimer);

				lastPlayerPos = playerTransform.position;
			}

			var interpolate = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.originSrc = Vector3.Lerp(data.originSrc, data.originDst, interpolate);
			data.targetSrc = Vector3.Lerp(data.targetSrc, data.targetDst, interpolate);
			//data.fovSrc = Mathf.Lerp(data.fovSrc, data.fovDst, interpolate); ToDo initialise FOV in StartSequence and lerp
		}

		protected abstract void UpdateCinemaSequence(ref CameraData data, float easedSequenceTimer = 0);

		public override bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;
	}
}
