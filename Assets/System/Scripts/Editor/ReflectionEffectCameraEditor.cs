using UnityEditor;

[CustomEditor(typeof(ReflectionEffectCamera))]
public class ReflectionEffectCameraEditor : Editor
{
	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		// Plane settings
		EditorGUILayout.LabelField("Plane Settings", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(serializedObject.FindProperty("planeNormal"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("offset"));

		// Effect settings
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Reflection Effects", EditorStyles.boldLabel);
		var effectModeProp = serializedObject.FindProperty("effectMode");
		EditorGUILayout.PropertyField(effectModeProp);

		// Show properties based on the selected effect mode
		switch ((ReflectionEffectCamera.EffectMode)effectModeProp.enumValueIndex)
		{
			case ReflectionEffectCamera.EffectMode.PerfectMirror:
				// No additional properties for PerfectMirror
				break;

			case ReflectionEffectCamera.EffectMode.SurfaceFilm:
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("filmIntensity"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseScale"));
				break;

			case ReflectionEffectCamera.EffectMode.FrostEffect:
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseStrength"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("frostDepth"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("baseColor"));
				break;

			case ReflectionEffectCamera.EffectMode.Water:
				EditorGUILayout.PropertyField(serializedObject.FindProperty("baseColor"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleSpeed"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleAmplitude"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleFrequency"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleOffset"));
				break;

			case ReflectionEffectCamera.EffectMode.OceanEffect:
				EditorGUILayout.PropertyField(serializedObject.FindProperty("baseColor"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleSpeed"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleAmplitude"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleFrequency"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("rippleOffset"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("frostDepth"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("frostThreshold"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("frostFadeRange"));
				break;
		}

		if (serializedObject.ApplyModifiedProperties())
		{
			var targetScript = (ReflectionEffectCamera)target;
			targetScript.OnValidate();
		}
	}
}