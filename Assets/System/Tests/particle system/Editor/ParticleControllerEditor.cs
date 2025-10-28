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
		private DragState dragState = DragState.None; // Tracks which edge is being dragged
		private int draggedPulseIndex = -1; // Tracks which pulse is being dragged
		private const float MIN_PULSE_WIDTH = 0.01f; // Minimum pulse width

		public override void OnInspectorGUI()
		{
			var controller = (ParticleController)target;

			serializedObject.Update();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("particleMaterial"));
			var settingsProp = serializedObject.FindProperty("settings");
			//EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("speed"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("lifetime"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("lifetimeVariation"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("radius"));

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

			// PWM Timeline Editor
			EditorGUILayout.LabelField("PWM Timeline:", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("cycleTime"));
			var pulsesProp = settingsProp.FindPropertyRelative("pulses");

			// Pulse management buttons
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add Pulse"))
			{
				pulsesProp.arraySize++;
				var newPulse = pulsesProp.GetArrayElementAtIndex(pulsesProp.arraySize - 1);
				newPulse.FindPropertyRelative("start").floatValue = 0f;
				newPulse.FindPropertyRelative("end").floatValue = 0.1f;
			}
			if (GUILayout.Button("Remove Last Pulse") && pulsesProp.arraySize > 1)
			{
				pulsesProp.arraySize--;
			}
			EditorGUILayout.EndHorizontal();

			// Custom timeline UI
			Rect timelineRect = EditorGUILayout.GetControlRect(false, 30);
			EditorGUI.DrawRect(timelineRect, new Color(0.2f, 0.2f, 0.2f)); // Background
			float timelineWidth = timelineRect.width;

			// Draw all pulses
			for (int i = 0; i < pulsesProp.arraySize; i++)
			{
				var pulseProp = pulsesProp.GetArrayElementAtIndex(i);
				var startProp = pulseProp.FindPropertyRelative("start");
				var endProp = pulseProp.FindPropertyRelative("end");

				// Ensure end >= start with minimum width
				if (endProp.floatValue < startProp.floatValue + MIN_PULSE_WIDTH)
					endProp.floatValue = startProp.floatValue + MIN_PULSE_WIDTH;

				float startX = timelineRect.x + startProp.floatValue * timelineWidth;
				float endX = timelineRect.x + endProp.floatValue * timelineWidth;
				if (Mathf.Approximately(startProp.floatValue, endProp.floatValue))
				{
					// Draw minimum-width pulse as a thin vertical line
					EditorGUI.DrawRect(new Rect(startX, timelineRect.y, 2f, timelineRect.height), new Color(0, 0.8f, 0));
				}
				else
				{
					// Draw normal pulse
					EditorGUI.DrawRect(new Rect(startX, timelineRect.y, endX - startX, timelineRect.height), new Color(0, 0.8f, 0));
				}
			}

			// Handle mouse events
			Vector2 mousePos = Event.current.mousePosition;
			float normalizedPos = Mathf.Clamp01((mousePos.x - timelineRect.x) / timelineWidth);
			float handleWidth = 0.1f; // 10% of timeline

			if (Event.current.type == EventType.MouseDown && timelineRect.Contains(mousePos))
			{
				// Check if clicking within an existing pulse
				bool inPulse = false;
				int closestPulseIndex = -1;
				bool isStartEdge = false;
				float minDistance = float.MaxValue;

				for (int i = 0; i < pulsesProp.arraySize; i++)
				{
					var pulseProp = pulsesProp.GetArrayElementAtIndex(i);
					var startProp = pulseProp.FindPropertyRelative("start");
					var endProp = pulseProp.FindPropertyRelative("end");

					if (normalizedPos >= startProp.floatValue && normalizedPos <= endProp.floatValue)
					{
						inPulse = true; // Click is within a pulse
					}

					float startDistance = Mathf.Abs(normalizedPos - startProp.floatValue);
					float endDistance = Mathf.Abs(normalizedPos - endProp.floatValue);

					if (startDistance < minDistance && startDistance <= handleWidth)
					{
						minDistance = startDistance;
						closestPulseIndex = i;
						isStartEdge = true;
					}
					if (endDistance < minDistance && endDistance <= handleWidth)
					{
						minDistance = endDistance;
						closestPulseIndex = i;
						isStartEdge = false;
					}
				}

				if (closestPulseIndex >= 0)
				{
					// Start dragging the closest edge
					draggedPulseIndex = closestPulseIndex;
					dragState = isStartEdge ? DragState.Start : DragState.End;
				}
				else if (!inPulse)
				{
					// Create new pulse only if not in an existing pulse
					pulsesProp.arraySize++;
					var newPulse = pulsesProp.GetArrayElementAtIndex(pulsesProp.arraySize - 1);
					float newWidth = 0.1f;
					newPulse.FindPropertyRelative("start").floatValue = Mathf.Clamp01(normalizedPos - newWidth * 0.5f);
					newPulse.FindPropertyRelative("end").floatValue = Mathf.Clamp01(normalizedPos + newWidth * 0.5f);
					dragState = DragState.None;
					draggedPulseIndex = -1;
				}
				Event.current.Use();
			}
			else if (Event.current.type == EventType.MouseDrag && dragState != DragState.None && draggedPulseIndex >= 0)
			{
				// Continue dragging the selected edge
				var pulseProp = pulsesProp.GetArrayElementAtIndex(draggedPulseIndex);
				var startProp = pulseProp.FindPropertyRelative("start");
				var endProp = pulseProp.FindPropertyRelative("end");

				if (dragState == DragState.Start)
				{
					startProp.floatValue = Mathf.Clamp01(normalizedPos);
					if (startProp.floatValue > endProp.floatValue - MIN_PULSE_WIDTH)
						endProp.floatValue = startProp.floatValue + MIN_PULSE_WIDTH;
				}
				else if (dragState == DragState.End)
				{
					endProp.floatValue = Mathf.Clamp01(normalizedPos);
					if (endProp.floatValue < startProp.floatValue + MIN_PULSE_WIDTH)
						startProp.floatValue = endProp.floatValue - MIN_PULSE_WIDTH;
				}
				Event.current.Use();
			}
			else if (Event.current.type == EventType.MouseUp && dragState != DragState.None && draggedPulseIndex >= 0)
			{
				// Merge overlapping pulses on drag end
				var pulses = new List<(float start, float end, int index)>();
				for (int i = 0; i < pulsesProp.arraySize; i++)
				{
					var pulseProp = pulsesProp.GetArrayElementAtIndex(i);
					var startProp = pulseProp.FindPropertyRelative("start");
					var endProp = pulseProp.FindPropertyRelative("end");
					pulses.Add((startProp.floatValue, endProp.floatValue, i));
				}

				// Sort by start time
				pulses.Sort((a, b) => a.start.CompareTo(b.start));

				// Merge overlapping pulses
				var mergedPulses = new List<(float start, float end)>();
				float currentStart = pulses[0].start;
				float currentEnd = pulses[0].end;
				for (int i = 1; i < pulses.Count; i++)
				{
					if (pulses[i].start <= currentEnd)
					{
						// Overlap: extend current pulse
						currentEnd = Mathf.Max(currentEnd, pulses[i].end);
					}
					else
					{
						// No overlap: add current pulse and start new one
						mergedPulses.Add((currentStart, currentEnd));
						currentStart = pulses[i].start;
						currentEnd = pulses[i].end;
					}
				}
				mergedPulses.Add((currentStart, currentEnd));

				// Update pulses array
				pulsesProp.arraySize = mergedPulses.Count;
				for (int i = 0; i < mergedPulses.Count; i++)
				{
					var pulseProp = pulsesProp.GetArrayElementAtIndex(i);
					pulseProp.FindPropertyRelative("start").floatValue = mergedPulses[i].start;
					pulseProp.FindPropertyRelative("end").floatValue = mergedPulses[i].end;
				}

				// End dragging
				dragState = DragState.None;
				draggedPulseIndex = -1;
				Event.current.Use();
			}
			else if (Event.current.type == EventType.MouseUp)
			{
				// End dragging without merging
				dragState = DragState.None;
				draggedPulseIndex = -1;
				Event.current.Use();
			}

			// Display pulse details
			for (int i = 0; i < pulsesProp.arraySize; i++)
			{
				var pulseProp = pulsesProp.GetArrayElementAtIndex(i);
				var startProp = pulseProp.FindPropertyRelative("start");
				var endProp = pulseProp.FindPropertyRelative("end");
				EditorGUILayout.LabelField($"Pulse {i + 1}: Start = {startProp.floatValue:F2}, End = {endProp.floatValue:F2}");
			}

			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("particleCount"));
			EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("velocity"));

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