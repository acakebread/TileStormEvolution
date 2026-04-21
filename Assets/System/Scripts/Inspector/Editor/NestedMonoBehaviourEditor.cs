//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEditorInternal;
//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	[CustomEditor(typeof(MonoBehaviour), true)]
//	public class NestedMonoBehaviourEditor : Editor
//	{
//		public override void OnInspectorGUI()
//		{
//			var type = target.GetType();
//			bool isNested = type.DeclaringType != null;

//			if (isNested)
//			{
//				DrawNestedScriptHeader(type);
//				EditorGUILayout.Space(2);
//			}
//			else
//			{
//				// Normal components use default inspector
//				DrawDefaultInspector();
//				return;
//			}

//			serializedObject.Update();
//			DrawPropertiesExcluding(serializedObject, "m_Script");
//			serializedObject.ApplyModifiedProperties();
//		}

//		private void DrawNestedScriptHeader(System.Type type)
//		{
//			Rect rect = EditorGUILayout.GetControlRect(false, 20f);

//			// Dark background like native Script field
//			EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

//			Rect labelRect = new Rect(rect.x + 6, rect.y + 2, rect.width - 60, rect.height - 4);
//			Rect buttonRect = new Rect(rect.xMax - 48, rect.y + 2, 42, rect.height - 4);

//			string displayName = type.Name;

//			// Label styling
//			GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
//			{
//				normal = { textColor = new Color(0.7f, 0.85f, 1f) }
//			};

//			EditorGUI.LabelField(labelRect, displayName, labelStyle);
//			EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.Link);

//			// === Mouse Interaction ===
//			var e = Event.current;

//			// Single left click → Ping the script asset
//			if (e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition))
//			{
//				if (e.clickCount == 1)
//				{
//					PingOuterScript(type);
//				}
//				else if (e.clickCount >= 2)   // Double click → Open script
//				{
//					OpenOuterScript(type);
//				}
//				e.Use();
//			}

//			// Right click → Context menu
//			if (e.type == EventType.ContextClick && labelRect.Contains(e.mousePosition))
//			{
//				GenericMenu menu = new GenericMenu();
//				menu.AddItem(new GUIContent("Ping"), false, () => PingOuterScript(type));
//				menu.AddItem(new GUIContent("Edit Script"), false, () => OpenOuterScript(type));
//				menu.ShowAsContext();
//				e.Use();
//			}

//			// Object picker button (right side) - opens script (native-like)
//			if (GUI.Button(buttonRect, GUIContent.none, "ObjectFieldButton"))
//			{
//				OpenOuterScript(type);
//			}
//		}

//		private void PingOuterScript(System.Type type)
//		{
//			MonoScript scriptAsset = GetOuterMonoScript(type);
//			if (scriptAsset != null)
//			{
//				EditorGUIUtility.PingObject(scriptAsset);
//			}
//			else
//			{
//				Debug.LogWarning($"Could not find script for {type.DeclaringType?.Name}");
//			}
//		}

//		private void OpenOuterScript(System.Type type)
//		{
//			if (type.DeclaringType == null) return;

//			string outerName = type.DeclaringType.Name;
//			var guids = AssetDatabase.FindAssets(outerName + " t:MonoScript");

//			foreach (var guid in guids)
//			{
//				string path = AssetDatabase.GUIDToAssetPath(guid);
//				if (path.EndsWith(outerName + ".cs", System.StringComparison.OrdinalIgnoreCase))
//				{
//					InternalEditorUtility.OpenFileAtLineExternal(path, 1);
//					return;
//				}
//			}

//			// Fallback
//			if (guids.Length > 0)
//			{
//				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
//				InternalEditorUtility.OpenFileAtLineExternal(path, 1);
//			}
//			else
//			{
//				Debug.LogWarning($"Could not find script file for outer class '{outerName}'");
//			}
//		}

//		private MonoScript GetOuterMonoScript(System.Type type)
//		{
//			if (type.DeclaringType == null) return null;

//			string outerName = type.DeclaringType.Name;
//			var guids = AssetDatabase.FindAssets(outerName + " t:MonoScript");

//			foreach (var guid in guids)
//			{
//				string path = AssetDatabase.GUIDToAssetPath(guid);
//				if (path.EndsWith(outerName + ".cs", System.StringComparison.OrdinalIgnoreCase))
//				{
//					return AssetDatabase.LoadAssetAtPath<MonoScript>(path);
//				}
//			}
//			return null;
//		}
//	}
//}
//#endif

////#if UNITY_EDITOR
////using UnityEditor;
////using UnityEditorInternal;
////using UnityEngine;

////namespace MassiveHadronLtd
////{
////	// This editor applies to ALL MonoBehaviours (be careful in large projects).
////	// It only adds special handling for nested classes; otherwise it falls back to normal drawing.
////	[CustomEditor(typeof(MonoBehaviour), true)]
////	public class NestedMonoBehaviourEditor : Editor
////	{
////		public override void OnInspectorGUI()
////		{
////			var type = target.GetType();
////			bool isNested = type.DeclaringType != null;

////			if (isNested)
////			{
////				DrawNestedScriptHeader(type);
////				EditorGUILayout.Space(2);
////			}
////			else
////			{
////				// For normal (non-nested) components, draw the default script field + properties
////				DrawDefaultInspector();
////				return;
////			}

////			// Draw the rest of the properties (excluding the hidden m_Script)
////			serializedObject.Update();
////			DrawPropertiesExcluding(serializedObject, "m_Script");
////			serializedObject.ApplyModifiedProperties();
////		}

////		private void DrawNestedScriptHeader(System.Type type)
////		{
////			// Get the MonoScript asset for the outer class
////			MonoScript scriptAsset = GetOuterMonoScript(type);

////			Rect rect = EditorGUILayout.GetControlRect(false, 20f);

////			// Background similar to native Script field
////			EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f)); // dark gray

////			// Label area
////			Rect labelRect = new Rect(rect.x + 6, rect.y + 2, rect.width - 60, rect.height - 4);
////			Rect buttonRect = new Rect(rect.xMax - 48, rect.y + 2, 42, rect.height - 4);

////			string displayName = type.Name;

////			// Draw the label (bold + link cursor)
////			GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
////			{
////				normal = { textColor = new Color(0.7f, 0.85f, 1f) } // light blue-ish
////			};

////			EditorGUI.LabelField(labelRect, displayName, labelStyle);
////			EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.Link);

////			// Object picker button (looks like the native one)
////			if (GUI.Button(buttonRect, GUIContent.none, "ObjectFieldButton"))
////			{
////				if (scriptAsset != null)
////					Selection.activeObject = scriptAsset;
////				else
////					OpenOuterScript(type);
////			}

////			// Click handling
////			var e = Event.current;
////			if (e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition))
////			{
////				OpenOuterScript(type);
////				e.Use();
////			}

////			// Right-click context menu
////			if (e.type == EventType.ContextClick && labelRect.Contains(e.mousePosition))
////			{
////				GenericMenu menu = new GenericMenu();
////				menu.AddItem(new GUIContent("Edit Script"), false, () => OpenOuterScript(type));
////				if (scriptAsset != null)
////					menu.AddItem(new GUIContent("Ping Script Asset"), false, () => EditorGUIUtility.PingObject(scriptAsset));
////				menu.ShowAsContext();
////				e.Use();
////			}
////		}

////		private MonoScript GetOuterMonoScript(System.Type type)
////		{
////			if (type.DeclaringType == null) return null;

////			string outerName = type.DeclaringType.Name;
////			var guids = AssetDatabase.FindAssets(outerName + " t:MonoScript");

////			foreach (var guid in guids)
////			{
////				string path = AssetDatabase.GUIDToAssetPath(guid);
////				if (path.EndsWith(outerName + ".cs", System.StringComparison.OrdinalIgnoreCase))
////				{
////					return AssetDatabase.LoadAssetAtPath<MonoScript>(path);
////				}
////			}
////			return null;
////		}

////		private void OpenOuterScript(System.Type type)
////		{
////			if (type.DeclaringType == null) return;

////			string outerName = type.DeclaringType.Name;
////			var guids = AssetDatabase.FindAssets(outerName + " t:MonoScript");

////			foreach (var guid in guids)
////			{
////				string path = AssetDatabase.GUIDToAssetPath(guid);
////				if (path.EndsWith(outerName + ".cs", System.StringComparison.OrdinalIgnoreCase))
////				{
////					InternalEditorUtility.OpenFileAtLineExternal(path, 1);
////					return;
////				}
////			}

////			// Fallback: open first result
////			if (guids.Length > 0)
////			{
////				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
////				InternalEditorUtility.OpenFileAtLineExternal(path, 1);
////			}
////			else
////			{
////				Debug.LogWarning($"Could not find script file for outer class '{outerName}'");
////			}
////		}
////	}
////}
////#endif


////#if UNITY_EDITOR
////using UnityEditor;
////using UnityEngine;
////using UnityEditorInternal;

////[CustomEditor(typeof(MonoBehaviour), true)]
////public class NestedMonoBehaviourEditor : Editor
////{
////	public override void OnInspectorGUI()
////	{
////		var type = target.GetType();

////		bool isNested = type.DeclaringType != null;

////		if (isNested)
////		{
////			DrawScriptHeader(type);
////			EditorGUILayout.Space(4);
////		}

////		DrawPropertiesExcluding(serializedObject, "m_Script");

////		serializedObject.ApplyModifiedProperties();
////	}

////	private void DrawScriptHeader(System.Type type)
////	{
////		var rect = EditorGUILayout.GetControlRect();

////		// Split into label + button area
////		const float buttonWidth = 20f;
////		var labelRect = new Rect(rect.x, rect.y, rect.width - buttonWidth, rect.height);
////		var buttonRect = new Rect(rect.x + rect.width - buttonWidth, rect.y, buttonWidth, rect.height);

////		// Background
////		GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

////		// Label
////		var content = new GUIContent(type.Name, "Edit Script");
////		EditorGUI.LabelField(new Rect(labelRect.x + 6, labelRect.y + 2, labelRect.width - 6, labelRect.height - 4), content);

////		var e = Event.current;

////		// Cursor for label
////		EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.Link);

////		// LEFT CLICK (label)
////		if (e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition))
////		{
////			OpenDeclaringScript(type);
////			e.Use();
////		}

////		// RIGHT CLICK (label)
////		if (e.type == EventType.ContextClick && labelRect.Contains(e.mousePosition))
////		{
////			var menu = new GenericMenu();
////			menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
////			menu.ShowAsContext();
////			e.Use();
////		}

////		// Draw the little object-picker button
////		var buttonStyle = GUI.skin.FindStyle("ObjectFieldButton") ?? GUI.skin.button;

////		if (GUI.Button(buttonRect, GUIContent.none, buttonStyle))
////		{
////			// Option 1: just open script (consistent UX)
////			OpenDeclaringScript(type);

////			// Option 2 (optional): show context menu instead
////			// var menu = new GenericMenu();
////			// menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
////			// menu.ShowAsContext();
////		}
////	}

////	//private void DrawScriptHeader(System.Type type)
////	//{
////	//	var rect = EditorGUILayout.GetControlRect();

////	//	// Tooltip + label
////	//	var content = new GUIContent(type.Name, "Edit Script");

////	//	// Draw background like Unity field
////	//	GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

////	//	// Slight padding inside the box
////	//	var labelRect = new Rect(rect.x + 6, rect.y + 2, rect.width - 12, rect.height - 4);

////	//	// Draw label (no fake object field anymore)
////	//	EditorGUI.LabelField(labelRect, content);

////	//	var e = Event.current;

////	//	// Cursor hint
////	//	EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

////	//	// LEFT CLICK
////	//	if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
////	//	{
////	//		OpenDeclaringScript(type);
////	//		e.Use();
////	//	}

////	//	// RIGHT CLICK
////	//	if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
////	//	{
////	//		var menu = new GenericMenu();
////	//		menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
////	//		menu.ShowAsContext();
////	//		e.Use();
////	//	}
////	//}

////	//private void DrawScriptHeader(System.Type type)
////	//{
////	//	var content = new GUIContent(type.Name, "Edit Script");

////	//	using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
////	//	{
////	//		EditorGUI.BeginDisabledGroup(true);

////	//		// Fake script field (label only, since MonoScript is unavailable)
////	//		EditorGUILayout.ObjectField(content, null, typeof(MonoScript), false);

////	//		EditorGUI.EndDisabledGroup();

////	//		var rect = GUILayoutUtility.GetLastRect();
////	//		var e = Event.current;

////	//		// Change cursor to link-style
////	//		EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

////	//		// LEFT CLICK → open script
////	//		if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
////	//		{
////	//			OpenDeclaringScript(type);
////	//			e.Use();
////	//		}

////	//		// RIGHT CLICK → context menu
////	//		if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
////	//		{
////	//			var menu = new GenericMenu();
////	//			menu.AddItem(new GUIContent("Edit Script"), false, () => OpenDeclaringScript(type));
////	//			menu.ShowAsContext();
////	//			e.Use();
////	//		}
////	//	}
////	//}

////	private void OpenDeclaringScript(System.Type type)
////	{
////		var outer = type.DeclaringType.Name;

////		var guids = AssetDatabase.FindAssets($"{outer} t:Script");

////		foreach (var guid in guids)
////		{
////			var path = AssetDatabase.GUIDToAssetPath(guid);

////			if (path.EndsWith(outer + ".cs"))
////			{
////				InternalEditorUtility.OpenFileAtLineExternal(path, 1);
////				return;
////			}
////		}

////		Debug.LogError($"Could not locate {outer}.cs");
////	}
////}
////#endif

//////#if UNITY_EDITOR
//////using UnityEditor;
//////using UnityEngine;
//////using UnityEditorInternal;

//////[CustomEditor(typeof(MonoBehaviour), true)]
//////public class NestedMonoBehaviourEditor : Editor
//////{
//////	public override void OnInspectorGUI()
//////	{
//////		DrawDefaultInspector();

//////		var type = target.GetType();

//////		// Only show for nested types
//////		if (type.DeclaringType == null) return;

//////		GUILayout.Space(8);

//////		if (GUILayout.Button("Open Declaring Script"))
//////		{
//////			var outer = type.DeclaringType.Name;

//////			var guids = AssetDatabase.FindAssets($"{outer} t:Script");

//////			foreach (var guid in guids)
//////			{
//////				var path = AssetDatabase.GUIDToAssetPath(guid);

//////				if (path.EndsWith(outer + ".cs"))
//////				{
//////					InternalEditorUtility.OpenFileAtLineExternal(path, 1);
//////					return;
//////				}
//////			}

//////			Debug.LogError($"Could not locate {outer}.cs");
//////		}
//////	}
//////}
//////#endif