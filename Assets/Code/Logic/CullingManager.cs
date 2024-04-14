using System;
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
		private AABBDataUnmanaged _aabbData;
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
				case CullingMode.Mono:
				{
					_dataManaged.Clear();

					for (int i = 0; i < count; i++)
					{
						_dataManaged.Positions.Add(_random.NextFloat3Direction() * _random.NextFloat() * spawnRadius);
						_dataManaged.Radii.Add(Constants.SphereRadius);
					}

					break;
				}
				case CullingMode.ParallelJob:
				case CullingMode.SingleJob:
				case CullingMode.ParallelJobBurst:
				case CullingMode.ParallelJobBurstBranchless:
				case CullingMode.BatchJobBurstBranchless:
				case CullingMode.ParallelJobBurstSIMD:
				{
					_dataUnmanaged.Init(count);

					for (int i = 0; i < count; i++)
					{
						_dataUnmanaged.Positions[i] = _random.NextFloat3Direction() * _random.NextFloat() * spawnRadius;
						_dataUnmanaged.Radii[i] = Constants.SphereRadius;
					}

					break;
				}
				case CullingMode.ParallelJobBurstSSE:
				case CullingMode.ParallelJobBurstArmNeon:
				case CullingMode.ParallelJobBurstSIMDSoA:
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
				case CullingMode.AABBCullSIMD:
				{
					_aabbData.Init(count);

					for (int i = 0; i < count; i++)
					{
						var pos = _random.NextFloat3Direction() * _random.NextFloat() * spawnRadius;
						_aabbData.Positions[i] = pos;
						_aabbData.AABBs[i] = new AABB
						{
							Center = pos,
							Extents = new float3(Constants.SphereRadius)
						};
					}

					break;
				}
				case CullingMode.Uninitialized:
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void LateUpdate()
		{
			switch (_spawnedCullingMode)
			{
				case CullingMode.Uninitialized:
				case CullingMode.NoCull:
				case CullingMode.Mono:
					break;
				case CullingMode.SingleJob:
				case CullingMode.ParallelJobBurst:
				case CullingMode.ParallelJob:
				case CullingMode.ParallelJobBurstBranchless:
				case CullingMode.BatchJobBurstBranchless:
				case CullingMode.ParallelJobBurstSIMD:
				case CullingMode.ParallelJobBurstSSE:
				case CullingMode.ParallelJobBurstSIMDSoA:
				case CullingMode.ParallelJobBurstArmNeon:
				case CullingMode.AABBCullSIMD:
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
				case CullingMode.Mono:
				{
					var matrices = new NativeList<InstanceData>(count, Allocator.Temp);
					for (int i = 0; i < count; i++)
					{
						var position = _dataManaged.Positions[i];
						if (FrustumCullHelper.Cull(position, _nativePlanes))
						{
							var rotation = quaternion.identity;
							var scale = 1.0f;
							matrices.Add(new InstanceData { objectToWorld = float4x4.TRS(position, rotation, scale) });
						}
					}

					Graphics.RenderMeshInstanced(new RenderParams(material), mesh, 0, matrices.AsArray());
					break;
				}
				case CullingMode.SingleJob:
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
				case CullingMode.ParallelJob:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullParallelJob
					{
						CameraPlanes = _nativePlanes,
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
					}.Schedule(count, JobBatchCount);
					break;
				}
				case CullingMode.ParallelJobBurst:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullParallelJobBurst
					{
						CameraPlanes = _nativePlanes,
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
					}.Schedule(count, JobBatchCount);
					break;
				}

				case CullingMode.ParallelJobBurstBranchless:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullParallelJobBurstBranchless
					{
						Planes = _nativePlanes,
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
					}.Schedule(count, JobBatchCount);
					break;
				}
				case CullingMode.BatchJobBurstBranchless:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullBatchJobBurstBranchless
					{
						Planes = _nativePlanes,
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
					}.ScheduleBatch(count, JobBatchCount);

					break;
				}
				case CullingMode.ParallelJobBurstSIMD:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullParallelJobSIMD
					{
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
						PlanePackets = FrustumCullHelper.CreatePlanePackets()
					}.Schedule(count, JobBatchCount);

					break;
				}
				case CullingMode.ParallelJobBurstSSE:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullBatchJobSSE
					{
						Output = _jobResult.AsParallelWriter(),
						Xs = _dataSIMD.Xs,
						Ys = _dataSIMD.Ys,
						Zs = _dataSIMD.Zs,
						Planes = _nativePlanes.Reinterpret<float4>(),
					}.Schedule(count, JobBatchCount);

					break;
				}
				case CullingMode.ParallelJobBurstArmNeon:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullBatchJobArmNeon
					{
						Output = _jobResult.AsParallelWriter(),
						Xs = _dataSIMD.Xs,
						Ys = _dataSIMD.Ys,
						Zs = _dataSIMD.Zs,
						Planes = _nativePlanes.Reinterpret<float4>(),
					}.Schedule(count, JobBatchCount);

					break;
				}
				case CullingMode.ParallelJobBurstSIMDSoA:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullBatchJobSIMDSoA
					{
						Output = _jobResult.AsParallelWriter(),
						Xs = _dataSIMD.Xs,
						Ys = _dataSIMD.Ys,
						Zs = _dataSIMD.Zs,
						Planes = _nativePlanes.Reinterpret<float4>(),
					}.Schedule(count, JobBatchCount);

					break;
				}
				case CullingMode.AABBCullSIMD:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullAABBParallelJobBurstSIMD
					{
						Output = _jobResult.AsParallelWriter(),
						AABBs = _aabbData.AABBs,
						Positions = _aabbData.Positions,
						Planes = FrustumCullHelper.CreatePlanePackets(),
					}.Schedule(count, JobBatchCount);

					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}