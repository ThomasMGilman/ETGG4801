using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class MapGeneration : MonoBehaviour
{
    

    /// Structs and Enums ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// These Structures are for handling incoming data and organization inorder to build and connect all mesh rooms created when spawing the world.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Room Neighbour orientation
    /// </summary>
    public enum Neighbour{ LEFT, RIGHT, SAME_X, ABOVE, BELOW, SAME_Z };

    public struct MapRoom
    {
        public GameObject room_Obj;                         // Room object
        public HashSet<Vector2Int> connections;             // Set of all rooms that this room is connected to by their list position
        public List<MapRoom> connectionPath;                // Rooms to go through to get to this room
        public List<Vector2Int> unConnectedNeighbours;      // Neighbouring rooms that havnt been connected in some way yet
        public Vector3 worldPos;                            // Position of object in world space
        public Vector2Int map_coords;                       // Position in list

        /// <summary>
        /// Creation state booleans.
        /// Used to state in what state of the creation process this room is in.
        /// </summary>
        public bool generated;                              // Whether Rooms Mesh was created or not
        public bool readyToGenMesh;                         // Whether the Room was fully created
        public bool connectedToMainRoom;                    // Has Room been connected to main room yet
    };

    /// Settings and Shared Variables ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// These Are the Prefab mesh objects, along with static variables that are used in each room/mesh creation process. The reason they are public static,
    /// is so that child classes that inherit from the MapGeneration class,
    /// can access them without them being changed by the inspector on every instance of a new child object. Inorder to set the static variables,
    /// they are set using a settings file that is read in on the start of every game.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public GameObject Room_Prefab;
    public GameObject Spawn_Prefab;
    public GameObject Goal_Prefab;
    public GameObject TmpFloor_Prefab;
    public GameObject TmpRoof_Prefab;
    public string mapSettingsName;

    public static int worldWidth = 5, worldHeight = 5;              //dimensions of Wold Space
    public static int roomWidth = 100, roomHeight = 100;            //dimensions of Room space
    public static int roomYPos = 15;
    public static float halfRoomWidth, halfRoomHeight;

    public static int hallWidth = 1;                                //Room passage hallwayWidth
    public static int roomConnectionWidth = 1;                      //HallwayBetween MapRoomsWidth
    public static int squareSize = 1;                               //size of each tile
    public static float halfSquareSize = .5f;
    public static float square_andHalf = 1.5f;
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

    /// Initializer and Destructor //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Entry point for the start of this class, as well as the on destroy action to clean up.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // Start is called before the first frame update
    void Start()
    {
        import_map_settings(mapSettingsName);   
        create_world();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < this.transform.childCount; i++)
        {
            Destroy(this.transform.GetChild(i));
        }
        Destroy(this.gameObject);
    }

    /// Map Generation Code /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Creates a N by M size map specified by the worldSettings file that the settings are imported from.
    /// Each Room object has a map created using the marching squares algorithm to geberate the rooms layout based on the specified thresholds given from the settings file.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Starts initialization of every Room.
    /// </summary>
    private void create_world()
    {
        if (useRandSeed) seed = DateTime.Now.Ticks.ToString();
        for(int x = 0; x < worldWidth; x++)
        {
            for (int z = 0; z < worldHeight; z++)
                create_room(x, z, true);
        }
    }

    /// <summary>
    /// Create a new Room object, given an x, and z coordinate.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="z"></param>
    /// <param name="doGeneration"></param>
    private void create_room(int x, int z, bool doGeneration)
    {
        if (doGeneration)
        {
            Vector3 worldPos = new Vector3(x * (roomWidth + halfRoomWidth), roomYPos, z * (roomHeight + halfRoomHeight));
            MapRoom newRoom = create_room_struct(x, z, doGeneration, (x == 0 && z == 0), Instantiate(Room_Prefab, worldPos, Room_Prefab.transform.rotation, this.transform));
            newRoom.worldPos = worldPos;
            newRoom.room_Obj.name = "Room: [" + x.ToString() + "," + z.ToString() + "]";
            
            // Set unconnected Rooms
            if (x >= 1)  newRoom.unConnectedNeighbours.Add(new Vector2Int(x - 1, z));               // Right of Room
            if (x < worldWidth - 1) newRoom.unConnectedNeighbours.Add(new Vector2Int(x + 1, z));    // Left of Room
            if (z >= 1) newRoom.unConnectedNeighbours.Add(new Vector2Int(x, z - 1));                // Behind Room
            if (z < worldHeight - 1)  newRoom.unConnectedNeighbours.Add(new Vector2Int(x, z + 1));  // In front of Room

            map_ObjList[x, z] = newRoom;
        }
        else
            map_ObjList[x, z] = create_room_struct(x, z, doGeneration, false);
    }

    /// <summary>
    /// Return a new MapRoom Structure
    /// </summary>
    /// <param name="x"></param>
    /// <param name="z"></param>
    /// <param name="generated"></param>
    /// <param name="isConnected"></param>
    /// <param name="newRoom"></param>
    /// <returns></returns>
    MapRoom create_room_struct(int x, int z, bool generated, bool isConnected, GameObject newRoom = null)
    {
        return new MapRoom()
        {
            room_Obj = newRoom,
            connections = new HashSet<Vector2Int>(),
            connectionPath = new List<MapRoom>(),
            unConnectedNeighbours = new List<Vector2Int>(),
            map_coords = new Vector2Int(x, z),
            generated = generated,
            readyToGenMesh = false,
            connectedToMainRoom = isConnected
        };
    }

    /// Room Connection Code /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// This Code Bellow Connects all the Room Objects together.
    /// Makes sure every room has a connection to the main room and is connected in someway to their neighbor room even if not directly so.
    /// Upon Creating a connection to a room, the Room object has a path drawn to a point between the rooms from their nearest internal room.
    /// 
    /// 
    /// Once All the Rooms have been connected together, all the Rooms have their Meshes created. 
    /// Afterwhich an end room has the goal placed into it and the first room in the map_ObjList(mainRoom (0,0)) has the spawn placed into it.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Creates a Point package of two PointToSend Structs (Room Coordinates) 
    /// for the rooms to get and draw a passage to.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private (Vector2Int, Vector2Int) get_point_package(MapRoom a, MapRoom b)
    {
        //SetPoint between Rooms to create a passage to
        Vector2Int aConPoint = Vector2Int.zero;
        Vector2Int bConPoint = Vector2Int.zero;

        int xConPoint = roomWidth - 1;
        int zConPoint = roomHeight - 1;

        ////////////////////////// Set point X Pos
        // room b Right of room a
        if (b.map_coords.x < a.map_coords.x) bConPoint.x = xConPoint;
        // room b Left of room a
        else if (b.map_coords.x > a.map_coords.x) aConPoint.x = xConPoint;
        // rooms are along same x coord plane
        else
        {
            aConPoint.x = UnityEngine.Random.Range(5, xConPoint);
            bConPoint.x = aConPoint.x;
        }

        ////////////////////////// Set point Z Pos
        // room b Behind room a
        if (b.map_coords.y < a.map_coords.y) bConPoint.y = zConPoint;
        // room B in front of room a
        else if (b.map_coords.y > a.map_coords.y) aConPoint.y = zConPoint;
        // rooms are along same z coord plane
        else
        {
            aConPoint.y = UnityEngine.Random.Range(5, zConPoint);
            bConPoint.y = aConPoint.y;
        }

        return (aConPoint, bConPoint);
    }

    /// <summary>
    /// Update the maprooms connections list with the new connection, 
    /// also notify every other room connected of this new connection.
    /// </summary>
    /// <param name="room"></param>
    /// <param name="newConnection"></param>
    private void update_connections(ref MapRoom room, Vector2Int newConnection)
    {
        MapRoom roomToConnect = map_ObjList[newConnection.x, newConnection.y];

        room.connectionPath.Add(roomToConnect);
        roomToConnect.connectionPath.Add(room);

        // Update connected rooms connections to include the new connection
        foreach (Vector2Int coord in room.connections)
        {
            if(coord != room.map_coords)
            {
                MapRoom connectedRoom = map_ObjList[coord.x, coord.y];

                //update Connections
                connectedRoom.connections.Add(newConnection);
                connectedRoom.connections.Add(coord);

                //if connection is has neighbour as unconnected, connect it
                if (connectedRoom.unConnectedNeighbours.Contains(newConnection))
                {
                    connectedRoom.unConnectedNeighbours.Remove(newConnection);
                    connectedRoom.unConnectedNeighbours.Remove(coord);
                }

                //if new connection connectedToMainRoom, set it true
                if (connectedRoom.connectedToMainRoom)
                    connectedRoom.connectedToMainRoom = true;

                //UnionConnections
                connectedRoom.connections.UnionWith(roomToConnect.connections); 
                roomToConnect.connections.UnionWith(connectedRoom.connections);
            }
        }

        room.connections.Add(newConnection);
        room.unConnectedNeighbours.Remove(newConnection);
        room.connections.UnionWith(roomToConnect.connections);
    }

    /// <summary>
    /// Connects all the rooms. Makes sure every room is accessible to one another and can connect to the main start room
    /// </summary>
    private void connect_rooms()
    {
        List<MapRoom> notConnected = new List<MapRoom>();
        List<int> indeciesToRemove = new List<int>();
        System.Random rand = new System.Random();

        // Add all rooms to not connected list, only connected when it is accessible from start
        foreach (MapRoom room in map_ObjList)   
            notConnected.Add(room);
        
        while(notConnected.Count > 0)
        {
            int ri = rand.Next(0, notConnected.Count - 1);
            MapRoom room = notConnected[ri];

            //Room is connected to itself
            if (!room.connections.Contains(room.map_coords))
                room.connections.Add((room.map_coords));

            //Room is Connected to all its neighbours in some way
            if (room.unConnectedNeighbours.Count == 0)
                notConnected.RemoveAt(ri);

            //Pick random neighbouring room to connect to
            else
            {
                int neighbourIndex = rand.Next(0, room.unConnectedNeighbours.Count - 1);
                Vector2Int otherRoomCoords = room.unConnectedNeighbours[neighbourIndex];
                MapRoom otherRoom = map_ObjList[otherRoomCoords.x, otherRoomCoords.y];

                update_connections(ref room, otherRoomCoords);
                update_connections(ref otherRoom, room.map_coords);

                //Tell Rooms to create passage to point specified
                (Vector2Int, Vector2Int) pConPack = get_point_package(room, otherRoom);
                room.room_Obj.SendMessage("connect_to_point", pConPack.Item1);
                otherRoom.room_Obj.SendMessage("connect_to_point", pConPack.Item2);
            }
        }
    }

    /// <summary>
    /// Place End goal object along with spawner plate to get back to
    /// </summary>
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
            if (!(r.map_coords == Vector2Int.zero))
            {
                int randNumGoals = UnityEngine.Random.Range(5, 15);
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

    /// Import Function //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Imports settings from the settingsFile for generating the rooms and size of the map
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Import Global MapGeneration Settings.
    /// Load from settings file located in resources.
    /// </summary>
    /// <param name="fileName"></param>
    private void import_map_settings(string fileName)
    {
        TextAsset settings = Resources.Load<TextAsset>(fileName);   // Load in settings from resources settings file
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

    /// Debug functions /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Functions used to help debug errors, including exception handeling.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

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
        print(exceptionLocation + " Exception!!!:\n\t" + e.Message +
            "\n\tStackTrace: " + e.StackTrace +
            "\n\tSource: " + e.Source +
            "\n\tInnerMessage: " + e.InnerException.Message);
    }
}
