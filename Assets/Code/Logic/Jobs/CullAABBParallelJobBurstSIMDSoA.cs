using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FrustumCulling
{
	[BurstCompile]
	public struct CullAABBParallelJobBurstSIMDSoA : IJobParallelForBatch
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[ReadOnly]
		public NativeArray<float4> Planes;

		[ReadOnly]
		public NativeArray<float> AABBCenterXs;

		[ReadOnly]
		public NativeArray<float> AABBCenterYs;

		[ReadOnly]
		public NativeArray<float> AABBCenterZs;
		
		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int startIndex, int count)
		{
			Debug.Assert(count % 4 == 0);
			
			var p0 = Planes[0];
			var p1 = Planes[1];
			var p2 = Planes[2];
			var p3 = Planes[3];
			var p4 = Planes[4];
			var p5 = Planes[5];

			for (int i = 0; i < count; i += 4)
			{
				var idx = startIndex + i;
				var cxs = AABBCenterXs.ReinterpretLoad<float4>(idx);
				var cys = AABBCenterYs.ReinterpretLoad<float4>(idx);
				var czs = AABBCenterZs.ReinterpretLoad<float4>(idx);

				// Tests 4 AABB against 6 Planes
				bool4 mask = p0.x * cxs + p0.y * cys + p0.z * czs + p0.w + radii > 0.0f &
				             p1.x * cxs + p1.y * cys + p1.z * czs + p1.w + radii > 0.0f &
				             p2.x * cxs + p2.y * cys + p2.z * czs + p2.w + radii > 0.0f &
				             p3.x * cxs + p3.y * cys + p3.z * czs + p3.w + radii > 0.0f &
				             p4.x * cxs + p4.y * cys + p4.z * czs + p4.w + radii > 0.0f &
				             p5.x * cxs + p5.y * cys + p5.z * czs + p5.w + radii > 0.0f;

				// Normally you'd do reinterpretStore to visibility mask, but we're doing culling and adding at the same time.
				if (mask.x)
				{
					var pos = Positions[idx];
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (mask.y)
				{
					var pos = Positions[idx + 1];
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (mask.z)
				{
					var pos = Positions[idx + 2];
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (mask.w)
				{
					var pos = Positions[idx + 3];
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}
			}
		}
	}
}