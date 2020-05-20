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
        this.topLeft = topLeft;
        this.topRight = topRight;
        this.bottomRight = bottomRight;
        this.bottomLeft = bottomLeft;


        centreTop = topLeft.right;
        centreRight = bottomRight.forward;
        centreBottom = bottomLeft.right;
        centreLeft = bottomLeft.forward;

        if (topLeft.active) configuration += 8;
        if (topRight.active) configuration += 4;
        if (bottomRight.active) configuration += 2;
        if (bottomLeft.active) configuration += 1;
    }
}