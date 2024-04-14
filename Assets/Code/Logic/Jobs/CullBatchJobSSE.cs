using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Sse;
using static Unity.Burst.Intrinsics.X86.Sse2;

namespace FrustumCulling
{
	[BurstCompile]
	public unsafe struct CullBatchJobSSE : IJobParallelForBatch
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
			if (!IsSse2Supported)
			{
				Debug.LogError("SSE2 isn't supported on this device.");
				return;
			}
			
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
			var results = stackalloc float[4];

			for (int i = 0; i < count; i += 4)
			{
				var idx = startIndex + i;
				// v128: 128-bit, 32-byte, 4 floats
				var xs = load_ps(xPtr + idx);
				var ys = load_ps(yPtr + idx);
				var zs = load_ps(zPtr + idx);
				var zero = setzero_ps();
				var radii = set_ps1(Constants.SphereRadius);
				var res0 = cmpgt_ps(
					add_ps(
						add_ps(
							add_ps(add_ps(mul_ps(set_ps1(p0.x), xs), mul_ps(set_ps1(p0.y), ys)),
								mul_ps(set_ps1(p0.z), zs)), set_ps1(p0.w)), radii), zero);
				var res1 = cmpgt_ps(
					add_ps(
						add_ps(
							add_ps(add_ps(mul_ps(set_ps1(p1.x), xs), mul_ps(set_ps1(p1.y), ys)),
								mul_ps(set_ps1(p1.z), zs)), set_ps1(p1.w)), radii), zero);
				var res2 = cmpgt_ps(
					add_ps(
						add_ps(
							add_ps(add_ps(mul_ps(set_ps1(p2.x), xs), mul_ps(set_ps1(p2.y), ys)),
								mul_ps(set_ps1(p2.z), zs)), set_ps1(p2.w)), radii), zero);
				var res3 = cmpgt_ps(
					add_ps(
						add_ps(
							add_ps(add_ps(mul_ps(set_ps1(p3.x), xs), mul_ps(set_ps1(p3.y), ys)),
								mul_ps(set_ps1(p3.z), zs)), set_ps1(p3.w)), radii), zero);
				var res4 = cmpgt_ps(
					add_ps(
						add_ps(
							add_ps(add_ps(mul_ps(set_ps1(p4.x), xs), mul_ps(set_ps1(p4.y), ys)),
								mul_ps(set_ps1(p4.z), zs)), set_ps1(p4.w)), radii), zero);
				var res5 = cmpgt_ps(
					add_ps(
						add_ps(
							add_ps(add_ps(mul_ps(set_ps1(p5.x), xs), mul_ps(set_ps1(p5.y), ys)),
								mul_ps(set_ps1(p5.z), zs)), set_ps1(p5.w)), radii), zero);
				var res = and_ps(and_ps(and_ps(and_ps(and_ps(res0, res1), res2), res3), res4), res5);

				storeu_ps(results, res);

				if (results[0] > 0f)
				{
					var pos = new float3(xPtr[idx + 0], yPtr[idx + 0], zPtr[idx + 0]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (results[1] > 0f)
				{
					var pos = new float3(xPtr[idx + 1], yPtr[idx + 1], zPtr[idx + 1]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (results[2] > 0f)
				{
					var pos = new float3(xPtr[idx + 2], yPtr[idx + 2], zPtr[idx + 2]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (results[3] > 0f)
				{
					var pos = new float3(xPtr[idx + 3], yPtr[idx + 3], zPtr[idx + 3]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}
			}
		}
	}
}