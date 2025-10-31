#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
	[CustomEditor(typeof(ParticleController))]
	public class ParticleControllerEditor : Editor
	{
		private bool presetApplied = false;
		private enum DragState { None, Start, End }
		private DragState dragState = DragState.None;
		private int draggedPulseIndex = -1;
		private const float MIN_PULSE_WIDTH = 0.01f;

		private static bool IsGameViewMaximized()
		{
#if UNITY_EDITOR
			var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
			return gameView != null && gameView.maximized;
#else
    return false;
#endif
		}

		public override void OnInspectorGUI()
		{
			//// Don't draw inspector if Game View is full screen or maximized
			//if (IsGameViewMaximized())
			//	return;

			var controller = (ParticleController)target;
			serializedObject.Update();

			// ----- Debug -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("showInSceneView"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("debugOutlinePixels"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("updateParticles"));

			// ----- Lifetime -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("lifetime"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("lifetimeVariation"));

			// ----- Appearance -----// ----- Rendering -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("useThreeZoneSlicing"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("particleMaterial"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("color"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("radius"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeStartTime"));

			// ----- Physics -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("gravity"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("friction"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("velocityBias"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("velocityMagnitude"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("enableCollision"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("groundHeight"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("bounceDamping"));

			// ----- Animation -----
			EditorGUILayout.LabelField("");//spacer
			EditorGUILayout.LabelField("Scale Curve (%):", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("(Y-axis: 0 = 0%, 1 = 100%, 2 = 200%)");
			var curveProp = serializedObject.FindProperty("scaleCurve");
			var curve = curveProp.animationCurveValue;
			var rect = EditorGUILayout.GetControlRect(false, 100);
			var newCurve = EditorGUI.CurveField(rect, curve, Color.green, new Rect(0, 0, 1, 2));

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Linear (100%)")) { newCurve = CreateLinearCurve(); presetApplied = true; }
			if (GUILayout.Button("Scale Up (0% to 200%)")) { newCurve = CreateScaleUpCurve(); presetApplied = true; }
			if (GUILayout.Button("Scale Down (200% to 0%)")) { newCurve = CreateScaleDownCurve(); presetApplied = true; }
			EditorGUILayout.EndHorizontal();

			if (!AreCurvesEqual(curve, newCurve) && !presetApplied)
			{
				var temp = new AnimationCurve(newCurve.keys);
				if (temp.keys.Length < 2) temp = CreateLinearCurve();
				else
				{
					for (int i = 0; i < temp.keys.Length; i++)
					{
						var k = temp.keys[i];
						k.value = Mathf.Clamp(k.value, 0f, 2f);
						k.time = Mathf.Clamp01(k.time);
						temp.MoveKey(i, k);
					}
					bool hasStart = temp.keys.Any(k => Mathf.Approximately(k.time, 0f));
					bool hasEnd = temp.keys.Any(k => Mathf.Approximately(k.time, 1f));
					if (!hasStart) temp.AddKey(new Keyframe(0f, Mathf.Clamp(temp.Evaluate(0f), 0f, 2f)));
					if (!hasEnd) temp.AddKey(new Keyframe(1f, Mathf.Clamp(temp.Evaluate(1f), 0f, 2f)));
				}

				for (int i = 0; i < temp.keys.Length; i++)
				{
					AnimationUtility.SetKeyBroken(temp, i, true);
					AnimationUtility.SetKeyLeftTangentMode(temp, i, AnimationUtility.TangentMode.Free);
					AnimationUtility.SetKeyRightTangentMode(temp, i, AnimationUtility.TangentMode.Free);
				}
				newCurve = temp;
			}
			presetApplied = false;
			curveProp.animationCurveValue = newCurve;

			// ----- Emission -----
			EditorGUILayout.PropertyField(serializedObject.FindProperty("particleCount"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("scatterScalar"));

			// ----- PWM Timeline -----
			//EditorGUILayout.LabelField("PWM Timeline:", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("cycleTime"));
			var pulsesProp = serializedObject.FindProperty("pulses");

			// Pulse management
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add Pulse"))
			{
				pulsesProp.arraySize++;
				var p = pulsesProp.GetArrayElementAtIndex(pulsesProp.arraySize - 1);
				p.FindPropertyRelative("start").floatValue = 0f;
				p.FindPropertyRelative("end").floatValue = 0.1f;
			}
			if (GUILayout.Button("Remove Last Pulse") && pulsesProp.arraySize > 1)
				pulsesProp.arraySize--;
			EditorGUILayout.EndHorizontal();

			// Timeline UI
			Rect timelineRect = EditorGUILayout.GetControlRect(false, 30);
			EditorGUI.DrawRect(timelineRect, new Color(0.2f, 0.2f, 0.2f));
			float timelineWidth = timelineRect.width;

			// Draw pulses
			for (int i = 0; i < pulsesProp.arraySize; i++)
			{
				var pulse = pulsesProp.GetArrayElementAtIndex(i);
				var startProp = pulse.FindPropertyRelative("start");
				var endProp = pulse.FindPropertyRelative("end");
				if (endProp.floatValue < startProp.floatValue + MIN_PULSE_WIDTH)
					endProp.floatValue = startProp.floatValue + MIN_PULSE_WIDTH;

				float startX = timelineRect.x + startProp.floatValue * timelineWidth;
				float endX = timelineRect.x + endProp.floatValue * timelineWidth;

				if (Mathf.Approximately(startProp.floatValue, endProp.floatValue))
					EditorGUI.DrawRect(new Rect(startX, timelineRect.y, 2f, timelineRect.height), new Color(0, 0.8f, 0));
				else
					EditorGUI.DrawRect(new Rect(startX, timelineRect.y, endX - startX, timelineRect.height), new Color(0, 0.8f, 0));
			}

			// Mouse handling (unchanged, just uses pulsesProp)
			Vector2 mousePos = Event.current.mousePosition;
			float normalizedPos = Mathf.Clamp01((mousePos.x - timelineRect.x) / timelineWidth);
			const float handleWidth = 0.1f;

			if (Event.current.type == EventType.MouseDown && timelineRect.Contains(mousePos))
			{
				bool inPulse = false;
				int closestIdx = -1;
				bool isStart = false;
				float minDist = float.MaxValue;

				for (int i = 0; i < pulsesProp.arraySize; i++)
				{
					var p = pulsesProp.GetArrayElementAtIndex(i);
					var s = p.FindPropertyRelative("start").floatValue;
					var e = p.FindPropertyRelative("end").floatValue;

					if (normalizedPos >= s && normalizedPos <= e) inPulse = true;

					float dStart = Mathf.Abs(normalizedPos - s);
					float dEnd = Mathf.Abs(normalizedPos - e);
					if (dStart < minDist && dStart <= handleWidth) { minDist = dStart; closestIdx = i; isStart = true; }
					if (dEnd < minDist && dEnd <= handleWidth) { minDist = dEnd; closestIdx = i; isStart = false; }
				}

				if (closestIdx >= 0)
				{
					draggedPulseIndex = closestIdx;
					dragState = isStart ? DragState.Start : DragState.End;
				}
				else if (!inPulse)
				{
					pulsesProp.arraySize++;
					var np = pulsesProp.GetArrayElementAtIndex(pulsesProp.arraySize - 1);
					float w = 0.1f;
					np.FindPropertyRelative("start").floatValue = Mathf.Clamp01(normalizedPos - w * 0.5f);
					np.FindPropertyRelative("end").floatValue = Mathf.Clamp01(normalizedPos + w * 0.5f);
				}
				Event.current.Use();
			}
			else if (Event.current.type == EventType.MouseDrag && dragState != DragState.None && draggedPulseIndex >= 0)
			{
				var p = pulsesProp.GetArrayElementAtIndex(draggedPulseIndex);
				var startProp = p.FindPropertyRelative("start");
				var endProp = p.FindPropertyRelative("end");

				if (dragState == DragState.Start)
				{
					startProp.floatValue = Mathf.Clamp01(normalizedPos);
					if (startProp.floatValue > endProp.floatValue - MIN_PULSE_WIDTH)
						endProp.floatValue = startProp.floatValue + MIN_PULSE_WIDTH;
				}
				else
				{
					endProp.floatValue = Mathf.Clamp01(normalizedPos);
					if (endProp.floatValue < startProp.floatValue + MIN_PULSE_WIDTH)
						startProp.floatValue = endProp.floatValue - MIN_PULSE_WIDTH;
				}
				Event.current.Use();
			}
			else if (Event.current.type == EventType.MouseUp && dragState != DragState.None && draggedPulseIndex >= 0)
			{
				// Merge overlapping pulses
				var list = new List<(float s, float e, int idx)>();
				for (int i = 0; i < pulsesProp.arraySize; i++)
				{
					var p = pulsesProp.GetArrayElementAtIndex(i);
					list.Add((p.FindPropertyRelative("start").floatValue,
							  p.FindPropertyRelative("end").floatValue, i));
				}
				list.Sort((a, b) => a.s.CompareTo(b.s));

				var merged = new List<(float s, float e)>();
				float curS = list[0].s, curE = list[0].e;
				for (int i = 1; i < list.Count; i++)
				{
					if (list[i].s <= curE) curE = Mathf.Max(curE, list[i].e);
					else { merged.Add((curS, curE)); curS = list[i].s; curE = list[i].e; }
				}
				merged.Add((curS, curE));

				pulsesProp.arraySize = merged.Count;
				for (int i = 0; i < merged.Count; i++)
				{
					var p = pulsesProp.GetArrayElementAtIndex(i);
					p.FindPropertyRelative("start").floatValue = merged[i].s;
					p.FindPropertyRelative("end").floatValue = merged[i].e;
				}

				dragState = DragState.None;
				draggedPulseIndex = -1;
				Event.current.Use();
			}
			else if (Event.current.type == EventType.MouseUp)
			{
				dragState = DragState.None;
				draggedPulseIndex = -1;
				Event.current.Use();
			}

			// Pulse details
			for (int i = 0; i < pulsesProp.arraySize; i++)
			{
				var p = pulsesProp.GetArrayElementAtIndex(i);
				var s = p.FindPropertyRelative("start").floatValue;
				var e = p.FindPropertyRelative("end").floatValue;
				EditorGUILayout.LabelField($"Pulse {i + 1}: Start = {s:F2}, End = {e:F2}");
			}

			serializedObject.ApplyModifiedProperties();

			if (GUI.changed) EditorUtility.SetDirty(controller);
		}

		private bool AreCurvesEqual(AnimationCurve a, AnimationCurve b)
		{
			if (a.keys.Length != b.keys.Length) return false;
			for (int i = 0; i < a.keys.Length; i++)
			{
				var ka = a.keys[i];
				var kb = b.keys[i];
				if (ka.time != kb.time || ka.value != kb.value ||
					ka.inTangent != kb.inTangent || ka.outTangent != kb.outTangent)
					return false;
			}
			return true;
		}

		private AnimationCurve CreateLinearCurve()
		{
			var c = new AnimationCurve();
			float[] times = { 0f, 0.33f, 0.66f, 1f };
			for (int i = 0; i < times.Length; i++)
			{
				var k = new Keyframe(times[i], 1f, 0f, 0f);
				c.AddKey(k);
				AnimationUtility.SetKeyBroken(c, i, true);
				AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Free);
			}
			return c;
		}

		private AnimationCurve CreateScaleUpCurve()
		{
			var c = new AnimationCurve();
			float[] times = { 0f, 0.33f, 0.66f, 1f };
			float[] vals = { 0f, 0.66f, 1.33f, 2f };
			float slope = 2f;
			for (int i = 0; i < times.Length; i++)
			{
				var k = new Keyframe(times[i], vals[i], slope, slope);
				c.AddKey(k);
				AnimationUtility.SetKeyBroken(c, i, true);
				AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Free);
			}
			return c;
		}

		private AnimationCurve CreateScaleDownCurve()
		{
			var c = new AnimationCurve();
			float[] times = { 0f, 0.33f, 0.66f, 1f };
			float[] vals = { 2f, 1.33f, 0.66f, 0f };
			float slope = -2f;
			for (int i = 0; i < times.Length; i++)
			{
				var k = new Keyframe(times[i], vals[i], slope, slope);
				c.AddKey(k);
				AnimationUtility.SetKeyBroken(c, i, true);
				AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Free);
				AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Free);
			}
			return c;
		}
	}
}
#endif