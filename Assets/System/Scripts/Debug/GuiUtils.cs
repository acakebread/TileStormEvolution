using UnityEngine;
using InputSystem = UnityEngine.InputSystem;   // Add this line

namespace MassiveHadronLtd
{
	public static partial class GuiUtils
	{
		public static bool IsMouseInsideWindow()
		{
			// Fixed: Use New Input System instead of legacy Input.mousePosition
			Vector3 pos = InputSystem.Mouse.current != null
				? (Vector3)InputSystem.Mouse.current.position.ReadValue()
				: Vector3.zero;

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

		/// <summary>
		/// Core helper: flips the Y origin of a Rect between bottom-up (screen) and top-down (GUI).
		/// </summary>
		private static Rect FlipRectY(Rect rect, float screenHeight, bool toGUI)
		{
			float newY = screenHeight - rect.yMax;
			return new Rect(
				rect.x,
				newY,
				rect.width,
				rect.height
			);
		}

		/// <summary>
		/// Converts screen-space Rect (bottom-left origin) to GUI-space Rect (top-left origin).
		/// </summary>
		public static Rect ToGUIRect(this Rect screenRect) => FlipRectY(screenRect, Screen.height, toGUI: true);

		/// <summary>
		/// Converts GUI-space Rect (top-left origin) to screen-space Rect (bottom-left origin).
		/// </summary>
		public static Rect ToScreenRect(this Rect guiRect) => FlipRectY(guiRect, Screen.height, toGUI: false);

		// Overloads with explicit height
		public static Rect ToGUIRect(this Rect screenRect, float screenHeight) => FlipRectY(screenRect, screenHeight, toGUI: true);
		public static Rect ToScreenRect(this Rect guiRect, float screenHeight) => FlipRectY(guiRect, screenHeight, toGUI: false);

		/// <summary>
		/// Returns the normalized UV coordinate (0..1) of a screen point relative to the Rect.
		/// </summary>
		public static Vector2 NormalisedPoint(this Rect rect, Vector2 screenPoint)
		{
			if (rect.width <= 0f || rect.height <= 0f)
				return new Vector2(0.5f, 0.5f);

			float uvX = (screenPoint.x - rect.xMin) / rect.width;
			float uvY = (screenPoint.y - rect.yMin) / rect.height;
			return new Vector2(uvX, uvY);
		}

		/// <summary>
		/// Clamped version
		/// </summary>
		public static Vector2 NormalisedPointClamped(this Rect rect, Vector2 screenPoint)
		{
			if (rect.width <= 0f || rect.height <= 0f)
				return new Vector2(0.5f, 0.5f);

			float uvX = (screenPoint.x - rect.xMin) / rect.width;
			float uvY = (screenPoint.y - rect.yMin) / rect.height;
			return new Vector2(
				Mathf.Clamp01(uvX),
				Mathf.Clamp01(uvY)
			);
		}

		public static Vector2 NormalisedPoint(this Rect rect, Vector3 mousePoint)
			=> rect.NormalisedPoint(new Vector2(mousePoint.x, mousePoint.y));

		// Static constructor: creates the manager automatically
		static GuiUtils()
		{
			var go = new GameObject("_GuiUtils_Internal");
			go.hideFlags = HideFlags.HideAndDontSave;
			Object.DontDestroyOnLoad(go);
			go.AddComponent<FrameManager>();
		}
	}
}