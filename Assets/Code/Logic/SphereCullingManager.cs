using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace SphereCulling
{
	public class SphereCullingManager : MonoBehaviour
	{
		public int JobBatchCount = 32;
		public SphereDemoConfig DemoConfig;

		private SphereCullingMode _spawnedCullingMode = SphereCullingMode.Uninitialized;
		private int _spawnedCount;
		private SphereDataManaged _dataManaged = new();
		private SphereDataUnmanaged _dataUnmanaged;
		private SphereDataSIMD _dataSIMD;
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
				case SphereCullingMode.CullJobsBurstBranchlessBatch:
				case SphereCullingMode.CullJobsBurstSIMD:
				{
					_dataUnmanaged.Init(count);

					for (int i = 0; i < count; i++)
					{
						_dataUnmanaged.Positions[i] = Random.NextFloat3Direction() * Random.NextFloat() * spawnRadius;
						_dataUnmanaged.Radii[i] = Constants.SphereRadius;
					}

					break;
				}
				case SphereCullingMode.CullJobsBurstExplicitSSE:
				{
					_dataSIMD.Init(count);

					for (int i = 0; i < count; i++)
					{
						var pos = Random.NextFloat3Direction() * Random.NextFloat() * spawnRadius;
						_dataSIMD.Xs[i] = pos.x;
						_dataSIMD.Ys[i] = pos.y;
						_dataSIMD.Zs[i] = pos.z;
					}

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
				case SphereCullingMode.CullJobsBurstBranchlessBatch:
				case SphereCullingMode.CullJobsBurstSIMD:
				case SphereCullingMode.CullJobsBurstExplicitSSE:
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
				case SphereCullingMode.CullJobsBurstBranchlessBatch:
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
				case SphereCullingMode.CullJobsBurstSIMD:
				{
					_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

					_currentJobHandle = new CullMultiJobSIMD
					{
						Output = _jobResult.AsParallelWriter(),
						Positions = _dataUnmanaged.Positions,
						PlanePackets = CullUtils.CreatePlanePackets()
					}.Schedule(count, JobBatchCount);

					break;
				}
				case SphereCullingMode.CullJobsBurstExplicitSSE:
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

				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}