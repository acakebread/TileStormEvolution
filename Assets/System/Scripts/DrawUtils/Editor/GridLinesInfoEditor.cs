//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEngine;
//using System.IO;

//[CustomEditor(typeof(MassiveHadronLtd.GridLinesHelper.GridLinesInfo))]
//public class GridLinesInfoEditor : Editor
//{
//	public override void OnInspectorGUI()
//	{
//		DrawDefaultInspector();

//		if (GUILayout.Button("Open Script"))
//		{
//			// Hard resolve by type name → file search
//			var scripts = AssetDatabase.FindAssets("GridLinesHelper t:Script");

//			foreach (var guid in scripts)
//			{
//				var path = AssetDatabase.GUIDToAssetPath(guid);

//				if (path.EndsWith("GridLinesHelper.cs"))
//				{
//					UnityEditorInternal.InternalEditorUtility
//						.OpenFileAtLineExternal(path, 1);
//					return;
//				}
//			}

//			Debug.LogError("Could not locate GridLinesHelper.cs");
//		}
//	}
//}
//#endif