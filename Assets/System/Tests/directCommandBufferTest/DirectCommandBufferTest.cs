using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	public class DirectCommandBufferTest : MonoBehaviour, IDirectCommandProvider
	{
		[Header("1. Required Material")]
		public Material testMaterial;

		[Header("2. Output UI")]
		public RawImage outputRawImage;

		// Runtime
		private GameObject dummyGO;
		private Camera dummyCam;
		private RenderTexture rt;
		private Mesh quadMesh;
		private TestCommandCamera _testCommandCamera;

		public TestCommandCamera testCommandCamera => _testCommandCamera;

		private void Awake()
		{
			if (!testMaterial)
			{
				Debug.LogError("testMaterial is NULL!", this);
				return;
			}
			CreateQuadMesh();
		}

		private void Start()
		{
			CreateDummyCameraAndRT();
		}

		private void Update()
		{
			if (dummyCam != null)
			{
				dummyCam.Render();
				UpdateUI();
				SyncTestCommandCameraWithDummy(); // keep in sync every frame
			}
		}

		private void CreateDummyCameraAndRT()
		{
			if (dummyGO != null) return;

			dummyGO = new GameObject("DummyCamera [RenderTest]");
			dummyCam = dummyGO.AddComponent<Camera>();

			dummyCam.enabled = false;
			dummyCam.cullingMask = 0;
			dummyCam.clearFlags = CameraClearFlags.SolidColor;
			dummyCam.backgroundColor = new Color(0.1f, 0.1f, 0.3f, 1f);
			dummyCam.nearClipPlane = 0.01f;
			dummyCam.farClipPlane = 50f;

			rt = new RenderTexture(1024, 768, 24, RenderTextureFormat.Default)
			{
				name = "DummyRenderResult",
				antiAliasing = 1
			};
			rt.Create();

			dummyCam.targetTexture = rt;
			dummyCam.aspect = rt.width / (float)rt.height;

			dummyGO.AddComponent<UniversalAdditionalCameraData>();

			var hook = dummyGO.AddComponent<DummyHook>();
			hook.Provider = this;

			// Create the TestCommandCamera here (after dummyCam exists)
			_testCommandCamera = new TestCommandCamera();
			SyncTestCommandCameraWithDummy(); // initial sync

			dummyGO.transform.SetParent(transform, false);

			Debug.Log("Dummy camera + RT + TestCommandCamera created");
			UpdateUI();
		}

		private void SyncTestCommandCameraWithDummy()
		{
			if (dummyCam == null || _testCommandCamera == null) return;

			_testCommandCamera.position = dummyCam.transform.position;
			_testCommandCamera.rotation = dummyCam.transform.rotation;
			_testCommandCamera.nearClipPlane = dummyCam.nearClipPlane;
			_testCommandCamera.farClipPlane = dummyCam.farClipPlane;
			_testCommandCamera.aspect = dummyCam.aspect;
			_testCommandCamera.orthographic = dummyCam.orthographic;

			if (dummyCam.orthographic)
				_testCommandCamera.orthographicSize = dummyCam.orthographicSize;
			else
				_testCommandCamera.fieldOfView = dummyCam.fieldOfView;

			_testCommandCamera.RecalculateMatrices();
		}

		private void UpdateUI()
		{
			if (outputRawImage && rt)
			{
				outputRawImage.texture = rt;
				outputRawImage.color = Color.white;
			}
		}

		private void CreateQuadMesh()
		{
			quadMesh = new Mesh
			{
				vertices = new[]
				{
					new Vector3(-0.5f, -0.5f, 0f),
					new Vector3(-0.5f,  0.5f, 0f),
					new Vector3( 0.5f,  0.5f, 0f),
					new Vector3( 0.5f, -0.5f, 0f)
				},
				uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
				triangles = new[] { 0, 1, 2, 0, 2, 3 }
			};
			quadMesh.RecalculateBounds();
		}

		private void DrawTheQuad(RasterCommandBuffer cmd, TestCommandCamera camera)
		{
			//if (camera == null)
			//{
			//	Debug.LogWarning("TestCommandCamera is null - using identity matrices");
			//	cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
			//}
			//else
			//{
			//	RenderingUtils.SetViewAndProjectionMatrices(
			//		cmd,
			//		camera.viewMatrix,
			//		camera.projectionMatrix,
			//		setInverseMatrices: true
			//	);

			//	cmd.SetGlobalVector("_WorldSpaceCameraPos", camera.position);
			//}


			cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

			if (testMaterial == null) return;

			cmd.DrawMesh(quadMesh, Matrix4x4.identity, testMaterial, 0, 0);
		}

		public bool HasCommands(RenderPassEvent evt) => evt == RenderPassEvent.BeforeRendering;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, TestCommandCamera camera)
		{
			if (HasCommands(evt))
			{
				// Use our own synced camera (ignore incoming parameter for now)
				DrawTheQuad(cmd, _testCommandCamera);
			}
		}

		private void OnDestroy()
		{
			if (rt != null) rt.Release();
			if (quadMesh != null) Destroy(quadMesh);
			if (dummyGO != null) Destroy(dummyGO);
		}
	}

	internal class DummyHook : MonoBehaviour, IDirectCommandProvider
	{
		public IDirectCommandProvider Provider { get; set; }

		public TestCommandCamera testCommandCamera { get; }

		public bool HasCommands(RenderPassEvent evt) => Provider?.HasCommands(evt) ?? false;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, TestCommandCamera camera)
			=> Provider?.ExecuteCommands(evt, cmd, camera);
	}
}

//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using UnityEngine.UI;

//namespace MassiveHadronLtd
//{
//	public class DirectCommandBufferTest : MonoBehaviour, IDirectCommandProvider
//	{
//		[Header("1. Required Material")]
//		[Tooltip("MUST be URP/Unlit or similar with bright color")]
//		public Material testMaterial;

//		[Header("2. Output UI")]
//		public RawImage outputRawImage;

//		[Header("4. Quad Transform Controls")]
//		[Tooltip("Position in screen space (-1..1 for x/y, z=0)")]
//		public Vector3 quadPosition = Vector3.zero;
//		[Tooltip("Rotation in degrees")]
//		public Vector3 quadRotation = Vector3.zero;
//		[Tooltip("Scale (1=full screen height/width ish)")]
//		public Vector3 quadScale = Vector3.one;

//		// Runtime
//		private GameObject dummyGO;
//		private Camera dummyCam;
//		private RenderTexture rt;
//		private Mesh quadMesh;

//		private void Awake()
//		{
//			if (!testMaterial)
//			{
//				Debug.LogError("testMaterial is NULL! Assign URP/Unlit material with bright color.", this);
//				return;
//			}
//			CreateQuadMesh();
//		}

//		private void Start()
//		{
//			CreateDummyCameraAndRT();
//		}

//		private void Update()
//		{
//			if (dummyCam != null)
//			{
//				dummyCam.Render();
//				UpdateUI();

//				// Sync every frame in case anything changes transform/FOV/etc.
//				SyncTestCommandCameraWithDummy();
//			}
//		}

//		private void CreateDummyCameraAndRT()
//		{
//			if (dummyGO != null) return;

//			dummyGO = new GameObject("DummyCamera [RenderTest]");
//			dummyCam = dummyGO.AddComponent<Camera>();

//			dummyCam.enabled = false;
//			dummyCam.cullingMask = 0;
//			dummyCam.clearFlags = CameraClearFlags.SolidColor;
//			dummyCam.backgroundColor = new Color(0.1f, 0.1f, 0.3f, 1f);
//			dummyCam.nearClipPlane = 0.01f;
//			dummyCam.farClipPlane = 50f;

//			rt = new RenderTexture(1024, 768, 24, RenderTextureFormat.Default)
//			{
//				name = "DummyRenderResult",
//				antiAliasing = 1
//			};
//			rt.Create();

//			dummyCam.targetTexture = rt;
//			dummyCam.aspect = (float)rt.width / rt.height;

//			dummyGO.AddComponent<UniversalAdditionalCameraData>();

//			var hook = dummyGO.AddComponent<DummyHook>();
//			hook.Provider = this;

//			// Create the instance here
//			_testCommandCamera = new TestCommandCamera();

//			// Initial sync
//			SyncTestCommandCameraWithDummy();

//			// Optional: if you want DummyHook to have access too, but not necessary anymore
//			// hook.testCommandCamera = _testCommandCamera;

//			dummyGO.transform.SetParent(transform, false);

//			Debug.Log($"TestCommandCamera created & assigned. Is null? {_testCommandCamera == null}");
//			UpdateUI();
//		}

//		// Helper method to copy current dummyCam state into TestCommandCamera
//		private void SyncTestCommandCameraWithDummy()
//		{
//			if (dummyCam == null || _testCommandCamera == null) return;

//			_testCommandCamera.position = dummyCam.transform.position;
//			_testCommandCamera.rotation = dummyCam.transform.rotation;

//			_testCommandCamera.nearClipPlane = dummyCam.nearClipPlane;
//			_testCommandCamera.farClipPlane = dummyCam.farClipPlane;
//			_testCommandCamera.aspect = dummyCam.aspect;

//			_testCommandCamera.orthographic = dummyCam.orthographic;

//			if (dummyCam.orthographic)
//			{
//				_testCommandCamera.orthographicSize = dummyCam.orthographicSize;
//			}
//			else
//			{
//				_testCommandCamera.fieldOfView = dummyCam.fieldOfView;
//			}

//			// Regenerate matrices after updating properties
//			_testCommandCamera.RecalculateMatrices();
//		}

//		private void UpdateUI()
//		{
//			if (outputRawImage && rt)
//			{
//				outputRawImage.texture = rt;
//				outputRawImage.color = Color.white;
//			}
//		}

//		private void CreateQuadMesh()
//		{
//			// Smaller centered quad (-0.5..0.5) for transform control
//			quadMesh = new Mesh
//			{
//				vertices = new[]
//				{
//					new Vector3(-0.5f, -0.5f, 0f),
//					new Vector3(-0.5f,  0.5f, 0f),
//					new Vector3( 0.5f,  0.5f, 0f),
//					new Vector3( 0.5f, -0.5f, 0f)
//				},
//				uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
//				triangles = new[] { 0, 1, 2, 0, 2, 3 }
//			};
//			quadMesh.RecalculateBounds();
//			Debug.Log("Quad mesh ready");
//		}

//		//private void DrawTheQuad(RasterCommandBuffer cmd, TestCommandCamera camera)
//		//{
//		//	cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

//		//	if (testMaterial)
//		//	{
//		//		cmd.DrawMesh(quadMesh, Matrix4x4.identity, testMaterial, 0, 0);
//		//	}
//		//}

//		private void DrawTheQuad(RasterCommandBuffer cmd, TestCommandCamera camera)
//		{
//			if (camera == null)
//			{
//				Debug.LogWarning("TestCommandCamera is null - falling back to identity");
//				cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
//			}
//			else
//			{
//				// Use the synced camera matrices from dummyCam
//				// This is the key change - the quad now lives in the camera's "world" space
//				RenderingUtils.SetViewAndProjectionMatrices(
//					cmd,
//					camera.viewMatrix,
//					camera.projectionMatrix,
//					setInverseMatrices: true   // important for correct lighting / Unity shader variables
//				);

//				// Optional but often helpful for shaders that still read legacy variables
//				cmd.SetGlobalVector("_WorldSpaceCameraPos", camera.position);
//			}

//			if (testMaterial == null)
//				return;

//			// Now transform the quad in world space (relative to the TestCommandCamera)
//			// This is much more natural than NDC positioning
//			Matrix4x4 localToWorld = Matrix4x4.TRS(
//				quadPosition,                    // position in "world" space
//				Quaternion.Euler(quadRotation),  // rotation
//				quadScale                        // scale
//			);

//			cmd.DrawMesh(
//				quadMesh,
//				localToWorld,
//				testMaterial,
//				0,   // submesh
//				0    // shader pass
//			);
//		}

//		public bool HasCommands(RenderPassEvent evt)
//		{
//			return evt == RenderPassEvent.BeforeRendering;
//		}

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, TestCommandCamera camera)
//		{
//			if (HasCommands(evt))
//			{
//				DrawTheQuad(cmd, camera);
//			}
//		}

//		private void OnDestroy()
//		{
//			if (rt) rt.Release();
//			if (quadMesh) Destroy(quadMesh);
//			if (dummyGO) Destroy(dummyGO);
//		}

//		private TestCommandCamera _testCommandCamera;

//		// Interface property - just exposes the field
//		TestCommandCamera IDirectCommandProvider.testCommandCamera
//		{
//			get => _testCommandCamera;
//			set => _testCommandCamera = value;
//		}

//		private void __DrawTheQuad(RasterCommandBuffer cmd)//reders static full screen but colored
//		{
//			// Identity matrices = treat final positions as NDC (works for color)
//			cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

//			if (testMaterial)
//			{
//				// Apply transform in clip space
//				Matrix4x4 localToClip = Matrix4x4.TRS(quadPosition, Quaternion.Euler(quadRotation), quadScale);
//				cmd.DrawMesh(quadMesh, localToClip, testMaterial, 0, 0);
//			}
//		}

//		private void ___DrawTheQuad(RasterCommandBuffer cmd)//comes out black but can be moved around
//		{
//			// Simple NDC quad - no custom matrices needed for basic test
//			Matrix4x4 view = Matrix4x4.LookAt(Vector3.zero, Vector3.forward, Vector3.up);
//			Matrix4x4 proj = Matrix4x4.Ortho(-1, 1, -1, 1, 0.01f, 100f);
//			cmd.SetViewProjectionMatrices(view, proj);
//			if (testMaterial)
//			{
//				testMaterial.SetPass(0);
//				cmd.DrawMesh(quadMesh, Matrix4x4.identity, testMaterial);
//			}
//		}
//	}

//	internal class DummyHook : MonoBehaviour, IDirectCommandProvider
//	{
//		public IDirectCommandProvider Provider { get; set; }

//		public TestCommandCamera testCommandCamera { get; set; }

//		public bool HasCommands(RenderPassEvent evt) => Provider?.HasCommands(evt) ?? false;

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, TestCommandCamera camera)
//			=> Provider?.ExecuteCommands(evt, cmd, camera);
//	}
//}


//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using UnityEngine.UI;

//namespace MassiveHadronLtd
//{
//	public class DirectCommandBufferTest : MonoBehaviour, IDirectCommandProvider
//	{
//		[Header("1. Required Material")]
//		[Tooltip("MUST be URP/Unlit or similar with bright color")]
//		public Material testMaterial;

//		[Header("2. Output UI")]
//		public RawImage outputRawImage;

//		[Header("3. Custom Camera for this draw command")]
//		[Tooltip("If null → falls back to the one provided by render feature")]
//		public TestCommandCamera customCommandCamera;

//		[Header("4. Quad Transform Controls")]
//		[Tooltip("Position in world/camera space")]
//		public Vector3 quadPosition = Vector3.zero;

//		[Tooltip("Rotation in degrees")]
//		public Vector3 quadRotation = Vector3.zero;

//		[Tooltip("Scale (1 = roughly full screen in ortho view)")]
//		public Vector3 quadScale = Vector3.one * 2f;

//		// Runtime
//		private GameObject dummyGO;
//		private Camera dummyCam;
//		private RenderTexture rt;
//		private Mesh quadMesh;

//		private void Awake()
//		{
//			if (!testMaterial)
//			{
//				Debug.LogError("testMaterial is NULL!", this);
//				enabled = false;
//				return;
//			}

//			CreateQuadMesh();
//		}

//		private void OnEnable()
//		{
//			CreateDummyCameraAndRT();
//		}

//		private void Update()
//		{
//			if (dummyCam != null && dummyCam.isActiveAndEnabled)
//			{
//				dummyCam.Render();
//				UpdateUI();
//			}
//		}

//		private void CreateDummyCameraAndRT()
//		{
//			if (dummyGO != null) return;

//			dummyGO = new GameObject("DummyCamera [RenderTest]");
//			dummyCam = dummyGO.AddComponent<Camera>();

//			dummyCam.enabled = false;
//			dummyCam.cullingMask = 0;
//			dummyCam.clearFlags = CameraClearFlags.SolidColor;
//			dummyCam.backgroundColor = new Color(0.08f, 0.08f, 0.22f, 1f);
//			dummyCam.nearClipPlane = 0.01f;
//			dummyCam.farClipPlane = 50f;

//			rt = new RenderTexture(1024, 768, 24, RenderTextureFormat.DefaultHDR)
//			{
//				name = "DummyRenderResult",
//				antiAliasing = 1,
//				filterMode = FilterMode.Bilinear
//			};
//			rt.Create();

//			dummyCam.targetTexture = rt;
//			dummyCam.aspect = (float)rt.width / rt.height;

//			// Important for URP
//			var additionalData = dummyGO.AddComponent<UniversalAdditionalCameraData>();
//			additionalData.SetRenderer(1); // usually the 2nd renderer (can be changed)

//			// Hook into the command system
//			var hook = dummyGO.AddComponent<DummyHook>();
//			hook.Provider = this;

//			dummyGO.transform.SetParent(transform, false);

//			Debug.Log("Dummy camera + RT created", this);
//			UpdateUI();
//		}

//		private void UpdateUI()
//		{
//			if (outputRawImage != null && rt != null)
//			{
//				outputRawImage.texture = rt;
//				outputRawImage.color = Color.white;
//			}
//		}

//		private void CreateQuadMesh()
//		{
//			quadMesh = new Mesh
//			{
//				name = "DebugQuad",
//				vertices = new[]
//				{
//					new Vector3(-0.5f, -0.5f, 0f),
//					new Vector3(-0.5f,  0.5f, 0f),
//					new Vector3( 0.5f,  0.5f, 0f),
//					new Vector3( 0.5f, -0.5f, 0f)
//				},
//				uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
//				triangles = new[] { 0, 1, 2, 0, 2, 3 }
//			};
//			quadMesh.RecalculateNormals();
//			quadMesh.RecalculateBounds();
//		}

//		// ────────────────────────────────────────────────────────────────
//		//               The important part – actual drawing
//		// ────────────────────────────────────────────────────────────────
//		private void DrawTheQuad(RasterCommandBuffer cmd, TestCommandCamera camera)
//		{
//			if (camera == null)
//			{
//				Debug.LogWarning("No TestCommandCamera provided – skipping draw", this);
//				return;
//			}

//			// Make sure matrices are fresh
//			camera.RecalculateMatrices();

//			// Use RenderingUtils version – handles inverse matrices + many URP compatibility issues
//			RenderingUtils.SetViewAndProjectionMatrices(
//				cmd,
//				camera.viewMatrix,
//				camera.projectionMatrix,
//				setInverseMatrices: true
//			);

//			// Optional: help shaders that still read legacy globals
//			cmd.SetGlobalVector("_WorldSpaceCameraPos", camera.position);

//			// Transform in "world" space relative to our custom camera
//			Matrix4x4 localToWorld = Matrix4x4.TRS(quadPosition, Quaternion.Euler(quadRotation), quadScale);

//			if (testMaterial)
//			{
//				cmd.DrawMesh(quadMesh, localToWorld, testMaterial, 0, 0);
//			}
//		}

//		// ────────────────────────────────────────────────────────────────
//		//               Interface implementation
//		// ────────────────────────────────────────────────────────────────
//		public bool HasCommands(RenderPassEvent evt)
//		{
//			// You can make this more sophisticated later
//			return evt == RenderPassEvent.BeforeRendering;
//		}

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, TestCommandCamera incomingCamera)
//		{
//			if (!HasCommands(evt)) return;

//			// Priority order:
//			// 1. Our explicitly assigned custom camera (highest control)
//			// 2. Whatever the render feature passed us
//			var cameraToUse = customCommandCamera != null ? customCommandCamera : incomingCamera;

//			DrawTheQuad(cmd, cameraToUse);
//		}

//		private void OnDisable()
//		{
//			if (rt != null)
//			{
//				rt.Release();
//				rt = null;
//			}
//		}

//		private void OnDestroy()
//		{
//			if (quadMesh != null)
//			{
//				if (Application.isPlaying)
//					Destroy(quadMesh);
//				else
//					DestroyImmediate(quadMesh);
//				quadMesh = null;
//			}

//			if (dummyGO != null)
//			{
//				Destroy(dummyGO);
//				dummyGO = null;
//			}
//		}

//		// For quick testing in editor
//		private void OnValidate()
//		{
//			if (testMaterial && !string.IsNullOrEmpty(testMaterial.shader.name) &&
//				!testMaterial.shader.name.Contains("Unlit"))
//			{
//				Debug.LogWarning("testMaterial should probably use an Unlit shader", testMaterial);
//			}
//		}
//	}

//	internal class DummyHook : MonoBehaviour, IDirectCommandProvider
//	{
//		public IDirectCommandProvider Provider { get; set; }

//		public bool HasCommands(RenderPassEvent evt) => Provider?.HasCommands(evt) ?? false;

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, TestCommandCamera camera)
//			=> Provider?.ExecuteCommands(evt, cmd, camera);
//	}
//}
////		private void __DrawTheQuad(RasterCommandBuffer cmd)//reders static full screen but colored
////		{
////			// Identity matrices = treat final positions as NDC (works for color)
////			cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

////			if (testMaterial)
////			{
////				// Apply transform in clip space
////				Matrix4x4 localToClip = Matrix4x4.TRS(quadPosition, Quaternion.Euler(quadRotation), quadScale);
////				cmd.DrawMesh(quadMesh, localToClip, testMaterial, 0, 0);
////			}
////		}

////		private void ___DrawTheQuad(RasterCommandBuffer cmd)//comes out black but can be moved around
////		{
////			// Simple NDC quad - no custom matrices needed for basic test
////			Matrix4x4 view = Matrix4x4.LookAt(Vector3.zero, Vector3.forward, Vector3.up);
////			Matrix4x4 proj = Matrix4x4.Ortho(-1, 1, -1, 1, 0.01f, 100f);
////			cmd.SetViewProjectionMatrices(view, proj);
////			if (testMaterial)
////			{
////				testMaterial.SetPass(0);
////				cmd.DrawMesh(quadMesh, Matrix4x4.identity, testMaterial);
////			}
////		}
