using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FrustumCulling
{
	[BurstCompile]
	public unsafe struct CullBatchJobBurstBranchless : IJobParallelForBatch
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[ReadOnly]
		public NativeArray<Plane> Planes;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int startIndex, int count)
		{
			var renderIndices = new UnsafeList<int>(count, Allocator.Temp);

			for (int i = 0; i < count; i++)
			{
				var idx = startIndex + i;
				var position = Positions[idx];

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
					renderIndices.Add(idx);
				}
			}

			// Optimizations: Single interlocked operation, collecting indices then accessing the elements
			var renderCount = renderIndices.Length;
			var newLength = Interlocked.Add(ref Output.ListData->m_length, renderCount);
			var basePtr = Output.ListData->Ptr + newLength - renderCount;

			for (int i = 0; i < renderCount; i++)
			{
				*(basePtr + i) = float4x4.TRS(Positions[renderIndices[i]], quaternion.identity, new float3(1, 1, 1));
			}
		}
	}
}