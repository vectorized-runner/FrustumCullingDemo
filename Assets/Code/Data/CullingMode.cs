namespace FrustumCulling
{
	public enum CullingMode
	{
		Uninitialized,
		NoCull,
		// Sphere Bounds
		Mono,
		SingleJob,
		ParallelJob,
		ParallelJobBurst,
		ParallelJobBurstBranchless,
		BatchJobBurstBranchless,
		ParallelJobBurstSIMD,
		ParallelJobBurstSIMDSoA,
		ParallelJobBurstSSE,
		ParallelJobBurstArmNeon,
		// AABB Bounds (only want to see SIMD comparison, other cases not included)
		AABBCullSIMD,
		AABBCullSIMDSoA,
		AABBCullArmNeon,
	}
}