using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class CinemaCameraPath : CinemaCameraBase
{
	private const float MinDistance = 1f;
	private const float MinFocusPointDistanceFromPlayer = 4f;
	private const float MaxLookAtAngle = 20f;
	private const float FovMin = 35f;
	private const float FovMax = 55f;
	private const float DebugDrawDuration = 5f;
	private const float TangentLineExtension = 2f;
	private const bool SortDstNearerPlayer = true;
	private const bool UseAnimationCurve = false; // New: false for direct Bézier, true for AnimationCurve

	public static bool DEBUG_VISUALIZE_LOZENGE = true;

	private float currentFovMax;
	private BezierData bezierData;

	private struct BezierData
	{
		public Vector3 P0; // Src
		public Vector3 P1; // Control point
		public Vector3 P2; // Dst
		public AnimationCurve curveX;
		public AnimationCurve curveY;
		public AnimationCurve curveZ;
	}

	public override void Reset()
	{
		base.Reset();
		currentFovMax = FovMax;
		bezierData = default;
	}

	public override void StartSequence(Transform transform, List<Vector3> points)
	{
		base.StartSequence(transform, points);
		if (null == playerTransform)
			return;

		// Select start focus point
		Vector3 startFocusPoint = playerTransform.position;
		if (focusPoints.Count > 0)
		{
			var validFocusPoint = focusPoints.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) >= MinFocusPointDistanceFromPlayer).ToList();
			if (validFocusPoint.Count > 0) startFocusPoint = validFocusPoint[Random.Range(0, validFocusPoint.Count)];
		}

		targetSrc = new Vector3(startFocusPoint.x, VerticalOffset, startFocusPoint.z);
		targetDst = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);

		// Define lozenge
		Vector3 targetPath = targetDst - targetSrc;
		Vector3 pathDir = targetPath.magnitude > 0.1f ? targetPath.normalized : Random.onUnitSphere;
		Vector3 midPoint = (targetSrc + targetDst) / 2f;
		Vector3 perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;
		float lozengeMajor = targetPath.magnitude + 2f * MinDistance;
		float lozengeMinor = Mathf.Max(lozengeMajor * 0.66f, MinDistance * 2f);

		// Generate camera points
		var (src, dst) = SampleCameraPosition(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor);
		originSrc = src;
		originDst = dst;

		AdjustHeight(ref originSrc, targetSrc);
		AdjustHeight(ref originDst, targetDst);

		// Initialize FOV
		currentFovMax = Random.value < 0.2f ? 60f : FovMax;

		// Visualize focus points
		if (DEBUG_VISUALIZE_LOZENGE)
		{
			Debug.DrawRay(targetSrc, Vector3.up * 5f, Color.red, DebugDrawDuration);
			Debug.DrawRay(targetDst, Vector3.up * 5f, Color.blue, DebugDrawDuration);
			//Debug.Log($"TargetSrc={targetSrc}, TargetDst={targetDst}, LozengeMajor={lozengeMajor}, LozengeMinor={lozengeMinor}, PathDir={pathDir}, Perpendicular={perpendicular}");
		}
	}

	protected override (Vector3 transOrigin, Vector3 transTarget, float fov) ComputeSequencePositionsAndFov(float easedT, Vector3 playerDelta)
	{
		originDst += playerDelta;
		targetDst += playerDelta;

		// Update Bezier P2 (Dst) with player movement
		bezierData.P2 += playerDelta;

		// Evaluate Bezier curve
		Vector3 transOrigin = EvaluateBezier(easedT);
		Vector3 transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);
		float fovT = SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration);
		float fov = Mathf.Lerp(FovMin, currentFovMax, fovT);

		return (transOrigin, transTarget, fov);
	}

	private Vector3 EvaluateBezier(float t)
	{
		if (UseAnimationCurve && bezierData.curveX != null)
		{
			return new Vector3(
				bezierData.curveX.Evaluate(t),
				bezierData.curveY.Evaluate(t),
				bezierData.curveZ.Evaluate(t)
			);
		}
		return QuadraticBezierPoint(t, bezierData.P0, bezierData.P1, bezierData.P2); // Direct evaluation
	}

	private (Vector3 src, Vector3 dst) SampleCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor)
	{
		Vector2 midPointXZ = new Vector2(midPoint.x, midPoint.z);
		Vector2 targetDstXZ = new Vector2(targetDst.x, targetDst.z);

		// Generate points
		var (src, perimeterPointSrc, tangentSrc1, tangentSrc2, thetaSrc, projectionDistanceSrc) = GeneratePointOutsideLozenge(midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);
		var (dst, perimeterPointDst, tangentDst1, tangentDst2, thetaDst, projectionDistanceDst) = GeneratePointOutsideLozenge(midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);

		// Check if points are on the same side
		Vector2 srcXZ = new Vector2(src.x, src.z);
		Vector2 dstXZ = new Vector2(dst.x, dst.z);
		Vector2 v1 = srcXZ - midPointXZ;
		Vector2 v2 = dstXZ - midPointXZ;
		float dot = Vector2.Dot(v1, v2);
		bool flipped = false;

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
		bool sorted = false;//debug
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
				sorted = true;
			}
		}

		// Compute tangent points
		var (tangentPointSrc1, tangentPointSrc2) = MathEllipse.ComputeTangentPoints(src, midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);
		var (tangentPointDst1, tangentPointDst2) = MathEllipse.ComputeTangentPoints(dst, midPointXZ, pathDir, perpendicular, lozengeMajor, lozengeMinor);

		// Compute control point (intersection of purple Src and cyan Dst, or cyan Src and purple Dst)
		Vector3? controlPoint = null;
		bool colinear = false;//debug
		Vector2? intersection1 = null, intersection2 = null;

		if (tangentPointSrc1.HasValue && tangentPointSrc2.HasValue && tangentPointDst1.HasValue && tangentPointDst2.HasValue)
		{
			Vector2 src1XZ = new Vector2(tangentPointSrc1.Value.x, tangentPointSrc1.Value.z);
			Vector2 src2XZ = new Vector2(tangentPointSrc2.Value.x, tangentPointSrc2.Value.z);
			Vector2 dst1XZ = new Vector2(tangentPointDst1.Value.x, tangentPointDst1.Value.z);
			Vector2 dst2XZ = new Vector2(tangentPointDst2.Value.x, tangentPointDst2.Value.z);
			Vector2 srcDir1 = (src1XZ - srcXZ).normalized;
			Vector2 srcDir2 = (src2XZ - srcXZ).normalized;
			Vector2 dstDir1 = (dst1XZ - dstXZ).normalized;
			Vector2 dstDir2 = (dst2XZ - dstXZ).normalized;

			// Check for colinear tangents
			float cross1 = Mathf.Abs(srcDir1.x * dstDir2.y - srcDir1.y * dstDir2.x);
			float cross2 = Mathf.Abs(srcDir2.x * dstDir1.y - srcDir2.y * dstDir1.x);
			if (cross1 < 0.01f || cross2 < 0.01f)
			{
				colinear = true;
				controlPoint = (src + dst) / 2f;
			}
			else
			{
				intersection1 = LineIntersection(srcXZ, srcXZ + srcDir1, dstXZ, dstXZ + dstDir2);
				intersection2 = LineIntersection(srcXZ, srcXZ + srcDir2, dstXZ, dstXZ + dstDir1);

				if (intersection1.HasValue && intersection2.HasValue)
				{
					float dist1 = Vector2.Distance(intersection1.Value, midPointXZ);
					float dist2 = Vector2.Distance(intersection2.Value, midPointXZ);
					Vector2 selectedIntersection = dist1 < dist2 ? intersection1.Value : intersection2.Value;
					controlPoint = new Vector3(selectedIntersection.x, (src.y + dst.y) / 2f, selectedIntersection.y);
				}
			}
		}

		// Fallback to midpoint
		if (!controlPoint.HasValue)
		{
			controlPoint = (src + dst) / 2f;
			colinear = true;
		}

		// Create Bezier curve
		bezierData = CreateBezierCurve(src, controlPoint.Value, dst);

		// Debug visualization
		if (DEBUG_VISUALIZE_LOZENGE)
		{
			VisualizeLozenge(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor, (MinCameraHeight + MaxCameraHeight) / 2f);
			Debug.DrawRay(perimeterPointSrc, Vector3.up * 5f, Color.yellow, DebugDrawDuration);
			Debug.DrawRay(perimeterPointDst, Vector3.up * 5f, Color.yellow, DebugDrawDuration);
			Debug.DrawRay(src, Vector3.up * 3f, Color.green, DebugDrawDuration);
			Debug.DrawRay(dst, Vector3.up * 3f, flipped ? Color.magenta : Color.green, DebugDrawDuration);
			Debug.DrawRay(controlPoint.Value, Vector3.up * 4f, Color.yellow, DebugDrawDuration);

			if (tangentPointSrc1.HasValue && tangentPointSrc2.HasValue)
			{
				Vector3 tangentSrc1End = tangentPointSrc1.Value + (tangentPointSrc1.Value - src).normalized * TangentLineExtension;
				Vector3 tangentSrc2End = tangentPointSrc2.Value + (tangentPointSrc2.Value - src).normalized * TangentLineExtension;
				Debug.DrawLine(src, tangentSrc1End, Color.magenta, DebugDrawDuration);
				Debug.DrawLine(src, tangentSrc2End, Color.cyan, DebugDrawDuration);
				Debug.DrawRay(tangentPointSrc1.Value, Vector3.up * 2f, Color.white, DebugDrawDuration);
				Debug.DrawRay(tangentPointSrc2.Value, Vector3.up * 2f, Color.white, DebugDrawDuration);
			}
			if (tangentPointDst1.HasValue && tangentPointDst2.HasValue)
			{
				Vector3 tangentDst1End = tangentPointDst1.Value + (tangentPointDst1.Value - dst).normalized * TangentLineExtension;
				Vector3 tangentDst2End = tangentPointDst2.Value + (tangentPointDst2.Value - dst).normalized * TangentLineExtension;
				Debug.DrawLine(dst, tangentDst1End, Color.magenta, DebugDrawDuration + 2f);
				Debug.DrawLine(dst, tangentDst2End, Color.cyan, DebugDrawDuration + 2f);
				Debug.DrawRay(tangentPointDst1.Value, Vector3.up * 2f, Color.white, DebugDrawDuration + 2f);
				Debug.DrawRay(tangentPointDst2.Value, Vector3.up * 2f, Color.white, DebugDrawDuration + 2f);
			}

			VisualizeBezierCurve();
			VisualizeBezierConstruction(); // New: Show Q0(t), Q1(t), B(t)

			//Debug.Log($"PerimeterSrc={perimeterPointSrc}, Src={src}, ThetaSrc={thetaSrc * Mathf.Rad2Deg}deg, TangentSrc1={tangentSrc1}, TangentSrc2={tangentSrc2}, ProjectionDistanceSrc={projectionDistanceSrc}, " +
			//		  $"PerimeterDst={perimeterPointDst}, Dst={dst}, ThetaDst={thetaDst * Mathf.Rad2Deg}deg, TangentDst1={tangentDst1}, TangentDst2={tangentDst2}, ProjectionDistanceDst={projectionDistanceDst}, " +
			//		  $"TangentPointSrc1={tangentPointSrc1}, TangentPointSrc2={tangentPointSrc2}, TangentPointDst1={tangentPointDst1}, TangentPointDst2={tangentPointDst2}, " +
			//		  $"DotProduct={dot}, FlippedDst={flipped}, Sorted={sorted}, SrcDistToPlayer={Vector2.Distance(srcXZ, targetDstXZ)}, DstDistToPlayer={Vector2.Distance(dstXZ, targetDstXZ)}, " +
			//		  $"Intersection1={intersection1}, Intersection2={intersection2}, ControlPoint={controlPoint}, Colinear={colinear}");
		}

		return (src, dst);
	}

	private Vector2? LineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
	{
		float denom = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
		if (Mathf.Abs(denom) < 0.0001f) return null;

		float t = ((p1.x - p3.x) * (p3.y - p4.y) - (p1.y - p3.y) * (p3.x - p4.x)) / denom;
		float u = -((p1.x - p2.x) * (p1.y - p3.y) - (p1.y - p2.y) * (p1.x - p3.x)) / denom;

		if (t >= 0 && u >= 0)
		{
			return p1 + t * (p2 - p1);
		}
		return null;
	}

	private BezierData CreateBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2)
	{
		BezierData data = new BezierData { P0 = p0, P1 = p1, P2 = p2 };

		if (UseAnimationCurve)
		{
			data.curveX = new AnimationCurve();
			data.curveY = new AnimationCurve();
			data.curveZ = new AnimationCurve();

			const int samples = 20;
			for (int i = 0; i <= samples; i++)
			{
				float t = i / (float)samples;
				Vector3 point = QuadraticBezierPoint(t, p0, p1, p2);
				data.curveX.AddKey(t, point.x);
				data.curveY.AddKey(t, point.y);
				data.curveZ.AddKey(t, point.z);
			}

			for (int i = 0; i < data.curveX.keys.Length; i++)
			{
				data.curveX.SmoothTangents(i, 0f);
				data.curveY.SmoothTangents(i, 0f);
				data.curveZ.SmoothTangents(i, 0f);
			}
		}

		return data;
	}

	private Vector3 QuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
	{
		float u = 1f - t;
		float tt = t * t;
		float uu = u * u;
		return uu * p0 + 2f * u * t * p1 + tt * p2;
	}

	private void VisualizeBezierCurve()
	{
		const int segments = 20;
		Vector3[] points = new Vector3[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float t = i / (float)segments;
			points[i] = EvaluateBezier(t);
		}
		for (int i = 0; i < segments; i++)
		{
			Debug.DrawLine(points[i], points[i + 1], Color.white, DebugDrawDuration);
		}
	}

	private void VisualizeBezierConstruction()
	{
		// Visualize Q0(t), Q1(t), and B(t) for a few t values
		float[] tValues = { 0.25f, 0.5f, 0.75f };
		foreach (float t in tValues)
		{
			Vector3 q0 = Vector3.Lerp(bezierData.P0, bezierData.P1, t); // Q0(t)
			Vector3 q1 = Vector3.Lerp(bezierData.P1, bezierData.P2, t); // Q1(t)
			Vector3 b = Vector3.Lerp(q0, q1, t); // B(t)

			Debug.DrawRay(q0, Vector3.up * 1f, Color.red, DebugDrawDuration); // Q0(t)
			Debug.DrawRay(q1, Vector3.up * 1f, Color.blue, DebugDrawDuration); // Q1(t)
			Debug.DrawRay(b, Vector3.up * 1.5f, Color.green, DebugDrawDuration); // B(t)
			Debug.DrawLine(q0, q1, Color.gray, DebugDrawDuration); // Line from Q0 to Q1
		}
	}

	private (Vector3 point, Vector2 perimeterPoint, Vector3 tangent1, Vector3 tangent2, float theta, float projectionDistance) GeneratePointOutsideLozenge(Vector2 midPointXZ, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor)
	{
		float theta = Random.Range(0f, 2f * Mathf.PI);
		Vector2 perimeterPoint = MathEllipse.GetEllipsePoint(theta, lozengeMajor, lozengeMinor, midPointXZ, pathDir, perpendicular);
		Vector2 direction = (perimeterPoint - midPointXZ).normalized;
		float distanceToPerimeter = Vector2.Distance(midPointXZ, perimeterPoint);
		float projectionDistance = distanceToPerimeter + Random.Range(1f, 5f);
		Vector2 pointXZ = midPointXZ + direction * projectionDistance;
		Vector3 point = new Vector3(pointXZ.x, Random.Range(MinCameraHeight, MaxCameraHeight), pointXZ.y);

		var (tangent1, tangent2) = MathEllipse.ComputeTangentAtPoint(theta, lozengeMajor, lozengeMinor, pathDir, perpendicular);

		return (point, perimeterPoint, tangent1, tangent2, theta, projectionDistance);
	}

	private void VisualizeLozenge(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor, float height)
	{
		const int segments = 36;
		Vector3[] points = new Vector3[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float angle = i * 2f * Mathf.PI / segments;
			Vector2 pointXZ = MathEllipse.GetEllipsePoint(angle, lozengeMajor, lozengeMinor, new Vector2(midPoint.x, midPoint.z), pathDir, perpendicular);
			points[i] = new Vector3(pointXZ.x, height, pointXZ.y);
		}
		for (int i = 0; i < segments; i++)
		{
			Debug.DrawLine(points[i], points[i + 1], Color.green, DebugDrawDuration);
		}
	}

	private void AdjustHeight(ref Vector3 position, Vector3 target)
	{
		Vector2 positionXZ = new Vector2(position.x, position.z);
		Vector2 targetXZ = new Vector2(target.x, target.z);
		float distXZ = Vector2.Distance(positionXZ, targetXZ);

		Vector3 direction = (target - position).normalized;
		float pitch = Vector3.Angle(direction, Vector3.down) - 90f;
		if (pitch > MaxLookAtAngle)
		{
			float maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
			float idealHeight = target.y + VerticalOffset + distXZ / Mathf.Tan(maxPitchRad);
			position.y = Mathf.Clamp(idealHeight, MinCameraHeight, MaxCameraHeight);
		}
	}
}