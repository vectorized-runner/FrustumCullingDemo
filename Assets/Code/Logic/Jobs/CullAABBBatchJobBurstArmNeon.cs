using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Burst.Intrinsics.Arm.Neon;

namespace FrustumCulling
{
	[BurstCompile]
	public unsafe struct CullAABBBatchJobBurstArmNeon : IJobParallelForBatch
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

		[ReadOnly]
		public NativeArray<float> AABBExtentXs;

		[ReadOnly]
		public NativeArray<float> AABBExtentYs;

		[ReadOnly]
		public NativeArray<float> AABBExtentZs;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int startIndex, int count)
		{
			if (!IsNeonSupported)
				return;
			
			Debug.Assert(count % 4 == 0);

			var p0 = Planes[0];
			var p1 = Planes[1];
			var p2 = Planes[2];
			var p3 = Planes[3];
			var p4 = Planes[4];
			var p5 = Planes[5];
			var cxPtr = (float*)AABBCenterXs.GetUnsafeReadOnlyPtr();
			var cyPtr = (float*)AABBCenterYs.GetUnsafeReadOnlyPtr();
			var czPtr = (float*)AABBCenterZs.GetUnsafeReadOnlyPtr();
			var exPtr = (float*)AABBExtentXs.GetUnsafeReadOnlyPtr();
			var eyPtr = (float*)AABBExtentYs.GetUnsafeReadOnlyPtr();
			var ezPtr = (float*)AABBExtentZs.GetUnsafeReadOnlyPtr();
			var zero = vdupq_n_f32(0.0f);
			var results = stackalloc uint[4];

			for (int i = 0; i < count; i += 4)
			{
				var idx = startIndex + i;
				var cxs = vld1q_f32(cxPtr + idx);
				var cys = vld1q_f32(cyPtr + idx);
				var czs = vld1q_f32(czPtr + idx);
				var exs = vld1q_f32(exPtr + idx);
				var eys = vld1q_f32(eyPtr + idx);
				var ezs = vld1q_f32(ezPtr + idx);

				var p0x = vdupq_n_f32(p0.x);
				var p0y = vdupq_n_f32(p0.y);
				var p0z = vdupq_n_f32(p0.z);
				var dist0 = vaddq_f32(
					vaddq_f32(vaddq_f32(vmulq_f32(p0x, cxs), vmulq_f32(p0y, cys)), vmulq_f32(p0z, czs)),
					vdupq_n_f32(p0.w));
				var rad0 = vaddq_f32(vaddq_f32(vmulq_f32(vabsq_f32(p0x), exs), vmulq_f32(vabsq_f32(p0y), eys)),
					vmulq_f32(vabsq_f32(p0z), ezs));
				var sum0 = vaddq_f32(dist0, rad0);
				var res0 = vcgtq_f32(sum0, zero);

				var p1x = vdupq_n_f32(p1.x);
				var p1y = vdupq_n_f32(p1.y);
				var p1z = vdupq_n_f32(p1.z);
				var dist1 = vaddq_f32(
					vaddq_f32(vaddq_f32(vmulq_f32(p1x, cxs), vmulq_f32(p1y, cys)), vmulq_f32(p1z, czs)),
					vdupq_n_f32(p1.w));
				var rad1 = vaddq_f32(vaddq_f32(vmulq_f32(vabsq_f32(p1x), exs), vmulq_f32(vabsq_f32(p1y), eys)),
					vmulq_f32(vabsq_f32(p1z), ezs));
				var sum1 = vaddq_f32(dist1, rad1);
				var res1 = vcgtq_f32(sum1, zero);

				var p2x = vdupq_n_f32(p2.x);
				var p2y = vdupq_n_f32(p2.y);
				var p2z = vdupq_n_f32(p2.z);
				var dist2 = vaddq_f32(
					vaddq_f32(vaddq_f32(vmulq_f32(p2x, cxs), vmulq_f32(p2y, cys)), vmulq_f32(p2z, czs)),
					vdupq_n_f32(p2.w));
				var rad2 = vaddq_f32(vaddq_f32(vmulq_f32(vabsq_f32(p2x), exs), vmulq_f32(vabsq_f32(p2y), eys)),
					vmulq_f32(vabsq_f32(p2z), ezs));
				var sum2 = vaddq_f32(dist2, rad2);
				var res2 = vcgtq_f32(sum2, zero);

				var p3x = vdupq_n_f32(p3.x);
				var p3y = vdupq_n_f32(p3.y);
				var p3z = vdupq_n_f32(p3.z);
				var dist3 = vaddq_f32(
					vaddq_f32(vaddq_f32(vmulq_f32(p3x, cxs), vmulq_f32(p3y, cys)), vmulq_f32(p3z, czs)),
					vdupq_n_f32(p3.w));
				var rad3 = vaddq_f32(vaddq_f32(vmulq_f32(vabsq_f32(p3x), exs), vmulq_f32(vabsq_f32(p3y), eys)),
					vmulq_f32(vabsq_f32(p3z), ezs));
				var sum3 = vaddq_f32(dist3, rad3);
				var res3 = vcgtq_f32(sum3, zero);

				var p4x = vdupq_n_f32(p4.x);
				var p4y = vdupq_n_f32(p4.y);
				var p4z = vdupq_n_f32(p4.z);
				var dist4 = vaddq_f32(
					vaddq_f32(vaddq_f32(vmulq_f32(p4x, cxs), vmulq_f32(p4y, cys)), vmulq_f32(p4z, czs)),
					vdupq_n_f32(p4.w));
				var rad4 = vaddq_f32(vaddq_f32(vmulq_f32(vabsq_f32(p4x), exs), vmulq_f32(vabsq_f32(p4y), eys)),
					vmulq_f32(vabsq_f32(p4z), ezs));
				var sum4 = vaddq_f32(dist4, rad4);
				var res4 = vcgtq_f32(sum4, zero);

				var p5x = vdupq_n_f32(p5.x);
				var p5y = vdupq_n_f32(p5.y);
				var p5z = vdupq_n_f32(p5.z);
				var dist5 = vaddq_f32(
					vaddq_f32(vaddq_f32(vmulq_f32(p5x, cxs), vmulq_f32(p5y, cys)), vmulq_f32(p5z, czs)),
					vdupq_n_f32(p5.w));
				var rad5 = vaddq_f32(vaddq_f32(vmulq_f32(vabsq_f32(p5x), exs), vmulq_f32(vabsq_f32(p5y), eys)),
					vmulq_f32(vabsq_f32(p5z), ezs));
				var sum5 = vaddq_f32(dist5, rad5);
				var res5 = vcgtq_f32(sum5, zero);

				var res = vandq_u32(vandq_u32(vandq_u32(vandq_u32(vandq_u32(res0, res1), res2), res3), res4), res5);

				vst1q_u32(results, res);

				// Normally you'd do reinterpretStore to visibility mask, but we're doing culling and adding at the same time.
				if (results[0] != 0)
				{
					var pos = Positions[idx];
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (results[1] != 0)
				{
					var pos = Positions[idx + 1];
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (results[2] != 0)
				{
					var pos = Positions[idx + 2];
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (results[3] != 0)
				{
					var pos = Positions[idx + 3];
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}
			}
		}
	}
}