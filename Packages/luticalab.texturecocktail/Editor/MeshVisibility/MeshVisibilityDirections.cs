using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Nested direction samples on the surface of a cube, normalized onto the sphere.
    /// Resolutions 1, 2 and 4 produce 26, 98 and 386 directions. Every set explicitly
    /// includes both signs of X, Y and Z, face diagonals and corner diagonals.
    /// </summary>
    public static class MeshVisibilityDirections
    {
        public static IReadOnlyList<Vector3> Create(int resolution)
        {
            if (resolution != 1 && resolution != 2 && resolution != 4)
                throw new ArgumentOutOfRangeException(nameof(resolution), "Choose direction resolution 1, 2 or 4.");
            var directions = new List<Vector3>(24 * resolution * resolution + 2)
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
            };
            for (int x = -resolution; x <= resolution; x++)
            for (int y = -resolution; y <= resolution; y++)
            for (int z = -resolution; z <= resolution; z++)
            {
                if (Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z))) != resolution) continue;
                // The six axis directions were inserted first for clear diagnostics.
                if ((x == 0 ? 0 : 1) + (y == 0 ? 0 : 1) + (z == 0 ? 0 : 1) == 1) continue;
                directions.Add(new Vector3(x, y, z).normalized);
            }
            return directions.AsReadOnly();
        }
    }
}
