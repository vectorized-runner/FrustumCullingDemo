using System;
using UnityEngine;

namespace FrustumCulling
{
	[Serializable]
	public class DemoConfig
	{
		public Mesh SphereMesh;
		public Material SphereMaterial;

		public int SphereCount;
		public float SpawnRadius = 100_000;
		public CullingMode CullingMode;
	}
}