using System.Collections.Generic;
using Unity.Mathematics;

namespace FrustumCulling
{
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
}