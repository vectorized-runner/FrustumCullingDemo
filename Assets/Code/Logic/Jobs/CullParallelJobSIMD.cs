using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FrustumCulling
{
	[BurstCompile]
	public struct CullParallelJobSIMD : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[DeallocateOnJobCompletion]
		[ReadOnly]
		public NativeArray<PlanePacket4> PlanePackets;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int index)
		{
			var pos = Positions[index];
			var p0 = PlanePackets[0];
			var p1 = PlanePackets[1];

			// Inside: Radius + Dot(Normal, Point) + Distance > 0
			bool4 masks = (p0.Xs * pos.x + p0.Ys * pos.y + p0.Zs * pos.z + p0.Distances + Constants.SphereRadius > 0) &
			              (p1.Xs * pos.x + p1.Ys * pos.y + p1.Zs * pos.z + p1.Distances + Constants.SphereRadius > 0);

			if (math.all(masks))
			{
				Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
			}
		}
	}
}