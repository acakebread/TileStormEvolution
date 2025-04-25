using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MirroredPassRenderer : MonoBehaviour
{
	public float yOffset = 0f;
	public LayerMask reflectedMask = 2147483647;//'everything'

	Camera mainCam;
	Camera mirrorCam;

	void Start()
	{
		mainCam = Camera.main;

		GameObject camObj = new GameObject("MirrorCamera");
		mirrorCam = camObj.AddComponent<Camera>();
		mirrorCam.enabled = true; // Ensure enabled
	}

	void OnPreRender()
	{
		if (!mainCam || !mirrorCam) return;

		// Copy camera settings
		mirrorCam.CopyFrom(mainCam);
		mirrorCam.cullingMask = -1; // Render all layers
		mirrorCam.clearFlags = CameraClearFlags.Color;
		mirrorCam.cullingMask = reflectedMask.value;
		mirrorCam.depth = mainCam.depth - 1;

		// Reflect position and rotation across Y=0 plane
		Vector4 plane = new Vector4(0, 1, 0, yOffset);
		Matrix4x4 reflectionMat = MatrixReflect(plane);

		// Reflect position
		Vector4 pos = new Vector4(mainCam.transform.position.x, mainCam.transform.position.y, mainCam.transform.position.z, 1);
		pos = reflectionMat * pos;
		mirrorCam.transform.position = new Vector3(pos.x, pos.y, pos.z);

		// Reflect rotation
		Matrix4x4 mainCamWorldMatrix = mainCam.transform.localToWorldMatrix;
		Matrix4x4 reflectedWorldMatrix = reflectionMat * mainCamWorldMatrix;
		mirrorCam.transform.rotation = Quaternion.LookRotation(reflectedWorldMatrix.GetColumn(2), reflectedWorldMatrix.GetColumn(1));

		// Apply mirrored projection matrix
		Matrix4x4 proj = mainCam.projectionMatrix;
		proj = Matrix4x4.Scale(new Vector3(-1, 1, 1)) * proj;
		mirrorCam.projectionMatrix = proj;

		// Use replacement shader for backface rendering
		Shader backfaceShader = Shader.Find("Custom/Backface");
		if (backfaceShader != null)
		{
			mirrorCam.SetReplacementShader(backfaceShader, "");
			Debug.Log("Backface shader applied successfully.");
		}
		else
		{
			Debug.LogWarning("Backface shader not found! Create Custom/Backface.shader in Assets/Custom.");
		}
	}

	Matrix4x4 MatrixReflect(Vector4 plane)
	{
		Matrix4x4 reflectionMat = Matrix4x4.identity;

		float a = plane.x;
		float b = plane.y;
		float c = plane.z;
		float d = plane.w;

		float length = Mathf.Sqrt(a * a + b * b + c * c);
		if (length > 0)
		{
			a /= length;
			b /= length;
			c /= length;
			d /= length;
		}

		reflectionMat[0, 0] = -2 * a * a + 1;
		reflectionMat[0, 1] = -2 * a * b;
		reflectionMat[0, 2] = -2 * a * c;
		reflectionMat[0, 3] = -2 * a * d;

		reflectionMat[1, 0] = -2 * b * a;
		reflectionMat[1, 1] = -2 * b * b + 1;
		reflectionMat[1, 2] = -2 * b * c;
		reflectionMat[1, 3] = -2 * b * d;

		reflectionMat[2, 0] = -2 * c * a;
		reflectionMat[2, 1] = -2 * c * b;
		reflectionMat[2, 2] = -2 * c * c + 1;
		reflectionMat[2, 3] = -2 * c * d;

		reflectionMat[3, 0] = 0;
		reflectionMat[3, 1] = 0;
		reflectionMat[3, 2] = 0;
		reflectionMat[3, 3] = 1;

		return reflectionMat;
	}

	void OnDestroy()
	{
		if (mirrorCam != null)
			Destroy(mirrorCam.gameObject);
	}
}



//using UnityEngine;

//[RequireComponent(typeof(Camera))]
//public class MirroredPassRenderer : MonoBehaviour
//{
//	public float yOffset = 0f;

//	Camera mainCam;
//	Camera mirrorCam;
//	public RenderTexture debugRT;

//	void Start()
//	{
//		mainCam = Camera.main;

//		GameObject camObj = new GameObject("MirrorCamera");
//		mirrorCam = camObj.AddComponent<Camera>();

//		// Create temporary render texture for debugging
//		debugRT = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
//		debugRT.name = "MirrorCamDebugRT";
//		debugRT.Create();
//		mirrorCam.targetTexture = debugRT;

//		// Assign debugRT to quad's material (adjust as needed)
//		Renderer quadRenderer = GetComponent<Renderer>();
//		if (quadRenderer != null)
//		{
//			quadRenderer.material.mainTexture = debugRT;
//			Debug.Log("Assigned debugRT to quad material.");
//		}
//		else
//		{
//			Debug.LogWarning("No Renderer found on quad! Add a MeshRenderer.");
//		}
//	}

//	void OnPreCull()
//	{
//		if (!mainCam || !mirrorCam) return;

//		// Copy camera settings first, then override position/rotation
//		mirrorCam.CopyFrom(mainCam);
//		mirrorCam.cullingMask = -1; // Render all layers
//		mirrorCam.clearFlags = CameraClearFlags.SolidColor;
//		mirrorCam.depth = mainCam.depth - 1;

//		// Reflect position and rotation across Y=0 plane
//		Vector4 plane = new Vector4(0, 1, 0, yOffset);
//		Matrix4x4 reflectionMat = MatrixReflect(plane);

//		// Reflect position
//		Vector4 pos = new Vector4(mainCam.transform.position.x, mainCam.transform.position.y, mainCam.transform.position.z, 1);
//		pos = reflectionMat * pos;
//		mirrorCam.transform.position = new Vector3(pos.x, pos.y, pos.z);

//		// Reflect rotation
//		Matrix4x4 mainCamWorldMatrix = mainCam.transform.localToWorldMatrix;
//		Matrix4x4 reflectedWorldMatrix = reflectionMat * mainCamWorldMatrix;
//		mirrorCam.transform.rotation = Quaternion.LookRotation(reflectedWorldMatrix.GetColumn(2), reflectedWorldMatrix.GetColumn(1));
//	}

//	void OnPreRender()
//	{
//		if (!mainCam || !mirrorCam) return;

//		// Apply mirrored projection matrix
//		Matrix4x4 proj = mainCam.projectionMatrix;
//		proj = Matrix4x4.Scale(new Vector3(-1, 1, 1)) * proj;
//		mirrorCam.projectionMatrix = proj;

//		// Use replacement shader for backface rendering
//		Shader backfaceShader = Shader.Find("Custom/BackfaceURP"); // Use Custom/Backface for Built-in
//		if (backfaceShader != null)
//		{
//			mirrorCam.SetReplacementShader(backfaceShader, "RenderType=Opaque");
//			Debug.Log("Backface shader applied successfully.");
//		}
//		else
//		{
//			Debug.LogWarning("Backface shader not found! Create Custom/BackfaceURP.shader in Assets/Custom.");
//		}

//		// Render
//		mirrorCam.Render();

//		// Reset state
//		mirrorCam.ResetCullingMatrix();
//		mirrorCam.ResetReplacementShader();
//	}

//	Matrix4x4 MatrixReflect(Vector4 plane)
//	{
//		Matrix4x4 reflectionMat = Matrix4x4.identity;

//		float a = plane.x;
//		float b = plane.y;
//		float c = plane.z;
//		float d = plane.w;

//		float length = Mathf.Sqrt(a * a + b * b + c * c);
//		if (length > 0)
//		{
//			a /= length;
//			b /= length;
//			c /= length;
//			d /= length;
//		}

//		reflectionMat[0, 0] = -2 * a * a + 1;
//		reflectionMat[0, 1] = -2 * a * b;
//		reflectionMat[0, 2] = -2 * a * c;
//		reflectionMat[0, 3] = -2 * a * d;

//		reflectionMat[1, 0] = -2 * b * a;
//		reflectionMat[1, 1] = -2 * b * b + 1;
//		reflectionMat[1, 2] = -2 * b * c;
//		reflectionMat[1, 3] = -2 * b * d;

//		reflectionMat[2, 0] = -2 * c * a;
//		reflectionMat[2, 1] = -2 * c * b;
//		reflectionMat[2, 2] = -2 * c * c + 1;
//		reflectionMat[2, 3] = -2 * c * d;

//		reflectionMat[3, 0] = 0;
//		reflectionMat[3, 1] = 0;
//		reflectionMat[3, 2] = 0;
//		reflectionMat[3, 3] = 1;

//		return reflectionMat;
//	}

//	void OnDestroy()
//	{
//		if (mirrorCam != null)
//			Destroy(mirrorCam.gameObject);
//		if (debugRT != null)
//		{
//			debugRT.Release();
//			Destroy(debugRT);
//		}
//	}
//}



//using UnityEngine;

//[RequireComponent(typeof(Camera))]
//public class MirroredPassRenderer : MonoBehaviour
//{
//	public float yOffset = 0f;

//	Camera mainCam;
//	Camera mirrorCam;
//	RenderTexture debugRT;

//	void Start()
//	{
//		mainCam = Camera.main;

//		GameObject camObj = new GameObject("MirrorCamera");
//		mirrorCam = camObj.AddComponent<Camera>();

//		// Create temporary render texture for debugging
//		debugRT = new RenderTexture(512, 512, 24);
//		mirrorCam.targetTexture = debugRT;
//	}

//	void OnPreCull()
//	{
//		// Copy camera settings first, then override position/rotation
//		mirrorCam.CopyFrom(mainCam);
//		mirrorCam.cullingMask = -1; // Render all layers
//		mirrorCam.clearFlags = CameraClearFlags.SolidColor; // Your setting
//		mirrorCam.depth = mainCam.depth - 1;

//		// Reflect position and rotation across Y=0 plane
//		Vector4 plane = new Vector4(0, 1, 0, yOffset); // Y=0 plane (normal = (0,1,0), offset = 0)
//		Matrix4x4 reflectionMat = MatrixReflect(plane);

//		// Reflect position
//		Vector4 pos = new Vector4(mainCam.transform.position.x, mainCam.transform.position.y, mainCam.transform.position.z, 1);
//		pos = reflectionMat * pos;
//		mirrorCam.transform.position = new Vector3(pos.x, pos.y, pos.z);

//		// Reflect rotation
//		Matrix4x4 mainCamWorldMatrix = mainCam.transform.localToWorldMatrix;
//		Matrix4x4 reflectedWorldMatrix = reflectionMat * mainCamWorldMatrix;
//		mirrorCam.transform.rotation = Quaternion.LookRotation(reflectedWorldMatrix.GetColumn(2), reflectedWorldMatrix.GetColumn(1));
//	}

//	void OnPreRender()
//	{
//		if (!mainCam || !mirrorCam) return;

//		// Apply Y-inverted projection matrix for upside-down output
//		Matrix4x4 proj = mainCam.projectionMatrix;
//		//proj[1, 1] *= -1; // Primary Y-inversion
//		proj = Matrix4x4.Scale(new Vector3(-1, 1, 1)) * proj;
//		mirrorCam.projectionMatrix = proj;

//		GL.invertCulling = true;
//		GL.InvalidateState();// I even tried to invalidate the state but it has no effect
//		mirrorCam.Render();
//		GL.invertCulling = false;
//	}

//	// Adapted from your MatrixReflect function
//	Matrix4x4 MatrixReflect(Vector4 plane)
//	{
//		Matrix4x4 reflectionMat = Matrix4x4.identity;

//		float a = plane.x;
//		float b = plane.y;
//		float c = plane.z;
//		float d = plane.w;

//		float length = Mathf.Sqrt(a * a + b * b + c * c);
//		if (length > 0)
//		{
//			a /= length;
//			b /= length;
//			c /= length;
//			d /= length;
//		}

//		reflectionMat[0, 0] = -2 * a * a + 1;
//		reflectionMat[0, 1] = -2 * a * b;
//		reflectionMat[0, 2] = -2 * a * c;
//		reflectionMat[0, 3] = -2 * a * d;

//		reflectionMat[1, 0] = -2 * b * a;
//		reflectionMat[1, 1] = -2 * b * b + 1;
//		reflectionMat[1, 2] = -2 * b * c;
//		reflectionMat[1, 3] = -2 * b * d;

//		reflectionMat[2, 0] = -2 * c * a;
//		reflectionMat[2, 1] = -2 * c * b;
//		reflectionMat[2, 2] = -2 * c * c + 1;
//		reflectionMat[2, 3] = -2 * c * d;

//		reflectionMat[3, 0] = 0;
//		reflectionMat[3, 1] = 0;
//		reflectionMat[3, 2] = 0;
//		reflectionMat[3, 3] = 1;

//		return reflectionMat;
//	}

//	void OnDestroy()
//	{
//		if (mirrorCam != null)
//			Destroy(mirrorCam.gameObject);
//		if (debugRT != null)
//			Destroy(debugRT);
//	}
//}