using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SphereCulling
{
	[BurstCompile]
	public struct CullMultiJobSIMD : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[DeallocateOnJobCompletion]
		[ReadOnly] 
		public NativeArray<PlanePacket4> PlanePackets;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int index)
		{
			var pos = Positions[index];
			var p0 = PlanePackets[0];
			var p1 = PlanePackets[1];
            
			// Inside: Radius + Dot(Normal, Point) + Distance > 0
			bool4 masks = (p0.Xs * pos.x + p0.Ys * pos.y + p0.Zs * pos.z + p0.Distances + Constants.SphereRadius > 0) &
			              (p1.Xs * pos.x + p1.Ys * pos.y + p1.Zs * pos.z + p1.Distances + Constants.SphereRadius > 0);

			if (math.all(masks))
			{
				Output.AddNoResize(float4x4.TRS(pos, quaternion.identity, new float3(1, 1, 1)));
			}

			// Shit, this requires batch job

			// public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			// {
			//     // Get arrays of the components in this chunk that we're interested in.
			//     // Reinterpret the data as floats to make it easier to manipulate for packing
			//     var chunkTransforms = chunk.GetNativeArray(ref LocalToWorldTypeHandle).AsReadOnly();
			//     var chunkRadii = chunk.GetNativeArray(ref RadiusTypeHandle).Reinterpret<float>();
			//     var chunkVis = chunk.GetNativeArray(ref VisibilityTypeHandle);
			//    
			//     var p0 = FP[0];
			//     var p1 = FP[1];
			//     var p2 = FP[2];
			//     var p3 = FP[3];
			//     var p4 = FP[4];
			//     var p5 = FP[5];
			//
			//     for (var i = 0; chunk.Count - i >= 4; i += 4)
			//     {
			//         // Load 4 float3 positions, then "shuffle" them into vertical Xs, Ys and Zs 
			//         var a = chunkTransforms[i].Position;
			//         var b = chunkTransforms[i+1].Position;
			//         var c = chunkTransforms[i+2].Position;
			//         var d = chunkTransforms[i+3].Position;
			//         var Xs = new float4(a.x, b.x, c.x, d.x);
			//         var Ys = new float4(a.y, b.y, c.y, d.y);
			//         var Zs = new float4(a.z, b.z, c.z, d.z);
			//        
			//         // Grab 4 radii in a single float4 
			//         var Radii = chunkRadii.ReinterpretLoad<float4>(i);
			//        
			//         // Test each of the 6 planes against the 4 shuffled spheres
			//         bool4 mask =
			//             p0.x * Xs + p0.y * Ys + p0.z * Zs + p0.w + Radii > 0.0f &
			//             p1.x * Xs + p1.y * Ys + p1.z * Zs + p1.w + Radii > 0.0f &
			//             p2.x * Xs + p2.y * Ys + p2.z * Zs + p2.w + Radii > 0.0f &
			//             p3.x * Xs + p3.y * Ys + p3.z * Zs + p3.w + Radii > 0.0f &
			//             p4.x * Xs + p4.y * Ys + p4.z * Zs + p4.w + Radii > 0.0f &
			//             p5.x * Xs + p5.y * Ys + p5.z * Zs + p5.w + Radii > 0.0f;
			//
			//         chunkVis.ReinterpretStore(i, new int4(mask));
			//     }
			//
			//     // In case the number of entities in this chunk isn't neatly divisible by 4, cull the last few spheres individually
			//     for (var i = (chunk.Count >> 2) << 2; i < chunk.Count; ++i)
			//     {
			//         var pos = chunkTransforms[i].Position;
			//         var radius = chunkRadii[i];
			//
			//         int visible =
			//             (math.dot(p0.xyz, pos) + p0.w + radius > 0.0f &&
			//              math.dot(p1.xyz, pos) + p1.w + radius > 0.0f &&
			//              math.dot(p2.xyz, pos) + p2.w + radius > 0.0f &&
			//              math.dot(p3.xyz, pos) + p3.w + radius > 0.0f &&
			//              math.dot(p4.xyz, pos) + p4.w + radius > 0.0f &&
			//              math.dot(p5.xyz, pos) + p5.w + radius > 0.0f) ? 1 : 0;
			//        
			//         chunkVis[i] = new SphereVisible { Value = visible };
			//     }
		}
	}
}