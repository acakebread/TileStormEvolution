using UnityEngine;
using UnityEngine.Rendering;

public static class MaterialUtils
{
	/// <summary>
	/// Creates a transparent URP Unlit material with specified base color.
	/// </summary>
	/// <param name="baseColor">The base color (including alpha) for the material.</param>
	/// <returns>A configured Material, or null if the URP Unlit shader is not found.</returns>
	public static Material CreateTransparentUnlitMaterial(Color baseColor)
	{
		var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
		if (unlitShader == null)
		{
			Debug.LogError("MaterialUtils: Universal Render Pipeline/Unlit shader not found! Ensure URP is installed.");
			return null;
		}

		var material = new Material(unlitShader) { renderQueue = (int)RenderQueue.Transparent };
		material.SetColor("_BaseColor", baseColor);
		material.SetFloat("_Surface", 1f); // Transparent
		material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		material.SetFloat("_ZWrite", 0f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.SetOverrideTag("RenderType", "Transparent");
		return material;
	}
}