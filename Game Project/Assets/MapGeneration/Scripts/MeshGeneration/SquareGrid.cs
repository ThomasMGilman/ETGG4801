using UnityEngine;

public class SquareGrid
{
    public Square[,] squares;
    public SquareGrid(int[,] map, float squareSize, int controlNodeType = 1)
    {
        int nodeCountX = map.GetLength(0);
        int nodeCountY = map.GetLength(1);
        float mapWidth = nodeCountX * squareSize;
        float mapHeight = nodeCountY * squareSize;
        float square_andHalf = squareSize + squareSize / 2;
        float halfMapWidth = mapWidth / 2;
        float halfMapHeight = mapHeight / 2;
        ControlNode[,] controlNodes = new ControlNode[nodeCountX, nodeCountY];

        for (int x = 0; x < nodeCountX; x++)
        {
            float xPos = -halfMapWidth + x * square_andHalf;
            for (int y = 0; y < nodeCountY; y++)
            {
                float zPos = -halfMapHeight + y * square_andHalf;
                Vector3 pos = new Vector3(xPos, 0, zPos);
                controlNodes[x, y] = new ControlNode(pos, map[x, y] == controlNodeType, squareSize);
            }
        }

        squares = new Square[nodeCountX - 1, nodeCountY - 1];
        for (int x = 0; x < nodeCountX - 1; x++)
        {
            for (int y = 0; y < nodeCountY - 1; y++)
                squares[x, y] = new Square(controlNodes[x, y + 1],          //topLeft
                                            controlNodes[x + 1, y + 1],     //topRight
                                            controlNodes[x + 1, y],         //bottomRight
                                            controlNodes[x, y]);            //bottomLeft
        }
    }
}