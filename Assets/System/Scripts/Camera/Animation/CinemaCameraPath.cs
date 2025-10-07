using UnityEngine;
using System.Linq;

namespace MassiveHadronLtd
{
	public class CinemaCameraPath : CinemaCameraBase
	{
		private const float VerticalOffset = 0.5f;
		private const float MinDistance = 1f;
		private const float MinFocusPointDistanceFromPlayer = 4f;
		private const float MaxLookAtAngle = 20f;
		private const float FovMin = 35f;
		private const float FovMax = 55f;
		private const float MinCameraHeight = 1.5f;
		private const float MaxCameraHeight = 4f;
		private const float TangentLineExtension = 2f;
		private const bool SortDstNearerPlayer = true;

		private const float DEBUG_DRAW_DURATION = 5f;
		public static bool DEBUG_VISUALIZE_LOZENGE = true;

		private float currentFovMax;
		private BezierData bezierData;

		private struct BezierData
		{
			public Vector3 P0; // Src
			public Vector3 P1; // Control point
			public Vector3 P2; // Dst
			//public AnimationCurve curveX;
			//public AnimationCurve curveY;
			//public AnimationCurve curveZ;
	}

		//private Vector3 originSrc { get => cameraData.originSrc; set => cameraData.originSrc = value; }
		//private Vector3 originDst { get => cameraData.originDst; set => cameraData.originDst = value; }
		//private Vector3 targetSrc { get => cameraData.targetSrc; set => cameraData.targetSrc = value; }
		//private Vector3 targetDst { get => cameraData.targetDst; set => cameraData.targetDst = value; }
		//private float fieldOfView { get => cameraData.fieldOfView; set => cameraData.fieldOfView = value; }
		//private float smoothing { get => cameraData.smoothing; set => cameraData.smoothing = value; }
		//private float shake { get => cameraData.shake; set => cameraData.shake = value; }

		protected override void StartCinemaSequence(ref CameraData data)
		{
			if (null == playerTransform) return;

			data.shake = 1f;
			bezierData = default;
			currentFovMax = FovMax;

			// Select start focus point
			var startFocusPoint = playerTransform.position;
			if (focusPoints.Count > 0)
			{
				var validFocusPoint = focusPoints.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) >= MinFocusPointDistanceFromPlayer).ToList();
				if (validFocusPoint.Count > 0) startFocusPoint = validFocusPoint[Random.Range(0, validFocusPoint.Count)];
			}

			data.targetSrc = startFocusPoint + Vector3.up * VerticalOffset;
			data.targetDst = playerTransform.position + Vector3.up * VerticalOffset;

			// Define lozenge
			var targetPath = data.targetDst - data.targetSrc;
			var pathDir = targetPath.magnitude > 0.1f ? targetPath.normalized : Random.onUnitSphere;
			var midPoint = (data.targetSrc + data.targetDst) / 2f;
			var perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;
			var lozengeMajor = targetPath.magnitude + 2f * MinDistance;
			var lozengeMinor = Mathf.Max(lozengeMajor * 0.66f, MinDistance * 2f);

			// Generate camera points
			var (src, dst) = SampleCameraPosition(ref data, midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor);
			data.originSrc = src;
			data.originDst = dst;

			data.originSrc = AdjustHeight(data.originSrc, data.targetSrc);
			data.originDst = AdjustHeight(data.originDst, data.targetDst);

			// Initialize FOV
			data.fieldOfView = FovMin;
			currentFovMax = Random.value < 0.2f ? 60f : FovMax;

			// Visualize focus points
			if (DEBUG_VISUALIZE_LOZENGE)
			{
				Debug.DrawRay(data.targetSrc, Vector3.up * 5f, Color.red, DEBUG_DRAW_DURATION);
				Debug.DrawRay(data.targetDst, Vector3.up * 5f, Color.blue, DEBUG_DRAW_DURATION);
				//Debug.Log($"TargetSrc={targetSrc}, TargetDst={targetDst}, LozengeMajor={lozengeMajor}, LozengeMinor={lozengeMinor}, PathDir={pathDir}, Perpendicular={perpendicular}");
			}

			static Vector3 AdjustHeight(Vector3 position, Vector3 target)
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

		protected override void UpdateCinemaSequence(ref CameraData data, float easedSequenceTimer)
		{
			//update target
			data.targetDst = Vector3.Lerp(data.targetSrc, predictedPlayerPosition + Vector3.up * VerticalOffset, easedSequenceTimer);

			// Update Bezier P1 (camera path mid point) and P2 (camera path Dst) with player movement
			var playerDelta = playerTransform.position - lastPlayerPos;
			bezierData.P1 += playerDelta * 0.5f;
			bezierData.P2 += playerDelta;

			//update camera dest position and FOV
			data.originDst = EvaluateBezier(easedSequenceTimer);// Evaluate Bezier curve
			data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration));

			//update camera lerping
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, 16, currentSequenceDuration, Time.deltaTime, CameraData.TargetFPS);
		}

		private Vector3 EvaluateBezier(float t) => QuadraticBezierPoint(t, bezierData.P0, bezierData.P1, bezierData.P2); // Direct evaluation

		private (Vector3 src, Vector3 dst) SampleCameraPosition(ref CameraData data, Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor)
		{
			var midPointXZ = new Vector2(midPoint.x, midPoint.z);
			var targetDstXZ = new Vector2(data.targetDst.x, data.targetDst.z);

			// Generate points
			var (src, perimeterPointSrc, tangentSrc1, tangentSrc2, thetaSrc, projectionDistanceSrc) = GeneratePointOutsideLozenge(midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);
			var (dst, perimeterPointDst, tangentDst1, tangentDst2, thetaDst, projectionDistanceDst) = GeneratePointOutsideLozenge(midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);

			// Check if points are on the same side
			var srcXZ = new Vector2(src.x, src.z);
			var dstXZ = new Vector2(dst.x, dst.z);
			var v1 = srcXZ - midPointXZ;
			var v2 = dstXZ - midPointXZ;
			var dot = Vector2.Dot(v1, v2);
			var flipped = false;

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
				flipped = true;
				dstXZ = new Vector2(dst.x, dst.z);
			}

			// Sort points so Dst is closer to targetDst if SortDstNearerPlayer is true
			if (SortDstNearerPlayer)
			{
				float srcDist = Vector2.Distance(srcXZ, targetDstXZ);
				float dstDist = Vector2.Distance(dstXZ, targetDstXZ);
				if (srcDist < dstDist)
				{
					(src, dst) = (dst, src);
					(perimeterPointSrc, perimeterPointDst) = (perimeterPointDst, perimeterPointSrc);
					(tangentSrc1, tangentDst1) = (tangentDst1, tangentSrc1);
					(tangentSrc2, tangentDst2) = (tangentDst2, tangentSrc2);
					(thetaSrc, thetaDst) = (thetaDst, thetaSrc);
					(projectionDistanceSrc, projectionDistanceDst) = (projectionDistanceDst, projectionDistanceSrc);
					(srcXZ, dstXZ) = (dstXZ, srcXZ);
					//sorted = true
				}
			}

			// Compute tangent points
			var (tangentPointSrc1, tangentPointSrc2) = MathEllipse.ComputeTangentPoints(src, midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);
			var (tangentPointDst1, tangentPointDst2) = MathEllipse.ComputeTangentPoints(dst, midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);

			// Compute control point (intersection of purple Src and cyan Dst, or cyan Src and purple Dst)
			Vector3? controlPoint = null;
			//var colinear = false;//debug

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

				// Check for colinear tangents
				var cross1 = Mathf.Abs(srcDir1.x * dstDir2.y - srcDir1.y * dstDir2.x);
				var cross2 = Mathf.Abs(srcDir2.x * dstDir1.y - srcDir2.y * dstDir1.x);
				if (cross1 < 0.01f || cross2 < 0.01f)
				{
					//colinear = true;
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
				//colinear = true;
			}

			// Create Bezier curve
			bezierData = CreateBezierCurve(src, controlPoint.Value, dst);

			// Debug visualization
			if (DEBUG_VISUALIZE_LOZENGE)
			{
				VisualizeLozenge(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor, (MinCameraHeight + MaxCameraHeight) / 2f);
				Debug.DrawRay(perimeterPointSrc, Vector3.up * 5f, Color.yellow, DEBUG_DRAW_DURATION);
				Debug.DrawRay(perimeterPointDst, Vector3.up * 5f, Color.yellow, DEBUG_DRAW_DURATION);
				Debug.DrawRay(src, Vector3.up * 3f, Color.green, DEBUG_DRAW_DURATION);
				Debug.DrawRay(dst, Vector3.up * 3f, flipped ? Color.magenta : Color.green, DEBUG_DRAW_DURATION);
				Debug.DrawRay(controlPoint.Value, Vector3.up * 4f, Color.yellow, DEBUG_DRAW_DURATION);

				if (tangentPointSrc1.HasValue && tangentPointSrc2.HasValue)
				{
					Vector3 tangentSrc1End = tangentPointSrc1.Value + (tangentPointSrc1.Value - src).normalized * TangentLineExtension;
					Vector3 tangentSrc2End = tangentPointSrc2.Value + (tangentPointSrc2.Value - src).normalized * TangentLineExtension;
					Debug.DrawLine(src, tangentSrc1End, Color.magenta, DEBUG_DRAW_DURATION);
					Debug.DrawLine(src, tangentSrc2End, Color.cyan, DEBUG_DRAW_DURATION);
					Debug.DrawRay(tangentPointSrc1.Value, Vector3.up * 2f, Color.white, DEBUG_DRAW_DURATION);
					Debug.DrawRay(tangentPointSrc2.Value, Vector3.up * 2f, Color.white, DEBUG_DRAW_DURATION);
				}
				if (tangentPointDst1.HasValue && tangentPointDst2.HasValue)
				{
					Vector3 tangentDst1End = tangentPointDst1.Value + (tangentPointDst1.Value - dst).normalized * TangentLineExtension;
					Vector3 tangentDst2End = tangentPointDst2.Value + (tangentPointDst2.Value - dst).normalized * TangentLineExtension;
					Debug.DrawLine(dst, tangentDst1End, Color.magenta, DEBUG_DRAW_DURATION + 2f);
					Debug.DrawLine(dst, tangentDst2End, Color.cyan, DEBUG_DRAW_DURATION + 2f);
					Debug.DrawRay(tangentPointDst1.Value, Vector3.up * 2f, Color.white, DEBUG_DRAW_DURATION + 2f);
					Debug.DrawRay(tangentPointDst2.Value, Vector3.up * 2f, Color.white, DEBUG_DRAW_DURATION + 2f);
				}

				VisualizeBezierCurve();
				VisualizeBezierConstruction(); // New: Show Q0(t), Q1(t), B(t)
			}
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

		private BezierData CreateBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2) => new BezierData { P0 = p0, P1 = p1, P2 = p2 };

		private Vector3 QuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
		{
			var u = 1f - t;
			var tt = t * t;
			var uu = u * u;
			return uu * p0 + 2f * u * t * p1 + tt * p2;
		}

		private void VisualizeBezierCurve()
		{
			const int segments = 20;
			var points = new Vector3[segments + 1];
			for (var i = 0; i <= segments; i++)
			{
				float t = i / (float)segments;
				points[i] = EvaluateBezier(t);
			}
			for (var i = 0; i < segments; i++)
			{
				Debug.DrawLine(points[i], points[i + 1], Color.white, DEBUG_DRAW_DURATION);
			}
		}

		private void VisualizeBezierConstruction()
		{
			// Visualize Q0(t), Q1(t), and B(t) for a few t values
			float[] tValues = { 0.25f, 0.5f, 0.75f };
			foreach (var t in tValues)
			{
				var q0 = Vector3.Lerp(bezierData.P0, bezierData.P1, t); // Q0(t)
				var q1 = Vector3.Lerp(bezierData.P1, bezierData.P2, t); // Q1(t)
				var b = Vector3.Lerp(q0, q1, t); // B(t)

				Debug.DrawRay(q0, Vector3.up * 1f, Color.red, DEBUG_DRAW_DURATION); // Q0(t)
				Debug.DrawRay(q1, Vector3.up * 1f, Color.blue, DEBUG_DRAW_DURATION); // Q1(t)
				Debug.DrawRay(b, Vector3.up * 1.5f, Color.green, DEBUG_DRAW_DURATION); // B(t)
				Debug.DrawLine(q0, q1, Color.gray, DEBUG_DRAW_DURATION); // Line from Q0 to Q1
			}
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

		private void VisualizeLozenge(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor, float height)
		{
			const int segments = 36;
			var points = new Vector3[segments + 1];
			for (var i = 0; i <= segments; i++)
			{
				var angle = i * 2f * Mathf.PI / segments;
				Vector2 pointXZ = MathEllipse.GetEllipsePoint(angle, lozengeMajor, lozengeMinor, new Vector2(midPoint.x, midPoint.z), pathDir, perpendicular);
				points[i] = new Vector3(pointXZ.x, height, pointXZ.y);
			}
			for (var i = 0; i < segments; i++)
			{
				Debug.DrawLine(points[i], points[i + 1], Color.green, DEBUG_DRAW_DURATION);
			}
		}
	}
}