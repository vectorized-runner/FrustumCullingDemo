using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Burst.Intrinsics.Arm.Neon;

namespace SphereCulling
{
	[BurstCompile]
	public unsafe struct CullMultiJobSIMDExplicitNeon : IJobParallelForBatch
	{
		[ReadOnly]
		public NativeArray<float> Xs;

		[ReadOnly]
		public NativeArray<float> Ys;

		[ReadOnly]
		public NativeArray<float> Zs;

		// [ReadOnly]
		// public NativeArray<float> Radii;

		[ReadOnly]
		public NativeArray<float4> Planes;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int startIndex, int count)
		{
			// Count not divisible by 4 not handled here
			Debug.Assert(count % 4 == 0);

			var p0 = Planes[0];
			var p1 = Planes[1];
			var p2 = Planes[2];
			var p3 = Planes[3];
			var p4 = Planes[4];
			var p5 = Planes[5];
			var xPtr = (float*)Xs.GetUnsafeReadOnlyPtr();
			var yPtr = (float*)Ys.GetUnsafeReadOnlyPtr();
			var zPtr = (float*)Zs.GetUnsafeReadOnlyPtr();
			var results = stackalloc uint[4];

			for (int i = 0; i < count; i += 4)
			{
				var idx = startIndex + i;
				var xs = vld1q_f32(xPtr + idx);
				var ys = vld1q_f32(yPtr + idx);
				var zs = vld1q_f32(zPtr + idx);
				var zero = vdupq_n_f32(0.0f);
				var radii = vdupq_n_f32(Constants.SphereRadius);
				var res0 = vcgtq_f32(
					vaddq_f32(
						vaddq_f32(
							vaddq_f32(vaddq_f32(vmulq_f32(vdupq_n_f32(p0.x), xs), vmulq_f32(vdupq_n_f32(p0.y), ys)),
								vmulq_f32(vdupq_n_f32(p0.z), zs)), vdupq_n_f32(p0.w)), radii), zero);
				var res1 = vcgtq_f32(
					vaddq_f32(
						vaddq_f32(
							vaddq_f32(vaddq_f32(vmulq_f32(vdupq_n_f32(p1.x), xs), vmulq_f32(vdupq_n_f32(p1.y), ys)),
								vmulq_f32(vdupq_n_f32(p1.z), zs)), vdupq_n_f32(p1.w)), radii), zero);
				var res2 = vcgtq_f32(
					vaddq_f32(
						vaddq_f32(
							vaddq_f32(vaddq_f32(vmulq_f32(vdupq_n_f32(p2.x), xs), vmulq_f32(vdupq_n_f32(p2.y), ys)),
								vmulq_f32(vdupq_n_f32(p2.z), zs)), vdupq_n_f32(p2.w)), radii), zero);
				var res3 = vcgtq_f32(
					vaddq_f32(
						vaddq_f32(
							vaddq_f32(vaddq_f32(vmulq_f32(vdupq_n_f32(p3.x), xs), vmulq_f32(vdupq_n_f32(p3.y), ys)),
								vmulq_f32(vdupq_n_f32(p3.z), zs)), vdupq_n_f32(p3.w)), radii), zero);
				var res4 = vcgtq_f32(
					vaddq_f32(
						vaddq_f32(
							vaddq_f32(vaddq_f32(vmulq_f32(vdupq_n_f32(p4.x), xs), vmulq_f32(vdupq_n_f32(p4.y), ys)),
								vmulq_f32(vdupq_n_f32(p4.z), zs)), vdupq_n_f32(p4.w)), radii), zero);
				var res5 = vcgtq_f32(
					vaddq_f32(
						vaddq_f32(
							vaddq_f32(vaddq_f32(vmulq_f32(vdupq_n_f32(p5.x), xs), vmulq_f32(vdupq_n_f32(p5.y), ys)),
								vmulq_f32(vdupq_n_f32(p5.z), zs)), vdupq_n_f32(p5.w)), radii), zero);

				// vcgtq_f32: works on float32x4, returns 128-bit vector mask, 0xFFFFFFFF or 0x00000000 for each 4 values.
				// Use integer representation to apply bitwise
				var res = vandq_u32(vandq_u32(vandq_u32(vandq_u32(vandq_u32(res0, res1), res2), res3), res4), res5);

				vst1q_u32(results, res);

				if (results[0] != 0)
				{
					var pos = new float3(xPtr[idx + 0], yPtr[idx + 0], zPtr[idx + 0]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (results[1] != 0)
				{
					var pos = new float3(xPtr[idx + 1], yPtr[idx + 1], zPtr[idx + 1]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (results[2] != 0)
				{
					var pos = new float3(xPtr[idx + 2], yPtr[idx + 2], zPtr[idx + 2]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (results[3] != 0)
				{
					var pos = new float3(xPtr[idx + 3], yPtr[idx + 3], zPtr[idx + 3]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}
			}
		}
	}
}