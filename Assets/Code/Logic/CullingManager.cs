using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace FrustumCulling
{
	public class CullingManager : MonoBehaviour
	{
		public int JobBatchCount = 32;
		public DemoConfig DemoConfig;

		private CullingMode _spawnedCullingMode = CullingMode.Uninitialized;
		private int _spawnedCount;
		private SphereDataManaged _dataManaged = new();
		private SphereDataUnmanaged _dataUnmanaged;
		private SphereDataSIMD _dataSIMD;
		private Plane[] _managedPlanes = new Plane[Constants.PlaneCount];
		private Camera _camera;
		private JobHandle _currentJobHandle;
		private NativeList<float4x4> _jobResult;
		private NativeArray<Plane> _nativePlanes;
		private Random _random;

		private void Start()
		{
			_camera = Camera.main;
			_nativePlanes = new NativeArray<Plane>(Constants.PlaneCount, Allocator.Persistent);
		}

		private void RespawnSpheres()
		{
			var count = DemoConfig.SphereCount;
			var spawnRadius = DemoConfig.SpawnRadius;
			_random = new Random(1);

			switch (DemoConfig.CullingMode)
			{
				case CullingMode.NoCull:
				case CullingMode.CullMono:
				{
					_dataManaged.Clear();

					for (int i = 0; i < count; i++)
					{
						_dataManaged.Positions.Add(_random.NextFloat3Direction() * _random.NextFloat() * spawnRadius);
						_dataManaged.Radii.Add(Constants.SphereRadius);
					}

					break;
				}
				case CullingMode.CullMultiJob:
				case CullingMode.CullSingleJob:
				case CullingMode.CullMultiJobBurst:
				case CullingMode.CullJobsBurstBranchless:
				case CullingMode.CullJobsBurstBranchlessBatch:
				case CullingMode.CullJobsBurstSIMD:
				{
					_dataUnmanaged.Init(count);

					for (int i = 0; i < count; i++)
					{
						_dataUnmanaged.Positions[i] = _random.NextFloat3Direction() * _random.NextFloat() * spawnRadius;
						_dataUnmanaged.Radii[i] = Constants.SphereRadius;
					}

					break;
				}
				case CullingMode.CullJobsBurstExplicitSSE:
				case CullingMode.CullJobsBurstExplicitArmNeon:
				case CullingMode.CullJobsBurstSIMDShuffled:
				{
					_dataSIMD.Init(count);

					for (int i = 0; i < count; i++)
					{
						var pos = _random.NextFloat3Direction() * _random.NextFloat() * spawnRadius;
						_dataSIMD.Xs[i] = pos.x;
						_dataSIMD.Ys[i] = pos.y;
						_dataSIMD.Zs[i] = pos.z;
					}

					break;
				}
				case CullingMode.Uninitialized:
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
				case CullingMode.Uninitialized:
				case CullingMode.NoCull:
				case CullingMode.CullMono:
					break;
				case CullingMode.CullSingleJob:
				case CullingMode.CullMultiJobBurst:
				case CullingMode.CullMultiJob:
				case CullingMode.CullJobsBurstBranchless:
				case CullingMode.CullJobsBurstBranchlessBatch:
				case CullingMode.CullJobsBurstSIMD:
				case CullingMode.CullJobsBurstExplicitSSE:
				case CullingMode.CullJobsBurstSIMDShuffled:
				case CullingMode.CullJobsBurstExplicitArmNeon:
				{
					_currentJobHandle.Complete();

					Debug.Assert(_jobResult.Length > 0);

					Graphics.RenderMeshInstanced(new RenderParams(DemoConfig.SphereMaterial), DemoConfig.SphereMesh, 0,
						_jobResult.AsArray().Reinterpret<InstanceData>());

					_jobResult.Dispose();
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void Update()
		{
			if (DemoConfig.SphereCount == 0)
			{
				// It becomes 0 when hand-increasing the count, wait
				return;
			}
			
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
				case CullingMode.Uninitialized:
					break;
				case CullingMode.NoCull:
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
				case CullingMode.CullMono:
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
				case CullingMode.CullSingleJob:
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
				case CullingMode.CullMultiJob:
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
				case CullingMode.CullMultiJobBurst:
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

				case CullingMode.CullJobsBurstBranchless:
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
				case CullingMode.CullJobsBurstBranchlessBatch:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullMultiJobBurstBranchlessBatch
					{
						Planes = _nativePlanes,
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
					}.ScheduleBatch(count, JobBatchCount);

					break;
				}
				case CullingMode.CullJobsBurstSIMD:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullMultiJobSIMD
					{
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
						PlanePackets = FrustumCullHelper.CreatePlanePackets()
					}.Schedule(count, JobBatchCount);

					break;
				}
				case CullingMode.CullJobsBurstExplicitSSE:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullMultiJobSIMDExplicitSSE
					{
						Output = _jobResult.AsParallelWriter(),
						Xs = _dataSIMD.Xs,
						Ys = _dataSIMD.Ys,
						Zs = _dataSIMD.Zs,
						Planes = _nativePlanes.Reinterpret<float4>(),
					}.Schedule(count, JobBatchCount);

					break;
				}
				case CullingMode.CullJobsBurstExplicitArmNeon:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullMultiJobSIMDExplicitNeon
					{
						Output = _jobResult.AsParallelWriter(),
						Xs = _dataSIMD.Xs,
						Ys = _dataSIMD.Ys,
						Zs = _dataSIMD.Zs,
						Planes = _nativePlanes.Reinterpret<float4>(),
					}.Schedule(count, JobBatchCount);
					
					break;
				}
				case CullingMode.CullJobsBurstSIMDShuffled:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullMultiJobSIMDShuffled
					{
						Output = _jobResult.AsParallelWriter(),
						Xs = _dataSIMD.Xs,
						Ys = _dataSIMD.Ys,
						Zs = _dataSIMD.Zs,
						Planes = _nativePlanes.Reinterpret<float4>(),
					}.Schedule(count, JobBatchCount);

					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}