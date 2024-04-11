using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SphereCulling
{
	public struct CullSingleJob : IJob
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[ReadOnly]
		public NativeArray<DotsPlane> CameraPlanes;

		public NativeList<float4x4> Output;

		public void Execute()
		{
			var count = Positions.Length;

			for (int i = 0; i < count; i++)
			{
				var position = Positions[i];
				if (CullUtils.Cull(position, CameraPlanes))
				{
					Output.Add(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
				}
			}
		}
	}
}