using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public enum SphereCullingMode
{
	Uninitialized,
	NoCull,
	CullMono,
	CullJobs,
	CullJobsBurst,
	CullJobsBurstSIMD,
}


public struct InstanceData
{
	// This exact naming is required by Unity
	// ReSharper disable once InconsistentNaming
	public float4x4 objectToWorld;
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
	public NativeList<float3> Positions;
	public NativeList<float> Radii;

	public void Clear()
	{
		Positions.Clear();
		Radii.Clear();
	}
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

public class SphereCullingManager : MonoBehaviour
{
	public SphereDemoData DemoData;

	private SphereCullingMode _spawnedCullingMode = SphereCullingMode.Uninitialized;
	private int _spawnedCount;
	private SphereDataManaged _dataManaged = new();
	private Random Random;
	private Plane[] _cameraPlanes = new Plane[6];
	private Camera _camera;

	private const float _sphereRadius = 0.5f;

	private void Start()
	{
		Random = new Random(1);
		_camera = Camera.main;
	}

	private void DestroySpheres()
	{
		_dataManaged.Clear();
	}

	private void SpawnSpheres()
	{
		var count = DemoData.SphereCount;
		var spawnRadius = DemoData.SpawnRadius;

		switch (DemoData.CullingMode)
		{
			case SphereCullingMode.NoCull:
			case SphereCullingMode.CullMono:
			{
				for (int i = 0; i < count; i++)
				{
					_dataManaged.Positions.Add(Random.NextFloat3Direction() * Random.NextFloat() * spawnRadius);
					_dataManaged.Radii.Add(_sphereRadius);
				}

				break;
			}
			case SphereCullingMode.CullJobs:
			{
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
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool Cull(float3 position)
	{
		const int planeCount = 6;

		for (int i = 0; i < planeCount; i++)
		{
			var plane = _cameraPlanes[i];
			var n = math.dot(position, plane.normal);
			// Distance is from plane to origin
			var outside = _sphereRadius + n <= -plane.distance;
			if (outside)
				return false;
		}

		return true;
	}

	private void Update()
	{
		if (_spawnedCullingMode != DemoData.CullingMode || _spawnedCount != DemoData.SphereCount)
		{
			DestroySpheres();
			SpawnSpheres();
			_spawnedCullingMode = DemoData.CullingMode;
			_spawnedCount = DemoData.SphereCount;
		}

		GeometryUtility.CalculateFrustumPlanes(_camera, _cameraPlanes);
		var material = DemoData.SphereMaterial;
		var mesh = DemoData.SphereMesh;

		switch (_spawnedCullingMode)
		{
			case SphereCullingMode.Uninitialized:
				break;
			case SphereCullingMode.NoCull:
			{
				var count = _dataManaged.Positions.Count;
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
				var count = _dataManaged.Positions.Count;
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
				
				Debug.Log(matrices.Length);

				Graphics.RenderMeshInstanced(new RenderParams(material), mesh, 0, matrices.AsArray());
				break;
			}
			case SphereCullingMode.CullJobs:
				// TODO:
				break;
			case SphereCullingMode.CullJobsBurst:
				// TODO:
				break;
			case SphereCullingMode.CullJobsBurstSIMD:
				// TODO:
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}