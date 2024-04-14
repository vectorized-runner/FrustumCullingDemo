using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FrustumCulling
{
	public struct CullParallelJob : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[ReadOnly]
		public NativeArray<Plane> CameraPlanes;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int index)
		{
			var position = Positions[index];
			if (FrustumCullHelper.Cull(position, CameraPlanes))
			{
				Output.AddNoResize(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
			}
		}
	}
}