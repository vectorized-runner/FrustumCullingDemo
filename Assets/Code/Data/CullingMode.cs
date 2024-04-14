namespace FrustumCulling
{
	public enum CullingMode
	{
		Uninitialized,
		NoCull,
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
	}
}