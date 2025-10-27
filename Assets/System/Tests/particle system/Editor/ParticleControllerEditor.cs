#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MassiveHadronLtd
{
	[CustomEditor(typeof(ParticleController))]
	public class ParticleControllerEditor : Editor
	{
		private bool presetApplied = false;

		public override void OnInspectorGUI()
		{
			var controller = (ParticleController)target;

			serializedObject.Update();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("particleMaterial"));
			var settingsProp = serializedObject.FindProperty("settings");
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("speed"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("lifetime"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("lifetimeVariation"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("width"));

			EditorGUILayout.LabelField("Scale Curve (%):", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("(Y-axis: 0 = 0%, 1 = 100%, 2 = 200%)");
			var scaleCurveProp = settingsProp.FindPropertyRelative("scaleCurve");
			var curve = scaleCurveProp.animationCurveValue;
			var rect = EditorGUILayout.GetControlRect(false, 100);
			var newCurve = EditorGUI.CurveField(rect, curve, Color.green, new Rect(0, 0, 1, 2));

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Linear (100%)"))
			{
				newCurve = CreateLinearCurve();
				presetApplied = true;
			}
			if (GUILayout.Button("Scale Up (0% to 200%)"))
			{
				newCurve = CreateScaleUpCurve();
				presetApplied = true;
			}
			if (GUILayout.Button("Scale Down (200% to 0%)"))
			{
				newCurve = CreateScaleDownCurve();
				presetApplied = true;
			}
			EditorGUILayout.EndHorizontal();

			if (!AreCurvesEqual(curve, newCurve) && !presetApplied)
			{
				AnimationCurve tempCurve = new AnimationCurve(newCurve.keys);
				if (tempCurve.keys.Length < 2)
				{
					tempCurve = CreateLinearCurve();
				}
				else
				{
					for (int i = 0; i < tempCurve.keys.Length; i++)
					{
						Keyframe key = tempCurve.keys[i];
						key.value = Mathf.Clamp(key.value, 0f, 2f);
						key.time = Mathf.Clamp01(key.time);
						tempCurve.MoveKey(i, key);
					}
					bool hasStart = false, hasEnd = false;
					for (int i = 0; i < tempCurve.keys.Length; i++)
					{
						if (Mathf.Approximately(tempCurve.keys[i].time, 0f)) hasStart = true;
						if (Mathf.Approximately(tempCurve.keys[i].time, 1f)) hasEnd = true;
					}
					if (!hasStart)
						tempCurve.AddKey(new Keyframe(0f, Mathf.Clamp(tempCurve.Evaluate(0f), 0f, 2f), 0f, 0f));
					if (!hasEnd)
						tempCurve.AddKey(new Keyframe(1f, Mathf.Clamp(tempCurve.Evaluate(1f), 0f, 2f), 0f, 0f));
				}
				newCurve = tempCurve;

				for (int i = 0; i < newCurve.keys.Length; i++)
				{
					AnimationUtility.SetKeyBroken(newCurve, i, true);
					AnimationUtility.SetKeyLeftTangentMode(newCurve, i, AnimationUtility.TangentMode.Free);
					AnimationUtility.SetKeyRightTangentMode(newCurve, i, AnimationUtility.TangentMode.Free);
				}
			}

			presetApplied = false;
			scaleCurveProp.animationCurveValue = newCurve;
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("color"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("gravity"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("bounceDamping"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("groundHeight"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("useGlobalGroundPlane"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("useThreeZoneSlicing"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("updateParticles"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("fadeStartTime"));

			serializedObject.ApplyModifiedProperties();

			if (GUI.changed)
			{
				EditorUtility.SetDirty(controller);
			}
		}

		private bool AreCurvesEqual(AnimationCurve curve1, AnimationCurve curve2)
		{
			if (curve1.keys.Length != curve2.keys.Length)
				return false;

			for (int i = 0; i < curve1.keys.Length; i++)
			{
				var key1 = curve1.keys[i];
				var key2 = curve2.keys[i];
				if (key1.time != key2.time || key1.value != key2.value ||
					key1.inTangent != key2.inTangent || key1.outTangent != key2.outTangent)
					return false;
			}
			return true;
		}

		private AnimationCurve CreateLinearCurve()
		{
			var curve = new AnimationCurve();
			float[] fixedTimes = { 0f, 0.33f, 0.66f, 1f };
			for (int i = 0; i < 4; i++)
			{
				float tangent = 0f;
				curve.AddKey(new Keyframe(fixedTimes[i], 1.0f, tangent, tangent));
				AnimationUtility.SetKeyBroken(curve, i, true);
				AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
			}
			return curve;
		}

		private AnimationCurve CreateScaleUpCurve()
		{
			var curve = new AnimationCurve();
			float[] fixedTimes = { 0f, 0.33f, 0.66f, 1f };
			float[] values = { 0f, 0.66f, 1.33f, 2.0f };
			float slope = (2.0f - 0f) / (1f - 0f);
			for (int i = 0; i < 4; i++)
			{
				float tangent = slope;
				curve.AddKey(new Keyframe(fixedTimes[i], values[i], tangent, tangent));
				AnimationUtility.SetKeyBroken(curve, i, true);
				AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
			}
			return curve;
		}

		private AnimationCurve CreateScaleDownCurve()
		{
			var curve = new AnimationCurve();
			float[] fixedTimes = { 0f, 0.33f, 0.66f, 1f };
			float[] values = { 2.0f, 1.33f, 0.66f, 0f };
			float slope = (0f - 2.0f) / (1f - 0f);
			for (int i = 0; i < 4; i++)
			{
				float tangent = slope;
				curve.AddKey(new Keyframe(fixedTimes[i], values[i], tangent, tangent));
				AnimationUtility.SetKeyBroken(curve, i, true);
				AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
			}
			return curve;
		}
	}
}
#endif