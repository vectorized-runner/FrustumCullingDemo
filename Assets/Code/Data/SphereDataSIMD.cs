using Unity.Collections;

namespace FrustumCulling
{
	public struct SphereDataSIMD
	{
		public NativeArray<float> Xs;
		public NativeArray<float> Ys;
		public NativeArray<float> Zs;
		public bool IsCreated;

		public void Init(int count)
		{
			Dispose();
			Xs = new NativeArray<float>(count, Allocator.Persistent);
			Ys = new NativeArray<float>(count, Allocator.Persistent);
			Zs = new NativeArray<float>(count, Allocator.Persistent);
			IsCreated = true;
		}

		public void Dispose()
		{
			if (!IsCreated)
				return;

			Xs.Dispose();
			Ys.Dispose();
			Zs.Dispose();
		}
	}
}