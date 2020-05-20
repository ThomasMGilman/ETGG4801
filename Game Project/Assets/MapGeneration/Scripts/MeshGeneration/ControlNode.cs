using UnityEngine;

public class ControlNode : Node
{
    public bool active;
    public Node forward, right;
    public ControlNode(Vector3 pos, bool active, float squareSize) : base(pos)
    {
        this.active = active;
        forward = new Node(pos + Vector3.forward * squareSize / 2f);
        right = new Node(pos + Vector3.right * squareSize / 2f);
    }
}