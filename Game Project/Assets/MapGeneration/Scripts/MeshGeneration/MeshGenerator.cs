using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class MeshGenerator : MonoBehaviour
{
    public int tileAmount = 10;

    public float wallHeight = 10;  //Height of the walls

    public SquareGrid Square_Grid;
    public MeshFilter Wall_MeshFilter;
    public MeshFilter Room_MeshFilter;

    public bool is2D;

    List<Vector3> Mesh_Vertices;
    List<int> Mesh_Triangles;

    Dictionary<int, List<Triangle>> TriangleDictionary = new Dictionary<int, List<Triangle>>();
    List<List<int>> Outlines = new List<List<int>>();
    HashSet<int> CheckedVertices = new HashSet<int>();

    public void GenerateMesh(int[,] map, float squareSize)
    {
        TriangleDictionary.Clear();
        Outlines.Clear();
        CheckedVertices.Clear();

        Square_Grid = new SquareGrid(map, squareSize);

        Mesh_Vertices = new List<Vector3>();
        Mesh_Triangles = new List<int>();

        for (int x = 0; x < Square_Grid.squares.GetLength(0); x++)
        {
            for(int y = 0; y < Square_Grid.squares.GetLength(1); y++)
                TriangulateSquares(Square_Grid.squares[x, y]);
        }

        Mesh mesh = new Mesh();
        Room_MeshFilter.mesh = mesh;

        mesh.vertices = Mesh_Vertices.ToArray();
        mesh.triangles = Mesh_Triangles.ToArray();
        setUVs(in mesh, Mesh_Vertices, true);

        if (is2D)
            Generate2DColliders();
        else
            CreateWallMesh();
    }

    public void setUVs(in Mesh mesh, List<Vector3> vertices, bool usingZ = false)
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
    
    private void CreateWallMesh()
    {
        CalculateMeshOutlines();

        List<Vector3> wallVertices = new List<Vector3>();
        List<int> wallTriangles = new List<int>();

        Mesh wallMesh = new Mesh();
        foreach(List<int> outline in Outlines)
        {
            for(int i = 0; i < outline.Count - 1; i++)
            {
                int startIndex = wallVertices.Count;
                wallVertices.Add(Mesh_Vertices[outline[i]]);                                 // left
                wallVertices.Add(Mesh_Vertices[outline[i + 1]]);                             // right
                wallVertices.Add(Mesh_Vertices[outline[i]] - Vector3.up * wallHeight);       // bottom left
                wallVertices.Add(Mesh_Vertices[outline[i + 1]] - Vector3.up * wallHeight);   // bottom right


                wallTriangles.Add(startIndex + 0);
                wallTriangles.Add(startIndex + 2);
                wallTriangles.Add(startIndex + 3);

                wallTriangles.Add(startIndex + 3);
                wallTriangles.Add(startIndex + 1);
                wallTriangles.Add(startIndex + 0);
            }
        }
        wallMesh.vertices = wallVertices.ToArray();
        wallMesh.triangles = wallTriangles.ToArray();
        setUVs(wallMesh, wallVertices);

        this.Wall_MeshFilter.mesh = wallMesh;

        MeshCollider wallCollider = Wall_MeshFilter.gameObject.AddComponent<MeshCollider>();
        wallCollider.sharedMesh = wallMesh;
    }

    void Generate2DColliders()
    {
        EdgeCollider2D[] currentColliders = gameObject.GetComponent<EdgeCollider2D[]>();
        for(int i = 0; i < currentColliders.Length; i++)
            Destroy(currentColliders[i]);

        CalculateMeshOutlines();

        foreach(List<int> outline in Outlines)
        {
            EdgeCollider2D edgeCollider = gameObject.AddComponent<EdgeCollider2D>();
            Vector2[] edgePoints = new Vector2[outline.Count];

            for (int i = 0; i < outline.Count; i++)
                edgePoints[i] = new Vector2(Mesh_Vertices[outline[i]].x, Mesh_Vertices[outline[i]].z);

            edgeCollider.points = edgePoints;
        }
    }

    private void TriangulateSquares(Square s)
    {
        switch(s.configuration)
        {
            case 1:
                MeshFromPoints(s.centreLeft, s.centreBottom, s.bottomLeft);
                break;
            case 2:
                MeshFromPoints(s.bottomRight, s.centreBottom, s.centreRight);
                break;
            case 4:
                MeshFromPoints(s.topRight, s.centreRight, s.centreTop);
                break;
            case 8:
                MeshFromPoints(s.topLeft, s.centreTop, s.centreLeft);
                break;

            // 2 points:
            case 3:
                MeshFromPoints(s.centreRight, s.bottomRight, s.bottomLeft, s.centreLeft);
                break;
            case 6:
                MeshFromPoints(s.centreTop, s.topRight, s.bottomRight, s.centreBottom);
                break;
            case 9:
                MeshFromPoints(s.topLeft, s.centreTop, s.centreBottom, s.bottomLeft);
                break;
            case 12:
                MeshFromPoints(s.topLeft, s.topRight, s.centreRight, s.centreLeft);
                break;
            case 5:
                MeshFromPoints(s.centreTop, s.topRight, s.centreRight, s.centreBottom, s.bottomLeft, s.centreLeft);
                break;
            case 10:
                MeshFromPoints(s.topLeft, s.centreTop, s.centreRight, s.bottomRight, s.centreBottom, s.centreLeft);
                break;

            // 3 point:
            case 7:
                MeshFromPoints(s.centreTop, s.topRight, s.bottomRight, s.bottomLeft, s.centreLeft);
                break;
            case 11:
                MeshFromPoints(s.topLeft, s.centreTop, s.centreRight, s.bottomRight, s.bottomLeft);
                break;
            case 13:
                MeshFromPoints(s.topLeft, s.topRight, s.centreRight, s.centreBottom, s.bottomLeft);
                break;
            case 14:
                MeshFromPoints(s.topLeft, s.topRight, s.bottomRight, s.centreBottom, s.centreLeft);
                break;

            // 4 point:
            case 15:
                MeshFromPoints(s.topLeft, s.topRight, s.bottomRight, s.bottomLeft);
                CheckedVertices.Add(s.topLeft.vertexIndex);
                CheckedVertices.Add(s.topRight.vertexIndex);
                CheckedVertices.Add(s.bottomRight.vertexIndex);
                CheckedVertices.Add(s.bottomLeft.vertexIndex);
                break;

            default:
                break;
        }
    }

    private void MeshFromPoints(params Node[] points)
    {
        AssignVertices(points);

        int pointsLength = points.Length;
        if (pointsLength >= 3) CreateTriangle(points[0], points[1], points[2]);
        if (pointsLength >= 4) CreateTriangle(points[0], points[2], points[3]);
        if (pointsLength >= 5) CreateTriangle(points[0], points[3], points[4]);
        if (pointsLength >= 6) CreateTriangle(points[0], points[4], points[5]);
    }

    private void AssignVertices(Node[] points)
    {
        for(int i = 0; i < points.Length; i++)
        {
            if(points[i].vertexIndex == -1)
            {
                points[i].vertexIndex = Mesh_Vertices.Count;
                Mesh_Vertices.Add(points[i].position);
            }
        }
    }

    /// <summary>
    /// Set Triangles Indicies from Nodes passed
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="c"></param>
    private void CreateTriangle(Node a, Node b, Node c)
    {
        Mesh_Triangles.Add(a.vertexIndex);
        Mesh_Triangles.Add(b.vertexIndex);
        Mesh_Triangles.Add(c.vertexIndex);

        Triangle t = new Triangle(a.vertexIndex, b.vertexIndex, c.vertexIndex);
        AddTriangleToDictionary(t.vertexIndexA, t);
        AddTriangleToDictionary(t.vertexIndexB, t);
        AddTriangleToDictionary(t.vertexIndexC, t);
    }

    /// <summary>
    /// Check TriangleDictionary for vertexIndex of triangle. 
    /// If it contains the vertexIndex, append the Triangle to the List inside the TriangleDictionary.
    /// Otherwise create a new list of triangles and append the list with the vertexIndex as the key
    /// </summary>
    /// <param name="vertexIndexKey"></param>
    /// <param name="t"></param>
    private void AddTriangleToDictionary(int vertexIndexKey, Triangle t)
    {
        if (TriangleDictionary.ContainsKey(vertexIndexKey))
            TriangleDictionary[vertexIndexKey].Add(t);
        else
        {
            List<Triangle> tList = new List<Triangle>();
            tList.Add(t);
            TriangleDictionary.Add(vertexIndexKey, tList);
        }
    }

    private void CalculateMeshOutlines()
    {
        for(int vertexIndex = 0; vertexIndex < Mesh_Vertices.Count; vertexIndex++)
        {
            if(!CheckedVertices.Contains(vertexIndex))
            {
                int newOutlineVertex = GetConnectedOutlineVertex(vertexIndex);
                if(newOutlineVertex != -1)
                {
                    CheckedVertices.Add(vertexIndex);

                    List<int> newOutline = new List<int>();
                    newOutline.Add(vertexIndex);
                    Outlines.Add(newOutline);
                    FollowOutline(newOutlineVertex, Outlines.Count - 1);
                    Outlines[Outlines.Count - 1].Add(vertexIndex);
                }
            }
        }
    }

    private void FollowOutline(int vertexIndex, int outlineIndex)
    {
        Outlines[outlineIndex].Add(vertexIndex);
        CheckedVertices.Add(vertexIndex);
        int nextVertexIndex = GetConnectedOutlineVertex(vertexIndex);
        if (nextVertexIndex != -1)
            FollowOutline(nextVertexIndex, outlineIndex);
    }

    private int GetConnectedOutlineVertex(int vertexIndex)
    {
        List<Triangle> tContainingVertex = TriangleDictionary[vertexIndex];
        for(int i = 0; i < tContainingVertex.Count; i++)
        {
            Triangle t = tContainingVertex[i];
            for(int j = 0; j < 3; j++)
            {
                int vertexB = t[j];
                if(vertexB != vertexIndex && !CheckedVertices.Contains(vertexB))
                {
                    if (IsOutlineEdge(vertexIndex, vertexB))
                        return vertexB;
                }
            }
        }
        return -1;
    }

    private bool IsOutlineEdge(int vertexA, int vertexB)
    {
        List<Triangle> tContainingVertexA = TriangleDictionary[vertexA];
        int sharedTriangleCount = 0;

        for(int i = 0; i < tContainingVertexA.Count; i++)
        {
            if(tContainingVertexA[i].Contains(vertexB))
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

    public bool Contains(int vertexIndex)
    {
        return vertexIndex == vertexIndexA || vertexIndex == vertexIndexB || vertexIndex == vertexIndexC;
    }
}