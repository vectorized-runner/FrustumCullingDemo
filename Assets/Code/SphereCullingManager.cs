using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public enum SphereCullingMode
{
	Uninitialized,
	NoCull,
	CullMono,
	CullSingleJob,
	CullMultiJob,
	CullMultiJobBurst,
	CullJobsBurstBranchless,
	CullJobsBurstSIMD,
}

public struct InstanceData
{
	// This exact naming is required by Unity
	// ReSharper disable once InconsistentNaming
	public float4x4 objectToWorld;
}

public struct CullSingleJob : IJob
{
	[ReadOnly]
	public NativeArray<float3> Positions;

	[ReadOnly]
	public NativeArray<DotsPlane> CameraPlanes;

	public NativeList<float4x4> Output;

	public void Execute()
	{
		var count = Positions.Length;

		for (int i = 0; i < count; i++)
		{
			var position = Positions[i];
			if (CullMethods.Cull(position, CameraPlanes))
			{
				Output.Add(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
			}
		}
	}
}

public struct CullMultiJob : IJobParallelFor
{
	[ReadOnly]
	public NativeArray<float3> Positions;

	[ReadOnly]
	public NativeArray<DotsPlane> CameraPlanes;

	public NativeList<float4x4>.ParallelWriter Output;

	public void Execute(int index)
	{
		var position = Positions[index];
		if (CullMethods.Cull(position, CameraPlanes))
		{
			Output.AddNoResize(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
		}
	}
}

[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct CullMultiJobBurst : IJobParallelFor
{
	[ReadOnly]
	public NativeArray<float3> Positions;

	[ReadOnly]
	public NativeArray<DotsPlane> CameraPlanes;

	public NativeList<float4x4>.ParallelWriter Output;

	public void Execute(int index)
	{
		var position = Positions[index];
		if (CullMethods.Cull(position, CameraPlanes))
		{
			Output.AddNoResize(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
		}
	}
}

[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct CullMultiJobBranchless : IJobParallelFor
{
	[ReadOnly]
	public NativeArray<float3> Positions;

	[ReadOnly]
	public NativeArray<DotsPlane> Planes;

	public NativeList<float4x4>.ParallelWriter Output;

	public void Execute(int index)
	{
		var position = Positions[index];

		// Inside: R + N + D > 0
		var result =
			Planes[0].Distance + Constants.SphereRadius + math.dot(position, Planes[0].Normal) > 0 &&
			Planes[1].Distance + Constants.SphereRadius + math.dot(position, Planes[1].Normal) > 0 &&
			Planes[2].Distance + Constants.SphereRadius + math.dot(position, Planes[2].Normal) > 0 &&
			Planes[3].Distance + Constants.SphereRadius + math.dot(position, Planes[3].Normal) > 0 &&
			Planes[4].Distance + Constants.SphereRadius + math.dot(position, Planes[4].Normal) > 0 &&
			Planes[5].Distance + Constants.SphereRadius + math.dot(position, Planes[5].Normal) > 0;

		if (result)
		{
			Output.AddNoResize(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
		}
	}
}

[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct CullMultiJobSIMD : IJobParallelFor
{
	[ReadOnly]
	public NativeArray<float3> Positions;

	[ReadOnly]
	public NativeArray<DotsPlane> Planes;

	public NativeList<float4x4>.ParallelWriter Output;

	public void Execute(int index)
	{
		var position = Positions[index];

		// Inside: R + N + D > 0
		var result =
			Planes[0].Distance + Constants.SphereRadius + math.dot(position, Planes[0].Normal) > 0 &&
			Planes[1].Distance + Constants.SphereRadius + math.dot(position, Planes[1].Normal) > 0 &&
			Planes[2].Distance + Constants.SphereRadius + math.dot(position, Planes[2].Normal) > 0 &&
			Planes[3].Distance + Constants.SphereRadius + math.dot(position, Planes[3].Normal) > 0 &&
			Planes[4].Distance + Constants.SphereRadius + math.dot(position, Planes[4].Normal) > 0 &&
			Planes[5].Distance + Constants.SphereRadius + math.dot(position, Planes[5].Normal) > 0;

		if (result)
		{
			Output.AddNoResize(float4x4.TRS(position, quaternion.identity, new float3(1, 1, 1)));
		}
	}
}



[Serializable]
public class SphereDemoData
{
	public Mesh SphereMesh;
	public Material SphereMaterial;

	public int SphereCount;
	public float SpawnRadius = 100_000;
	public SphereCullingMode CullingMode;
}

public struct SphereDataUnmanaged
{
	public NativeArray<float3> Positions;
	public NativeArray<float> Radii;
	public bool IsCreated;

	public void Init(int count)
	{
		Dispose();
		Positions = new NativeArray<float3>(count, Allocator.Persistent);
		Radii = new NativeArray<float>(count, Allocator.Persistent);
	}

	public void Dispose()
	{
		if (!IsCreated)
			return;

		Positions.Dispose();
		Radii.Dispose();
	}
}

public struct DotsPlane
{
	public float3 Normal;
	public float Distance;
}

public class SphereDataManaged
{
	public List<float3> Positions = new List<float3>();
	public List<float> Radii = new List<float>();

	public void Clear()
	{
		Positions.Clear();
		Radii.Clear();
	}
}


public static class CullMethods
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Cull(float3 position, NativeArray<DotsPlane> cameraPlanes)
	{
		for (int i = 0; i < Constants.PlaneCount; i++)
		{
			var plane = cameraPlanes[i];
			var n = math.dot(position, plane.Normal);
			var d = plane.Distance;
			var r = Constants.SphereRadius;
			// Distance is from plane to origin
			var inside = r + n + d > 0;
			if (!inside)
				return false;
		}

		return true;
	}
}

public static class Constants
{
	public const float SphereRadius = 0.5f;
	public const int PlaneCount = 6;
}

public class SphereCullingManager : MonoBehaviour
{
	public SphereDemoData DemoData;

	private SphereCullingMode _spawnedCullingMode = SphereCullingMode.Uninitialized;
	private int _spawnedCount;
	private SphereDataManaged _dataManaged = new();
	private SphereDataUnmanaged _dataUnmanaged;
	private Random Random;
	private Plane[] _cameraPlanes = new Plane[Constants.PlaneCount];
	private Camera _camera;
	private JobHandle _currentJobHandle;
	private NativeList<float4x4> _jobResult;
	private NativeArray<DotsPlane> _nativePlanes;

	private void Start()
	{
		Random = new Random(1);
		_camera = Camera.main;
		_nativePlanes = new NativeArray<DotsPlane>(Constants.PlaneCount, Allocator.Persistent);
	}

	private void RespawnSpheres()
	{
		var count = DemoData.SphereCount;
		var spawnRadius = DemoData.SpawnRadius;

		switch (DemoData.CullingMode)
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
			var plane = _cameraPlanes[i];
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

				Graphics.RenderMeshInstanced(new RenderParams(DemoData.SphereMaterial), DemoData.SphereMesh, 0,
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

	private void Update()
	{
		if (_spawnedCullingMode != DemoData.CullingMode || _spawnedCount != DemoData.SphereCount)
		{
			_currentJobHandle.Complete();
			RespawnSpheres();
			_spawnedCullingMode = DemoData.CullingMode;
			_spawnedCount = DemoData.SphereCount;
		}

		GeometryUtility.CalculateFrustumPlanes(_camera, _cameraPlanes);
		var material = DemoData.SphereMaterial;
		var mesh = DemoData.SphereMesh;

		for (int i = 0; i < Constants.PlaneCount; i++)
		{
			_nativePlanes[i] = new DotsPlane
			{
				Distance = _cameraPlanes[i].distance,
				Normal = _cameraPlanes[i].normal
			};
		}

		var count = DemoData.SphereCount;

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
				}.Schedule(count, 64);
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
				}.Schedule(count, 64);
				break;
			}

			case SphereCullingMode.CullJobsBurstBranchless:
			{
				_jobResult = new NativeList<float4x4>(count, Allocator.TempJob);

				_currentJobHandle = new CullMultiJobBranchless
				{
					Planes = _nativePlanes,
					Output = _jobResult.AsParallelWriter(),
					Positions = _dataUnmanaged.Positions,
				}.Schedule(count, 64);
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