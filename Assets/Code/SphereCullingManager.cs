using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
	CullJobsBurst,
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
			// Distance is from plane to origin
			var outside = Constants.SphereRadius + n <= -plane.Distance;
			if (outside)
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


	private void Start()
	{
		Random = new Random(1);
		_camera = Camera.main;
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
			{
				_dataUnmanaged.Init(count);

				for (int i = 0; i < count; i++)
				{
					_dataUnmanaged.Positions[i] = Random.NextFloat3Direction() * Random.NextFloat() * spawnRadius;
					_dataUnmanaged.Radii[i] = Constants.SphereRadius;
				}

				break;
			}
			case SphereCullingMode.CullJobsBurst:
			{
				break;
			}
			case SphereCullingMode.CullJobsBurstSIMD:
			{
				break;
			}
			case SphereCullingMode.CullJobsBurstBranchless:
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

	private void Update()
	{
		if (_spawnedCullingMode != DemoData.CullingMode || _spawnedCount != DemoData.SphereCount)
		{
			RespawnSpheres();
			_spawnedCullingMode = DemoData.CullingMode;
			_spawnedCount = DemoData.SphereCount;
		}

		GeometryUtility.CalculateFrustumPlanes(_camera, _cameraPlanes);
		var material = DemoData.SphereMaterial;
		var mesh = DemoData.SphereMesh;
		var cameraPlanes = new NativeArray<DotsPlane>(Constants.PlaneCount, Allocator.TempJob);
		for (int i = 0; i < Constants.PlaneCount; i++)
		{
			cameraPlanes[i] = new DotsPlane
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
				var result = new NativeList<float4x4>(count, Allocator.TempJob);

				new CullSingleJob
				{
					CameraPlanes = cameraPlanes,
					Output = result,
					Positions = _dataUnmanaged.Positions,
				}.Run();

				Graphics.RenderMeshInstanced(new RenderParams(material), mesh, 0,
					result.AsArray().Reinterpret<InstanceData>());
				result.Dispose();
				break;
			}
			case SphereCullingMode.CullMultiJob:
			{
				var result = new NativeList<float4x4>(count, Allocator.TempJob);

				new CullMultiJob
				{
					CameraPlanes = cameraPlanes,
					Output = result.AsParallelWriter(),
					Positions = _dataUnmanaged.Positions,
				}.Run(count);

				Graphics.RenderMeshInstanced(new RenderParams(material), mesh, 0,
					result.AsArray().Reinterpret<InstanceData>());
				result.Dispose();
				break;
			}
			case SphereCullingMode.CullJobsBurstBranchless:
			{
				break;
			}
			case SphereCullingMode.CullJobsBurstSIMD:
				// TODO:
				break;
			case SphereCullingMode.CullJobsBurst:
			default:
				throw new ArgumentOutOfRangeException();
		}

		// Cleanup
		{
			cameraPlanes.Dispose();
		}
	}
}