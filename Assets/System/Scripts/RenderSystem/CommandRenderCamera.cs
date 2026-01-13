using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Manages the Unity Camera used for command-buffer based rendering.
	/// Pure camera wrapper — no knowledge of models, lights or command buffers.
	/// </summary>
	public sealed class CommandRenderCamera : MonoBehaviour
	{
		private Camera cam;

		public Camera Camera => cam;

		private void Awake()
		{
			cam = GetComponent<Camera>();
			if (cam == null)
			{
				Debug.LogError($"{nameof(CommandRenderCamera)} requires a Camera component on the same GameObject.", this);
				enabled = false;
				return;
			}

			cam.enabled = false;
			cam.cullingMask = 0; // We render everything manually via command buffers

			// Ensure URP additional data exists
			if (!cam.TryGetComponent<UniversalAdditionalCameraData>(out _))
			{
				gameObject.AddComponent<UniversalAdditionalCameraData>();
			}
		}

		/// <summary>
		/// Factory method to create a new render camera setup
		/// </summary>
		public static CommandRenderCamera Create(
			Transform parent,
			RenderTexture targetTexture,
			Color backgroundColor,
			float fov = 60f,
			string name = "CommandRenderCamera")
		{
			var go = new GameObject(name);
			if (parent != null)
				go.transform.SetParent(parent, false);

			var cameraComp = go.AddComponent<Camera>();
			cameraComp.clearFlags = CameraClearFlags.SolidColor;
			cameraComp.backgroundColor = backgroundColor;
			cameraComp.fieldOfView = fov;
			cameraComp.nearClipPlane = 0.03f;
			cameraComp.farClipPlane = 50f;
			cameraComp.targetTexture = targetTexture;

			go.AddComponent<UniversalAdditionalCameraData>();

			return go.AddComponent<CommandRenderCamera>();
		}

		public void SetTargetTexture(RenderTexture rt) => Camera.targetTexture = rt;
		public void SetFieldOfView(float fov) => Camera.fieldOfView = fov;
		public void SetBackgroundColor(Color color) => Camera.backgroundColor = color;
		public void SetAspect(float aspect) => Camera.aspect = aspect;
		public void Render() => Camera?.Render();
	}

	// ────────────────────────────────────────────────────────────────
	// Hook — forwards everything to the actual provider (CommandRenderScene)
	// ────────────────────────────────────────────────────────────────
	internal class CommandCameraHook : MonoBehaviour, ICommandBufferProvider
	{
		public ICommandBufferProvider Provider { get; set; }

		public bool HasCommands(RenderPassEvent evt)
		{
			return Provider?.HasCommands(evt) ?? false;
		}

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			Provider?.ExecuteCommands(evt, cmd, camera);
		}
	}
}