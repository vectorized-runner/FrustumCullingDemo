using Unity.Mathematics;

namespace SphereCulling
{
	public struct InstanceData
	{
		// This exact naming is required by Unity
		// ReSharper disable once InconsistentNaming
		public float4x4 objectToWorld;
	}
}