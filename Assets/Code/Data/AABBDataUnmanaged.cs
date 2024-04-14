using Unity.Collections;
using Unity.Mathematics;

namespace FrustumCulling
{
	public struct AABBDataUnmanaged
	{
		public NativeArray<float3> Positions;
		public NativeArray<AABB> AABBs;
		public bool IsCreated;

		public void Init(int count)
		{
			Dispose();
			Positions = new NativeArray<float3>(count, Allocator.Persistent);
			AABBs = new NativeArray<AABB>(count, Allocator.Persistent);
			IsCreated = true;
		}

		public void Dispose()
		{
			if (!IsCreated)
				return;

			Positions.Dispose();
			AABBs.Dispose();
		}
	}
}