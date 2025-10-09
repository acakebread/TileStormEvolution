using UnityEngine;
using System.Linq;

namespace MassiveHadronLtd
{
	public class CameraPath : CameraBase
	{
		private const float VerticalOffset = 0.5f;
		private const float MinDistance = 1f;
		private const float MinFocusPointDistanceFromPlayer = 4f;
		private const float MaxLookAtAngle = 20f;
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

		public override bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;

		protected override void Start()
		{
			InitializeCinemaSequence();
			if (_data.camera == null || playerTransform == null)
			{
				Debug.LogWarning("CameraPath.Start: Missing camera or playerTransform");
				return;
			}

			_data.shake = 1f;
			bezierData = default;
			currentFovMax = FovMax;

			// Select start focus point
			var startFocusPoint = playerTransform.position;
			if (focusPoints != null && focusPoints.Count > 0)
			{
				var validFocusPoint = focusPoints
					.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) >= MinFocusPointDistanceFromPlayer)
					.ToList();
				if (validFocusPoint.Count > 0) startFocusPoint = validFocusPoint[Random.Range(0, validFocusPoint.Count)];
			}

			_data.lerpedTarget = _data.target = startFocusPoint + Vector3.up * VerticalOffset;
			_data.target = playerTransform.position + Vector3.up * VerticalOffset;

			// Define lozenge
			var targetPath = _data.target - _data.lerpedTarget;
			var pathDir = targetPath.magnitude > 0.1f ? targetPath.normalized : Random.onUnitSphere;
			var midPoint = (_data.lerpedTarget + _data.target) / 2f;
			var perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;
			var lozengeMajor = targetPath.magnitude + 2f * MinDistance;
			var lozengeMinor = Mathf.Max(lozengeMajor * 0.66f, MinDistance * 2f);

			// Generate camera points
			var (src, dst) = SampleCameraPosition(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor);
			_data.lerpedPosition = src;
			_data.position = dst;

			_data.lerpedPosition = AdjustHeight(_data.lerpedPosition, _data.lerpedTarget);
			_data.position = AdjustHeight(_data.position, _data.target);

			// Initialize FOV
			_data.fieldOfView = FovMin;
			currentFovMax = Random.value < 0.2f ? 60f : FovMax;
		}

		protected override void Update()
		{
			if (_data.camera == null || playerTransform == null)
			{
				Debug.LogWarning("CameraPath.Update: Missing camera or playerTransform");
				return;
			}

			if (!UpdateCinemaSequence()) return;

			if (sequenceTimer > 0f)
			{
				var easedSequenceTimer = SmoothingUtils.Ease(currentSequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f);

				// Update target
				_data.target = Vector3.Lerp(_data.lerpedTarget, predictedPlayerPosition + Vector3.up * VerticalOffset, easedSequenceTimer);

				// Update Bezier P1 (camera path mid point) and P2 (camera path Dst) with player movement
				var playerDelta = playerTransform.position - lastPlayerPos;
				bezierData.P1 += playerDelta * 0.5f;
				bezierData.P2 += playerDelta;

				// Update camera dest position and FOV
				_data.position = EvaluateBezier(easedSequenceTimer);
				_data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration));
			}

			// Update camera lerping
			_data.smoothing = SmoothingUtils.Smooth(_data.smoothing, 16, currentSequenceDuration, Time.deltaTime, CameraData.TargetFPS);
		}

		private Vector3 EvaluateBezier(float t)
		{
			var u = 1f - t;
			var tt = t * t;
			var uu = u * u;
			return uu * bezierData.P0 + 2f * u * t * bezierData.P1 + tt * bezierData.P2;
		}

		private (Vector3 src, Vector3 dst) SampleCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor)
		{
			var midPointXZ = new Vector2(midPoint.x, midPoint.z);
			var targetXZ = new Vector2(_data.target.x, _data.target.z);

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
				controlPoint = (src + dst) / 2f;

			// Create Bezier curve
			bezierData = new BezierData { P0 = src, P1 = controlPoint.Value, P2 = dst };

			return (src, dst);
		}

		private Vector2? LineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
		{
			var denom = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
			if (Mathf.Abs(denom) < 0.0001f) return null;

			var t = ((p1.x - p3.x) * (p3.y - p4.y) - (p1.y - p3.y) * (p3.x - p4.x)) / denom;
			var u = -((p1.x - p2.x) * (p1.y - p3.y) - (p1.y - p2.y) * (p1.x - p3.x)) / denom;

			return t >= 0 && u >= 0 ? p1 + t * (p2 - p1) : null;
		}

		private (Vector3 point, Vector2 perimeterPoint, Vector3 tangent1, Vector3 tangent2, float theta, float projectionDistance) GeneratePointOutsideLozenge(Vector2 midPointXZ, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor)
		{
			var theta = Random.Range(0f, 2f * Mathf.PI);
			var perimeterPoint = MathEllipse.GetEllipsePoint(theta, lozengeMajor, lozengeMinor, midPointXZ, pathDir, perpendicular);
			var direction = (perimeterPoint - midPointXZ).normalized;
			var distanceToPerimeter = Vector2.Distance(midPointXZ, perimeterPoint);
			var projectionDistance = distanceToPerimeter + Random.Range(1f, 5f);
			var pointXZ = midPointXZ + direction * projectionDistance;
			var point = new Vector3(pointXZ.x, Random.Range(MinCameraHeight, MaxCameraHeight), pointXZ.y);

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