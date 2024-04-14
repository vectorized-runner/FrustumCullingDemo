using Unity.Collections;
using Unity.Mathematics;

namespace FrustumCulling
{
	public struct SphereDataUnmanaged
	{
		public NativeArray<float3> Positions;
		public NativeArray<float> Radii;
		public bool IsCreated;

		public void Init(int count)
		{
			Dispose();
			Positions = new NativeArray<float3>(count, Allocator.Persistent);
			Radii = new NativeArray<float>(count, Allocator.Persistent);
			IsCreated = true;
		}

		public void Dispose()
		{
			if (!IsCreated)
				return;

			Positions.Dispose();
			Radii.Dispose();
		}
	}
}