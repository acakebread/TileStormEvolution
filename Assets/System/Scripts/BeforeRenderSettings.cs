using System;
using UnityEngine;
using UnityEngine.Rendering;

public class BeforeRenderSettings : MonoBehaviour
{
	public Action<CommandBuffer> BeforeRender;
	public Action<CommandBuffer> AfterRender;
}
