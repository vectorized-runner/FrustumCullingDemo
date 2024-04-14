using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FrustumCulling
{
	[BurstCompile]
	public struct CullAABBParallelJobBurstSIMD : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[ReadOnly]
		public NativeArray<AABB> AABBs;

		[ReadOnly]
		[DeallocateOnJobCompletion]
		public NativeArray<PlanePacket4> Planes;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int index)
		{
			if (Intersect2NoPartial(Planes, AABBs[index]))
			{
				Output.AddNoResize(float4x4.TRS(Positions[index], quaternion.identity, new float3(1, 1, 1)));
			}
		}

		/// <summary>
		/// Code from Unity Hybrid Renderer package
		/// </summary>
		/// <param name="cullingPlanePackets"></param>
		/// <param name="a"></param>
		/// <returns></returns>
		public static bool Intersect2NoPartial(NativeArray<PlanePacket4> cullingPlanePackets, AABB a)
		{
			float4 mx = a.Center.xxxx;
			float4 my = a.Center.yyyy;
			float4 mz = a.Center.zzzz;

			float4 ex = a.Extents.xxxx;
			float4 ey = a.Extents.yyyy;
			float4 ez = a.Extents.zzzz;

			int4 masks = 0;

			for (int i = 0; i < cullingPlanePackets.Length; i++)
			{
				var p = cullingPlanePackets[i];
				float4 distances = dot4(p.Xs, p.Ys, p.Zs, mx, my, mz) + p.Distances;
				float4 radii = dot4(ex, ey, ez, math.abs(p.Xs), math.abs(p.Ys), math.abs(p.Zs));

				masks += (int4)(distances + radii <= 0);
			}

			int outCount = math.csum(masks);
			return outCount == 0;
		}

		private static float4 dot4(float4 xs, float4 ys, float4 zs, float4 mx, float4 my, float4 mz)
		{
			return xs * mx + ys * my + zs * mz;
		}
	}
}