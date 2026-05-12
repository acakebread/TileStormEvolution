using UnityEngine;

[ExecuteAlways]
public class ShaderPrimer : MonoBehaviour
{
	private static bool hasPrimed = false;

	void Awake()
	{
		if (hasPrimed) return;
		PrimeShaders();
	}

	private void PrimeShaders()
	{
		// Prime the main lit shader
		var template = Resources.Load<Material>("ForceInclude/URP lit opaque");
		if (template != null)
		{
			var dummy = new Material(template);
			dummy.SetColor("_EmissionColor", Color.green);
			dummy.EnableKeyword("_EMISSION");
			DestroyImmediate(dummy);
			Debug.Log("ShaderPrimer: Pre-warmed URP Lit + Emission");
		}

		hasPrimed = true;
	}
}