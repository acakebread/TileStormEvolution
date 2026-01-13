using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public sealed class CommandRenderScene : MonoBehaviour, ICommandBufferProvider
	{
		private static readonly int MainLightPositionID = Shader.PropertyToID("_MainLightPosition");
		private static readonly int MainLightColorID = Shader.PropertyToID("_MainLightColor");

		[SerializeField] private CommandRenderCamera renderCamera;

		private CommandRenderModelData[] models;

		[SerializeField] private Vector3 lightDirection = new Vector3(0.5f, 1f, -0.3f).normalized;
		[SerializeField] private Color lightColor = new Color(0.75f, 0.75f, 0.75f);

		public CommandRenderCamera Camera => renderCamera;

		private void Awake()
		{
			if (renderCamera == null)
			{
				renderCamera = CommandRenderCamera.Create(transform, null, Color.black, 60f);
				ConnectHook();
			}
		}

		public void Initialize(
			RenderTexture targetRT,
			Color background,
			float fov = 60f,
			string cameraName = "PreviewCamera")
		{
			if (renderCamera == null)
			{
				renderCamera = CommandRenderCamera.Create(transform, targetRT, background, fov, cameraName);
				ConnectHook();
			}
			else
			{
				renderCamera.SetTargetTexture(targetRT);
				renderCamera.SetBackgroundColor(background);
				renderCamera.SetFieldOfView(fov);
			}
		}

		private void ConnectHook()
		{
			var hook = renderCamera.GetComponent<CommandCameraHook>();
			if (hook == null)
			{
				hook = renderCamera.gameObject.AddComponent<CommandCameraHook>();
			}
			hook.Provider = this;
		}

		public void SetModels(CommandRenderModelData[] data)
		{
			models = data;
		}

		public void SetLight(Vector3 direction, Color color)
		{
			lightDirection = direction.normalized;
			lightColor = color;
		}

		public void Render()
		{
			renderCamera?.Render();
		}

		// ─────────────── ICommandBufferProvider ───────────────

		public bool HasCommands(RenderPassEvent evt)
		{
			return evt == RenderPassEvent.BeforeRenderingOpaques;
		}

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			if (evt != RenderPassEvent.BeforeRenderingOpaques || models == null)
				return;

			// Fake main light
			Vector4 mainLightPos = new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 1f);
			cmd.SetGlobalVector(MainLightPositionID, mainLightPos);
			cmd.SetGlobalVector(MainLightColorID, lightColor.linear);

			// Draw models
			foreach (var model in models)
			{
				if (model == null) continue;

				foreach (var inst in model.meshInstances)
				{
					if (inst.mesh == null) continue;

					for (int s = 0; s < inst.subMeshCount; s++)
					{
						var mat = s < inst.materials.Length ? inst.materials[s] : inst.materials[0];
						if (mat == null) continue;

						cmd.DrawMesh(inst.mesh, inst.localToWorld, mat, s, 0);
					}
				}
			}
		}
	}
}