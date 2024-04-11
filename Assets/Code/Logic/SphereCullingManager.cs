using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace SphereCulling
{
	public struct PlanePacket4
	{
		public float4 Xs;
		public float4 Ys;
		public float4 Zs;
		public float4 Distances;
	}

	[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
	public struct CullMultiJobSIMD : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<float3> Positions;

		[ReadOnly]
		public NativeArray<Plane> Planes;

		public NativeList<float4x4>.ParallelWriter Output;

		public void Execute(int index)
		{
			var position = Positions[index];

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
				Output.AddNoResize(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
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

	public class SphereCullingManager : MonoBehaviour
	{
		public int JobBatchCount = 16;
		public SphereDemoConfig DemoConfig;

		private SphereCullingMode _spawnedCullingMode = SphereCullingMode.Uninitialized;
		private int _spawnedCount;
		private SphereDataManaged _dataManaged = new();
		private SphereDataUnmanaged _dataUnmanaged;
		private Random Random;
		private Plane[] _managedPlanes = new Plane[Constants.PlaneCount];
		private Camera _camera;
		private JobHandle _currentJobHandle;
		private NativeList<float4x4> _jobResult;
		private NativeArray<Plane> _nativePlanes;

		private void Start()
		{
			Random = new Random(1);
			_camera = Camera.main;
			_nativePlanes = new NativeArray<Plane>(Constants.PlaneCount, Allocator.Persistent);
		}

		private void RespawnSpheres()
		{
			var count = DemoConfig.SphereCount;
			var spawnRadius = DemoConfig.SpawnRadius;

			switch (DemoConfig.CullingMode)
			{
				case SphereCullingMode.NoCull:
				case SphereCullingMode.CullMono:
				{
					_dataManaged.Clear();

					for (int i = 0; i < count; i++)
					{
						_dataManaged.Positions.Add(Random.NextFloat3Direction() * Random.NextFloat() * spawnRadius);
						_dataManaged.Radii.Add(Constants.SphereRadius);
					}

					break;
				}
				case SphereCullingMode.CullMultiJob:
				case SphereCullingMode.CullSingleJob:
				case SphereCullingMode.CullMultiJobBurst:
				case SphereCullingMode.CullJobsBurstBranchless:
				{
					_dataUnmanaged.Init(count);

					for (int i = 0; i < count; i++)
					{
						_dataUnmanaged.Positions[i] = Random.NextFloat3Direction() * Random.NextFloat() * spawnRadius;
						_dataUnmanaged.Radii[i] = Constants.SphereRadius;
					}

					break;
				}
				case SphereCullingMode.CullJobsBurstSIMD:
				{
					break;
				}
				case SphereCullingMode.Uninitialized:
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool Cull(float3 position)
		{
			for (int i = 0; i < Constants.PlaneCount; i++)
			{
				var plane = _managedPlanes[i];
				var n = math.dot(position, plane.normal);
				// Distance is from plane to origin
				var outside = Constants.SphereRadius + n <= -plane.distance;
				if (outside)
					return false;
			}

			return true;
		}

		private void LateUpdate()
		{
			switch (_spawnedCullingMode)
			{
				case SphereCullingMode.Uninitialized:
				case SphereCullingMode.NoCull:
				case SphereCullingMode.CullMono:
					break;
				case SphereCullingMode.CullSingleJob:
				case SphereCullingMode.CullMultiJobBurst:
				case SphereCullingMode.CullMultiJob:
				case SphereCullingMode.CullJobsBurstBranchless:
				{
					_currentJobHandle.Complete();

					Debug.Assert(_jobResult.Length > 0);

					Graphics.RenderMeshInstanced(new RenderParams(DemoConfig.SphereMaterial), DemoConfig.SphereMesh, 0,
						_jobResult.AsArray().Reinterpret<InstanceData>());

					_jobResult.Dispose();
					break;
				}
				case SphereCullingMode.CullJobsBurstSIMD:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		// TODO: Learn to use this job type. This is it.
		public struct asdf : IJobParallelForBatch
		{
			public void Execute(int startIndex, int count)
			{
				throw new NotImplementedException();
			}
		}

		private void Update()
		{
			if (_spawnedCullingMode != DemoConfig.CullingMode || _spawnedCount != DemoConfig.SphereCount)
			{
				_currentJobHandle.Complete();
				RespawnSpheres();
				_spawnedCullingMode = DemoConfig.CullingMode;
				_spawnedCount = DemoConfig.SphereCount;
			}

			GeometryUtility.CalculateFrustumPlanes(_camera, _managedPlanes);
			var material = DemoConfig.SphereMaterial;
			var mesh = DemoConfig.SphereMesh;
			_nativePlanes.CopyFrom(_managedPlanes);

			var count = DemoConfig.SphereCount;

			switch (_spawnedCullingMode)
			{
				case SphereCullingMode.Uninitialized:
					break;
				case SphereCullingMode.NoCull:
				{
					var matrices = new NativeArray<InstanceData>(count, Allocator.Temp);
					for (int i = 0; i < count; i++)
					{
						var position = _dataManaged.Positions[i];
						var rotation = quaternion.identity;
						var scale = 1.0f;
						matrices[i] = new InstanceData { objectToWorld = float4x4.TRS(position, rotation, scale) };
					}

					Graphics.RenderMeshInstanced(new RenderParams(material), mesh, 0, matrices);
					break;
				}
				case SphereCullingMode.CullMono:
				{
					var matrices = new NativeList<InstanceData>(count, Allocator.Temp);
					for (int i = 0; i < count; i++)
					{
						var position = _dataManaged.Positions[i];
						if (Cull(position))
						{
							var rotation = quaternion.identity;
							var scale = 1.0f;
							matrices.Add(new InstanceData { objectToWorld = float4x4.TRS(position, rotation, scale) });
						}
					}

					Graphics.RenderMeshInstanced(new RenderParams(material), mesh, 0, matrices.AsArray());
					break;
				}
				case SphereCullingMode.CullSingleJob:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullSingleJob
					{
						CameraPlanes = _nativePlanes,
						Output = _jobResult,
						Positions = _dataUnmanaged.Positions,
					}.Schedule();
					break;
				}
				case SphereCullingMode.CullMultiJob:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullMultiJob
					{
						CameraPlanes = _nativePlanes,
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
					}.Schedule(count, JobBatchCount);
					break;
				}
				case SphereCullingMode.CullMultiJobBurst:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullMultiJobBurst
					{
						CameraPlanes = _nativePlanes,
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
					}.Schedule(count, JobBatchCount);
					break;
				}

				case SphereCullingMode.CullJobsBurstBranchless:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullMultiJobBurstBranchless
					{
						Planes = _nativePlanes,
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
					}.Schedule(count, JobBatchCount);
					break;
				}
				case SphereCullingMode.CullJobsBurstSIMD:
					// TODO:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}