using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Mesh Helper to help subdivide mesh.
/// Referenced from https://wiki.unity3d.com/index.php/MeshHelper#Code
/// </summary>
public class MeshHelper
{
    List<Vector3> vertices, normals;
    List<Color> colors;
    List<List<Vector2>> uvs;

    List<int> indices;
    Dictionary<uint, int> newVertices;

    void setupArrays(in Mesh mesh)
    {
        vertices = new List<Vector3>(mesh.vertices);
        normals = new List<Vector3>(mesh.normals);
        colors = new List<Color>(mesh.colors);
        uvs = new List<List<Vector2>>();

        // Mesh UV's
        uvs.Add(new List<Vector2>(mesh.uv));
        uvs.Add(new List<Vector2>(mesh.uv2));
        uvs.Add(new List<Vector2>(mesh.uv3));
        uvs.Add(new List<Vector2>(mesh.uv4));
        uvs.Add(new List<Vector2>(mesh.uv5));
        uvs.Add(new List<Vector2>(mesh.uv6));
        uvs.Add(new List<Vector2>(mesh.uv7));
        uvs.Add(new List<Vector2>(mesh.uv8));

        indices = new List<int>();
    }

    void clean_up()
    {
        vertices = null;
        normals = null;
        colors = null;
        for (int i = 0; i < uvs.Count; i++)
            uvs[i] = null;
        indices = null;
    }

    void set_mesh_uvs(ref Mesh mesh)
    {
        if (uvs[0].Count > 0) mesh.uv = uvs[0].ToArray();
        if (uvs[1].Count > 0) mesh.uv2 = uvs[1].ToArray();
        if (uvs[2].Count > 0) mesh.uv3 = uvs[2].ToArray();
        if (uvs[3].Count > 0) mesh.uv4 = uvs[3].ToArray();
        if (uvs[4].Count > 0) mesh.uv5 = uvs[4].ToArray();
        if (uvs[5].Count > 0) mesh.uv6 = uvs[5].ToArray();
        if (uvs[6].Count > 0) mesh.uv7 = uvs[6].ToArray();
        if (uvs[7].Count > 0) mesh.uv8 = uvs[7].ToArray();
    }

    #region Subdivide4 (2x2)
    int get_new_vertex4(int i1, int i2)
    {
        int newIndex = vertices.Count;
        uint t1 = ((uint)i1 << 16) | (uint)i2;
        uint t2 = ((uint)i2 << 16) | (uint)i1;

        if (newVertices.ContainsKey(t2)) return newVertices[t2];
        if (newVertices.ContainsKey(t1)) return newVertices[t1];

        newVertices.Add(t1, newIndex);

        vertices.Add((vertices[i1] + vertices[i2]) * 0.5f);
        if (normals.Count > 0) normals.Add((normals[i1] + normals[i2]).normalized);
        if (colors.Count > 0) colors.Add((colors[i1] + colors[i2]) * 0.5f);
        for (int i = 0; i < uvs.Count; i++)
        {
            if (uvs[i].Count > 0)
                uvs[i].Add((uvs[i][i1] + uvs[i][i2]) * 0.5f);
        }
        return newIndex;
    }

    public void Subdivide4(ref Mesh mesh)
    {
        newVertices = new Dictionary<uint, int>();
        setupArrays(mesh);

        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i1 = triangles[i];
            int i2 = triangles[i + 1];
            int i3 = triangles[i + 2];

            int a = get_new_vertex4(i1, i2);
            int b = get_new_vertex4(i2, i3);
            int c = get_new_vertex4(i3, i1);
            indices.Add(i1); indices.Add(a); indices.Add(c);
            indices.Add(i2); indices.Add(b); indices.Add(a);
            indices.Add(i3); indices.Add(c); indices.Add(b);
            indices.Add(a); indices.Add(b); indices.Add(c); // center triangle
        }
        mesh.vertices = vertices.ToArray();
        if (normals.Count > 0) mesh.normals = normals.ToArray();
        if (colors.Count > 0) mesh.colors = colors.ToArray();
        set_mesh_uvs(ref mesh);
        mesh.triangles = indices.ToArray();

        clean_up();
    }
    #endregion Subdivide4 (2x2)

    #region Subdivide9 (3x3)
    int GetNewVertex9(int i1, int i2, int i3)
    {
        int newIndex = vertices.Count;

        // center points don't go into the edge list
        if (i3 == i1 || i3 == i2)
        {
            uint t1 = ((uint)i1 << 16) | (uint)i2;
            if (newVertices.ContainsKey(t1)) return newVertices[t1];
            newVertices.Add(t1, newIndex);
        }

        // calculate new vertex
        vertices.Add((vertices[i1] + vertices[i2] + vertices[i3]) / 3.0f);
        if (normals.Count > 0) normals.Add((normals[i1] + normals[i2] + normals[i3]).normalized);
        if (colors.Count > 0) colors.Add((colors[i1] + colors[i2] + colors[i3]) / 3.0f);

        for (int i = 0; i < uvs.Count; i++)
        {
            if (uvs[i].Count > 0)
                uvs[i].Add((uvs[i][i1] + uvs[i][i2] + uvs[i][i3]) / 3.0f);
        }

        return newIndex;
    }

    /// <summary>
    /// Devides each triangles into 9. A quad(2 tris) will be splitted into 3x3 quads( 18 tris )
    /// </summary>
    /// <param name="mesh"></param>
    public void Subdivide9(ref Mesh mesh)
    {
        newVertices = new Dictionary<uint, int>();
        setupArrays(mesh);

        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i1 = triangles[i];
            int i2 = triangles[i + 1];
            int i3 = triangles[i + 2];

            int a1 = GetNewVertex9(i1, i2, i1);
            int a2 = GetNewVertex9(i2, i1, i2);
            int b1 = GetNewVertex9(i2, i3, i2);
            int b2 = GetNewVertex9(i3, i2, i3);
            int c1 = GetNewVertex9(i3, i1, i3);
            int c2 = GetNewVertex9(i1, i3, i1);

            int d = GetNewVertex9(i1, i2, i3);

            indices.Add(i1); indices.Add(a1); indices.Add(c2);
            indices.Add(i2); indices.Add(b1); indices.Add(a2);
            indices.Add(i3); indices.Add(c1); indices.Add(b2);
            indices.Add(d); indices.Add(a1); indices.Add(a2);
            indices.Add(d); indices.Add(b1); indices.Add(b2);
            indices.Add(d); indices.Add(c1); indices.Add(c2);
            indices.Add(d); indices.Add(c2); indices.Add(a1);
            indices.Add(d); indices.Add(a2); indices.Add(b1);
            indices.Add(d); indices.Add(b2); indices.Add(c1);
        }

        mesh.vertices = vertices.ToArray();
        if (normals.Count > 0) mesh.normals = normals.ToArray();
        if (colors.Count > 0) mesh.colors = colors.ToArray();
        set_mesh_uvs(ref mesh);
        mesh.triangles = indices.ToArray();

        clean_up();
    }
    #endregion Subdivide9 (3x3)

    #region Subdivide
    /// <summary>
    /// This functions subdivides the mesh based on the level parameter
    /// Note that only the 4 and 9 subdivides are supported so only those divides
    /// are possible. [2,3,4,6,8,9,12,16,18,24,27,32,36,48,64, ...]
    /// The function tried to approximate the desired level 
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="level">Should be a number made up of (2^x * 3^y)
    /// [2,3,4,6,8,9,12,16,18,24,27,32,36,48,64, ...]
    /// </param>
    public void Subdivide(ref Mesh mesh, int level)
    {
        if (level < 2) return;
        while (level > 1)
        {
            // remove prime factor 3
            while (level % 3 == 0)
            {
                Subdivide9(ref mesh);
                level /= 3;
            }
            // remove prime factor 2
            while (level % 2 == 0)
            {
                Subdivide4(ref mesh);
                level /= 2;
            }
            // try to approximate. All other primes are increased by one
            // so they can be processed
            if (level > 3)
                level++;
        }
    }
    #endregion Subdivide


}

public class MeshSmoother
{
    public static Mesh DuplicateMesh(Mesh mesh)
    {
        return (Mesh)UnityEngine.Object.Instantiate(mesh);
    }

    private static int[] subDivision = new int[] { 0, 2, 3, 4, 6, 8, 9, 12, 16, 18, 24 };

    //[Range(0, 10)]
    //public int subdivisionLevel;

    //[Range(0, 10)]
    //public int timesToSubdivide;

    public void smooth_mesh(ref Mesh mesh, in uint timesToSubdivide, in uint subDivisionLevel)
    {
        MeshHelper helper = new MeshHelper();
        for (int i = 0; i < timesToSubdivide; i++)
            helper.Subdivide(ref mesh, subDivision[subDivisionLevel]);
    }

    public void smooth_mesh(ref MeshFilter meshFilter, in uint timesToSubdivide, in uint subDivisionLevel)
    {
        Mesh mesh = DuplicateMesh(meshFilter.mesh);
        smooth_mesh(ref mesh, timesToSubdivide, subDivisionLevel);
        meshFilter.mesh = mesh;
    }
}
