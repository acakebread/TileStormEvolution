using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class WindController : MonoBehaviour
	{
		public enum DirectionPattern
		{
			Circular, // Rotates continuously around Y-axis
			Oscillating // Oscillates within an angle range
		}

		[SerializeField] private Vector3 baseWaveDirection = Vector3.right; // Initial direction (world space)
		[SerializeField] private DirectionPattern directionPattern = DirectionPattern.Oscillating; // Pattern for direction change
		[SerializeField] private float directionChangeSpeed = 0.1f; // Speed of direction rotation (radians per second)
		[SerializeField] private float oscillationRange = 90f; // Max angle (degrees) for oscillating pattern
		[SerializeField] private float waveSpeed = 0.2f; // Speed of wave propagation
		[SerializeField] private float waveAmplitude = 0.3f; // Max sway displacement during gusts
		[SerializeField] private float waveFrequency = 0.08f; // Spatial frequency of the wave
		[SerializeField] private float minAmplitude = 0.02f; // Min sway displacement during calm periods
		[SerializeField] private float gustFrequency = 0.1f; // Frequency of gust cycles
		[SerializeField] private float gustSharpness = 1f; // Controls sharpness of gust transitions

		private List<(MorphGeomSway sway, Vector3 position)> swayComponents = new();

		// Initialize with MorphGeomSway components and their world positions
		public void Initialize(IEnumerable<(MorphGeomSway sway, Vector3 position)> components)
		{
			swayComponents.Clear();
			swayComponents.AddRange(components);
			foreach (var (sway, _) in swayComponents)
			{
				sway.SetSwayVector(Vector3.zero); // Reset to ensure external control
				sway.useExternalSwayVector = true;
			}
		}

		private void Update()
		{
			// Calculate current wave direction
			Vector3 currentWaveDirection = CalculateWaveDirection();

			// Calculate current amplitude using Perlin noise for gusts
			float noiseInput = Time.time * gustFrequency;
			float noise = Mathf.PerlinNoise(noiseInput, 0f); // Range [0, 1]
			float currentAmplitude = Mathf.Lerp(minAmplitude, waveAmplitude, Mathf.Pow(noise, gustSharpness));

			foreach (var (sway, position) in swayComponents)
			{
				if (sway == null) continue; // Skip destroyed components

				// Project position along current wave direction
				float projection = Vector3.Dot(position, currentWaveDirection.normalized);
				// Calculate phase for wave effect
				float phase = (projection * waveFrequency + Time.time * waveSpeed) * 2f * Mathf.PI;
				float swayMagnitude = Mathf.Sin(phase) * currentAmplitude;

				// Apply sway vector (in world space, aligned with current wave direction)
				Vector3 swayVector = currentWaveDirection.normalized * swayMagnitude;
				sway.SetSwayVector(swayVector);
			}
		}

		private Vector3 CalculateWaveDirection()
		{
			float angle;
			if (directionPattern == DirectionPattern.Circular)
			{
				// Continuous rotation around Y-axis
				angle = Time.time * directionChangeSpeed;
			}
			else // Oscillating
			{
				// Oscillate within ±oscillationRange degrees
				angle = Mathf.Sin(Time.time * directionChangeSpeed) * oscillationRange * Mathf.Deg2Rad;
			}

			// Rotate baseWaveDirection around Y-axis
			float cosAngle = Mathf.Cos(angle);
			float sinAngle = Mathf.Sin(angle);
			Vector3 direction = new Vector3(
				baseWaveDirection.x * cosAngle - baseWaveDirection.z * sinAngle,
				0f, // Keep direction in XZ plane (world space)
				baseWaveDirection.x * sinAngle + baseWaveDirection.z * cosAngle
			);

			return direction.normalized;
		}

		private void OnValidate()
		{
			waveAmplitude = Mathf.Max(0f, waveAmplitude);
			minAmplitude = Mathf.Max(0f, minAmplitude);
			waveSpeed = Mathf.Max(0f, waveSpeed);
			waveFrequency = Mathf.Max(0f, waveFrequency);
			directionChangeSpeed = Mathf.Max(0f, directionChangeSpeed);
			oscillationRange = Mathf.Clamp(oscillationRange, 0f, 180f);
			gustFrequency = Mathf.Max(0f, gustFrequency);
			gustSharpness = Mathf.Max(0.1f, gustSharpness);
		}

		// Visualize current wave direction
		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.green;
			Vector3 currentDirection = CalculateWaveDirection();
			Gizmos.DrawRay(transform.position, currentDirection.normalized * 2f);
		}
	}
}