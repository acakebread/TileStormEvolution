//using UnityEngine;
//using UnityEditor;
//using System.IO;
//using System.Diagnostics;
//using Debug = UnityEngine.Debug;

//namespace com.massivehadron.utils.cloud_ai_utils
//{
//	public class ChatGPTIntegration : EditorWindow
//	{
//		private string scriptPrompt;
//		private string consolePrompt;

//		private static string lastConsoleMessage = "";
//		private static string lastConsoleStackTrace = "";
//		private static LogType lastConsoleType;

//		// Subscribe to Unity's log events
//		[InitializeOnLoadMethod]
//		private static void InitLogListener()
//		{
//			Application.logMessageReceived -= CaptureLogMessage;
//			Application.logMessageReceived += CaptureLogMessage;
//		}

//		private static void CaptureLogMessage(string condition, string stackTrace, LogType type)
//		{
//			lastConsoleMessage = condition;
//			lastConsoleStackTrace = stackTrace;
//			lastConsoleType = type;
//		}

//		// ===============================
//		// SCRIPT CONTEXT MENU
//		// ===============================
//		[MenuItem("Assets/Send Script to ChatGPT", false, 2000)]
//		private static void SendScriptToChatGPT_Context()
//		{
//			SendSelectedScriptToChatGPT();
//		}

//		// ===============================
//		// CONSOLE CONTEXT MENU
//		// ===============================
//		[MenuItem("Assets/Send Last Console Message to ChatGPT", false, 2001)]
//		private static void SendConsoleToChatGPT_Context()
//		{
//			SendConsoleToChatGPT();
//		}

//		// ===============================
//		// Shared logic for scripts
//		// ===============================
//		private static void SendSelectedScriptToChatGPT()
//		{
//			var selected = Selection.activeObject;
//			if (selected == null)
//			{
//				Debug.LogWarning("⚠️ No script selected.");
//				return;
//			}

//			string path = AssetDatabase.GetAssetPath(selected);
//			if (!path.EndsWith(".cs"))
//			{
//				Debug.LogWarning("⚠️ Selected asset is not a C# script.");
//				return;
//			}

//			string scriptText = File.ReadAllText(path);

//			string prompt = EditorPrefs.GetString(
//				"ChatGPT_ScriptPrompt",
//				"please refactor this script for efficiency"
//			);

//			string combined = prompt + "\n\n```csharp\n" + scriptText + "\n```";

//			GUIUtility.systemCopyBuffer = combined;

//			Debug.Log($"✅ Script + prompt copied to clipboard. Opening ChatGPT for {Path.GetFileName(path)}...");

//			OpenURL("https://chat.openai.com/");

//			EditorUtility.DisplayDialog(
//				"ChatGPT Ready",
//				"Your script + prompt has been copied to the clipboard.\n\n" +
//				"👉 Switch to ChatGPT and press Ctrl+V (Cmd+V on Mac) to paste it.",
//				"OK"
//			);
//		}

//		// ===============================
//		// Shared logic for console messages
//		// ===============================
//		private static void SendConsoleToChatGPT()
//		{
//			if (string.IsNullOrEmpty(lastConsoleMessage))
//			{
//				EditorUtility.DisplayDialog("No Console Entry",
//					"There are no captured console messages yet.", "OK");
//				return;
//			}

//			string prompt = EditorPrefs.GetString(
//				"ChatGPT_ConsolePrompt",
//				"please help me debug this Unity error"
//			);

//			string combined = prompt +
//							  $"\n\nLog Type: {lastConsoleType}\n" +
//							  $"Message: {lastConsoleMessage}\n\n" +
//							  $"Stack Trace:\n{lastConsoleStackTrace}";

//			GUIUtility.systemCopyBuffer = combined;

//			Debug.Log("✅ Console message + prompt copied to clipboard. Opening ChatGPT...");

//			OpenURL("https://chat.openai.com/");

//			EditorUtility.DisplayDialog(
//				"ChatGPT Ready",
//				"The last console message has been copied to the clipboard.\n\n" +
//				"👉 Switch to ChatGPT and press Ctrl+V (Cmd+V on Mac) to paste it.",
//				"OK"
//			);
//		}

//		// ===============================
//		// PROMPT CONFIG WINDOW
//		// ===============================
//		[MenuItem("Tools/ChatGPT/Set Prompts")]
//		private static void ShowPromptWindow()
//		{
//			GetWindow<ChatGPTIntegration>("ChatGPT Prompts").Show();
//		}

//		private void OnEnable()
//		{
//			scriptPrompt = EditorPrefs.GetString(
//				"ChatGPT_ScriptPrompt",
//				"please refactor this script for efficiency"
//			);

//			consolePrompt = EditorPrefs.GetString(
//				"ChatGPT_ConsolePrompt",
//				"please help me debug this Unity error"
//			);
//		}

//		private void OnGUI()
//		{
//			GUILayout.Label("Default Prompts for ChatGPT", EditorStyles.boldLabel);

//			GUILayout.Space(5);
//			GUILayout.Label("Script Prompt", EditorStyles.miniBoldLabel);
//			scriptPrompt = EditorGUILayout.TextField("Prompt:", scriptPrompt);

//			GUILayout.Space(10);
//			GUILayout.Label("Console Prompt", EditorStyles.miniBoldLabel);
//			consolePrompt = EditorGUILayout.TextField("Prompt:", consolePrompt);

//			GUILayout.Space(15);
//			if (GUILayout.Button("Save Prompts"))
//			{
//				EditorPrefs.SetString("ChatGPT_ScriptPrompt", scriptPrompt);
//				EditorPrefs.SetString("ChatGPT_ConsolePrompt", consolePrompt);
//				Debug.Log("💾 Saved ChatGPT prompts.");
//				Close();
//			}

//			if (GUILayout.Button("Reset to Defaults"))
//			{
//				EditorPrefs.DeleteKey("ChatGPT_ScriptPrompt");
//				EditorPrefs.DeleteKey("ChatGPT_ConsolePrompt");
//				scriptPrompt = "please refactor this script for efficiency";
//				consolePrompt = "please help me debug this Unity error";
//				Debug.Log("🔄 Reset ChatGPT prompts.");
//				Close();
//			}
//		}

//		// ===============================
//		// Helper
//		// ===============================
//		private static void OpenURL(string url)
//		{
//#if UNITY_EDITOR_OSX
//            Process.Start("open", url);
//#elif UNITY_EDITOR_WIN
//			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
//#else
//            Application.OpenURL(url);
//#endif
//		}
//	}
//}
