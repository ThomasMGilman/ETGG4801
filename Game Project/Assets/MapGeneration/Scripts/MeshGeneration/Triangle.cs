
struct Triangle
{
    public int vertexIndexA;
    public int vertexIndexB;
    public int vertexIndexC;
    int[] vertices;

    public Triangle(int pointA, int pointB, int pointC)
    {
        vertexIndexA = pointA;
        vertexIndexB = pointB;
        vertexIndexC = pointC;
        vertices = new int[3] { pointA, pointB, pointC };
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