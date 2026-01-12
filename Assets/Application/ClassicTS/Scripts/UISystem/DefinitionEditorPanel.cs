using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class DefinitionEditorPanel : UIPanel
	{
		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private ScrollRect definitionScrollView;
		[SerializeField] private Transform contentParent;
		[SerializeField] private GameObject definitionListItemPrefab;
		[SerializeField] private RawImage previewImage;
		[SerializeField] private Vector2 previewResolution = new Vector2(320, 240);

		[Header("Preview Settings - Camera")]
		[SerializeField] private float defaultFOV = 60f;
		[SerializeField] private float sizeToDistanceFactor = 1f;
		[SerializeField] private float defaultTiltAngle = 30f;
		[SerializeField] private float minTiltAngle = 0f;
		[SerializeField] private float maxTiltAngle = 90f;
		[SerializeField] private float minDistance = 0.8f;
		[SerializeField] private float maxDistance = 10f;

		[SerializeField] private float dragOrbitSensitivity = 0.2f;
		[SerializeField] private float dragTiltSensitivity = 0.2f;
		[SerializeField] private float scrollZoomSensitivity = 0.5f;

		[SerializeField] private float autoRotateSpeed = 15f;

		[Header("Preview Ground Plane")]
		[SerializeField] private Color groundColor = new Color(1f, 1f, 1f);
		[SerializeField] private float groundSize = 2.5f;
		[SerializeField] private float groundY = -0.02f;
		[SerializeField] private float groundUVScale = 1f;

		[Header("Ground Texture Override")]
		[SerializeField] private Texture2D groundOverrideTexture;

		//command buffer light
		private static readonly int MainLightPositionID = Shader.PropertyToID("_MainLightPosition");
		private static readonly int MainLightColorID = Shader.PropertyToID("_MainLightColor");

		// ── Runtime ───────────────────────────────────────────────────────
		private RenderTexture previewRenderTexture;
		private Camera previewCamera;
		private GameObject previewRoot;
		private string selectedDefinitionId;

		private readonly List<GameObject> spawnedListItems = new();

		private Mesh groundMesh;
		private Material groundMat;
		private Texture2D groundTex;
		private RenderModelData currentModelData;

		// Camera control
		private Vector3 gimbalPosition;
		private float currentOrbitAngle = 0f;
		private float currentTiltAngle;
		private float currentDistance;

		// Idle autorotate
		private float lastInputTime = -999f;
		private const float AutoRotateDelay = 3f;

		// RT resize tracking
		private RectTransform previewRect;
		private Vector2 lastPreviewSize;

		private RectTransform previewRaycastBlocker;

		// ────────────────────────────────────────────────────────────────

		protected override void Awake()
		{
			base.Awake();

			if (closeButton != null)
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (contentParent == null)
				contentParent = definitionScrollView?.content;
		}

		public override void OnPanelOpened()
		{
			base.OnPanelOpened();
			CleanupPreview();
			CreatePreviewSetup();
			RefreshDefinitionList();

			if (ResourceManager.Definitions.Count > 0)
				SelectDefinition(ResourceManager.Definitions[0].id);
		}

		public override void OnPanelClosed()
		{
			base.OnPanelClosed();
			CleanupPreview();
			ClearListItems();
		}

		private void Update()
		{
			if (previewCamera != null && previewRenderTexture != null && previewRenderTexture.IsCreated())
			{
				HandleRenderTextureResize();
				previewCamera.Render();
			}

			if (autoRotateSpeed > 0.01f && Time.unscaledTime - lastInputTime > AutoRotateDelay)
			{
				currentOrbitAngle -= autoRotateSpeed * Time.deltaTime;
				UpdateCameraTransform();
			}
		}

		protected override void OnDestroy()
		{
			CleanupPreview();
			base.OnDestroy();
		}

		// ─────────────────────────── LIST ────────────────────────────────

		private void RefreshDefinitionList()
		{
			ClearListItems();
			if (ResourceManager.Definitions.Count == 0) return;

			foreach (var def in ResourceManager.Definitions)
				CreateDefinitionListItem(def);
		}

		private void ClearListItems()
		{
			foreach (var item in spawnedListItems)
				if (item) Destroy(item);
			spawnedListItems.Clear();
		}

		private void CreateDefinitionListItem(Definition def)
		{
			if (!definitionListItemPrefab) return;

			var go = Instantiate(definitionListItemPrefab, contentParent);
			spawnedListItems.Add(go);

			var item = go.GetComponent<DefinitionListItem>();
			if (!item) return;

			item.Initialize(def.id, SelectDefinition);

			if (item.label)
			{
				item.label.richText = false;
				item.label.enableAutoSizing = false;
				item.label.fontSize = 20;
				item.label.text = $"{def.id} ({def.model ?? "—"})";
			}

			item.SetSelected(def.id == selectedDefinitionId);
		}

		private void SelectDefinition(string defId)
		{
			if (string.IsNullOrEmpty(defId)) return;

			selectedDefinitionId = defId;
			lastInputTime = -999f; // treat as idle so autorotate starts immediately

			foreach (var go in spawnedListItems)
			{
				if (!go) continue;
				var item = go.GetComponent<DefinitionListItem>();
				if (item) item.SetSelected(item.DefinitionId == selectedDefinitionId);
			}

			UpdatePreview(defId);
		}

		// ──────────────────────── PREVIEW SETUP ──────────────────────────

		private void CreatePreviewSetup()//I would like preview camera setup to not be dependant on previewRoot / rect - I would like the render texture to be passed in - the preview camera and command buffer features are going to be moved out of this class into a separate helper script
		{
			if (!previewImage) return;

			previewRect = previewImage.GetComponent<RectTransform>();

			previewRoot = new GameObject("DefinitionPreviewRoot");

			var camGO = new GameObject("PreviewCamera");
			previewCamera = camGO.AddComponent<Camera>();
			camGO.transform.SetParent(previewRoot.transform);

			previewCamera.enabled = false;
			previewCamera.cullingMask = 0;// 1 << LayerMask.NameToLayer("Water"); //I don't want to have to use a layer flag but we have to for lighting - I want procudral light added if possible to command buffer
			previewCamera.clearFlags = CameraClearFlags.SolidColor;
			ColorUtility.TryParseHtmlString("#21B2E1", out Color hashColor);
			previewCamera.backgroundColor = hashColor;
			previewCamera.orthographic = false;
			previewCamera.fieldOfView = defaultFOV;
			previewCamera.nearClipPlane = 0.03f;
			previewCamera.farClipPlane = 50f;

			previewRenderTexture = new RenderTexture(
				(int)previewResolution.x,
				(int)previewResolution.y,
				24,
				RenderTextureFormat.ARGB32);

			previewRenderTexture.Create();
			previewCamera.targetTexture = previewRenderTexture;
			previewImage.texture = previewRenderTexture;

			CreateGroundPlane();
			AttachCommandProvider(camGO);
			CreatePreviewRaycastBlocker();

			lastPreviewSize = Vector2.zero;
		}

		private void HandleRenderTextureResize()
		{
			if (!previewRect || !previewRenderTexture) return;

			Vector2 size = previewRect.rect.size;
			if (size == lastPreviewSize || size.x < 16 || size.y < 16) return;

			lastPreviewSize = size;

			int w = Mathf.RoundToInt(size.x);
			int h = Mathf.RoundToInt(size.y);

			if (previewRenderTexture.width == w && previewRenderTexture.height == h) return;

			previewRenderTexture.Release();
			previewRenderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
			previewRenderTexture.Create();

			previewCamera.targetTexture = previewRenderTexture;
			previewCamera.aspect = (float)w / h;
			previewImage.texture = previewRenderTexture;
		}

		private void CreateGroundPlane()
		{
			groundMesh = MeshUtils.GenerateQuadXZ(groundSize, groundUVScale, "PreviewGroundMesh");

			groundTex = groundOverrideTexture != null ? groundOverrideTexture : TextureUtils.GenerateXorTexture256();

			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			groundMat = new Material(shader)
			{
				name = "PreviewGroundMat",
				hideFlags = HideFlags.HideAndDontSave
			};

			groundMat.SetFloat("_Surface", 0f);
			groundMat.SetTexture("_BaseMap", groundTex);
			groundMat.SetColor("_BaseColor", groundColor);
		}

		private void AttachCommandProvider(GameObject camGO)
		{
			var provider = camGO.AddComponent<CommandBufferProvider>();

			provider.RegisterCommand(RenderPassEvent.BeforeRenderingOpaques, (cmd, cam) =>
			{
				// ── Fake main directional light ────────────────────────────────────────

				// Desired light direction (towards light source) — example: classic 3/4 top-down
				// You can make this dynamic: e.g. from camera orbit or fixed
				Vector3 lightDirTowardsSource = new Vector3(0.5f, 1f, -0.3f).normalized; // tweak as needed

				// For directional: position.w = 0? Wait no → URP uses .w = 1 for directional
				// Direction is actually -light forward (convention in URP)
				Vector4 mainLightPos = new Vector4(lightDirTowardsSource.x, lightDirTowardsSource.y, lightDirTowardsSource.z, 1.0f);  // .w = 1 → directional

				Color lightColorAndIntensity = new Color(0.75f, 0.75f, 0.75f);

				cmd.SetGlobalVector(MainLightPositionID, mainLightPos);
				cmd.SetGlobalVector(MainLightColorID, lightColorAndIntensity.linear); // use linear if HDR

				// Optional: early clear if you want full control (requires camera clearFlags = Nothing)
				// cmd.ClearRenderTarget(true, true, backgroundColor, 1.0f);

				// ── Then your existing draws ───────────────────────────────────────────
				if (groundMesh != null && groundMat != null)
					cmd.DrawMesh(groundMesh, Matrix4x4.Translate(Vector3.up * groundY), groundMat, 0, 0);

				if (currentModelData != null)
				{
					foreach (var instance in currentModelData.meshInstances)
					{
						if (instance.mesh == null) continue;
						for (int s = 0; s < instance.subMeshCount; s++)
						{
							var mat = s < instance.materials.Length ? instance.materials[s] : instance.materials[0];
							if (mat == null) continue;
							cmd.DrawMesh(instance.mesh, instance.localToWorld, mat, s, 0);
						}
					}
				}
			});
		}

		private void UpdatePreview(string defId)
		{
			currentModelData = null;

			var def = ResourceManager.GetDefinition(defId);
			if (def == null || string.IsNullOrEmpty(def.model)) return;

			currentModelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);

			// Reset camera for new model
			float modelSize = currentModelData?.bounds.size.magnitude ?? 5f;
			currentDistance = modelSize * sizeToDistanceFactor;
			currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

			currentTiltAngle = defaultTiltAngle;
			currentOrbitAngle = 0f;

			UpdateCameraTransform();
		}

		private void UpdateCameraTransform()
		{
			if (previewCamera == null) return;

			// Gimbal (target) position
			var gimbalY = currentModelData != null ? currentModelData.bounds.max.y * 0.5f : 1f;

			gimbalPosition = Vector3.up * gimbalY;

			// ─── Build camera transform in requested order ───────────────────────

			// 1. Start with identity rotation
			Quaternion rotation = Quaternion.identity;

			// 2. First apply orbit rotation around world Y-axis
			rotation *= Quaternion.Euler(0f, currentOrbitAngle, 0f);

			// 3. Then apply tilt (pitch down) around the *local* X-axis after orbit
			rotation *= Quaternion.Euler(currentTiltAngle, 0f, 0f);

			// 4. Direction the camera is facing (forward = -Z in Unity convention)
			Vector3 forward = rotation * Vector3.forward;

			// 5. Camera position = gimbal - forward * distance
			//    (because we want to look towards the gimbal)
			Vector3 cameraPosition = gimbalPosition - forward * currentDistance;

			// ─── Apply to transform ──────────────────────────────────────────────
			previewCamera.transform.position = cameraPosition;
			previewCamera.transform.rotation = rotation;
		}

		// ───────────────────── CAMERA INPUT ──────────────────────────────

		public void DragPreviewCamera(Vector2 delta)
		{
			lastInputTime = Time.unscaledTime;

			currentOrbitAngle += delta.x * dragOrbitSensitivity;
			currentTiltAngle -= delta.y * dragTiltSensitivity;
			currentTiltAngle = Mathf.Clamp(currentTiltAngle, minTiltAngle, maxTiltAngle);

			UpdateCameraTransform();
		}

		public void ZoomPreviewCamera(float scroll)
		{
			lastInputTime = Time.unscaledTime;

			currentDistance -= scroll * scrollZoomSensitivity;
			currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

			UpdateCameraTransform();
		}

		private void CreatePreviewRaycastBlocker()
		{
			if (previewRaycastBlocker || !previewImage) return;

			GameObject blocker = new GameObject("PreviewRaycastBlocker");
			blocker.transform.SetParent(previewImage.transform, false);

			var rt = blocker.AddComponent<RectTransform>();
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;

			var img = blocker.AddComponent<Image>();
			img.color = new Color(0, 0, 0, 0);
			img.raycastTarget = true;

			blocker.AddComponent<PreviewCameraInput>();
			previewRaycastBlocker = rt;
		}

		private void CleanupPreview()
		{
			if (previewRenderTexture != null)
			{
				previewRenderTexture.Release();
				previewRenderTexture = null;
			}

			if (groundMesh) DestroyImmediate(groundMesh);
			if (groundMat) DestroyImmediate(groundMat);
			if (groundTex && groundTex != groundOverrideTexture) DestroyImmediate(groundTex);

			if (previewRoot)
				Destroy(previewRoot);

			previewCamera = null;
			if (previewImage) previewImage.texture = null;
		}

		// ───────────────────── CAMERA INPUT HELPER ───────────────────────

		private class PreviewCameraInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
		{
			private DefinitionEditorPanel panel;
			private Vector2 lastPos;

			private void Awake()
			{
				panel = GetComponentInParent<DefinitionEditorPanel>();
			}

			public void OnPointerDown(PointerEventData e)
			{
				lastPos = e.position;
				panel.lastInputTime = Time.unscaledTime;
			}

			public void OnDrag(PointerEventData e)
			{
				Vector2 delta = e.position - lastPos;
				lastPos = e.position;
				panel.DragPreviewCamera(delta);
			}

			public void OnScroll(PointerEventData e)
			{
				panel.ZoomPreviewCamera(e.scrollDelta.y);
			}
		}

		// ───────────────────── COMMAND BUFFER PROVIDER ───────────────────

		private class CommandBufferProvider : MonoBehaviour, ICommandBufferProvider
		{
			private readonly Dictionary<RenderPassEvent, System.Action<RasterCommandBuffer, Camera>> commands = new();

			public void RegisterCommand(RenderPassEvent evt, System.Action<RasterCommandBuffer, Camera> action)
			{
				commands[evt] = action;
			}

			public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt);

			public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
			{
				if (commands.TryGetValue(evt, out var action))
				{
					try { action?.Invoke(commandBuffer, camera); }
					catch (System.Exception ex)
					{
						Debug.LogError($"Preview command failed: {ex.Message}");
					}
				}
			}

			private void OnDestroy() => commands.Clear();
		}
	}
}