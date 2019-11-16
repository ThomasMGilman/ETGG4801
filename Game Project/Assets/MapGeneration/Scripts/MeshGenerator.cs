using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        mesh.RecalculateNormals();

        setUVs(in mesh, in map, in squareSize);

        if (is2D)
            Generate2DColliders();
        else
            CreateWallMesh();
    }

    public void setUVs(in Mesh mesh, in int[,] map, in float squareSize)
    {
        Vector2[] uvs = new Vector2[Mesh_Vertices.Count];

        float xLerpAmount = map.GetLength(0) / 2 * squareSize;
        for (int i = 0; i < Mesh_Vertices.Count; i++)
        {
            float percentX = Mathf.InverseLerp(-xLerpAmount, xLerpAmount, Mesh_Vertices[i].x) * tileAmount;
            float percentY = Mathf.InverseLerp(-xLerpAmount, xLerpAmount, Mesh_Vertices[i].z) * tileAmount;
            uvs[i] = new Vector2(percentX, percentY);
        }
        mesh.uv = uvs;
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
                //MeshFromPoints(s.centreBottom, s.bottomLeft, s.centreLeft);
                break;
            case 2:
                MeshFromPoints(s.bottomRight, s.centreBottom, s.centreRight);
                //MeshFromPoints(s.centreRight, s.bottomRight, s.centreBottom);
                break;
            case 4:
                MeshFromPoints(s.topRight, s.centreRight, s.centreTop);
                //MeshFromPoints(s.centreTop, s.topRight, s.centreRight);
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
    
    /*
    /// <summary>
    /// Draw GizmoCube at a Squares CubeNode position.
    /// Color is black if active else white.
    /// </summary>
    /// <param name="orientation">CubeNode of Square</param>
    private void drawGizmoCube_ControlNode(ControlNode orientation)
    {
        Gizmos.color = orientation.active ? Color.black : Color.white;
        Gizmos.DrawCube(orientation.position, Vector3.one * .4f);
    }

    /// <summary>
    /// Draws GizmoCube at a Squares Node position
    /// </summary>
    /// <param name="orientation"></param>
    private void drawGizmoCube_Node(Node orientation)
    {
        Gizmos.DrawCube(orientation.position, Vector3.one * .15f);
    }

    private void OnDrawGizmos()
    {
        
        if(this.squareGrid != null)
        {
            for(int x = 0; x < squareGrid.squares.GetLength(0); x++)
            {
                for(int y = 0; y < squareGrid.squares.GetLength(1); y++)
                {
                    Square s = squareGrid.squares[x, y];
                    drawGizmoCube_ControlNode(s.topLeft);
                    drawGizmoCube_ControlNode(s.topRight);
                    drawGizmoCube_ControlNode(s.bottomRight);
                    drawGizmoCube_ControlNode(s.bottomLeft);

                    Gizmos.color = Color.grey;
                    drawGizmoCube_Node(s.centreTop);
                    drawGizmoCube_Node(s.centreRight);
                    drawGizmoCube_Node(s.centreLeft);
                    drawGizmoCube_Node(s.centreBottom);
                }
            }
        }
        
    }*/
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

public class SquareGrid
{
    public Square[,] squares;
    public SquareGrid(int[,] map, float squareSize)
    {
        int nodeCountX              = map.GetLength(0);
        int nodeCountY              = map.GetLength(1);
        float mapWidth              = nodeCountX * squareSize;
        float mapHeight             = nodeCountY * squareSize;
        float square_andHalf        = squareSize + squareSize / 2;
        float halfMapWidth          = mapWidth / 2;
        float halfMapHeight         = mapHeight / 2;
        ControlNode[,] controlNodes = new ControlNode[nodeCountX, nodeCountY];

        for(int x = 0; x < nodeCountX; x++)
        {
            float xPos = -halfMapWidth + x * square_andHalf;
            for(int y = 0; y < nodeCountY; y++)
            {
                float zPos = -halfMapHeight + y * square_andHalf;
                Vector3 pos = new Vector3(xPos, 0, zPos);
                controlNodes[x, y] = new ControlNode(pos, map[x, y] == 1, squareSize);
            }
        }

        squares = new Square[nodeCountX - 1, nodeCountY - 1];
        for(int x = 0; x < nodeCountX - 1; x++)
        {
            for(int y = 0; y < nodeCountY - 1; y++)
                squares[x, y] = new Square( controlNodes[x, y + 1],         //topLeft
                                            controlNodes[x + 1, y + 1],     //topRight
                                            controlNodes[x + 1, y],         //bottomRight
                                            controlNodes[x, y]);            //bottomLeft
        }
    }
}

/// <summary>
/// Square Tile, with coordinates to vertice Nodes
/// </summary>
public class Square
{
    public ControlNode topLeft, topRight, bottomRight, bottomLeft;
    public Node centreTop, centreRight, centreBottom, centreLeft;
    public int configuration;

    public Square(ControlNode topLeft, ControlNode topRight, ControlNode bottomRight, ControlNode bottomLeft)
    {
        this.topLeft            = topLeft;
        this.topRight           = topRight;
        this.bottomRight        = bottomRight;
        this.bottomLeft         = bottomLeft;
        

        centreTop               = topLeft.right;
        centreRight             = bottomRight.above;
        centreBottom            = bottomLeft.right;
        centreLeft              = bottomLeft.above;

        if (topLeft.active)     configuration += 8;
        if (topRight.active)    configuration += 4;
        if (bottomRight.active) configuration += 2;
        if (bottomLeft.active)  configuration += 1;
    }
}

public class Node
{
    public Vector3 position;
    public int vertexIndex = -1;

    public Node(Vector3 pos)
    {
        position = pos;
    }
}

public class ControlNode : Node
{
    public bool active;
    public Node above, right;
    public ControlNode(Vector3 pos, bool active, float squareSize) : base(pos)
    {
        this.active = active;
        above = new Node(pos + Vector3.forward * squareSize / 2f);
        right = new Node(pos + Vector3.right * squareSize / 2f);
    }
}