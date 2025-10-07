using UnityEngine;

namespace MassiveHadronLtd
{
	public abstract class CinemaCameraBase : CameraBase
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

		//cinema specific properties
		protected Vector3 lastPlayerPos;

		public override void Start()
		{
			base.Start();
			sequenceTimer = pauseTimer = 0f;//disable sequence by default
			if (null == playerTransform) return;

			cameraData = new CameraData
			{
				smoothing = 64f, // Default smoothing rate
				originSrc = Vector3.zero,
				originDst = Vector3.zero,
				targetSrc = Vector3.zero,
				targetDst = Vector3.zero,
				fieldOfView = 45f,
				enablePostProcessing = true
			};

			currentSequenceDuration = DefaultSequenceDuration + Random.Range(-2, 2);
			sequenceTimer = currentSequenceDuration;
			pauseTimer = PauseDuration;
			lastPlayerPos = predictedPlayerPosition = playerTransform.position;
			StartCinemaSequence();
			UpdateCinemaSequence();
		}

		protected abstract void StartCinemaSequence();

		public override void Update()
		{
			base.Update();
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
				UpdateCinemaSequence(easedSequenceTimer);

				lastPlayerPos = playerTransform.position;
			}

			var interpolate = SmoothingUtils.Smooth(0f, 1f, cameraData.smoothing, Time.deltaTime, CameraData.TargetFPS);
			cameraData.originSrc = Vector3.Lerp(cameraData.originSrc, cameraData.originDst, interpolate);
			cameraData.targetSrc = Vector3.Lerp(cameraData.targetSrc, cameraData.targetDst, interpolate);
			//cameraData.fovSrc = Mathf.Lerp(cameraData.fovSrc, cameraData.fovDst, interpolate); ToDo initialise FOV in StartSequence and lerp
		}

		protected abstract void UpdateCinemaSequence(float easedSequenceTimer = 0);

		public override bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;
	}
}