namespace SphereCulling
{
	public enum SphereCullingMode
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
	}
}