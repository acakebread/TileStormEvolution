using MassiveHadronLtd;
using System.Collections;
using UnityEngine;

public class ParticleTest : MonoBehaviour
{
	[SerializeField] private SparkController sparkController;
	[SerializeField][Range(0, 10)] private float delay = 0.1f;
	[SerializeField][Range(1, 128)] private int ct = 1;
	[SerializeField] private bool continuous = true;
	[SerializeField] private Vector3 spawnOffset = Vector3.zero;
	[SerializeField][Range(0, 2)] private float lifetimeVariation = 0.5f;
	[SerializeField] private Vector3 bias = Vector3.zero;

	void Awake()
	{
		if (sparkController == null)
		{
			Debug.LogError("ParticleTest: sparkController is not assigned!");
			enabled = false;
			return;
		}
	}

	IEnumerator Start()
	{
		while (true)
		{
			if (continuous || Input.GetKey(KeyCode.Space))
			{
				for (var n = 0; n < ct; ++n)
				{
					var vel = Random.onUnitSphere * Random.value * 0.5f;
					vel.y *= 2f;
					vel += bias;
					Vector3 worldPos = sparkController.transform.position + spawnOffset;
					sparkController.SpawnSpark(worldPos, vel, lifetimeVariation);
				}
			}
			yield return new WaitForSeconds(delay);
		}
	}
}