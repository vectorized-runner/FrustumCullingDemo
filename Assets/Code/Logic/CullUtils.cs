using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace SphereCulling
{
	public static class CullUtils
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

		public static void UpdateFrustumPlanes(ref NativeArray<float4> planes)
		{
			var managedPlanes = new Plane[Constants.PlaneCount];
			GeometryUtility.CalculateFrustumPlanes(Camera.main, managedPlanes);

			for (int i = 0; i < Constants.PlaneCount; ++i)
			{
				planes[i] = new float4(managedPlanes[i].normal, managedPlanes[i].distance);
			}
		}

		public static void CreatePlanePackets(ref NativeArray<PlanePacket4> planePackets)
		{
			var planes = new NativeArray<float4>(6, Allocator.Temp);
			UpdateFrustumPlanes(ref planes);

			int cullingPlaneCount = planes.Length;
			int packetCount = (cullingPlaneCount + 3) >> 2;

			for (int i = 0; i < cullingPlaneCount; i++)
			{
				var p = planePackets[i >> 2];
				p.Xs[i & 3] = planes[i].x;
				p.Ys[i & 3] = planes[i].y;
				p.Zs[i & 3] = planes[i].z;
				p.Distances[i & 3] = planes[i].w;
				planePackets[i >> 2] = p;
			}

			// Populate the remaining planes with values that are always "in"
			for (int i = cullingPlaneCount; i < 4 * packetCount; ++i)
			{
				var p = planePackets[i >> 2];
				p.Xs[i & 3] = 1.0f;
				p.Ys[i & 3] = 0.0f;
				p.Zs[i & 3] = 0.0f;

				// We want to set these distances to a very large number, but one which
				// still allows us to add sphere radius values. Let's try 1 billion.
				p.Distances[i & 3] = 1e9f;

				planePackets[i >> 2] = p;
			}
		}
	}
}