using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class MapGeneration : MonoBehaviour
{
    public GameObject Room_Prefab;
    public GameObject Spawn_Prefab;
    public GameObject Goal_Prefab;
    public GameObject TmpFloor_Prefab;
    public GameObject TmpRoof_Prefab;
    public string mapSettingsName;

    public static int worldWidth = 5, worldHeight = 5;              //dimensions of Wold Space
    public static int roomWidth = 100, roomHeight = 100;            //dimensions of Room space
    public static float halfRoomWidth, halfRoomHeight;

    public static int hallWidth = 1;                                //Room passage hallwayWidth
    public static int roomConnectionWidth = 1;                      //HallwayBetween MapRoomsWidth
    public static int squareSize = 1;                               //size of each tile
    public static float square_andHalf = 1.5f;
    public static float halfSquareSize = .5f;
    public static int borderSize = 1;                               //Mesh Border size, surrounding rooms
    public static int smoothTimes = 5;                              //Times to smooth the map out

    public static int wallThresholdSize = 50;
    public static int roomThresholdSize = 50;

    public static string seed = "Random";                           //When generating map using seed, uses Hash of string
    public static bool useRandSeed = false;

    public static int maxFillPercent = 52;
    public static int minFillPercent = 37;
    public static int fillPercent;
    public static bool useRandFillPercent = false;

    private static int roomProcessCount = 0;
    private static int numRooms = 0;

    private static int endGoalThreshold = 0;

    private MapRoom[,] map_ObjList;
    private MapRoom endGoalRoom_Obj, startRoom_Obj;
    private GameObject floor_Obj, floor2_Obj;
    private GameObject roof_Obj;
    

    public enum Neighbour{ LEFT, RIGHT, SAME_X, ABOVE, BELOW, SAME_Z };
    // Start is called before the first frame update
    void Start()
    {
        import_map_settings(mapSettingsName);   
        create_world();
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// Map Generation Code
    /// Creates a N by M size map specified by the worldSettings file that the settings are imported from.
    /// Each Room object has a map created using the marching squares algorithm to geberate the rooms layout based on the specified thresholds given from the settings file.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void create_world()
    {
        if (useRandSeed) seed = DateTime.Now.Ticks.ToString();

        float xPos, zPos;
        for(int x = 0; x < worldWidth; x++)
        {
            xPos = x * (roomWidth + halfRoomWidth);
            for (int z = 0; z < worldHeight; z++)
            {
                zPos = z * (roomHeight + halfRoomHeight);
                create_room(x, z, new Vector3(xPos, 15, zPos), true);
            }
        }
    }

    /// <summary>
    /// Creates temporary Floor and Ceiling from planes
    /// </summary>
    private void create_temp_floor_and_ceiling()
    {
        //Creates Floor and Ceiling for 
        Vector3 scaleVec = new Vector3(worldWidth / 2 * roomWidth, 1, worldHeight / 2 * roomHeight);
        Vector3 planePosition = scaleVec + this.transform.position;

        planePosition.y = 10f;
        floor_Obj = Instantiate(TmpFloor_Prefab, planePosition, TmpFloor_Prefab.transform.rotation, this.transform);
        floor_Obj.transform.localScale = scaleVec;

        scaleVec.y = -1;
        planePosition.y = 15f;
        roof_Obj = Instantiate(TmpRoof_Prefab, planePosition, TmpRoof_Prefab.transform.rotation, this.transform);
        roof_Obj.transform.localScale = scaleVec;

        planePosition.y += 1f;
        floor2_Obj = Instantiate(TmpFloor_Prefab, planePosition, TmpFloor_Prefab.transform.rotation, this.transform);
        floor2_Obj.transform.localScale = scaleVec;
    }

    private void create_room(int x, int z, Vector3 roomPosition, bool doGeneration)
    {
        if (doGeneration)
        {
            map_ObjList[x, z] = create_room_struct(x, z, doGeneration, (x == 0 && z == 0), Instantiate(Room_Prefab, roomPosition, Room_Prefab.transform.rotation, this.transform));
            map_ObjList[x, z].worldPos = roomPosition;
            map_ObjList[x, z].room_Obj.name = "Room: [" + x.ToString() + "," + z.ToString() + "]";

            if (x >= 1) map_ObjList[x, z].unConnectedNeighbours.Add((x - 1, z));
            if (x < worldWidth - 1) map_ObjList[x, z].unConnectedNeighbours.Add((x + 1, z));
            if (z >= 1) map_ObjList[x, z].unConnectedNeighbours.Add((x, z - 1));
            if (z < worldHeight - 1) map_ObjList[x, z].unConnectedNeighbours.Add((x, z + 1));
        }
        else
            map_ObjList[x, z] = create_room_struct(x, z, doGeneration, false);
    }

    MapRoom create_room_struct(int x, int z, bool generated, bool isConnected, GameObject newRoom = null)
    {
        return new MapRoom()
        {
            room_Obj = newRoom,
            mapIndex_X = x,
            mapIndex_Z = z,
            connections = new HashSet<(int, int)>(),
            connectionPath = new List<MapRoom>(),
            unConnectedNeighbours = new List<(int, int)>(),
            generated = generated,
            readyToGenMesh = false,
            connectedToMainRoom = isConnected
        };
    }

    public struct MapRoom
    {
        public HashSet<(int, int)> connections;             // Set of all rooms that this room is connected to by their list position
        public List<MapRoom> connectionPath;                // Rooms to go through to get to this room
        public List<(int, int)> unConnectedNeighbours;      // Neighbouring rooms that havnt been connected in some way yet
        public Vector3 worldPos;                            // Position of object in world space
        public GameObject room_Obj;                         // Room object
        public int mapIndex_X, mapIndex_Z;                  // Position in list
        public bool generated;                              // Whether Rooms Mesh was created or not
        public bool readyToGenMesh;                         // Whether the Room was fully created
        public bool connectedToMainRoom;                    // Has Room been connected to main room yet
    };

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// Room Connection Code
    /// This Code Bellow Connects all the Room Objects together.
    /// Makes sure every room has a connection to the main room and is connected in someway to their neighbor room even if not directly so.
    /// Upon Creating a connection to a room, the Room object has a path drawn to a point between the rooms from their nearest internal room.
    /// 
    /// 
    /// Once All the Rooms have been connected together, all the Rooms have their Meshes created. 
    /// Afterwhich an end room has the goal placed into it and the first room in the map_ObjList(mainRoom (0,0)) has the spawn placed into it.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Creates a Point package of two PointToSend Structs for the rooms to get and draw a passage to
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private ((int,int), (int,int)) get_point_package(MapRoom a, MapRoom b)
    {
        //SetPoint between Rooms to create a passage to
        (int, int) connectionPoint = (0, 0), otherPoint = (0, 0);

        //Set point X Pos
        if (b.mapIndex_X < a.mapIndex_X)
        {
            connectionPoint.Item1 = 0;
            otherPoint.Item1 = roomWidth - 1;
        }
        else if (b.mapIndex_X > a.mapIndex_X)
        {
            connectionPoint.Item1 = roomWidth - 1;
            otherPoint.Item1 = 0;
        }
        else
        {
            connectionPoint.Item1 = UnityEngine.Random.Range(0, roomWidth - roomConnectionWidth - 1);
            otherPoint.Item1 = connectionPoint.Item1;
        }
        //Set point Z Pos
        if (b.mapIndex_Z < a.mapIndex_Z)
        {
            connectionPoint.Item2 = 0;
            otherPoint.Item2 = roomHeight - 1;
        }
        else if (b.mapIndex_Z > a.mapIndex_Z)
        {
            connectionPoint.Item2 = roomHeight - 1;
            otherPoint.Item2 = 0;
        }
        else
        {
            connectionPoint.Item2 = UnityEngine.Random.Range(0, roomHeight - roomConnectionWidth - 1);
            otherPoint.Item2 = connectionPoint.Item2;
        }
        return (connectionPoint, otherPoint);
    }

    private void update_connections(ref MapRoom room, (int,int) newConnection)
    {
        room.connectionPath.Add(map_ObjList[newConnection.Item1, newConnection.Item2]);
        map_ObjList[newConnection.Item1, newConnection.Item2].connectionPath.Add(room);
        foreach ((int, int) coord in room.connections)
        {
            if(coord != (room.mapIndex_X,room.mapIndex_Z))
            {
                map_ObjList[coord.Item1, coord.Item2].connections.Add(newConnection);                                                   //updateConnection
                map_ObjList[newConnection.Item1, newConnection.Item2].connections.Add(coord);
                if (map_ObjList[coord.Item1, coord.Item2].unConnectedNeighbours.Contains(newConnection))                                //if connection is has neighbour as unconnected, connect it
                {
                    map_ObjList[coord.Item1, coord.Item2].unConnectedNeighbours.Remove(newConnection);
                    map_ObjList[newConnection.Item1, newConnection.Item2].unConnectedNeighbours.Remove(coord);
                }
                if (map_ObjList[newConnection.Item1, newConnection.Item2].connectedToMainRoom)                                          //if new connection connectedToMainRoom, set it true
                    map_ObjList[coord.Item1, coord.Item2].connectedToMainRoom = true;

                map_ObjList[coord.Item1, coord.Item2].connections.UnionWith(map_ObjList[newConnection.Item1, newConnection.Item2].connections); //UnionConnections
                map_ObjList[newConnection.Item1, newConnection.Item2].connections.UnionWith(map_ObjList[coord.Item1, coord.Item2].connections);
            }
        }
        room.connections.Add(newConnection);
        room.unConnectedNeighbours.Remove(newConnection);
        room.connections.UnionWith(map_ObjList[newConnection.Item1, newConnection.Item2].connections);
    }

    private void connect_rooms()
    {
        List<MapRoom> notConnected = new List<MapRoom>();
        List<int> indeciesToRemove = new List<int>();
        System.Random rand = new System.Random();

        foreach (MapRoom room in map_ObjList)
            notConnected.Add(room);
        /////////////////////////////////////////////////////////////////////////////////////////////////// While Rooms not Connected
        while(notConnected.Count > 0)
        {
            int ri = rand.Next(0, notConnected.Count - 1);
            int x = notConnected[ri].mapIndex_X;
            int z = notConnected[ri].mapIndex_Z;
            int oX, oZ;

            //Room is connected to itself
            if (!map_ObjList[x, z].connections.Contains((x, z)))
                map_ObjList[x, z].connections.Add((x, z));

            if (map_ObjList[x, z].unConnectedNeighbours.Count == 0) //connected to all its neighbours in some way
                notConnected.RemoveAt(ri);
            else
            {
                int neighbourIndex = rand.Next(0, map_ObjList[x, z].unConnectedNeighbours.Count - 1);
                (oX,oZ) = map_ObjList[x, z].unConnectedNeighbours[neighbourIndex]; //Get random neighbour room to connect to

                update_connections(ref map_ObjList[x, z], (oX,oZ));
                update_connections(ref map_ObjList[oX, oZ], (x, z));

                //Tell Rooms to create passage to point specified
                ((int,int), (int,int)) pPack = get_point_package(map_ObjList[x, z], map_ObjList[oX, oZ]);
                map_ObjList[x, z].room_Obj.SendMessage("connect_to_point", pPack.Item1);
                map_ObjList[oX, oZ].room_Obj.SendMessage("connect_to_point", pPack.Item2);
            }
        }
    }

    private void set_end_goal_and_spawn()
    {
        int randEndGoalX = UnityEngine.Random.Range(worldWidth - 1 - endGoalThreshold, worldWidth - 1);
        if (randEndGoalX <= 0) randEndGoalX = worldWidth - 1;

        int randEndGoalZ = UnityEngine.Random.Range(worldHeight - 1 - endGoalThreshold, worldHeight - 1);
        if (randEndGoalZ <= 0) randEndGoalZ = worldHeight - 1;
        endGoalRoom_Obj = map_ObjList[randEndGoalX, randEndGoalZ];
        endGoalRoom_Obj.room_Obj.SendMessage("set_goal", 2500);

        foreach(MapRoom r in map_ObjList)
        {
            
            if (!(r.mapIndex_X == 0 && r.mapIndex_Z == 0))
            {
                int randNumGoals = UnityEngine.Random.Range(5, 10);
                while(randNumGoals > 0)
                {
                    r.room_Obj.SendMessage("set_goal", -1);
                    randNumGoals--;
                }
            }
        }

        startRoom_Obj = map_ObjList[0, 0];
        startRoom_Obj.room_Obj.SendMessage("set_spawn");
    }

    /// <summary>
    /// After the last room has finished initializing the room layout, connect all the rooms together,
    /// Find furthest room path and create goal, then finally generate the meshes.
    /// </summary>
    private void room_finished()
    {
        roomProcessCount++;
        if (roomProcessCount == numRooms)
        {
            connect_rooms();
            set_end_goal_and_spawn();
        }
        //FinaleCheck Tells Rooms to generate their assigned Mesh's
        if(roomProcessCount == numRooms + 2)
        {
            foreach (MapRoom room in map_ObjList)
            {
                //room.Room.SendMessage("debugShowRoomTiles");
                room.room_Obj.SendMessage("generate_room_mesh");
            }
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// Debug functions

    public void print_variables()
    {
        print("WorldWidth/Height: [" + worldWidth + ", " + worldHeight + "]" +
            "\nRoomWidth/Height: [" + roomWidth + ", " + roomHeight + "]" +
            "\nHalfWidth/Height: [" + halfRoomWidth + ", " + halfRoomHeight + "]" +
            "\nHallWidtht: " + hallWidth + "" +
            "\nSquareSize: " + squareSize + "" +
            "\nBorederSize: " + borderSize + "" +
            "\nSmoothTimes: " + smoothTimes + "" +
            "\nWallThresholdSize: " + wallThresholdSize + "" +
            "\nRoomThresholdSize: " + roomThresholdSize + "" +
            "\nSeed: " + seed + "\n");
    }

    public void print_exception_message(string exceptionLocation, System.Exception e)
    {
        print(exceptionLocation+" Exception!!!:\n\t" + e.Message + 
            "\n\tStackTrace: " + e.StackTrace + 
            "\n\tSource: " + e.Source + 
            "\n\tInnerMessage: " + e.InnerException.Message);
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// import function
    /// Imports settings from the settingsFile for generating the rooms and size of the map
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Import Global MapGeneration Settings
    /// </summary>
    /// <param name="fileName"></param>
    private void import_map_settings(string fileName)
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
                        worldWidth = Int32.Parse(line[1].Trim());
                        break;
                    case ("worldheight"):
                        worldHeight = Int32.Parse(line[1].Trim());
                        break;
                    case ("roomwidth"):
                        roomWidth = Int32.Parse(line[1].Trim());
                        halfRoomWidth = roomWidth / 2;
                        break;
                    case ("roomheight"):
                        roomHeight = Int32.Parse(line[1].Trim());
                        halfRoomHeight = roomHeight / 2;
                        break;
                    case ("hallwidth"):
                        hallWidth = Int32.Parse(line[1].Trim());
                        break;
                    case ("roomconnectionwidth"):
                        roomConnectionWidth = Int32.Parse(line[1].Trim());
                        break;

                    case ("squaresize"):
                        squareSize = Int32.Parse(line[1].Trim());
                        halfSquareSize = squareSize / 2;
                        square_andHalf = squareSize + halfSquareSize;
                        break;
                    case ("bordersize"):
                        borderSize = Int32.Parse(line[1].Trim());
                        break;

                    case ("smoothtimes"):
                        smoothTimes = Int32.Parse(line[1].Trim());
                        break;

                    case ("wallthresholdsize"):
                        wallThresholdSize = Int32.Parse(line[1].Trim());
                        break;
                    case ("roomthresholdsize"):
                        roomThresholdSize = Int32.Parse(line[1].Trim());
                        break;

                    case ("seed"):
                        seed = line[1].Trim();
                        break;
                    case ("userandseed"):
                        useRandSeed = Boolean.Parse(line[1].Trim());
                        break;

                    case ("randfillpercent"):
                        int fillAmount = Int32.Parse(line[1].Trim());
                        fillAmount = fillAmount > maxFillPercent ? maxFillPercent : fillAmount;
                        fillAmount = fillAmount < minFillPercent ? minFillPercent : fillAmount;
                        fillPercent = fillAmount;
                        break;

                    case ("userandfillpercent"):
                        useRandFillPercent = bool.Parse(line[1].Trim());
                        break;

                    case ("endgoalthreshold"):
                        endGoalThreshold = Int32.Parse(line[1].Trim());
                        if (endGoalThreshold < 0) endGoalThreshold = 0;
                        if (worldHeight - endGoalThreshold < 1) endGoalThreshold = worldHeight - 1;
                        if (worldWidth - endGoalThreshold < 1) endGoalThreshold = worldWidth - 1;
                        break;
                    default:
                        print("\nSettingsException: " + line[0] + " doesnt exist as a valid setting!!\n\tLine: " + lines[i] + "\n\tLineLength: " + lines[i].Length + "\n");
                        break;
                }
            }
        }

        //Set Globals
        roomProcessCount = 0;
        numRooms = worldWidth * worldHeight;
        map_ObjList = new MapRoom[worldWidth, worldHeight];
    }

    private void OnDestroy()
    {
        for(int i = 0; i < this.transform.childCount; i++)
        {
            Destroy(this.transform.GetChild(i));
        }
        Destroy(this.gameObject);
    }
}
