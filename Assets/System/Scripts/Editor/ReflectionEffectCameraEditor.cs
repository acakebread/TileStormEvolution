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
		var usePerfectMirrorProp = serializedObject.FindProperty("usePerfectMirror");
		var useSurfaceFilmProp = serializedObject.FindProperty("useSurfaceFilm");
		var useFrostedEffectProp = serializedObject.FindProperty("useFrostedEffect");

		EditorGUILayout.PropertyField(usePerfectMirrorProp);

		if (!usePerfectMirrorProp.boolValue)
		{
			EditorGUILayout.PropertyField(useSurfaceFilmProp);
			EditorGUILayout.PropertyField(useFrostedEffectProp);

			// Ensure only one effect is enabled
			if (useSurfaceFilmProp.boolValue && useFrostedEffectProp.boolValue)
			{
				useFrostedEffectProp.boolValue = false; // Prioritize surface film if both are checked
			}

			if (useSurfaceFilmProp.boolValue)
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("filmIntensity"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseScale"));
			}
			else if (useFrostedEffectProp.boolValue)
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("frostRadius"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("baseColor"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseStrength"));
			}
		}
		else
		{
			useSurfaceFilmProp.boolValue = false;
			useFrostedEffectProp.boolValue = false;
		}

		EditorGUILayout.Space();
		EditorGUILayout.PropertyField(serializedObject.FindProperty("customReflectionMaterial"));

		if (serializedObject.ApplyModifiedProperties())
		{
			var targetScript = (ReflectionEffectCamera)target;
			targetScript.OnValidate();
		}
	}
}