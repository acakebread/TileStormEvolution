using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MassiveHadronLtd.EditorTools
{
	public static class QuickPlayMaximizeToolbar
	{
		private static readonly Type GameViewType = Type.GetType("UnityEditor.GameView, UnityEditor");

		[MenuItem("Window/MassiveHadron/Toggle Max Play %#m", false, 2000)]
		private static void ToggleMaxPlay()
		{
			var gameView = FindGameView();
			if (gameView == null)
			{
				Debug.LogWarning("QuickPlayMaximize: Could not find the Game view.");
				return;
			}

			var targetState = !GetMaximizeOnPlay(gameView);
			SetMaximizeOnPlay(gameView, targetState);
			gameView.maximized = targetState;
			gameView.Focus();
		}

		[MenuItem("Window/MassiveHadron/Toggle Max Play %#m", true)]
		private static bool ToggleMaxPlayValidate() => true;

		private static EditorWindow FindGameView()
		{
			if (GameViewType == null)
				return null;

			var existing = Resources.FindObjectsOfTypeAll(GameViewType)
				.OfType<EditorWindow>()
				.Where(window => window != null && window.GetType() == GameViewType)
				.FirstOrDefault();

			if (existing != null)
				return existing;

			var opened = EditorWindow.GetWindow(GameViewType, false, "Game", true);
			return opened != null && opened.GetType() == GameViewType ? opened : null;
		}

		private static bool GetMaximizeOnPlay(EditorWindow gameView)
		{
			if (gameView == null)
				return false;

			var type = gameView.GetType();
			var property = type.GetProperty("maximizeOnPlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (property != null && property.PropertyType == typeof(bool))
				return (bool)property.GetValue(gameView);

			var field = type.GetField("maximizeOnPlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
					 ?? type.GetField("m_MaximizeOnPlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (field != null && field.FieldType == typeof(bool))
				return (bool)field.GetValue(gameView);

			return false;
		}

		private static void SetMaximizeOnPlay(EditorWindow gameView, bool value)
		{
			if (gameView == null)
				return;

			var type = gameView.GetType();
			var property = type.GetProperty("maximizeOnPlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
			{
				property.SetValue(gameView, value);
				EditorUtility.SetDirty(gameView);
				return;
			}

			var field = type.GetField("maximizeOnPlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
					 ?? type.GetField("m_MaximizeOnPlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (field != null && field.FieldType == typeof(bool))
			{
				field.SetValue(gameView, value);
				EditorUtility.SetDirty(gameView);
			}
		}
	}
}
