using System;
using UnityEngine;

namespace SphereCulling
{
	[Serializable]
	public class SphereDemoConfig
	{
		public Mesh SphereMesh;
		public Material SphereMaterial;

		public int SphereCount;
		public float SpawnRadius = 100_000;
		public SphereCullingMode CullingMode;
	}
}