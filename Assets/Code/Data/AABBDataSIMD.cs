using Unity.Collections;
using Unity.Mathematics;

namespace FrustumCulling
{
	public struct AABBDataSIMD
	{
		public NativeArray<float3> Positions;
		public NativeArray<float> AABBCenterXs;
		public NativeArray<float> AABBCenterYs;
		public NativeArray<float> AABBCenterZs;
		public NativeArray<float> AABBExtentXs;
		public NativeArray<float> AABBExtentYs;
		public NativeArray<float> AABBExtentZs;

		public bool IsCreated;

		public void Init(int count)
		{
			Dispose();
			Positions = new NativeArray<float3>(count, Allocator.Persistent);
			AABBCenterXs = new NativeArray<float>(count, Allocator.Persistent);
			AABBCenterYs = new NativeArray<float>(count, Allocator.Persistent);
			AABBCenterZs = new NativeArray<float>(count, Allocator.Persistent);
			AABBExtentXs = new NativeArray<float>(count, Allocator.Persistent);
			AABBExtentYs = new NativeArray<float>(count, Allocator.Persistent);
			AABBExtentZs = new NativeArray<float>(count, Allocator.Persistent);
			IsCreated = true;
		}

		public void Dispose()
		{
			if (!IsCreated)
				return;

			Positions.Dispose();
			AABBCenterXs.Dispose();
			AABBCenterYs.Dispose();
			AABBCenterZs.Dispose();
			AABBExtentXs.Dispose();
			AABBExtentYs.Dispose();
			AABBExtentZs.Dispose();
		}
	}
}