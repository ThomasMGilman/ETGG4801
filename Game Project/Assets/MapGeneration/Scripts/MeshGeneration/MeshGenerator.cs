using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class MeshGenerator : MonoBehaviour
{
    public int tileAmount = 10;

    public float wallHeight = 10;  //Height of the walls, 

    public MeshFilter Wall_MeshFilter;
    public MeshFilter Room_MeshFilter;
    public MeshFilter Floor_MeshFilter;
    public MeshFilter Roof_MeshFilter;

    public bool is2D;

    public void generate_mesh(int[,] map, float squareSize)
    {
        // Create Room Mesh for map Room
        Mesh roomMesh = new Mesh();
        create_mesh_from_map(in map, in squareSize, 1, in roomMesh);
        Room_MeshFilter.mesh = roomMesh;

        Mesh floorMesh = new Mesh();
        Mesh roofMesh = new Mesh();
        create_mesh_from_map(in map, in squareSize, 0, in roofMesh, in floorMesh, true);
        Floor_MeshFilter.mesh = floorMesh;
        Roof_MeshFilter.mesh = roofMesh;
    }

    public void create_mesh_from_map(in int[,]map, in float squareSize, int fromTileType, in Mesh m, in Mesh m2 = null, bool generatingFloor = false)
    {
        // create mesh from map points
        Dictionary<int, List<Triangle>> TriangleDictionary = new Dictionary<int, List<Triangle>>();
        List<Vector3> meshVertices = new List<Vector3>();
        List<int> meshTriangles = new List<int>();
        List<List<int>> outlines = new List<List<int>>();
        HashSet<int> checkedVertices = new HashSet<int>();
        SquareGrid Square_Grid = new SquareGrid(map, squareSize, fromTileType);

        for (int x = 0; x < Square_Grid.squares.GetLength(0); x++)
        {
            for (int y = 0; y < Square_Grid.squares.GetLength(1); y++)
                triangulate_squares(in TriangleDictionary, in meshTriangles, in meshVertices, in checkedVertices,  Square_Grid.squares[x, y]);
        }

        m.vertices = meshVertices.ToArray();
        m.triangles = meshTriangles.ToArray();
        set_uvs(in m, meshVertices, true);

        if(!generatingFloor)
        {
            if (is2D)
                generate_2d_colliders(in TriangleDictionary, in meshVertices, in checkedVertices);
            else
                create_wall_mesh(in TriangleDictionary, in meshVertices, in checkedVertices);
        }
        else
        {
            // Create Roof Mesh collider and reverse its direction
            MeshCollider roofCollider = Roof_MeshFilter.gameObject.AddComponent<MeshCollider>();
            roofCollider.sharedMesh = m;
            Roof_MeshFilter.transform.localScale = new Vector3(1, -1, 1);

            List<Vector3> meshVertices2 = new List<Vector3>();
            foreach(Vector3 v in meshVertices)
            {
                Vector3 v2 = v;
                v2.y -= wallHeight;
                meshVertices2.Add(v2);
            }

            m2.vertices = meshVertices2.ToArray();
            m2.triangles = meshTriangles.ToArray();
            set_uvs(in m2, meshVertices2, true);

            MeshCollider floorCollider = Floor_MeshFilter.gameObject.AddComponent<MeshCollider>();
            floorCollider.sharedMesh = m2;
        }
    }

    public void set_uvs(in Mesh mesh, in List<Vector3> vertices, bool usingZ = false)
    {
        Vector2[] uvs = new Vector2[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, usingZ ? vertices[i].z : vertices[i].y);
        }
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.Optimize();
    }

    //Need to tweak this
    private void create_wall_mesh(in Dictionary<int, List<Triangle>> triangleDictionary, in List<Vector3> meshVertices, in HashSet<int> checkedVertices)
    {
        List<List<int>> meshOutlines = new List<List<int>>();
        calculate_mesh_outlines(in triangleDictionary, in meshOutlines, in meshVertices, in checkedVertices);

        Mesh mesh = new Mesh();
        List<Vector3> mesh_vertices = new List<Vector3>();
        List<int> mesh_triangles = new List<int>();

        foreach(List<int> outline in meshOutlines)
        {
            for(int i = 0; i < outline.Count - 1; i++)
            {
                int startIndex = mesh_vertices.Count;
                mesh_vertices.Add(meshVertices[outline[i]]);                                 // left
                mesh_vertices.Add(meshVertices[outline[i + 1]]);                             // right
                mesh_vertices.Add(meshVertices[outline[i]] - Vector3.up * wallHeight);       // bottom left
                mesh_vertices.Add(meshVertices[outline[i + 1]] - Vector3.up * wallHeight);   // bottom right


                mesh_triangles.Add(startIndex + 0);
                mesh_triangles.Add(startIndex + 2);
                mesh_triangles.Add(startIndex + 3);

                mesh_triangles.Add(startIndex + 3);
                mesh_triangles.Add(startIndex + 1);
                mesh_triangles.Add(startIndex + 0);
            }
        }
        mesh.vertices = mesh_vertices.ToArray();
        mesh.triangles = mesh_triangles.ToArray();
        set_uvs(mesh, mesh_vertices);

        this.Wall_MeshFilter.mesh = mesh;

        MeshCollider wallCollider = Wall_MeshFilter.gameObject.AddComponent<MeshCollider>();
        wallCollider.sharedMesh = mesh;
    }

    void generate_2d_colliders(in Dictionary<int, List<Triangle>> triangleDictionary, in List<Vector3> meshVertices, in HashSet<int> checkedVertices)
    {
        EdgeCollider2D[] currentColliders = gameObject.GetComponent<EdgeCollider2D[]>();
        for(int i = 0; i < currentColliders.Length; i++)
            Destroy(currentColliders[i]);

        List<List<int>> meshOutlines = new List<List<int>>();
        calculate_mesh_outlines(in triangleDictionary, in meshOutlines, in meshVertices, in checkedVertices);

        foreach(List<int> outline in meshOutlines)
        {
            EdgeCollider2D edgeCollider = gameObject.AddComponent<EdgeCollider2D>();
            Vector2[] edgePoints = new Vector2[outline.Count];

            for (int i = 0; i < outline.Count; i++)
                edgePoints[i] = new Vector2(meshVertices[outline[i]].x, meshVertices[outline[i]].z);

            edgeCollider.points = edgePoints;
        }
    }

    private void triangulate_squares(in Dictionary<int, List<Triangle>> triangleDictionary, in List<int> meshTriangles, in List<Vector3> meshVertices, in HashSet<int> checkedVertices, Square s)
    {
        switch(s.configuration)
        {
            case 1:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.centreLeft, s.centreBottom, s.bottomLeft);
                break;
            case 2:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.bottomRight, s.centreBottom, s.centreRight);
                break;
            case 4:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.topRight, s.centreRight, s.centreTop);
                break;
            case 8:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.topLeft, s.centreTop, s.centreLeft);
                break;

            // 2 points:
            case 3:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.centreRight, s.bottomRight, s.bottomLeft, s.centreLeft);
                break;
            case 6:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.centreTop, s.topRight, s.bottomRight, s.centreBottom);
                break;
            case 9:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.topLeft, s.centreTop, s.centreBottom, s.bottomLeft);
                break;
            case 12:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.topLeft, s.topRight, s.centreRight, s.centreLeft);
                break;
            case 5:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.centreTop, s.topRight, s.centreRight, s.centreBottom, s.bottomLeft, s.centreLeft);
                break;
            case 10:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.topLeft, s.centreTop, s.centreRight, s.bottomRight, s.centreBottom, s.centreLeft);
                break;

            // 3 point:
            case 7:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.centreTop, s.topRight, s.bottomRight, s.bottomLeft, s.centreLeft);
                break;
            case 11:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.topLeft, s.centreTop, s.centreRight, s.bottomRight, s.bottomLeft);
                break;
            case 13:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.topLeft, s.topRight, s.centreRight, s.centreBottom, s.bottomLeft);
                break;
            case 14:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.topLeft, s.topRight, s.bottomRight, s.centreBottom, s.centreLeft);
                break;

            // 4 point:
            case 15:
                mesh_from_points(in triangleDictionary, in meshTriangles, in meshVertices, s.topLeft, s.topRight, s.bottomRight, s.bottomLeft);
                checkedVertices.Add(s.topLeft.vertexIndex);
                checkedVertices.Add(s.topRight.vertexIndex);
                checkedVertices.Add(s.bottomRight.vertexIndex);
                checkedVertices.Add(s.bottomLeft.vertexIndex);
                break;

            default:
                break;
        }
    }

    private void mesh_from_points(in Dictionary<int, List<Triangle>> triangleDictionary, in List<int> meshTriangles, in List<Vector3> meshVertices, params Node[] points)
    {
        assign_vertices(points, in meshVertices);

        int pointsLength = points.Length;
        if (pointsLength >= 3) create_triangle(in triangleDictionary, in meshTriangles, points[0], points[1], points[2]);
        if (pointsLength >= 4) create_triangle(in triangleDictionary, in meshTriangles, points[0], points[2], points[3]);
        if (pointsLength >= 5) create_triangle(in triangleDictionary, in meshTriangles, points[0], points[3], points[4]);
        if (pointsLength >= 6) create_triangle(in triangleDictionary, in meshTriangles, points[0], points[4], points[5]);
    }

    private void assign_vertices(Node[] points, in List<Vector3> meshVertices)
    {
        for(int i = 0; i < points.Length; i++)
        {
            if(points[i].vertexIndex == -1)
            {
                points[i].vertexIndex = meshVertices.Count;
                meshVertices.Add(points[i].position);
            }
        }
    }

    /// <summary>
    /// Set Triangles Indicies from Nodes passed
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="c"></param>
    private void create_triangle(in Dictionary<int, List<Triangle>> triangleDictionary, in List<int> meshTriangles, Node a, Node b, Node c)
    {
        meshTriangles.Add(a.vertexIndex);
        meshTriangles.Add(b.vertexIndex);
        meshTriangles.Add(c.vertexIndex);

        Triangle t = new Triangle(a.vertexIndex, b.vertexIndex, c.vertexIndex);
        add_triangle_to_dictionary(in triangleDictionary, t.vertexIndexA, t);
        add_triangle_to_dictionary(in triangleDictionary, t.vertexIndexB, t);
        add_triangle_to_dictionary(in triangleDictionary, t.vertexIndexC, t);
    }

    /// <summary>
    /// Check TriangleDictionary for vertexIndex of triangle. 
    /// If it contains the vertexIndex, append the Triangle to the List inside the TriangleDictionary.
    /// Otherwise create a new list of triangles and append the list with the vertexIndex as the key
    /// </summary>
    /// <param name="vertexIndexKey"></param>
    /// <param name="t"></param>
    private void add_triangle_to_dictionary(in Dictionary<int, List<Triangle>> triangleDictionary, int vertexIndexKey, Triangle t)
    {
        if (triangleDictionary.ContainsKey(vertexIndexKey))
            triangleDictionary[vertexIndexKey].Add(t);
        else
        {
            List<Triangle> tList = new List<Triangle>();
            tList.Add(t);
            triangleDictionary.Add(vertexIndexKey, tList);
        }
    }

    private void calculate_mesh_outlines(in Dictionary<int, List<Triangle>> triangleDictionary, in List<List<int>> outlines, in List<Vector3> vertices, in HashSet<int> checkedVertices)
    {
        for(int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
        {
            if(!checkedVertices.Contains(vertexIndex))
            {
                int newOutlineVertex = get_connected_outline_vertex(in triangleDictionary, in checkedVertices, vertexIndex);
                if(newOutlineVertex != -1)
                {
                    checkedVertices.Add(vertexIndex);

                    List<int> newOutline = new List<int>();
                    newOutline.Add(vertexIndex);
                    outlines.Add(newOutline);
                    follow_outline(in triangleDictionary, in outlines, in checkedVertices, newOutlineVertex, outlines.Count - 1);
                    outlines[outlines.Count - 1].Add(vertexIndex);
                }
            }
        }
    }

    private void follow_outline(in Dictionary<int, List<Triangle>> triangleDictionary, in List<List<int>> outlines, in HashSet<int> checkedVertices, int vertexIndex, int outlineIndex)
    {
        outlines[outlineIndex].Add(vertexIndex);
        checkedVertices.Add(vertexIndex);
        int nextVertexIndex = get_connected_outline_vertex(in triangleDictionary, in checkedVertices, vertexIndex);
        if (nextVertexIndex != -1)
            follow_outline(in triangleDictionary, in outlines, in checkedVertices, nextVertexIndex, outlineIndex);
    }

    private int get_connected_outline_vertex(in Dictionary<int, List<Triangle>> triangleDictionary, in HashSet<int> checkedVertices, int vertexIndex)
    {
        List<Triangle> tContainingVertex = triangleDictionary[vertexIndex];
        for(int i = 0; i < tContainingVertex.Count; i++)
        {
            Triangle t = tContainingVertex[i];
            for(int j = 0; j < 3; j++)
            {
                int vertexB = t[j];
                if(vertexB != vertexIndex && !checkedVertices.Contains(vertexB))
                {
                    if (is_outline_edge(in triangleDictionary, vertexIndex, vertexB))
                        return vertexB;
                }
            }
        }
        return -1;
    }

    private bool is_outline_edge(in Dictionary<int, List<Triangle>> triangleDictionary, int vertexA, int vertexB)
    {
        List<Triangle> tContainingVertexA = triangleDictionary[vertexA];
        int sharedTriangleCount = 0;

        for(int i = 0; i < tContainingVertexA.Count; i++)
        {
            if(tContainingVertexA[i].contains(vertexB))
            {
                sharedTriangleCount++;
                if (sharedTriangleCount > 1)
                    break;
            }
        }
        return sharedTriangleCount == 1;
    }
}

struct Triangle
{
    public int vertexIndexA;
    public int vertexIndexB;
    public int vertexIndexC;
    int[] vertices;

    public Triangle(int pointA, int pointB, int pointC)
    {
        vertexIndexA    = pointA;
        vertexIndexB    = pointB;
        vertexIndexC    = pointC;
        vertices        = new int[3] {pointA, pointB, pointC};
    }

    public int this[int i]
    {
        get { return vertices[i]; }
    }

    public bool contains(int vertexIndex)
    {
        return vertexIndex == vertexIndexA || vertexIndex == vertexIndexB || vertexIndex == vertexIndexC;
    }
}