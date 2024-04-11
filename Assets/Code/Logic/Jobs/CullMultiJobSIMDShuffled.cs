using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SphereCulling
{
	[BurstCompile]
	public struct CullMultiJobSIMDShuffled : IJobParallelForBatch
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

			for (int i = 0; i < count; i += 4)
			{
				var idx = startIndex + i;
				var xs = Xs.ReinterpretLoad<float4>(idx);
				var ys = Ys.ReinterpretLoad<float4>(idx);
				var zs = Zs.ReinterpretLoad<float4>(idx);
				// var radii = Radii.ReinterpretLoad<float4>(idx);
				const float radii = Constants.SphereRadius;

				// Tests 4 Objects against 6 planes
				bool4 mask = p0.x * xs + p0.y * ys + p0.z * zs + p0.w + radii > 0.0f &
				             p1.x * xs + p1.y * ys + p1.z * zs + p1.w + radii > 0.0f &
				             p2.x * xs + p2.y * ys + p2.z * zs + p2.w + radii > 0.0f &
				             p3.x * xs + p3.y * ys + p3.z * zs + p3.w + radii > 0.0f &
				             p4.x * xs + p4.y * ys + p4.z * zs + p4.w + radii > 0.0f &
				             p5.x * xs + p5.y * ys + p5.z * zs + p5.w + radii > 0.0f;

				// Normally you'd do reinterpretStore to visibility mask, but we're doing culling and adding at the same time.
				if (mask.x)
				{
					var pos = new float3(xs[0], ys[0], zs[0]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (mask.y)
				{
					var pos = new float3(xs[1], ys[1], zs[1]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (mask.z)
				{
					var pos = new float3(xs[2], ys[2], zs[2]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}

				if (mask.w)
				{
					var pos = new float3(xs[3], ys[3], zs[3]);
					Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
				}
			}
		}
	}
}