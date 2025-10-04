//using UnityEngine;

//[AddComponentMenu("Rendering/Frosted Effect Controller")]
//public class FrostedEffectController : MonoBehaviour
//{
//	[SerializeField] private ReflectionPassCamera reflectionPassCamera;
//	[SerializeField, Range(1, 20)] private float frostRadius = 12f;
//	[SerializeField] private Color baseColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
//	[SerializeField] private Texture2D noiseTexture;

//	private void OnValidate()
//	{
//		if (reflectionPassCamera != null && reflectionPassCamera.UseFrostedEffect)
//		{
//			reflectionPassCamera.FrostRadius = frostRadius;
//			reflectionPassCamera.BaseColor = baseColor;
//			if (noiseTexture != null)
//				reflectionPassCamera.NoiseTexture = noiseTexture;
//		}
//	}

//	private void Update()
//	{
//		if (reflectionPassCamera != null && reflectionPassCamera.UseFrostedEffect && reflectionPassCamera.ReflectionMaterial != null)
//		{
//			var material = reflectionPassCamera.ReflectionMaterial;
//			if (material.HasProperty("_MainTex")) // Ensure it's a frosted material
//			{
//				reflectionPassCamera.FrostRadius = frostRadius;
//				reflectionPassCamera.BaseColor = baseColor;
//				if (noiseTexture != null)
//					reflectionPassCamera.NoiseTexture = noiseTexture;

//				material.SetFloat("_Radius", frostRadius);
//				material.SetColor("_BaseColor", baseColor);
//				if (noiseTexture != null)
//					material.SetTexture("_NoiseTex", noiseTexture);
//				material.SetFloat("_NoiseStrength", 0.02f);
//			}
//			else
//			{
//				Debug.LogWarning("FrostedEffectController: Reflection material does not support frosted effect properties!", this);
//			}
//		}
//	}

//	// Public methods to allow runtime control
//	public void SetFrostRadius(float radius)
//	{
//		frostRadius = Mathf.Clamp(radius, 1f, 20f);
//		if (reflectionPassCamera != null && reflectionPassCamera.UseFrostedEffect)
//			reflectionPassCamera.FrostRadius = frostRadius;
//	}

//	public void SetBaseColor(Color color)
//	{
//		baseColor = color;
//		if (reflectionPassCamera != null && reflectionPassCamera.UseFrostedEffect)
//			reflectionPassCamera.BaseColor = baseColor;
//	}

//	public void SetNoiseTexture(Texture2D texture)
//	{
//		noiseTexture = texture;
//		if (reflectionPassCamera != null && reflectionPassCamera.UseFrostedEffect)
//			reflectionPassCamera.NoiseTexture = noiseTexture;
//	}
//}