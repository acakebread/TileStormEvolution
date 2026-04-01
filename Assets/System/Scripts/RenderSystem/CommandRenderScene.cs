using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public class CommandRenderScene : ICommandBufferProvider
	{
		private CommandRenderModelData[] _models;

		// These are the only things you should touch from outside
		public Vector3 MainLightDirection { get; set; } = Vector3.forward;
		public Color MainLightColor { get; set; } = Color.white;
		public float MainLightIntensity { get; set; } = 1.0f;

		public Color AmbientColor { get; set; } = new Color(0.25f, 0.25f, 0.3f);
		public float AmbientIntensity { get; set; } = 0.8f;

		public void SetModels(CommandRenderModelData[] data) => _models = data;

		public bool HasCommands(RenderPassEvent evt) => evt == RenderPassEvent.BeforeRenderingOpaques;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			if (_models == null) return;

			// Force the direction you set (no extra rotation here)
			Vector3 dir = MainLightDirection.normalized;
			cmd.SetGlobalVector("_MainLightPosition", new Vector4(dir.x, dir.y, dir.z, 0f));
			cmd.SetGlobalColor("_MainLightColor", MainLightColor * MainLightIntensity);

			cmd.SetGlobalFloat("_MainLightShadowStrength", 0f);
			cmd.SetGlobalFloat("_AdditionalLightsCount", 0f);

			cmd.SetGlobalColor("_AmbientLight", AmbientColor * AmbientIntensity);

			// Very weak SH so it doesn't fight the main light
			float sh = AmbientIntensity * 0.5f;
			cmd.SetGlobalVector("unity_SHAr", new Vector4(0.2f, 0.2f, 0.2f, 0f) * sh);
			cmd.SetGlobalVector("unity_SHAg", new Vector4(0.2f, 0.2f, 0.2f, 0f) * sh);
			cmd.SetGlobalVector("unity_SHAb", new Vector4(0.2f, 0.2f, 0.2f, 0f) * sh);
			cmd.SetGlobalVector("unity_SHC", new Vector4(0.15f, 0.15f, 0.15f, 1f) * sh);

			// DRAW
			foreach (var model in _models)
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

		public void Destroy() { }
	}
}

//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//namespace MassiveHadronLtd
//{
//	public class CommandRenderScene : ICommandBufferProvider
//	{
//		private CommandRenderModelData[] _models;

//		public Vector3 MainLightDirection { get; set; } = new Vector3(0.5f, -0.8f, -0.4f);
//		public Color MainLightColour{ get; set; } = Color.white;
//		public float MainLightIntensity { get; set; } = 1f;

//		public Color AmbientColour { get; set; } = Color.white;
//		public float AmbientIntensity { get; set; } = 1f;

//		public void SetModels(CommandRenderModelData[] data) => _models = data;

//		public bool HasCommands(RenderPassEvent evt) => evt == RenderPassEvent.BeforeRenderingOpaques;

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
//		{
//			if (_models == null) return;

//			//Vector3 lightDirWS = (camera != null && camera.transform != null)
//			//	? (camera.transform.rotation * MainLightDirection).normalized
//			//	: MainLightDirection.normalized;

//			Vector3 lightDirWS = MainLightDirection.normalized;

//			// === MAIN LIGHT (only contribute when intensity > 0) ===
//			Color mainLightColor = MainLightColour * MainLightIntensity;   // your test color

//			cmd.SetGlobalVector("_MainLightPosition", new Vector4(lightDirWS.x, lightDirWS.y, lightDirWS.z, 0f));
//			cmd.SetGlobalColor("_MainLightColor", mainLightColor);

//			cmd.SetGlobalFloat("_MainLightShadowStrength", 0f);
//			cmd.SetGlobalFloat("_AdditionalLightsCount", 0f);

//			// === AMBIENT ONLY (force it stronger and more independent) ===
//			Color ambientColor = AmbientColour * AmbientIntensity;        // your test color

//			cmd.SetGlobalColor("_AmbientLight", ambientColor);

//			// Stronger SH probe contribution (this is what many URP shaders actually use for ambient)
//			float sh = AmbientIntensity * 1.2f;
//			cmd.SetGlobalVector("unity_SHAr", new Vector4(0.6f, 0.6f, 0.6f, 0f) * sh);
//			cmd.SetGlobalVector("unity_SHAg", new Vector4(0.6f, 0.6f, 0.6f, 0f) * sh);
//			cmd.SetGlobalVector("unity_SHAb", new Vector4(0.6f, 0.6f, 0.6f, 0f) * sh);
//			cmd.SetGlobalVector("unity_SHBr", new Vector4(0.3f, 0.3f, 0.3f, 0f) * sh);
//			cmd.SetGlobalVector("unity_SHBg", new Vector4(0.3f, 0.3f, 0.3f, 0f) * sh);
//			cmd.SetGlobalVector("unity_SHBb", new Vector4(0.3f, 0.3f, 0.3f, 0f) * sh);
//			cmd.SetGlobalVector("unity_SHC", new Vector4(0.4f, 0.4f, 0.4f, 1f) * sh);

//			// Extra trick: some shaders sample this too
//			cmd.SetGlobalColor("_GlossyEnvironmentColor", ambientColor * 0.8f);

//			// === DRAW MODELS ===
//			foreach (var model in _models)
//			{
//				if (model == null) continue;

//				foreach (var inst in model.meshInstances)
//				{
//					if (inst.mesh == null) continue;

//					for (int s = 0; s < inst.subMeshCount; s++)
//					{
//						var mat = s < inst.materials.Length ? inst.materials[s] : inst.materials[0];
//						if (mat == null) continue;

//						cmd.DrawMesh(inst.mesh, inst.localToWorld, mat, s, 0);
//					}
//				}
//			}
//		}

//		public void Destroy() { }
//	}
//}

//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//namespace MassiveHadronLtd
//{
//	public class CommandRenderScene : ICommandBufferProvider
//	{
//		private CommandRenderModelData[] _models;

//		public Vector3 MainLightDirection { get; set; } = new Vector3(0.5f, -0.8f, -0.4f); // change these numbers to rotate the bright side
//		public float MainLightIntensity { get; set; } = 0f;   // ← increased default
//		public float AmbientIntensity { get; set; } = 10f;     // ← stronger ambient fallback

//		public void SetModels(CommandRenderModelData[] data) => _models = data;

//		public bool HasCommands(RenderPassEvent evt) => evt == RenderPassEvent.BeforeRenderingOpaques;

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
//		{
//			if (_models == null) return;

//			// === MAIN LIGHT (this is what gives directional shading) ===
//			Vector3 lightDirWS = MainLightDirection.normalized;

//			if (camera != null && camera.transform != null)
//			{
//				// Light rotates with the camera view → consistent "key light" for every icon
//				lightDirWS = (camera.transform.rotation * MainLightDirection).normalized;
//			}

//			//Color mainLightColor = new Color(1.2f, 1.15f, 1.05f) * MainLightIntensity;
//			Color mainLightColor = Color.green * MainLightIntensity;

//			cmd.SetGlobalVector("_MainLightPosition", new Vector4(lightDirWS.x, lightDirWS.y, lightDirWS.z, 0f));
//			cmd.SetGlobalColor("_MainLightColor", mainLightColor);

//			cmd.SetGlobalFloat("_MainLightShadowStrength", 0f);
//			cmd.SetGlobalFloat("_AdditionalLightsCount", 0f);

//			// === STRONGER AMBIENT + SH PROBES (critical when main light is low) ===
//			//Color ambientBase = new Color(0.65f, 0.65f, 0.72f); // neutral cool gray – change to whatever you like
//			Color ambientBase = Color.red;
//			Color ambient = ambientBase * AmbientIntensity;

//			cmd.SetGlobalColor("_AmbientLight", ambient);

//			// Spherical Harmonics – this is what many URP shaders actually sample for ambient
//			float shScale = AmbientIntensity * 0.8f;
//			cmd.SetGlobalVector("unity_SHAr", new Vector4(0.4f, 0.4f, 0.4f, 0f) * shScale);
//			cmd.SetGlobalVector("unity_SHAg", new Vector4(0.4f, 0.4f, 0.4f, 0f) * shScale);
//			cmd.SetGlobalVector("unity_SHAb", new Vector4(0.4f, 0.4f, 0.4f, 0f) * shScale);
//			cmd.SetGlobalVector("unity_SHBr", new Vector4(0.2f, 0.2f, 0.2f, 0f) * shScale);
//			cmd.SetGlobalVector("unity_SHBg", new Vector4(0.2f, 0.2f, 0.2f, 0f) * shScale);
//			cmd.SetGlobalVector("unity_SHBb", new Vector4(0.2f, 0.2f, 0.2f, 0f) * shScale);
//			cmd.SetGlobalVector("unity_SHC", new Vector4(0.3f, 0.3f, 0.3f, 1f) * shScale);

//			// === DRAW ===
//			foreach (var model in _models)
//			{
//				if (model == null) continue;

//				foreach (var inst in model.meshInstances)
//				{
//					if (inst.mesh == null) continue;

//					for (int s = 0; s < inst.subMeshCount; s++)
//					{
//						var mat = s < inst.materials.Length ? inst.materials[s] : inst.materials[0];
//						if (mat == null) continue;

//						cmd.DrawMesh(inst.mesh, inst.localToWorld, mat, s, 0); // UniversalForward pass
//					}
//				}
//			}
//		}

//		public void Destroy() { }
//	}
//}

//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//namespace MassiveHadronLtd
//{
//	public class CommandRenderScene : ICommandBufferProvider
//	{
//		private CommandRenderModelData[] _models;

//		// Public property so ReusableIconRenderer can control the light easily
//		public Vector3 MainLightDirection { get; set; } = new Vector3(0.5f, -0.8f, -0.4f); // nice default 3/4 key light

//		// New: control main light intensity separately (0 = off)
//		public float MainLightIntensity { get; set; } = 0f;//1.8f;

//		// New: ambient intensity multiplier
//		public float AmbientIntensity { get; set; } = 1.0f;

//		public void SetModels(CommandRenderModelData[] data) => _models = data;

//		public bool HasCommands(RenderPassEvent evt) => evt == RenderPassEvent.BeforeRenderingOpaques;

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
//		{
//			if (_models == null) return;

//			// === MAIN LIGHT SETUP ===
//			Vector3 lightDirWS = MainLightDirection.normalized;

//			// Make light direction relative to camera (recommended for consistent atlas icons)
//			if (camera != null && camera.transform != null)
//			{
//				lightDirWS = (camera.transform.rotation * MainLightDirection).normalized;
//			}

//			Color mainLightColor = new Color(1.15f, 1.08f, 1.0f, 1f) * MainLightIntensity;

//			cmd.SetGlobalVector("_MainLightPosition", new Vector4(lightDirWS.x, lightDirWS.y, lightDirWS.z, 0f));
//			cmd.SetGlobalColor("_MainLightColor", mainLightColor);

//			cmd.SetGlobalFloat("_MainLightShadowStrength", 0f);
//			cmd.SetGlobalFloat("_AdditionalLightsCount", 0f);

//			// === AMBIENT / INDIRECT LIGHTING (more reliable) ===
//			// This helps a lot when main light is disabled
//			//Color ambient = new Color(0.55f, 0.55f, 0.60f, 1f) * AmbientIntensity;   // slightly cool gray
//			Color ambient = Color.green * AmbientIntensity;
//			cmd.SetGlobalColor("_AmbientLight", ambient);

//			// Extra SH ambient terms that many URP shaders sample
//			cmd.SetGlobalVector("unity_SHAr", new Vector4(0.3f, 0.3f, 0.3f, 0f) * AmbientIntensity);
//			cmd.SetGlobalVector("unity_SHAg", new Vector4(0.3f, 0.3f, 0.3f, 0f) * AmbientIntensity);
//			cmd.SetGlobalVector("unity_SHAb", new Vector4(0.3f, 0.3f, 0.3f, 0f) * AmbientIntensity);
//			cmd.SetGlobalVector("unity_SHBr", new Vector4(0.1f, 0.1f, 0.1f, 0f) * AmbientIntensity);
//			cmd.SetGlobalVector("unity_SHBg", new Vector4(0.1f, 0.1f, 0.1f, 0f) * AmbientIntensity);
//			cmd.SetGlobalVector("unity_SHBb", new Vector4(0.1f, 0.1f, 0.1f, 0f) * AmbientIntensity);
//			cmd.SetGlobalVector("unity_SHC", new Vector4(0.2f, 0.2f, 0.2f, 1f) * AmbientIntensity);

//			// === DRAW THE MODELS ===
//			foreach (var model in _models)
//			{
//				if (model == null) continue;

//				foreach (var inst in model.meshInstances)
//				{
//					if (inst.mesh == null) continue;

//					for (int s = 0; s < inst.subMeshCount; s++)
//					{
//						var mat = s < inst.materials.Length ? inst.materials[s] : inst.materials[0];
//						if (mat == null) continue;

//						cmd.DrawMesh(inst.mesh, inst.localToWorld, mat, s, 0); // pass 0 = UniversalForward
//					}
//				}
//			}
//		}

//		public void Destroy() { }
//	}
//}


//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//namespace MassiveHadronLtd
//{
//	public class CommandRenderScene : ICommandBufferProvider
//	{
//		private CommandRenderModelData[] _models;

//		// Optional: You can set a custom light direction from ReusableIconRenderer if you want
//		public Vector3 MainLightDirection { get; set; } = new Vector3(0.4f, -0.8f, -0.3f);

//		public void SetModels(CommandRenderModelData[] data) => _models = data;

//		public bool HasCommands(RenderPassEvent evt) => evt == RenderPassEvent.BeforeRenderingOpaques;

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
//		{
//			if (_models == null) return;

//			// === MAIN LIGHT SETUP (this makes Simple Lit / Lit actually lit) ===
//			Vector3 lightDir = MainLightDirection.normalized;

//			// If you have a real Light component on the camera, you can override with it
//			if (camera != null)
//			{
//				// Optional: make light always come from a nice 3/4 angle relative to camera
//				lightDir = camera.transform.rotation * new Vector3(0.4f, -0.8f, -0.3f);
//				lightDir.Normalize();
//			}

//			cmd.SetGlobalVector("_MainLightPosition", new Vector4(lightDir.x, lightDir.y, lightDir.z, 0f));
//			cmd.SetGlobalColor("_MainLightColor", new Color(1.2f, 1.1f, 1.0f, 1f));   // tune intensity here

//			// Fix for the error you were getting
//			cmd.SetGlobalFloat("_AdditionalLightsCount", 0f);

//			// Optional ambient boost (helps prevent completely black areas)
//			cmd.SetGlobalColor("_AmbientLight", new Color(0.35f, 0.35f, 0.4f));

//			// === DRAW THE MODELS ===
//			foreach (var model in _models)
//			{
//				if (model == null) continue;

//				foreach (var inst in model.meshInstances)
//				{
//					if (inst.mesh == null) continue;

//					for (int s = 0; s < inst.subMeshCount; s++)
//					{
//						var mat = s < inst.materials.Length ? inst.materials[s] : inst.materials[0];
//						if (mat == null) continue;

//						// IMPORTANT: shader pass 0 = UniversalForward (enables lighting)
//						cmd.DrawMesh(inst.mesh, inst.localToWorld, mat, s, 0);
//					}
//				}
//			}
//		}

//		public void Destroy() { }
//	}
//}

//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//namespace MassiveHadronLtd
//{
//	public class CommandRenderScene : ICommandBufferProvider
//	{
//		private CommandRenderModelData[] models;

//		public void SetModels(CommandRenderModelData[] data) => models = data;

//		// Keep SetLight for future use (icons can call it if we ever want to switch back)
//		//public void SetLight(Vector3 direction, Color color) { }

//		public bool HasCommands(RenderPassEvent evt) => evt == RenderPassEvent.BeforeRenderingOpaques;

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
//		{
//			foreach (var model in models)
//			{
//				if (model == null) continue;

//				foreach (var inst in model.meshInstances)
//				{
//					if (inst.mesh == null) continue;

//					for (int s = 0; s < inst.subMeshCount; s++)
//					{
//						var mat = s < inst.materials.Length ? inst.materials[s] : inst.materials[0];
//						if (mat == null) continue;

//						cmd.DrawMesh(inst.mesh, inst.localToWorld, mat, s, 0);
//					}
//				}
//			}
//		}

//		public void Destroy() { }
//	}
//}