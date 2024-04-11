using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SphereCulling
{
	[BurstCompile]
	public struct CullMultiJobBurstBranchless : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[ReadOnly]
		public NativeArray<DotsPlane> Planes;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int index)
		{
			var position = Positions[index];

			// Inside: R + N + D > 0
			var result =
				Planes[0].Distance + Constants.SphereRadius + math.dot(position, Planes[0].Normal) > 0 &&
				Planes[1].Distance + Constants.SphereRadius + math.dot(position, Planes[1].Normal) > 0 &&
				Planes[2].Distance + Constants.SphereRadius + math.dot(position, Planes[2].Normal) > 0 &&
				Planes[3].Distance + Constants.SphereRadius + math.dot(position, Planes[3].Normal) > 0 &&
				Planes[4].Distance + Constants.SphereRadius + math.dot(position, Planes[4].Normal) > 0 &&
				Planes[5].Distance + Constants.SphereRadius + math.dot(position, Planes[5].Normal) > 0;

			if (result)
			{
				Output.AddNoResize(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
			}
		}
	}
}