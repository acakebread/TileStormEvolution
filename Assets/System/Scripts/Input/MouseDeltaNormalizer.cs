using UnityEngine;

namespace MassiveHadronLtd
{
	public static class MouseDeltaNormalizer
	{
		// Tune this once in WebGL builds until the feel matches the Editor.
		// Most people end up between 0.40f and 0.60f.
		// Never goes above 1.0.
		private const float WEBGL_MOUSE_SCALE = 0.50f;

		public static float GetNormalizedMagnitude(float rawMagnitude)
		{
#if UNITY_WEBGL && !UNITY_EDITOR
            return rawMagnitude * WEBGL_MOUSE_SCALE;
#else
			return rawMagnitude;   // Editor + standalone = untouched
#endif
		}

		// For debug display only
		public static float GetCurrentScale()
		{
#if UNITY_WEBGL && !UNITY_EDITOR
            return WEBGL_MOUSE_SCALE;
#else
			return 1.0f;
#endif
		}
	}
}

//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public static class MouseDeltaNormalizer
//	{
//		// ================================================================
//		// TUNE THIS VALUE IN THE EDITOR
//		// Play in Editor, watch "Raw mag" in the debug panel while moving
//		// the mouse quickly. Set this constant to roughly the highest
//		// raw peak you see (e.g. 70–90). After you set it, Editor will
//		// always stay at scale = 1.0 and WebGL will match it perfectly.
//		// ================================================================
//		private const float TARGET_EDITOR_PEAK = 80f;   // ←←← CHANGE THIS

//		private const float CALIBRATION_DURATION = 5f;
//		private const float IGNORE_INITIAL_NOISE = 0.5f;
//		private const float MIN_SCALE = 0.2f;
//		private const float MAX_SCALE = 4f;

//		private static float currentScale = 1.0f;
//		private static float calibMaxRawMag = 0f;
//		private static float calibStartTime = 0f;
//		private static bool isCalibrating = true;
//		private static bool hasCalibrationData = false;

//		private static bool initialized = false;

//		public static float GetNormalizedMagnitude(float rawMagnitude)
//		{
//			if (!initialized)
//			{
//				calibStartTime = Time.time;
//				currentScale = 1.0f;
//				calibMaxRawMag = 0f;
//				isCalibrating = true;
//				hasCalibrationData = false;
//				initialized = true;
//			}

//#if UNITY_EDITOR || !UNITY_WEBGL
//			// Editor or any non-WebGL build → always raw, no scaling
//			return rawMagnitude;
//#else
//            // WebGL build only → do one-time calibration
//            float timeInCalib = Time.time - calibStartTime;

//            if (isCalibrating)
//            {
//                if (timeInCalib > IGNORE_INITIAL_NOISE)
//                {
//                    calibMaxRawMag = Mathf.Max(calibMaxRawMag, rawMagnitude);
//                }

//                if (timeInCalib >= CALIBRATION_DURATION)
//                {
//                    isCalibrating = false;

//                    if (calibMaxRawMag > 8f) // real movement happened
//                    {
//                        float desiredScale = TARGET_EDITOR_PEAK / calibMaxRawMag;
//                        currentScale = Mathf.Clamp(desiredScale, MIN_SCALE, MAX_SCALE);
//                        hasCalibrationData = true;
//                    }
//                }

//                // During calibration we pass raw through so the graph is usable
//                return rawMagnitude;
//            }

//            // After calibration → locked scale
//            return rawMagnitude * currentScale;
//#endif
//		}

//		// Debug helpers
//		public static float GetCurrentScale() => currentScale;
//		public static bool IsCalibrating() => isCalibrating && !UnityEngine.Application.isEditor;
//		public static float GetTimeLeft() => Mathf.Max(0f, CALIBRATION_DURATION - (Time.time - calibStartTime));
//		public static float GetCalibMaxRawMag() => calibMaxRawMag;
//		public static bool HasCalibrationData() => hasCalibrationData;

//		public static void TriggerManualCalibration()
//		{
//			calibStartTime = Time.time;
//			calibMaxRawMag = 0f;
//			isCalibrating = true;
//			hasCalibrationData = false;
//			currentScale = 1.0f;
//		}
//	}
//}