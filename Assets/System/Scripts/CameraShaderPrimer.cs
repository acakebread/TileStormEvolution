using UnityEngine;

[ExecuteAlways]  // So it works in editor/play mode without needing to be enabled manually
public class CameraShaderPrimer : MonoBehaviour
{
	private static bool s_hasPrimed = false;
	private GameObject s_dummyQuad = null;

	void Awake()
	{
		if (s_hasPrimed) return;

		// Only run once per session (static flag survives domain reloads in editor)
		PrimeShader();
	}

	//void OnValidate()
	//{
	//	// Optional: re-prime in editor if shader changes
	//	if (!s_hasPrimed && Application.isEditor)
	//	{
	//		PrimeShader();
	//	}
	//}

	private void PrimeShader()
	{
		if (Camera.main == null) return;

		// Create tiny quad – exact same as your working version
		s_dummyQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
		var renderer = s_dummyQuad.GetComponent<MeshRenderer>();

		//renderer.material = new Material(Shader.Find("Custom/CommandRender"));
		renderer.material = new Material(Shader.Find("Hidden/Internal-Colored")){color = Color.clear};

		// Make invisible / zero-cost
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		renderer.receiveShadows = false;
		renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
		renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
		renderer.allowOcclusionWhenDynamic = false;

		// Position + scale to be frustum-culled (or just disable if that suffices in your version)
		s_dummyQuad.transform.SetParent(Camera.main.transform, false);
		s_dummyQuad.transform.localPosition = new Vector3(0f, 0f, Camera.main.farClipPlane - 1f);
		s_dummyQuad.transform.localScale = Vector3.one * 0.0001f;

		// Usually safe to leave disabled – Unity often still processes it for variant collection
		// renderer.enabled = false;

		// If disabled prevents priming in your Unity version → uncomment this:
		// renderer.enabled = true;

		s_hasPrimed = true;

		// Optional: hide in hierarchy (still works)
		s_dummyQuad.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;

		// Do NOT destroy – keep it around forever (tiny cost, zero render cost)
		// If you must clean up on quit: use OnDestroy() + static reset, but usually unnecessary
	}

	// Optional cleanup (rarely needed)
	void OnDestroy()
	{
		if (s_dummyQuad != null)
		{
			Destroy(s_dummyQuad);
			s_dummyQuad = null;
		}
		s_hasPrimed = false;
	}
}