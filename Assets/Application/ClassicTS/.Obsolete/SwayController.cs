//Not currently needed

//using UnityEngine;

//namespace ClassicTilestorm
//{
//	public class SwayController : MonoBehaviour
//	{
//		private float sway = 0.1f;
//		private float mod1, mod2;

//		public void UpdateSway(bool isIdle)
//		{
//			mod1 += 7.8f * Time.deltaTime;
//			mod2 += 1.8f * Time.deltaTime;
//			sway = (sway * 99f + (isIdle ? 0.02f : 0.1f)) / 100f;
//			var pitch = sway * Mathf.Sin(mod1) * Mathf.Sin(mod2);
//			var rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
//			transform.localPosition = rotation * new Vector3(0f, 0f, -pitch);
//			transform.localRotation = rotation;
//		}
//	}
//}