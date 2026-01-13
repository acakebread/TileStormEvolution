using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public sealed class CommandRenderCamera : MonoBehaviour
	{
		private Camera cam;
		private CommandCameraHook hook;

		public Camera Camera => cam;

		private void Awake()
		{
			cam = GetComponent<Camera>();
			if (cam == null)
			{
				Debug.LogError("CommandRenderCamera requires a Camera component.", this);
				enabled = false;
				return;
			}

			cam.enabled = false;
			cam.cullingMask = 0;

			if (!cam.TryGetComponent<UniversalAdditionalCameraData>(out _))
				gameObject.AddComponent<UniversalAdditionalCameraData>();

			hook = gameObject.GetComponent<CommandCameraHook>();
			if (hook == null)
				hook = gameObject.AddComponent<CommandCameraHook>();
		}

		public static CommandRenderCamera Create(
			Transform parent = null,
			RenderTexture target = null,
			Color background = default,
			float fov = 60f,
			string name = "CommandRenderCamera")
		{
			var go = new GameObject(name);
			if (parent != null)
				go.transform.SetParent(parent, false);

			var camera = go.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = background;
			camera.fieldOfView = fov;
			camera.nearClipPlane = 0.03f;
			camera.farClipPlane = 50f;
			camera.targetTexture = target;
			camera.aspect = target != null ? (float)target.width / target.height : 16f / 9f;

			go.AddComponent<UniversalAdditionalCameraData>();

			return go.AddComponent<CommandRenderCamera>();
		}

		// Public setters
		public void SetTargetTexture(RenderTexture rt)
		{
			cam.targetTexture = rt;
			if (rt != null)
				cam.aspect = (float)rt.width / rt.height;
		}

		public void SetBackgroundColor(Color color) => cam.backgroundColor = color;
		public void SetFieldOfView(float fov) => cam.fieldOfView = fov;
		public void SetAspect(float aspect) => cam.aspect = aspect;

		public void Render() => cam?.Render();

		// Hook connection (called by scene)
		internal void SetCommandProvider(ICommandBufferProvider provider)
		{
			if (hook != null)
				hook.Provider = provider;
		}
	}

	internal class CommandCameraHook : MonoBehaviour, ICommandBufferProvider
	{
		public ICommandBufferProvider Provider { get; set; }

		public bool HasCommands(RenderPassEvent evt) => Provider?.HasCommands(evt) ?? false;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
			=> Provider?.ExecuteCommands(evt, cmd, camera);
	}
}