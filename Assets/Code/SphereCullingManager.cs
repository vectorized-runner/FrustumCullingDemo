using System;
using System.Collections.Generic;
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

	private void Start()
	{
		Random = new Random(1);
	}

	private void DestroySpheres()
	{
		_dataManaged.Clear();
	}

	private void SpawnSpheres()
	{
		var count = DemoData.SphereCount;
		var spawnRadius = DemoData.SpawnRadius;
		const float sphereRadius = 0.5f;

		switch (DemoData.CullingMode)
		{
			case SphereCullingMode.NoCull:
			{
				for (int i = 0; i < count; i++)
				{
					_dataManaged.Positions.Add(Random.NextFloat3Direction() * Random.NextFloat() * spawnRadius);
					_dataManaged.Radii.Add(sphereRadius);
				}

				break;
			}
			case SphereCullingMode.CullMono:
			{
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

	public struct InstanceData
	{
		// This exact naming is required by Unity
		// ReSharper disable once InconsistentNaming
		public float4x4 objectToWorld;
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

				Debug.Log($"Count: {matrices.Length}");
				Graphics.RenderMeshInstanced(new RenderParams(material), mesh, 0, matrices);

				Debug.Log("Running RenderMeshInstanced!");
				break;
			}
			case SphereCullingMode.CullMono:
				break;
			case SphereCullingMode.CullJobs:
				break;
			case SphereCullingMode.CullJobsBurst:
				break;
			case SphereCullingMode.CullJobsBurstSIMD:
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

		// TODO: Cull
		// TODO: Draw
	}
}