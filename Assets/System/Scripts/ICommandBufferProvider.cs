using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public interface ICommandBufferProvider
{
	bool HasCommands(RenderPassEvent evt);
	void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera);
}