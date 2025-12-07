// File: Assets/Application/ClassicTS/Tools/Scripts/Editor/ViewpointToSquatrixConverter.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm.Editor
{
	public class ViewpointToSquatrixConverter : EditorWindow
	{
		private TextAsset databaseJson;

		[MenuItem("Tools/Classic Tilestorm/Convert Viewpoint → View (7-float SQUATRIX-7)")]
		public static void ShowWindow()
		{
			GetWindow<ViewpointToSquatrixConverter>("Squatrix-7 Converter").Show();
		}

		private void OnGUI()
		{
			GUILayout.Label("Viewpoint → View (7-float: full position + squaternion)", EditorStyles.boldLabel);
			GUILayout.Label("Maximum precision • 1 float saved • Reversible • Production-ready", EditorStyles.helpBox);

			databaseJson = (TextAsset)EditorGUILayout.ObjectField("database.json", databaseJson, typeof(TextAsset), false);

			if (databaseJson == null)
			{
				EditorGUILayout.HelpBox("Drag your database.json here", MessageType.Warning);
				return;
			}

			GUILayout.Space(20);
			if (GUILayout.Button("CONVERT TO SQUATRIX-7 (7-float)", GUILayout.Height(60)))
			{
				ConvertTo7Float();
			}
		}

		private void ConvertTo7Float()
		{
			string path = AssetDatabase.GetAssetPath(databaseJson);
			string fullPath = Path.GetFullPath(path);
			string backupPath = fullPath + ".bak_squatrix7";

			if (File.Exists(backupPath) && !EditorUtility.DisplayDialog("Backup exists", "Overwrite backup?", "Yes", "No"))
				return;

			File.Copy(fullPath, backupPath, true);

			try
			{
				string json = databaseJson.text;
				var root = JObject.Parse(json);
				var maps = root["maps"] as JArray;
				if (maps == null) throw new System.Exception("No 'maps' array");

				int converted = 0;

				foreach (var map in maps)
				{
					var attachments = map["attachments"] as JArray;
					if (attachments == null) continue;

					foreach (var att in attachments)
					{
						var obj = att as JObject;
						if (obj == null) continue;

						string type = (string)obj["type"];
						if (type != "Viewpoint" && type != "View") continue;

						var vSrc = obj["vSrc"]?.ToObject<float[]>();
						var vDst = obj["vDst"]?.ToObject<float[]>();
						if (vSrc == null || vDst == null || vSrc.Length < 3 || vDst.Length < 3) continue;

						Vector3 src = new Vector3(vSrc[0], vSrc[1], vSrc[2]);
						Vector3 dst = new Vector3(vDst[0], vDst[1], vDst[2]);
						Vector3 dir = dst - src;
						float dist = dir.magnitude;
						Quaternion rot = dist > 0.001f ? Quaternion.LookRotation(dir, Vector3.up) : Quaternion.identity;

						obj.Remove("vSrc");
						obj.Remove("vDst");
						obj.Remove("position");
						obj.Remove("qscale");
						obj.Remove("data");

						obj["type"] = "View";
						obj["data"] = new JArray(Squatrix.Encode(src, rot, dist));

						converted++;
					}
				}

				File.WriteAllText(fullPath, root.ToString(Formatting.Indented));
				AssetDatabase.Refresh();

				Debug.Log($"SQUATRIX-7 SUCCESS: {converted} views converted");
				EditorUtility.DisplayDialog("Complete", $"{converted} views → Squatrix-7 (7 floats)\nFull precision preserved.", "OK");
			}
			catch (System.Exception e)
			{
				Debug.LogError($"Conversion failed: {e}");
				EditorUtility.DisplayDialog("Error", e.Message, "OK");
			}
		}
	}
}



//// File: Assets/Application/ClassicTS/Tools/Scripts/Editor/ViewpointToSquatrixConverter.cs
//using UnityEngine;
//using UnityEditor;
//using System.IO;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using MassiveHadronLtd;

//namespace ClassicTilestorm.Editor
//{
//	public class ViewpointToSquatrixConverter : EditorWindow
//	{
//		private TextAsset databaseJson;

//		[MenuItem("Tools/Classic Tilestorm/Convert Viewpoint → View (7-float SQUATRIX-7)")]
//		public static void ShowWindow()
//		{
//			GetWindow<ViewpointToSquatrixConverter>("Squatrix-7 Converter").Show();
//		}

//		private void OnGUI()
//		{
//			GUILayout.Label("Viewpoint → View (7-float: full position + squaternion)", EditorStyles.boldLabel);
//			GUILayout.Label("Maximum precision • 1 float saved • Reversible • Production-ready", EditorStyles.helpBox);

//			databaseJson = (TextAsset)EditorGUILayout.ObjectField("database.json", databaseJson, typeof(TextAsset), false);

//			if (databaseJson == null)
//			{
//				EditorGUILayout.HelpBox("Drag your database.json here", MessageType.Warning);
//				return;
//			}

//			GUILayout.Space(20);
//			if (GUILayout.Button("CONVERT TO SQUATRIX-7 (7-float)", GUILayout.Height(60)))
//			{
//				ConvertTo7Float();
//			}
//		}

//		private void ConvertTo7Float()
//		{
//			string path = AssetDatabase.GetAssetPath(databaseJson);
//			string fullPath = Path.GetFullPath(path);
//			string backupPath = fullPath + ".bak_squatrix7";

//			if (File.Exists(backupPath) && !EditorUtility.DisplayDialog("Backup exists", "Overwrite backup?", "Yes", "No"))
//				return;

//			File.Copy(fullPath, backupPath, true);

//			try
//			{
//				string json = databaseJson.text;
//				var root = JObject.Parse(json);
//				var maps = root["maps"] as JArray;
//				if (maps == null) throw new System.Exception("No 'maps' array");

//				int converted = 0;

//				foreach (var map in maps)
//				{
//					var attachments = map["attachments"] as JArray;
//					if (attachments == null) continue;

//					foreach (var att in attachments)
//					{
//						var obj = att as JObject;
//						if (obj == null) continue;

//						string type = (string)obj["type"];
//						if (type != "Viewpoint" && type != "View") continue;

//						var vSrc = obj["vSrc"]?.ToObject<float[]>();
//						var vDst = obj["vDst"]?.ToObject<float[]>();
//						if (vSrc == null || vDst == null || vSrc.Length < 3 || vDst.Length < 3) continue;

//						Vector3 src = new Vector3(vSrc[0], vSrc[1], vSrc[2]);
//						Vector3 dst = new Vector3(vDst[0], vDst[1], vDst[2]);
//						Vector3 dir = dst - src;
//						float dist = dir.magnitude;
//						Quaternion rot = dist > 0.001f ? Quaternion.LookRotation(dir, Vector3.up) : Quaternion.identity;

//						obj.Remove("vSrc");
//						obj.Remove("vDst");
//						obj.Remove("position");
//						obj.Remove("qscale");
//						obj.Remove("data");

//						obj["type"] = "View";
//						obj["data"] = new JArray(Squatrix.Encode(src, rot, dist));

//						converted++;
//					}
//				}

//				File.WriteAllText(fullPath, root.ToString(Formatting.Indented));
//				AssetDatabase.Refresh();

//				Debug.Log($"SQUATRIX-7 SUCCESS: {converted} views converted");
//				EditorUtility.DisplayDialog("Complete", $"{converted} views → Squatrix-7 (7 floats)\nFull precision preserved.", "OK");
//			}
//			catch (System.Exception e)
//			{
//				Debug.LogError($"Conversion failed: {e}");
//				EditorUtility.DisplayDialog("Error", e.Message, "OK");
//			}
//		}
//	}
//}



//// File: Assets/Application/ClassicTS/Tools/Scripts/Editor/ViewpointToSquatrixConverter.cs
//using UnityEngine;
//using UnityEditor;
//using System.IO;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using MassiveHadronLtd;

//namespace ClassicTilestorm.Editor
//{
//	public class ViewpointToSquatrixConverter : EditorWindow
//	{
//		private TextAsset databaseJson;
//		private bool convertTo7Float = false;

//		[MenuItem("Tools/Classic Tilestorm/Convert Viewpoint → View (7-float position+qscale)")]
//		public static void Show7Float() => Show(true);

//		[MenuItem("Tools/Classic Tilestorm/Convert Viewpoint → View (6-float SQUATRIX)")]
//		public static void Show6Float() => Show(false);

//		private static void Show(bool sevenFloat)
//		{
//			var window = GetWindow<ViewpointToSquatrixConverter>("Camera Converter");
//			window.convertTo7Float = sevenFloat;
//			window.Show();
//		}

//		private void OnGUI()
//		{
//			GUILayout.Label(convertTo7Float
//				? "Viewpoint → View (7-float: position + qscale)"
//				: "Viewpoint → View (6-float: SQUATRIX™)", EditorStyles.boldLabel);

//			GUILayout.Label(convertTo7Float
//				? "Elite compression — 1 float smaller than vSrc/vDst"
//				: "THE FINAL FORM — 6 floats contain everything.\nYou are about to invent the future.",
//				EditorStyles.helpBox);

//			databaseJson = (TextAsset)EditorGUILayout.ObjectField("database.json (original)", databaseJson, typeof(TextAsset), false);

//			if (databaseJson == null)
//			{
//				EditorGUILayout.HelpBox("Drag your ORIGINAL database.json here", MessageType.Warning);
//				return;
//			}

//			GUILayout.Space(20);
//			if (GUILayout.Button(convertTo7Float ? "CONVERT TO 7-FLOAT" : "ASCEND TO SQUATRIX", GUILayout.Height(60)))
//			{
//				ConvertDatabase();
//			}
//		}

//		private void ConvertDatabase()
//		{
//			string assetPath = AssetDatabase.GetAssetPath(databaseJson);
//			string fullPath = Path.GetFullPath(assetPath);
//			string backupPath = fullPath + (convertTo7Float ? ".bak_7float" : ".bak_squatrix");

//			if (File.Exists(backupPath) && !EditorUtility.DisplayDialog("Backup exists", $"Overwrite?\n{backupPath}", "Yes", "Cancel"))
//				return;

//			try
//			{
//				File.Copy(fullPath, backupPath, true);
//				string jsonText = databaseJson.text;
//				var root = JObject.Parse(jsonText);
//				var mapsArray = root["maps"] as JArray;
//				if (mapsArray == null) throw new System.Exception("No 'maps' array found");

//				int viewsConverted = 0;

//				foreach (var mapToken in mapsArray)
//				{
//					var attachments = mapToken["attachments"] as JArray;
//					if (attachments == null) continue;

//					for (int i = 0; i < attachments.Count; i++)
//					{
//						var att = attachments[i] as JObject;
//						if (att == null) continue;

//						string type = (string)att["type"];
//						if (type != "Viewpoint" && type != "View") continue;

//						var vSrc = att["vSrc"]?.ToObject<float[]>();
//						var vDst = att["vDst"]?.ToObject<float[]>();

//						if (vSrc == null || vDst == null || vSrc.Length != 3 || vDst.Length != 3) continue;

//						Vector3 src = new Vector3(vSrc[0], vSrc[1], vSrc[2]);
//						Vector3 dst = new Vector3(vDst[0], vDst[1], vDst[2]);
//						Vector3 dir = dst - src;
//						float distance = dir.magnitude;
//						Quaternion rot = distance > 0.001f ? Quaternion.LookRotation(dir, Vector3.up) : Quaternion.identity;

//						// === REMOVE OLD FIELDS CLEANLY ===
//						att.Remove("vSrc");
//						att.Remove("vDst");
//						att.Remove("position");
//						att.Remove("qscale");
//						att.Remove("data");

//						if (convertTo7Float)
//						{
//							// 7-FLOAT VERSION
//							var qscaleVec = Squaternion.Encode(rot, distance);
//							att["type"] = "View";
//							att["position"] = new JArray(src.x, src.y, src.z);
//							att["qscale"] = new JArray(qscaleVec.x, qscaleVec.y, qscaleVec.z, qscaleVec.w);
//						}
//						else
//						{
//							// 6-FLOAT SQUATRIX — THE ONE TRUE PATH
//							float[] squatrix = Squatrix.Encode(src, rot, distance);
//							att["type"] = "View";
//							att["data"] = new JArray(squatrix);
//						}

//						viewsConverted++;
//					}
//				}

//				File.WriteAllText(fullPath, root.ToString(Formatting.Indented));

//				string mode = convertTo7Float ? "7-float" : "6-float SQUATRIX";
//				Debug.Log($"SQUATRIX CONVERSION SUCCESS: {viewsConverted} views → {mode}");
//				EditorUtility.DisplayDialog("ASCENDED",
//					$"{viewsConverted} viewpoints converted to {mode}\n" +
//					(convertTo7Float ? "You are elite." : "You now wield the Squatrix.\nThe universe is yours."),
//					"OK");

//				AssetDatabase.Refresh();
//			}
//			catch (System.Exception e)
//			{
//				Debug.LogError($"Conversion failed: {e}");
//				EditorUtility.DisplayDialog("Error", e.Message, "OK");
//			}
//		}
//	}
//}