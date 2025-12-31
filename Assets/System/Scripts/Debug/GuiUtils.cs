using UnityEngine;

namespace MassiveHadronLtd
{
	public static partial class GuiUtils
	{
		public static bool IsMouseInsideWindow()
		{
			var pos = Input.mousePosition;
			return pos.x >= 0 && pos.y >= 0 && pos.x < Screen.width && pos.y < Screen.height;
		}

		// Was any GuiUtils GUI active during the PREVIOUS OnGUI frame?
		private static bool _wasGuiActivePreviousFrame = false;
		public static bool WasGuiActiveLastFrame => _wasGuiActivePreviousFrame;

		// Is GuiUtils GUI active during the CURRENT OnGUI frame?
		private static bool _isGuiActiveThisFrame = false;

		/// <summary>
		/// Call ONCE at the very start of your top-level OnGUI method
		/// </summary>
		public static void BeginOnGUIFrame()
		{
			_wasGuiActivePreviousFrame = _isGuiActiveThisFrame;
			_isGuiActiveThisFrame = false;
		}

		/// <summary>
		/// Call whenever a GuiUtils element is hovered, clicked, or otherwise consumes input
		/// </summary>
		public static void MarkGuiActive()
		{
			_isGuiActiveThisFrame = true;
			// Optional: also track native hotControl changes
			if (GUIUtility.hotControl != 0)
				_isGuiActiveThisFrame = true;
		}

		private static Texture2D MakeTex(int w, int h, Color col)
		{
			Color[] pix = new Color[w * h];
			for (int i = 0; i < pix.Length; i++) pix[i] = col;
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			tex.SetPixels(pix);
			tex.Apply();
			return tex;
		}

		private class FrameManager : MonoBehaviour
		{
			private void Awake()
			{
				hideFlags = HideFlags.HideAndDontSave;
				DontDestroyOnLoad(gameObject);
				gameObject.name = "_GuiUtils_FrameManager";
			}

			// Runs AFTER all regular Update() calls
			private void LateUpdate()
			{
				_wasGuiActivePreviousFrame = _isGuiActiveThisFrame;
				_isGuiActiveThisFrame = false;
			}
		}

		// Static constructor: creates the manager automatically on first access
		static GuiUtils()
		{
			// Create hidden GameObject that lives forever and runs Update()
			var go = new GameObject("_GuiUtils_Internal");
			go.hideFlags = HideFlags.HideAndDontSave;
			Object.DontDestroyOnLoad(go);
			go.AddComponent<FrameManager>();
		}
	}
}
