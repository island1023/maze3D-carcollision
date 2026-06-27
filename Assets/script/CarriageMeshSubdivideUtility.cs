using UnityEngine;
using System.Collections.Generic;

public static class CarriageMeshSubdivideUtility
{
    public static Mesh Subdivide(Mesh source, int iterations)
    {
        Mesh result = Object.Instantiate(source);
        result.name = source.name + "_subdivided";

        for (int i = 0; i < iterations; i++)
            result = SubdivideOnce(result);

        result.RecalculateNormals();
        result.RecalculateBounds();
        return result;
    }

    static Mesh SubdivideOnce(Mesh mesh)
    {
        var srcVerts = mesh.vertices;
        var srcTris = mesh.triangles;
        var srcUvs = mesh.uv;

        var newVerts = new List<Vector3>(srcVerts.Length * 2);
        var newUvs = srcUvs != null && srcUvs.Length == srcVerts.Length
            ? new List<Vector2>(srcVerts.Length * 2)
            : null;
        var newTris = new List<int>(srcTris.Length * 4);
        var edgeMid = new Dictionary<long, int>();

        for (int i = 0; i < srcVerts.Length; i++)
        {
            newVerts.Add(srcVerts[i]);
            if (newUvs != null) newUvs.Add(srcUvs[i]);
        }

        int GetMid(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (edgeMid.TryGetValue(key, out int idx)) return idx;

            idx = newVerts.Count;
            newVerts.Add((srcVerts[a] + srcVerts[b]) * 0.5f);
            if (newUvs != null)
                newUvs.Add((srcUvs[a] + srcUvs[b]) * 0.5f);
            edgeMid[key] = idx;
            return idx;
        }

        for (int i = 0; i < srcTris.Length; i += 3)
        {
            int v0 = srcTris[i];
            int v1 = srcTris[i + 1];
            int v2 = srcTris[i + 2];

            int m01 = GetMid(v0, v1);
            int m12 = GetMid(v1, v2);
            int m20 = GetMid(v2, v0);

            newTris.Add(v0);  newTris.Add(m01); newTris.Add(m20);
            newTris.Add(v1);  newTris.Add(m12); newTris.Add(m01);
            newTris.Add(v2);  newTris.Add(m20); newTris.Add(m12);
            newTris.Add(m01); newTris.Add(m12); newTris.Add(m20);
        }

        var result = new Mesh { name = mesh.name + "_sub" };
        if (newVerts.Count > 65535)
            result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        result.vertices = newVerts.ToArray();
        if (newUvs != null) result.uv = newUvs.ToArray();
        result.triangles = newTris.ToArray();
        return result;
    }
}
