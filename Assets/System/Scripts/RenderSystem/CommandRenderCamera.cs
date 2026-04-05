using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public class CommandRenderCamera
	{
		private Camera Camera { get; set; }
		private GameObject cameraGameObject;
		private CommandCameraHook hook;

		public Transform transform => Camera.transform;
		public Vector3 position { get => Camera.transform.position; set => Camera.transform.position = value; }
		public Quaternion rotation { get => Camera.transform.rotation; set => Camera.transform.rotation = value; }
		public float fieldOfView { get => Camera.fieldOfView; set => Camera.fieldOfView = value; }
		public Color backgroundColor { get => Camera.backgroundColor; set => Camera.backgroundColor = value; }
		public float aspect { get => Camera.aspect; set => Camera.aspect = value; }
		public RenderTexture targetTexture { get => Camera.targetTexture; set => Camera.targetTexture = value; }

		public CommandRenderCamera(
			string name,
			RenderTexture targetRT,
			Color background,
			float fov = 60f,
			Transform desiredParent = null)
		{
			cameraGameObject = new GameObject(name ?? "CommandRenderCamera") { hideFlags = HideFlags.HideAndDontSave };
			cameraGameObject.GetOrAddComponent<CameraShaderPrimer>();//workaround for shader problem in command buffer - required for rendering atlasses!!

			Camera = cameraGameObject.AddComponent<Camera>();
			Camera.enabled = false;
			Camera.cullingMask = 0;
			Camera.clearFlags = CameraClearFlags.SolidColor;
			Camera.backgroundColor = background;
			Camera.fieldOfView = fov;
			Camera.nearClipPlane = 0.03f;
			Camera.farClipPlane = 50f;
			Camera.targetTexture = targetRT;
			Camera.allowMSAA = false;
			Camera.allowHDR = false;

			if (targetRT != null)
				Camera.aspect = (float)targetRT.width / targetRT.height;

			// === NEW: Force minimal URP camera data ===
			var camData = Camera.GetUniversalAdditionalCameraData();
			if (camData != null)
			{
				camData.requiresColorOption = CameraOverrideOption.Off;
				camData.requiresDepthOption = CameraOverrideOption.Off;
				camData.renderShadows = false;
				camData.requiresColorTexture = false;
				camData.requiresDepthTexture = false;
				camData.antialiasing = AntialiasingMode.None;
				camData.stopNaN = false;
				camData.dithering = false;
			}

			hook = cameraGameObject.AddComponent<CommandCameraHook>();

			if (desiredParent != null)
				cameraGameObject.transform.SetParent(desiredParent, false);
		}

		public void Render()
		{
			var camData = Camera.GetUniversalAdditionalCameraData();
			if (camData != null)
			{
				camData.renderShadows = false;
				camData.requiresColorTexture = false;
				camData.requiresDepthTexture = false;
			}

			Camera?.Render();
		}

		public void AssignCommandProvider(ICommandBufferProvider provider)
		{
			if (hook != null)
				hook.Provider = provider;
		}

		public void Destroy()
		{
			if (cameraGameObject != null)
			{
				Object.DestroyImmediate(cameraGameObject);
				cameraGameObject = null;
				Camera = null;
				hook = null;
			}
		}

		public void SetUnityCameraParent(Transform newParent)
		{
			if (cameraGameObject != null)
				cameraGameObject.transform.SetParent(newParent, false);
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
