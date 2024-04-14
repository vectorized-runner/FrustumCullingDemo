using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FrustumCulling
{
	[BurstCompile]
	public struct CullMultiJobBurstBranchless : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[ReadOnly]
		public NativeArray<Plane> Planes;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int index)
		{
			var position = Positions[index];

			// Inside: R + N + D > 0
			var result =
				Planes[0].distance + Constants.SphereRadius + math.dot(position, Planes[0].normal) > 0 &&
				Planes[1].distance + Constants.SphereRadius + math.dot(position, Planes[1].normal) > 0 &&
				Planes[2].distance + Constants.SphereRadius + math.dot(position, Planes[2].normal) > 0 &&
				Planes[3].distance + Constants.SphereRadius + math.dot(position, Planes[3].normal) > 0 &&
				Planes[4].distance + Constants.SphereRadius + math.dot(position, Planes[4].normal) > 0 &&
				Planes[5].distance + Constants.SphereRadius + math.dot(position, Planes[5].normal) > 0;

			if (result)
			{
				Output.AddNoResize(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
			}
		}
	}
}