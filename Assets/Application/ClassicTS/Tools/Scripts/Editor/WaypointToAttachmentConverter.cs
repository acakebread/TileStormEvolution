// File: Assets/Application/ClassicTS/Tools/Scripts/Editor/WaypointToAttachmentConverter.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using ClassicTilestorm;

namespace ClassicTilestorm.Editor
{
	public class WaypointToAttachmentConverter : EditorWindow
	{
		private TextAsset databaseJson;

		[MenuItem("Tools/Classic Tilestorm/Convert Waypoints to Attachments (Viewpoint)")]
		public static void ShowWindow()
		{
			GetWindow<WaypointToAttachmentConverter>("Waypoint to Attachment Converter");
		}

		private void OnGUI()
		{
			GUILayout.Label("Waypoint to Attachment Converter", EditorStyles.boldLabel);
			GUILayout.Label("Moves camera waypoints (with vSrc/vDst) to Viewpoint attachments\n" +
							"Reduces waypoints[] to simple tile indices [12,17,42]", EditorStyles.helpBox);

			databaseJson = (TextAsset)EditorGUILayout.ObjectField("database.json", databaseJson, typeof(TextAsset), false);

			if (databaseJson == null)
			{
				EditorGUILayout.HelpBox("Drag your database.json TextAsset here first.", MessageType.Warning);
				return;
			}

			if (GUILayout.Button("CONVERT DATABASE NOW", GUILayout.Height(50)))
			{
				ConvertDatabase();
			}

			GUILayout.Space(10);
			EditorGUILayout.HelpBox(
				"Safe conversion – creates backup\n" +
				"• Camera waypoints become Viewpoint attachments\n" +
				"• waypoints[] becomes int[] only\n" +
				"• Names preserved (WP0, WP1…)",
				MessageType.Info);
		}

		private void ConvertDatabase()
		{
			string assetPath = AssetDatabase.GetAssetPath(databaseJson);
			string fullPath = Path.GetFullPath(assetPath);
			string backupPath = fullPath + ".bak";

			if (File.Exists(backupPath) && !EditorUtility.DisplayDialog("Backup exists",
				"Overwrite backup?\n" + backupPath, "Yes", "Cancel"))
				return;

			try
			{
				string jsonText = databaseJson.text;
				File.Copy(fullPath, backupPath, true);
				Debug.Log("Backup created: " + backupPath);

				var root = JObject.Parse(jsonText);
				var mapsArray = root["maps"] as JArray;
				if (mapsArray == null)
					throw new System.Exception("No 'maps' array found in database.json");

				int mapsConverted = 0;
				int viewpointsCreated = 0;

				// Use index-based loop to avoid "collection modified" error
				for (int m = 0; m < mapsArray.Count; m++)
				{
					var mapToken = mapsArray[m];
					var wpToken = mapToken["waypoints"] as JArray;
					if (wpToken == null || wpToken.Count == 0) continue;

					// Extract waypoint data manually
					var oldWaypoints = new List<(string name, int tile, float[] vSrc, float[] vDst)>();

					foreach (var w in wpToken)
					{
						string name = w["name"]?.ToString() ?? "";
						int tile = w["tile"]?.Value<int>() ?? -1;
						float[] vSrc = w["vSrc"]?.ToObject<float[]>();
						float[] vDst = w["vDst"]?.ToObject<float[]>();

						oldWaypoints.Add((name, tile, vSrc, vDst));
					}

					var newWaypointIndices = new List<int>();
					var newViewpointTokens = new List<JObject>();

					for (int i = 0; i < oldWaypoints.Count; i++)
					{
						var (name, tile, vSrc, vDst) = oldWaypoints[i];
						newWaypointIndices.Add(tile);

						if (vSrc != null && vDst != null && vSrc.Length == 3 && vDst.Length == 3)
						{
							var vp = new JObject
							{
								["type"] = "Viewpoint",
								["name"] = string.IsNullOrEmpty(name) ? $"WP{i}" : name,
								["tile"] = tile,
								["vSrc"] = JArray.FromObject(vSrc),
								["vDst"] = JArray.FromObject(vDst)
							};
							newViewpointTokens.Add(vp);
							viewpointsCreated++;
						}
					}

					// Replace waypoints with simple int array
					mapToken["waypoints"] = JArray.FromObject(newWaypointIndices);

					// Get or create attachments array
					var attachmentsToken = mapToken["attachments"] as JArray;
					if (attachmentsToken == null)
					{
						attachmentsToken = new JArray();
						mapToken["attachments"] = attachmentsToken;
					}

					// Add new Viewpoint attachments
					foreach (var vp in newViewpointTokens)
						attachmentsToken.Add(vp);

					mapsConverted++;
				}

				// Write final clean JSON
				File.WriteAllText(fullPath, root.ToString(Formatting.Indented));

				Debug.Log(
					$"CONVERSION SUCCESSFUL!\n" +
					$"• {mapsConverted} maps processed\n" +
					$"• {viewpointsCreated} Viewpoint attachments created\n" +
					$"• waypoints[] is now just tile indices\n" +
					$"Saved to: {fullPath}"
				);

				AssetDatabase.Refresh();
			}
			catch (System.Exception e)
			{
				Debug.LogError($"Conversion failed: {e.Message}\n{e.StackTrace}");
			}
		}
	}
}