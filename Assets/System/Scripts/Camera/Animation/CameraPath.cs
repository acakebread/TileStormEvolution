using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class CameraPath : CameraBase
	{
		protected Func<Vector3> originFn;
		protected Func<Vector3> targetFn;
		protected Func<IReadOnlyList<Vector3>> pointsFn;
		protected Vector3 origin => originFn?.Invoke() ?? Vector3.zero;
		protected Vector3 target => targetFn?.Invoke() ?? Vector3.zero;
		protected IReadOnlyList<Vector3> points => pointsFn?.Invoke() ?? Array.Empty<Vector3>();//focus points

		private const float VerticalOffset = 0.5f;
		private const float MinDistance = 1f;
		private const float MinFocusPointDistanceFromPlayer = 4f;
		private const float MaxLookAtAngle = 20f;
		private const float SmoothingRate = 16f;
		private const float FovMin = 35f;
		private const float FovMax = 55f;
		private const float MinCameraHeight = 1.5f;
		private const float MaxCameraHeight = 4f;
		private const bool SortDstNearerPlayer = true;

		private float currentFovMax;
		private BezierData bezierData;

		private struct BezierData
		{
			public Vector3 P0; // Src
			public Vector3 P1; // Control point
			public Vector3 P2; // Dst
		}

		private Vector3 localOrigin; // Local field to avoid shadowing CameraBase.origin
		private Vector3 localTarget; // Local field to avoid shadowing CameraBase.target

		protected const float DefaultSmoothingRate = 64f;
		protected const float ProjectionSmoothingRate = 8f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float DefaultPauseDuration = 1.5f;

		protected Vector3 lastTarget = Vector3.zero;
		protected Vector3 nextTarget = Vector3.zero;
		protected float sequenceDuration = DefaultSequenceDuration;
		protected float sequenceTimer = DefaultSequenceDuration;
		protected float pauseTimer = DefaultPauseDuration;

		public CameraPath(CameraConfig config) : base(config)
		{
			if (null != config)
			{
				data = config.data;
				originFn = config.origin;
				targetFn = config.target;
				pointsFn = config.points;
			}
		}

		public override void Awake()
		{
			//initialise camera
			var camera = data.camera;
			if (camera == null) return;
			camera.transform.position = originFn?.Invoke() ?? data.origin;
			var direction = (targetFn?.Invoke() ?? data.target) - camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}

		protected virtual void InitializeCinemaSequence()
		{
			sequenceTimer = pauseTimer = 0f;
			sequenceDuration = DefaultSequenceDuration + UnityEngine.Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = DefaultPauseDuration;
			lastTarget = nextTarget = this.target; // Use CameraBase.target property
		}

		protected virtual void UpdateCinemaLerping()
		{
			var interpolate = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, TargetFPS);
			data.origin = Vector3.Lerp(data.origin, localOrigin, interpolate);
			data.target = Vector3.Lerp(data.target, localTarget, interpolate);
		}

		public override bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;

		public override void Start()
		{
			base.Start();
			data.smoothing = DefaultSmoothingRate;
			data.fieldOfView = 45f;
			data.shake = 0f;
			data.postProcessingEnabled = true;

			if (data?.camera == null)
			{
				Debug.LogWarning("CameraPath.Awake: Missing camera or delegates");
				return;
			}

			var targetPosition = this.target; // Use CameraBase.target property
			var _focusPoints = this.points; // Use CameraBase.points property (focus points)

			sequenceDuration = DefaultSequenceDuration + UnityEngine.Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = DefaultPauseDuration;
			lastTarget = nextTarget = targetPosition;

			data.shake = 1f;
			bezierData = default;
			currentFovMax = FovMax;

			// Select start focus point
			var startFocusPoint = targetPosition;
			if (_focusPoints != null && _focusPoints.Count > 0)
			{
				var validFocusPoint = _focusPoints.Where(p =>
					Vector2.Distance(new Vector2(p.x, p.z), new Vector2(targetPosition.x, targetPosition.z)) >= MinFocusPointDistanceFromPlayer).ToList();
				if (validFocusPoint.Count > 0)
					startFocusPoint = validFocusPoint[UnityEngine.Random.Range(0, validFocusPoint.Count)];
			}

			data.target = localTarget = startFocusPoint + Vector3.up * VerticalOffset;
			localTarget = targetPosition + Vector3.up * VerticalOffset;

			// Define lozenge
			var targetPath = localTarget - data.target;
			var pathDir = targetPath.magnitude > 0.1f ? targetPath.normalized : UnityEngine.Random.onUnitSphere;
			var midPoint = (data.target + localTarget) / 2f;
			var perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;
			var lozengeMajor = targetPath.magnitude + 2f * MinDistance;
			var lozengeMinor = Mathf.Max(lozengeMajor * 0.66f, MinDistance * 2f);

			// Generate camera points
			var (src, dst) = SampleCameraPosition(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor);
			data.origin = src;
			localOrigin = dst;

			data.origin = AdjustHeight(data.origin, data.target);
			localOrigin = AdjustHeight(localOrigin, localTarget);

			// Initialize FOV
			data.fieldOfView = FovMin;
			currentFovMax = UnityEngine.Random.value < 0.2f ? 60f : FovMax;
		}

		public override void Update()
		{
			base.Update();

			var _target = this.target; // Use CameraBase.target property
			var posDelta = _target - lastTarget;
			lastTarget = _target;

			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer > 0f)
			{
				nextTarget = SmoothingUtils.SmoothVector(nextTarget, _target + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);

				// Update target
				var easedSequenceTimer = SmoothingUtils.Ease(sequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / sequenceDuration) : 1f);
				localTarget = Vector3.Lerp(data.target, nextTarget, easedSequenceTimer);

				// Update Bezier P1 and P2 with player movement
				bezierData.P1 += posDelta * 0.5f;
				bezierData.P2 += posDelta;

				// Update camera dest position and FOV
				localOrigin = QuadraticBezierPoint(easedSequenceTimer, bezierData.P0, bezierData.P1, bezierData.P2);
				data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / sequenceDuration));
			}
			else
				pauseTimer -= Time.deltaTime;

			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingRate, sequenceDuration, Time.deltaTime, TargetFPS);

			UpdateCinemaLerping();
			OnRender();
		}

		private Vector3 QuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
		{
			var u = 1f - t;
			var tt = t * t;
			var uu = u * u;
			return uu * p0 + 2f * u * t * p1 + tt * p2;
		}

		private (Vector3 src, Vector3 dst) SampleCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor)
		{
			var midPointXZ = new Vector2(midPoint.x, midPoint.z);
			var targetXZ = new Vector2(this.target.x, this.target.z); // Use CameraBase.target property

			// Generate points
			var (src, perimeterPointSrc, tangentSrc1, tangentSrc2, thetaSrc, projectionDistanceSrc) = GeneratePointOutsideLozenge(midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);
			var (dst, perimeterPointDst, tangentDst1, tangentDst2, thetaDst, projectionDistanceDst) = GeneratePointOutsideLozenge(midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);

			// Check if points are on the same side
			var srcXZ = new Vector2(src.x, src.z);
			var dstXZ = new Vector2(dst.x, dst.z);
			var v1 = srcXZ - midPointXZ;
			var v2 = dstXZ - midPointXZ;
			var dot = Vector2.Dot(v1, v2);

			if (dot > 0)
			{
				Vector2 relativeDst = dstXZ - midPointXZ;
				Vector2 flippedDst = midPointXZ - relativeDst;
				dst = new Vector3(flippedDst.x, dst.y, flippedDst.y);
				perimeterPointDst = MathEllipse.GetEllipsePoint(thetaDst, lozengeMajor, lozengeMinor, midPointXZ, pathDir, perpendicular);
				var (newTangent1, newTangent2) = MathEllipse.ComputeTangentAtPoint(thetaDst, lozengeMajor, lozengeMinor, pathDir, perpendicular);
				tangentDst1 = newTangent1;
				tangentDst2 = newTangent2;
				projectionDistanceDst = Vector2.Distance(midPointXZ, dstXZ) - Vector2.Distance(midPointXZ, perimeterPointDst);
				dstXZ = new Vector2(dst.x, dst.z);
			}

			// Sort points so Dst is closer to target if SortDstNearerPlayer is true
			if (SortDstNearerPlayer)
			{
				float srcDist = Vector2.Distance(srcXZ, targetXZ);
				float dstDist = Vector2.Distance(dstXZ, targetXZ);
				if (srcDist < dstDist)
				{
					(src, dst) = (dst, src);
					(perimeterPointSrc, perimeterPointDst) = (perimeterPointDst, perimeterPointSrc);
					(tangentSrc1, tangentDst1) = (tangentDst1, tangentSrc1);
					(tangentSrc2, tangentDst2) = (tangentDst2, tangentSrc2);
					(thetaSrc, thetaDst) = (thetaDst, thetaSrc);
					(projectionDistanceSrc, projectionDistanceDst) = (projectionDistanceDst, projectionDistanceSrc);
					(srcXZ, dstXZ) = (dstXZ, srcXZ);
				}
			}

			// Compute tangent points
			var (tangentPointSrc1, tangentPointSrc2) = MathEllipse.ComputeTangentPoints(src, midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);
			var (tangentPointDst1, tangentPointDst2) = MathEllipse.ComputeTangentPoints(dst, midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);

			// Compute control point
			Vector3? controlPoint = null;

			if (tangentPointSrc1.HasValue && tangentPointSrc2.HasValue && tangentPointDst1.HasValue && tangentPointDst2.HasValue)
			{
				var src1XZ = new Vector2(tangentPointSrc1.Value.x, tangentPointSrc1.Value.z);
				var src2XZ = new Vector2(tangentPointSrc2.Value.x, tangentPointSrc2.Value.z);
				var dst1XZ = new Vector2(tangentPointDst1.Value.x, tangentPointDst1.Value.z);
				var dst2XZ = new Vector2(tangentPointDst2.Value.x, tangentPointDst2.Value.z);
				var srcDir1 = (src1XZ - srcXZ).normalized;
				var srcDir2 = (src2XZ - srcXZ).normalized;
				var dstDir1 = (dst1XZ - dstXZ).normalized;
				var dstDir2 = (dst2XZ - dstXZ).normalized;

				var cross1 = Mathf.Abs(srcDir1.x * dstDir2.y - srcDir1.y * dstDir2.x);
				var cross2 = Mathf.Abs(srcDir2.x * dstDir1.y - srcDir2.y * dstDir1.x);
				if (cross1 < 0.01f || cross2 < 0.01f)
				{
					controlPoint = (src + dst) / 2f;
				}
				else
				{
					var intersection1 = LineIntersection(srcXZ, srcXZ + srcDir1, dstXZ, dstXZ + dstDir2);
					var intersection2 = LineIntersection(srcXZ, srcXZ + srcDir2, dstXZ, dstXZ + dstDir1);

					if (intersection1.HasValue && intersection2.HasValue)
					{
						var dist1 = Vector2.Distance(intersection1.Value, midPointXZ);
						var dist2 = Vector2.Distance(intersection2.Value, midPointXZ);
						var selectedIntersection = dist1 < dist2 ? intersection1.Value : intersection2.Value;
						controlPoint = new Vector3(selectedIntersection.x, (src.y + dst.y) / 2f, selectedIntersection.y);
					}
				}
			}

			// Fallback to midpoint
			if (!controlPoint.HasValue)
			{
				controlPoint = (src + dst) / 2f;
			}

			// Create Bezier curve
			bezierData = new BezierData { P0 = src, P1 = controlPoint.Value, P2 = dst };

			return (src, dst);
		}

		private Vector2? LineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector4 p4)
		{
			var denom = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
			if (Mathf.Abs(denom) < 0.0001f) return null;

			var t = ((p1.x - p3.x) * (p3.y - p4.y) - (p1.y - p3.y) * (p3.x - p4.x)) / denom;
			var u = -((p1.x - p2.x) * (p1.y - p3.y) - (p1.y - p2.y) * (p1.x - p3.x)) / denom;

			return t >= 0 && u >= 0 ? p1 + t * (p2 - p1) : null;
		}

		private (Vector3 point, Vector2 perimeterPoint, Vector3 tangent1, Vector3 tangent2, float theta, float projectionDistance)
			GeneratePointOutsideLozenge(Vector2 midPointXZ, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor)
		{
			var theta = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
			var perimeterPoint = MathEllipse.GetEllipsePoint(theta, lozengeMajor, lozengeMinor, midPointXZ, pathDir, perpendicular);
			var direction = (perimeterPoint - midPointXZ).normalized;
			var distanceToPerimeter = Vector2.Distance(midPointXZ, perimeterPoint);
			var projectionDistance = distanceToPerimeter + UnityEngine.Random.Range(1f, 5f);
			var pointXZ = midPointXZ + direction * projectionDistance;
			var point = new Vector3(pointXZ.x, UnityEngine.Random.Range(MinCameraHeight, MaxCameraHeight), pointXZ.y);

			var (tangent1, tangent2) = MathEllipse.ComputeTangentAtPoint(theta, lozengeMajor, lozengeMinor, pathDir, perpendicular);

			return (point, perimeterPoint, tangent1, tangent2, theta, projectionDistance);
		}

		private Vector3 AdjustHeight(Vector3 position, Vector3 target)
		{
			var positionXZ = new Vector2(position.x, position.z);
			var targetXZ = new Vector2(target.x, target.z);
			var distXZ = Vector2.Distance(positionXZ, targetXZ);

			var direction = (target - position).normalized;
			var pitch = Vector3.Angle(direction, Vector3.down) - 90f;
			if (pitch > MaxLookAtAngle)
			{
				var maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
				var idealHeight = target.y + VerticalOffset + distXZ / Mathf.Tan(maxPitchRad);
				position.y = Mathf.Clamp(idealHeight, MinCameraHeight, MaxCameraHeight);
			}
			return position;
		}
	}
}