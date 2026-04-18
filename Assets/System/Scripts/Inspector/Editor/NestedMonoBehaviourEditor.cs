#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace MassiveHadronLtd
{
	[CustomEditor(typeof(MonoBehaviour), true)]
	public class NestedMonoBehaviourEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var type = target.GetType();

			bool isNested = type.DeclaringType != null;

			if (isNested)
			{
				DrawScriptHeader(type);
				EditorGUILayout.Space(4);
			}

			DrawPropertiesExcluding(serializedObject, "m_Script");

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawScriptHeader(System.Type type)
		{
			var rect = EditorGUILayout.GetControlRect();

			// Split into label + button area
			const float buttonWidth = 20f;
			var labelRect = new Rect(rect.x, rect.y, rect.width - buttonWidth, rect.height);
			var buttonRect = new Rect(rect.x + rect.width - buttonWidth, rect.y, buttonWidth, rect.height);

			// Background
			GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

			// Label
			var content = new GUIContent(type.Name, "Edit Script");
			EditorGUI.LabelField(new Rect(labelRect.x + 6, labelRect.y + 2, labelRect.width - 6, labelRect.height - 4), content);

			var e = Event.current;

			// Cursor for label
			EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.Link);

			// LEFT CLICK (label)
			if (e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition))
			{
				OpenDeclaringScript(type);
				e.Use();
			}

			// RIGHT CLICK (label)
			if (e.type == EventType.ContextClick && labelRect.Contains(e.mousePosition))
			{
				var menu = new GenericMenu();
				menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
				menu.ShowAsContext();
				e.Use();
			}

			// Draw the little object-picker button
			var buttonStyle = GUI.skin.FindStyle("ObjectFieldButton") ?? GUI.skin.button;

			if (GUI.Button(buttonRect, GUIContent.none, buttonStyle))
			{
				// Option 1: just open script (consistent UX)
				OpenDeclaringScript(type);

				// Option 2 (optional): show context menu instead
				// var menu = new GenericMenu();
				// menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
				// menu.ShowAsContext();
			}
		}

		private void OpenDeclaringScript(System.Type type)
		{
			var outer = type.DeclaringType.Name;

			var guids = AssetDatabase.FindAssets($"{outer} t:Script");

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);

				if (path.EndsWith(outer + ".cs"))
				{
					InternalEditorUtility.OpenFileAtLineExternal(path, 1);
					return;
				}
			}

			Debug.LogError($"Could not locate {outer}.cs");
		}
	}
}
#endif


//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEngine;
//using UnityEditorInternal;

//[CustomEditor(typeof(MonoBehaviour), true)]
//public class NestedMonoBehaviourEditor : Editor
//{
//	public override void OnInspectorGUI()
//	{
//		var type = target.GetType();

//		bool isNested = type.DeclaringType != null;

//		if (isNested)
//		{
//			DrawScriptHeader(type);
//			EditorGUILayout.Space(4);
//		}

//		DrawPropertiesExcluding(serializedObject, "m_Script");

//		serializedObject.ApplyModifiedProperties();
//	}

//	private void DrawScriptHeader(System.Type type)
//	{
//		var rect = EditorGUILayout.GetControlRect();

//		// Split into label + button area
//		const float buttonWidth = 20f;
//		var labelRect = new Rect(rect.x, rect.y, rect.width - buttonWidth, rect.height);
//		var buttonRect = new Rect(rect.x + rect.width - buttonWidth, rect.y, buttonWidth, rect.height);

//		// Background
//		GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

//		// Label
//		var content = new GUIContent(type.Name, "Edit Script");
//		EditorGUI.LabelField(new Rect(labelRect.x + 6, labelRect.y + 2, labelRect.width - 6, labelRect.height - 4), content);

//		var e = Event.current;

//		// Cursor for label
//		EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.Link);

//		// LEFT CLICK (label)
//		if (e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition))
//		{
//			OpenDeclaringScript(type);
//			e.Use();
//		}

//		// RIGHT CLICK (label)
//		if (e.type == EventType.ContextClick && labelRect.Contains(e.mousePosition))
//		{
//			var menu = new GenericMenu();
//			menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
//			menu.ShowAsContext();
//			e.Use();
//		}

//		// Draw the little object-picker button
//		var buttonStyle = GUI.skin.FindStyle("ObjectFieldButton") ?? GUI.skin.button;

//		if (GUI.Button(buttonRect, GUIContent.none, buttonStyle))
//		{
//			// Option 1: just open script (consistent UX)
//			OpenDeclaringScript(type);

//			// Option 2 (optional): show context menu instead
//			// var menu = new GenericMenu();
//			// menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
//			// menu.ShowAsContext();
//		}
//	}

//	//private void DrawScriptHeader(System.Type type)
//	//{
//	//	var rect = EditorGUILayout.GetControlRect();

//	//	// Tooltip + label
//	//	var content = new GUIContent(type.Name, "Edit Script");

//	//	// Draw background like Unity field
//	//	GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

//	//	// Slight padding inside the box
//	//	var labelRect = new Rect(rect.x + 6, rect.y + 2, rect.width - 12, rect.height - 4);

//	//	// Draw label (no fake object field anymore)
//	//	EditorGUI.LabelField(labelRect, content);

//	//	var e = Event.current;

//	//	// Cursor hint
//	//	EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

//	//	// LEFT CLICK
//	//	if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
//	//	{
//	//		OpenDeclaringScript(type);
//	//		e.Use();
//	//	}

//	//	// RIGHT CLICK
//	//	if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
//	//	{
//	//		var menu = new GenericMenu();
//	//		menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
//	//		menu.ShowAsContext();
//	//		e.Use();
//	//	}
//	//}

//	//private void DrawScriptHeader(System.Type type)
//	//{
//	//	var content = new GUIContent(type.Name, "Edit Script");

//	//	using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
//	//	{
//	//		EditorGUI.BeginDisabledGroup(true);

//	//		// Fake script field (label only, since MonoScript is unavailable)
//	//		EditorGUILayout.ObjectField(content, null, typeof(MonoScript), false);

//	//		EditorGUI.EndDisabledGroup();

//	//		var rect = GUILayoutUtility.GetLastRect();
//	//		var e = Event.current;

//	//		// Change cursor to link-style
//	//		EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

//	//		// LEFT CLICK → open script
//	//		if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
//	//		{
//	//			OpenDeclaringScript(type);
//	//			e.Use();
//	//		}

//	//		// RIGHT CLICK → context menu
//	//		if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
//	//		{
//	//			var menu = new GenericMenu();
//	//			menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
//	//			menu.ShowAsContext();
//	//			e.Use();
//	//		}
//	//	}
//	//}

//	private void OpenDeclaringScript(System.Type type)
//	{
//		var outer = type.DeclaringType.Name;

//		var guids = AssetDatabase.FindAssets($"{outer} t:Script");

//		foreach (var guid in guids)
//		{
//			var path = AssetDatabase.GUIDToAssetPath(guid);

//			if (path.EndsWith(outer + ".cs"))
//			{
//				InternalEditorUtility.OpenFileAtLineExternal(path, 1);
//				return;
//			}
//		}

//		Debug.LogError($"Could not locate {outer}.cs");
//	}
//}
//#endif

////#if UNITY_EDITOR
////using UnityEditor;
////using UnityEngine;
////using UnityEditorInternal;

////[CustomEditor(typeof(MonoBehaviour), true)]
////public class NestedMonoBehaviourEditor : Editor
////{
////	public override void OnInspectorGUI()
////	{
////		DrawDefaultInspector();

////		var type = target.GetType();

////		// Only show for nested types
////		if (type.DeclaringType == null) return;

////		GUILayout.Space(8);

////		if (GUILayout.Button("Open Declaring Script"))
////		{
////			var outer = type.DeclaringType.Name;

////			var guids = AssetDatabase.FindAssets($"{outer} t:Script");

////			foreach (var guid in guids)
////			{
////				var path = AssetDatabase.GUIDToAssetPath(guid);

////				if (path.EndsWith(outer + ".cs"))
////				{
////					InternalEditorUtility.OpenFileAtLineExternal(path, 1);
////					return;
////				}
////			}

////			Debug.LogError($"Could not locate {outer}.cs");
////		}
////	}
////}
////#endif