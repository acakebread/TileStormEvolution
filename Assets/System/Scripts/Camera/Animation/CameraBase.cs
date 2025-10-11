using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public struct CameraDelegates
	{
		public Func<Vector3> target;
		public Func<IReadOnlyList<Vector3>> focusPoints;
	}

	public abstract class CameraBase
	{
		public CameraData data;
		public Func<CameraDelegates> delegates;

		public virtual void Awake() { }
		public virtual void Update() { }

		public virtual bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;

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

		protected virtual void ApplyProjection()
		{
			if (null == data.camera) return;
			data.camera.transform.position = data.lerpedOrigin;
			var direction = data.lerpedTarget - data.lerpedOrigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			data.camera.fieldOfView = data.fieldOfView;
			//CameraUtils.ApplyCameraShake(data.camera, data.shake);
		}

		// === Shared cinema constants ===
		protected const float ProjectionSmoothingRate = 8f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float DefaultPauseDuration = 1.5f;

		// === Shared cinema state ===
		protected Vector3 lastPlayerPos = Vector3.zero;
		protected Vector3 nextPlayerPos = Vector3.zero;//prediction
		protected float sequenceDuration = DefaultSequenceDuration;
		protected float sequenceTimer = DefaultSequenceDuration;
		protected float pauseTimer = DefaultPauseDuration;

		// === Shared cinema utilities ===
		protected virtual void InitializeCinemaSequence()
		{
			var delegatesInstance = delegates.Invoke();
			sequenceTimer = pauseTimer = 0f;

			sequenceDuration = DefaultSequenceDuration + UnityEngine.Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = DefaultPauseDuration;

			lastPlayerPos = nextPlayerPos = delegatesInstance.target.Invoke();
		}

		protected virtual bool UpdateCinemaSequence()
		{
			if (null == data.camera) return false;
			var delegatesInstance = delegates.Invoke();

			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer > 0f)
			{
				var _target = delegatesInstance.target.Invoke();
				var posDelta = _target - lastPlayerPos;
				nextPlayerPos = SmoothingUtils.SmoothVector(nextPlayerPos, _target + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CameraData.TargetFPS);
				lastPlayerPos = _target;
			}
			else
				pauseTimer -= Time.deltaTime;

			// Update camera lerping
			var interpolate = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.lerpedOrigin = Vector3.Lerp(data.lerpedOrigin, data.origin, interpolate);
			data.lerpedTarget = Vector3.Lerp(data.lerpedTarget, data.target, interpolate);

			return sequenceTimer > 0f || pauseTimer > 0f;
		}
	}
}



//using System;
//using UnityEngine;
//using System.Collections.Generic;

//namespace MassiveHadronLtd
//{
//	public struct CameraCallbacks
//	{
//		public Func<Transform> playerTransform;
//		public Func<IReadOnlyList<Vector3>> focusPoints;
//	}

//	public abstract class CameraBase
//	{
//		public CameraData data;
//		public Func<CameraCallbacks> callbacks;//implementation eeded in GameController
//		public Func<Transform> playerTransform;//I want to replace these with the callbacks
//		public Func<IReadOnlyList<Vector3>> focusPoints;//I want to replace these with the callbacks

//		public virtual void Awake() { }
//		public virtual void Update() { }

//		public virtual bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;

//		// === Common helpers ===
//		public virtual void SetOrigin(Vector3 value, bool immediate = false)
//		{
//			data.origin = value;
//			if (immediate) data.lerpedOrigin = value;
//		}

//		public virtual void SetTarget(Vector3 value, bool immediate = false)
//		{
//			data.target = value;
//			if (immediate) data.lerpedTarget = value;
//		}

//		protected virtual void ApplyProjection()
//		{
//			if (data.camera == null) return;
//			data.camera.transform.position = data.lerpedOrigin;
//			var direction = data.lerpedTarget - data.lerpedOrigin;
//			if (direction.sqrMagnitude > Mathf.Epsilon)
//				data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
//			data.camera.fieldOfView = data.fieldOfView;
//			CameraUtils.ApplyCameraShake(data.camera, data.shake);
//		}

//		// === Shared cinema constants ===
//		protected const float ProjectionSmoothingRate = 8f;
//		protected const float DefaultSequenceDuration = 8f;
//		protected const float DefaultPauseDuration = 1.5f;

//		// === Shared cinema state ===
//		protected Vector3 lastPlayerPos = Vector3.zero;
//		protected Vector3 nextPlayerPos = Vector3.zero;//prediction
//		protected float sequenceDuration = DefaultSequenceDuration;
//		protected float sequenceTimer = DefaultSequenceDuration;
//		protected float pauseTimer = DefaultPauseDuration;

//		// === Shared cinema utilities ===
//		protected virtual void InitializeCinemaSequence()
//		{
//			var transform = playerTransform?.Invoke();
//			sequenceTimer = pauseTimer = 0f;
//			if (null == transform) return;

//			sequenceDuration = DefaultSequenceDuration + UnityEngine.Random.Range(-2f, 2f);
//			sequenceTimer = sequenceDuration;
//			pauseTimer = DefaultPauseDuration;

//			lastPlayerPos = nextPlayerPos = transform.position;
//		}

//		protected virtual bool UpdateCinemaSequence()
//		{
//			var transform = playerTransform?.Invoke();
//			if (null == data.camera || null == transform) return false;

//			sequenceTimer -= Time.deltaTime;
//			if (sequenceTimer > 0f)
//			{
//				var posDelta = transform.position - lastPlayerPos;
//				nextPlayerPos = SmoothingUtils.SmoothVector(nextPlayerPos, transform.position + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CameraData.TargetFPS);
//				lastPlayerPos = transform.position;
//			}
//			else
//				pauseTimer -= Time.deltaTime;

//			// Update camera lerping
//			var interpolate = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
//			data.lerpedOrigin = Vector3.Lerp(data.lerpedOrigin, data.origin, interpolate);
//			data.lerpedTarget = Vector3.Lerp(data.lerpedTarget, data.target, interpolate);

//			return sequenceTimer > 0f || pauseTimer > 0f;
//		}
//	}
//}
