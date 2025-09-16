using System;
using UnityEngine;
using UnityEngine.Rendering;

public class CommandBufferSettings : MonoBehaviour
{
	public Action<CommandBuffer> OnBeforeRender;
	public Action<CommandBuffer> OnAfterRender;
}
