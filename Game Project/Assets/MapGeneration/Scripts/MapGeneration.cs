using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class MapGeneration : MonoBehaviour
{
    public GameObject Room_prefab;
    public GameObject Spawn_prefab;
    public GameObject Goal_prefab;
    public GameObject tmpFloor_prefab;
    public GameObject tmpRoof_prefab;
    public string mapSettingsName;

    public static int WorldWidth = 5, WorldHeight = 5;              //dimensions of Wold Space
    public static int RoomWidth = 100, RoomHeight = 100;            //dimensions of Room space
    public static float HalfWidth, HalfHeight;

    public static int HallWidth = 1;                                //Room passage hallwayWidth
    public static int roomConnectionWidth = 1;                      //HallwayBetween MapRoomsWidth
    public static int SquareSize = 1;                               //size of each tile
    public static float halfSquareSize = .5f;
    public static int BorderSize = 1;                               //Mesh Border size, surrounding rooms
    public static int SmoothTimes = 5;                              //Times to smooth the map out

    public static int WallThresholdSize = 50;
    public static int RoomThresholdSize = 50;

    public static string Seed = "Random";                           //When generating map using seed, uses Hash of string
    public static bool UseRandSeed = false;

    public static int MaxFillPercent = 90;
    public static int MinFillPercent = 10;
    public static int RandFillPercent;
    public static bool UseRandFillPercent = false;

    private static int roomProcessCount = 0;
    private static int numRooms = 0;

    private static int endGoalThreshold = 0;

    private Map_Room[,] Map;
    private GameObject floor, floor2;
    private GameObject roof;
    private Map_Room endGoalRoom, startRoom;

    public enum neighbour{ LEFT, RIGHT, SAME_X, ABOVE, BELOW, SAME_Z };
    // Start is called before the first frame update
    void Start()
    {
        roomProcessCount = 0;
        endGoalThreshold = 0;

        importMapSettings(mapSettingsName);
        numRooms = WorldWidth * WorldHeight;
        Map = new Map_Room[WorldWidth, WorldHeight];
        createWorld();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void createWorld()
    {
        if (UseRandSeed) Seed = DateTime.Now.Ticks.ToString();

        float xPos, zPos;
        for(int x = 0; x < WorldWidth; x++)
        {
            xPos = getRoomPosX(x);
            for(int z = 0; z < WorldHeight; z++)
            {
                zPos = getRoomPosZ(z);
                createRoom(x, z, new Vector3(xPos, 15, zPos), true);//(UnityEngine.Random.Range(0, 100) < RandFillPercent || (x == 0 && z == 0)) ? true : false);
            }
        }

        Vector3 scaleVec = new Vector3(WorldWidth / 2 * RoomWidth, 1, WorldHeight / 2 * RoomHeight);
        Vector3 planePosition = scaleVec + this.transform.position;
        planePosition.y = 10f;
        floor = Instantiate(tmpFloor_prefab, planePosition, tmpFloor_prefab.transform.rotation, this.transform);
        floor.transform.localScale = scaleVec;

        scaleVec.y = -1;
        planePosition.y = 15f;
        roof = Instantiate(tmpRoof_prefab, planePosition, tmpRoof_prefab.transform.rotation, this.transform);
        roof.transform.localScale = scaleVec;
        planePosition.y += 1f;
        floor2 = Instantiate(tmpFloor_prefab, planePosition, tmpFloor_prefab.transform.rotation, this.transform);
        floor2.transform.localScale = scaleVec;
    }

    /// <summary>
    /// Creates a Point package of two PointToSend Structs for the rooms to get and draw a passage to
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private ((int,int), (int,int)) getPointPackage(Map_Room a, Map_Room b)
    {
        //SetPoint between Rooms to create a passage to
        (int, int) connectionPoint = (0,0);
        (int, int) otherPoint = (0,0);
        //Set point X Pos
        if (b.mapIndex_X < a.mapIndex_X)
        {
            connectionPoint.Item1 = 0;
            otherPoint.Item1 = RoomWidth - 1;
        }
        else if (b.mapIndex_X > a.mapIndex_X)
        {
            connectionPoint.Item1 = RoomWidth - 1;
            otherPoint.Item1 = 0;
        }
        else
        {
            connectionPoint.Item1 = UnityEngine.Random.Range(0, RoomWidth - roomConnectionWidth - 1);
            otherPoint.Item1 = connectionPoint.Item1;
        }
        //Set point Z Pos
        if (b.mapIndex_Z < a.mapIndex_Z)
        {
            connectionPoint.Item2 = 0;
            otherPoint.Item2 = RoomHeight - 1;
        }
        else if (b.mapIndex_Z > a.mapIndex_Z)
        {
            connectionPoint.Item2 = RoomHeight - 1;
            otherPoint.Item2 = 0;
        }
        else
        {
            connectionPoint.Item2 = UnityEngine.Random.Range(0, RoomHeight - roomConnectionWidth - 1);
            otherPoint.Item2 = connectionPoint.Item2;
        }
        return (connectionPoint, otherPoint);
    }

    private void updateConnections(ref Map_Room room, (int,int) newConnection)
    {
        room.connectionPath.Add(Map[newConnection.Item1, newConnection.Item2]);
        Map[newConnection.Item1, newConnection.Item2].connectionPath.Add(room);
        foreach ((int, int) coord in room.connections)
        {
            if(coord != (room.mapIndex_X,room.mapIndex_Z))
            {
                Map[coord.Item1, coord.Item2].connections.Add(newConnection);                                                   //updateConnection
                Map[newConnection.Item1, newConnection.Item2].connections.Add(coord);
                if (Map[coord.Item1, coord.Item2].unConnectedNeighbours.Contains(newConnection))                                //if connection is has neighbour as unconnected, connect it
                {
                    Map[coord.Item1, coord.Item2].unConnectedNeighbours.Remove(newConnection);
                    Map[newConnection.Item1, newConnection.Item2].unConnectedNeighbours.Remove(coord);
                }
                if (Map[newConnection.Item1, newConnection.Item2].ConnectedToMainRoom)                                          //if new connection connectedToMainRoom, set it true
                    Map[coord.Item1, coord.Item2].ConnectedToMainRoom = true;

                Map[coord.Item1, coord.Item2].connections.UnionWith(Map[newConnection.Item1, newConnection.Item2].connections); //UnionConnections
                Map[newConnection.Item1, newConnection.Item2].connections.UnionWith(Map[coord.Item1, coord.Item2].connections);
            }
        }
        room.connections.Add(newConnection);
        room.unConnectedNeighbours.Remove(newConnection);
        room.connections.UnionWith(Map[newConnection.Item1, newConnection.Item2].connections);
    }

    private void connectRooms()
    {
        List<Map_Room> notConnected = new List<Map_Room>();
        List<int> indeciesToRemove = new List<int>();
        System.Random rand = new System.Random();

        foreach (Map_Room room in Map)
            notConnected.Add(room);
        /////////////////////////////////////////////////////////////////////////////////////////////////// While Rooms not Connected
        while(notConnected.Count > 0)
        {
            int ri = rand.Next(0, notConnected.Count - 1);
            int x = notConnected[ri].mapIndex_X;
            int z = notConnected[ri].mapIndex_Z;
            int oX, oZ;

            //Room is connected to itself
            if (!Map[x, z].connections.Contains((x, z)))
                Map[x, z].connections.Add((x, z));

            if (Map[x, z].unConnectedNeighbours.Count == 0) //connected to all its neighbours in some way
                notConnected.RemoveAt(ri);
            else
            {
                int neighbourIndex = rand.Next(0, Map[x, z].unConnectedNeighbours.Count - 1);
                (oX,oZ) = Map[x, z].unConnectedNeighbours[neighbourIndex]; //Get random neighbour room to connect to

                updateConnections(ref Map[x, z], (oX,oZ));
                updateConnections(ref Map[oX, oZ], (x, z));

                //Tell Rooms to create passage to point specified
                ((int,int), (int,int)) pPack = getPointPackage(Map[x, z], Map[oX, oZ]);
                Map[x, z].Room.SendMessage("connectToPoint", pPack.Item1);
                Map[oX, oZ].Room.SendMessage("connectToPoint", pPack.Item2);
            }
        }
    }

    private void generateMeshes()
    {
        foreach (Map_Room room in Map)
        {
            //room.Room.SendMessage("debugShowRoomTiles");
            room.Room.SendMessage("generateRoomMesh");
        }
    }

    private float getRoomPosX(int x)
    {
        return x * (RoomWidth + HalfWidth);
    }

    private float getRoomPosZ(int z)
    {
        return z * (RoomHeight + HalfHeight);
    }

    public bool isRoomGenerated(int x, int z)
    {
        return Map[x,z].Generated;
    }
    
    Map_Room createRoomStruct(int x, int z, bool generated, bool isConnected, GameObject newRoom = null)
    {
        return new Map_Room() { Room = newRoom,
                                mapIndex_X = x, mapIndex_Z = z,
                                connections = new HashSet<(int, int)>(),
                                connectionPath = new List<Map_Room>(),
                                unConnectedNeighbours = new List<(int, int)>(),
                                Generated = generated,
                                ReadyToGenMesh = false,
                                ConnectedToMainRoom = isConnected };
    }

    private void createRoom(int x, int z, Vector3 roomPosition, bool doGeneration)
    {
        if (doGeneration)
        {
            Map[x, z] = createRoomStruct(x, z, doGeneration, (x == 0 && z == 0), Instantiate(Room_prefab, roomPosition, Room_prefab.transform.rotation, this.transform));
            Map[x, z].worldPos = roomPosition;
            Map[x, z].Room.name = "Room: [" + x.ToString() + "," + z.ToString() + "]";

            if (x >= 1)             Map[x, z].unConnectedNeighbours.Add((x - 1, z));
            if (x < WorldWidth - 1)  Map[x, z].unConnectedNeighbours.Add((x + 1, z));
            if (z >= 1)             Map[x, z].unConnectedNeighbours.Add((x, z-1));
            if (z < WorldHeight - 1) Map[x, z].unConnectedNeighbours.Add((x, z+1));
        }
        else
            Map[x, z] = createRoomStruct(x, z, doGeneration, false);
    }

    public struct Map_Room
    {
        public GameObject Room;
        public int mapIndex_X, mapIndex_Z;
        public HashSet<(int, int)> connections;
        public List<Map_Room> connectionPath;
        public List<(int, int)> unConnectedNeighbours;
        public Vector3 worldPos;
        public bool Generated;
        public bool ReadyToGenMesh;
        public bool ConnectedToMainRoom;
    };

    private void roomReady(Transform t)
    {
        for(int x = 0; x < WorldWidth; x++)
        {
            for(int z = 0; z < WorldHeight; z++)
            {
                if(Map[x,z].Generated)
                {
                    if (Map[x, z].Room != null && Map[x, z].Room.name == t.name)
                        Map[x, z].ReadyToGenMesh = true;
                }
            }
        }
    }

    private void setEndGoalAndSpawn()
    {
        int randEndGoalX = UnityEngine.Random.Range(WorldWidth - 1 - endGoalThreshold, WorldWidth - 1);
        if (randEndGoalX <= 0) randEndGoalX = WorldWidth - 1;

        int randEndGoalZ = UnityEngine.Random.Range(WorldHeight - 1 - endGoalThreshold, WorldHeight - 1);
        if (randEndGoalZ <= 0) randEndGoalZ = WorldHeight - 1;
        endGoalRoom = Map[randEndGoalX, randEndGoalZ];
        endGoalRoom.Room.SendMessage("setGoal", 1000);

        foreach(Map_Room r in Map)
        {
            
            if (!(r.mapIndex_X == 0 && r.mapIndex_Z == 0))
            {
                r.Room.SendMessage("setGoal", -1);
            }
        }

        startRoom = Map[0, 0];
        startRoom.Room.SendMessage("setSpawn");
    }

    /// <summary>
    /// After the last room has finished initializing the room layout, connect all the rooms together,
    /// Find furthest room path and create goal, then finally generate the meshes.
    /// </summary>
    private void roomFinished()
    {
        roomProcessCount++;
        if (roomProcessCount == numRooms)
        {
            connectRooms();
            setEndGoalAndSpawn();
        }
        if(roomProcessCount == numRooms + 2) //FinaleCheck
        {
            generateMeshes();
        }
    }

    public void printVariables()
    {
        print("WorldWidth/Height: [" + WorldWidth + ", " + WorldHeight + "]" +
            "\nRoomWidth/Height: [" + RoomWidth + ", " + RoomHeight + "]" +
            "\nHalfWidth/Height: [" + HalfWidth + ", " + HalfHeight + "]" +
            "\nHallWidtht: " + HallWidth + "" +
            "\nSquareSize: " + SquareSize + "" +
            "\nBorederSize: " + BorderSize + "" +
            "\nSmoothTimes: " + SmoothTimes + "" +
            "\nWallThresholdSize: " + WallThresholdSize + "" +
            "\nRoomThresholdSize: " + RoomThresholdSize + "" +
            "\nSeed: " + Seed + "\n");
    }

    public void printExceptionMessage(string exceptionLocation, System.Exception e)
    {
        print(exceptionLocation+" Exception!!!:\n\t" + e.Message + 
            "\n\tStackTrace: " + e.StackTrace + 
            "\n\tSource: " + e.Source + 
            "\n\tInnerMessage: " + e.InnerException.Message);
    }

    /// <summary>
    /// Import Global MapGeneration Settings
    /// </summary>
    /// <param name="fileName"></param>
    private void importMapSettings(string fileName)
    {
        TextAsset settings = Resources.Load<TextAsset>(fileName);
        string[] lines = settings.text.Split('\n');
        foreach (string line in lines)
            line.Trim();
        if (!(lines.Length > 1))
        {
            print("Settings File contains ZERO settings!!!");
            return;
        }
        for (int i = 0; i < lines.Length; i++)
        {
            if (!(lines[i].Length > 1) || lines[i].StartsWith("#"))
                continue;
            else
            {
                string[] line = lines[i].Split();
                switch (line[0].Trim().ToLower())
                {
                    case ("worldwidth"):
                        WorldWidth = Int32.Parse(line[1].Trim());
                        break;
                    case ("worldheight"):
                        WorldHeight = Int32.Parse(line[1].Trim());
                        break;
                    case ("roomwidth"):
                        RoomWidth = Int32.Parse(line[1].Trim());
                        HalfWidth = RoomWidth / 2;
                        break;
                    case ("roomheight"):
                        RoomHeight = Int32.Parse(line[1].Trim());
                        HalfHeight = RoomHeight / 2;
                        break;
                    case ("hallwidth"):
                        HallWidth = Int32.Parse(line[1].Trim());
                        break;
                    case ("roomconnectionwidth"):
                        roomConnectionWidth = Int32.Parse(line[1].Trim());
                        break;

                    case ("squaresize"):
                        SquareSize = Int32.Parse(line[1].Trim());
                        halfSquareSize = SquareSize / 2;
                        break;
                    case ("bordersize"):
                        BorderSize = Int32.Parse(line[1].Trim());
                        break;

                    case ("smoothtimes"):
                        SmoothTimes = Int32.Parse(line[1].Trim());
                        break;

                    case ("wallthresholdsize"):
                        WallThresholdSize = Int32.Parse(line[1].Trim());
                        break;
                    case ("roomthresholdsize"):
                        RoomThresholdSize = Int32.Parse(line[1].Trim());
                        break;

                    case ("seed"):
                        Seed = line[1].Trim();
                        break;
                    case ("userandseed"):
                        UseRandSeed = Boolean.Parse(line[1].Trim());
                        break;

                    case ("randfillpercent"):
                        int fillAmount = Int32.Parse(line[1].Trim());
                        fillAmount = fillAmount > MaxFillPercent ? MaxFillPercent : fillAmount;
                        fillAmount = fillAmount < MinFillPercent ? MinFillPercent : fillAmount;
                        RandFillPercent = fillAmount;
                        break;

                    case ("endgoalthreshold"):
                        endGoalThreshold = Int32.Parse(line[1].Trim());
                        if (endGoalThreshold < 0) endGoalThreshold = 0;
                        if (WorldHeight - endGoalThreshold < 1) endGoalThreshold = WorldHeight - 1;
                        if (WorldWidth - endGoalThreshold < 1) endGoalThreshold = WorldWidth - 1;
                        break;
                    default:
                        print("\nSettingsException: " + line[0] + " doesnt exist as a valid setting!!\n\tLine: " + lines[i] + "\n\tLineLength: " + lines[i].Length + "\n");
                        break;
                }
            }
        }
    }

    private void OnDestroy()
    {
        for(int i = 0; i < this.transform.childCount; i++)
        {
            Destroy(this.transform.GetChild(i));
        }
        Destroy(this.gameObject);
    }

    /*delegate void TreeVisitor<RoomTree>(Map_Room room);

    class RoomTree
    {
        private Map_Room NodeRoom;
        private LinkedList<RoomTree> children;

        public RoomTree(Map_Room NodeRoom)
        {
            this.NodeRoom = NodeRoom;
            this.children = new LinkedList<RoomTree>();
        }

        public void AddChild(Map_Room roomToAdd)
        {
            this.children.AddFirst(new RoomTree(roomToAdd));
        }

        public RoomTree getChild(int i)
        {
            foreach(RoomTree r in children)
            {
                if (--i == 0)
                    return r;
            }
            return null;
        }

        public void Traverse(RoomTree node, TreeVisitor<RoomTree> visitor)
        {
            visitor(node.NodeRoom);
            foreach (RoomTree child in node.children)
                Traverse(child, visitor);
        }
    }*/

}
