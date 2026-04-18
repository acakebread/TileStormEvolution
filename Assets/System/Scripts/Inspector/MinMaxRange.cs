using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
	/// <summary>
	/// Generic min-max range struct. Reusable for velocity, lifetime, scale, intensity, etc.
	/// </summary>
	[System.Serializable]
	public struct MinMaxRange
	{
		[Range(0f, 50f)] public float min;
		[Range(0f, 50f)] public float max;

		public MinMaxRange(float minValue, float maxValue)
		{
			min = Mathf.Min(minValue, maxValue);
			max = Mathf.Max(minValue, maxValue);
		}
	}

#if UNITY_EDITOR
	/// <summary>
	/// Custom inspector drawer for MinMaxRange – shows a nice min-max slider + fields.
	/// </summary>
	[CustomPropertyDrawer(typeof(MinMaxRange))]
	public class MinMaxRangeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			// Label
			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			SerializedProperty minProp = property.FindPropertyRelative("min");
			SerializedProperty maxProp = property.FindPropertyRelative("max");

			float min = minProp.floatValue;
			float max = maxProp.floatValue;

			// Layout rectangles
			float labelWidth = 40f;
			float fieldWidth = 50f;

			Rect sliderRect = new Rect(position.x + labelWidth, position.y,
				position.width - labelWidth - fieldWidth * 2 - 10f, position.height);

			Rect minRect = new Rect(position.x + position.width - fieldWidth * 2 - 5f, position.y, fieldWidth, position.height);
			Rect maxRect = new Rect(position.x + position.width - fieldWidth, position.y, fieldWidth, position.height);

			// Min-Max Slider
			EditorGUI.BeginChangeCheck();
			EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, 0f, 50f);

			if (EditorGUI.EndChangeCheck())
			{
				minProp.floatValue = Mathf.Min(min, max);
				maxProp.floatValue = Mathf.Max(min, max);
			}

			// Float fields
			min = EditorGUI.FloatField(minRect, min);
			max = EditorGUI.FloatField(maxRect, max);

			// Clamp
			min = Mathf.Clamp(min, 0f, 50f);
			max = Mathf.Clamp(max, min, 50f);

			minProp.floatValue = min;
			maxProp.floatValue = max;

			EditorGUI.EndProperty();
		}
	}
#endif
}