namespace FrustumCulling
{
	public enum CullingMode
	{
		Uninitialized,
		NoCull,
		CullMono,
		CullSingleJob,
		CullMultiJob,
		CullMultiJobBurst,
		CullJobsBurstBranchless,
		CullJobsBurstBranchlessBatch,
		CullJobsBurstSIMD,
		CullJobsBurstSIMDShuffled,
		CullJobsBurstExplicitSSE,
		CullJobsBurstExplicitArmNeon,
	}
}