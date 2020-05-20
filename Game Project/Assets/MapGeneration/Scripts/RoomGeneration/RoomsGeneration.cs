using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Net;

public class RoomsGeneration : MapGeneration
{
    /// Structs, Classes, and Enums /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// These Structures are for handling incoming data and organization inorder to build and connect all mesh rooms created when spawing the world.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// Coordinate struct and functions ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////// 
    /// Contains the internal rooms tile and edge coordinates as well as the rooms its connected to.
    /// Once a path is made between this room and the main room, this room is then accessible from the main room
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public struct Coordinate
    {
        public Vector2Int pos;
        public Coordinate(Vector2Int p)
        {
            pos = p;
        }

        public Vector3 to_world_pos()
        {
            float x = (-halfRoomWidth + pos.x * square_andHalf);
            float y = roomYPos;
            float z = (-halfRoomHeight + pos.y * square_andHalf);
            return new Vector3(x, y, z);
        }
    }

    /// Internal Room Class /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Contains the internal rooms tile and edge coordinates as well as the rooms its connected to.
    /// Once a path is made between this room and the main room, this room is then accessible from the main room
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class Room : IComparable<Room>
    {
        public List<Coordinate> tiles;
        public List<Coordinate> edgeTiles;
        public HashSet<Room> connectedRooms;
        public int roomSize;
        public bool accessibleFromMainRoom;
        public bool mainRoom;

        object sharedLock = new object();

        public Room()
        { }

        public Room(List<Coordinate> roomTiles, int[,] map)
        {
            this.tiles = roomTiles;
            this.roomSize = roomTiles.Count;
            this.connectedRooms = new HashSet<Room>();
            this.edgeTiles = new List<Coordinate>();

            try
            {
                Parallel.ForEach(this.tiles, tile =>
                {
                    for (int posX = tile.pos.x - 1; posX <= tile.pos.x + 1; posX++)
                    {
                        for (int posY = tile.pos.y - 1; posY <= tile.pos.y + 1; posY++)
                        {
                            if ((posX == tile.pos.x || posY == tile.pos.y) && map[posX, posY] == 1)
                            {
                                lock (sharedLock)
                                    this.edgeTiles.Add(tile);
                            }
                        }
                    }
                });
            }
            catch (Exception e)
            {
                print("RoomCreation Exception!!!:\n\t" + e.Message +
                    "\n\tStackTrace: " + e.StackTrace +
                    "\n\tSource: " + e.Source +
                    "\n\tInnerMessage: " + e.InnerException.Message);
            }
        }

        public void set_accessible_from_main_room()
        {
            if (!this.accessibleFromMainRoom)
            {
                this.accessibleFromMainRoom = true;
                Parallel.ForEach(this.connectedRooms, connectedRoom =>
                   connectedRoom.set_accessible_from_main_room());
            }
        }

        public void connect_room(in Room room)
        {
            this.connectedRooms.Add(room);
        }

        public static void connect_rooms(ref Room roomA, ref Room roomB)
        {
            if (roomA.accessibleFromMainRoom)
                roomB.set_accessible_from_main_room();
            else if (roomB.accessibleFromMainRoom)
                roomA.set_accessible_from_main_room();

            roomA.connect_room(in roomB);
            roomB.connect_room(in roomA);
        }

        public bool is_connected(Room otherRoom)
        {
            return this.connectedRooms.Contains(otherRoom);
        }

        //IComparable function
        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }
    }

    /// Settings and Variables //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private int[,] tileMap;
    private int[,] borderMap;
    private int borderWidth, borderHeight;
    private int randFillPercent;
    private List<Room> rooms;
    private List<List<Coordinate>> placeCoords;
    public Vector3 worldPos;
    public Coordinate worldTile;
    object sharedLock;
    Vector3 goalScale = new Vector3(.25f, .25f, .25f);

    private GameObject goal_Obj = null, start_Obj = null;

    /// Initializer and Destructor //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Entry point for the start of this class, as well as the on destroy action to clean up.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // Start is called before the first frame update
    void Start()
    {
        if (!useRandFillPercent)
        {
            randFillPercent = UnityEngine.Random.Range(minFillPercent, maxFillPercent);
        }
        else
            randFillPercent = fillPercent;
        worldPos = this.transform.position;
        worldTile = new Coordinate(new Vector2Int((int)worldPos.x, (int)worldPos.z));

        //Debug.DrawLine(worldPos, worldPos + new Vector3(0, 20, 0), Color.cyan, 5000);
        sharedLock = new object();
        generate_rooms();
    }

    /// Utility Functions ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public double get_distance(in Vector3 a, in Vector3 b)
    {
        double px = Math.Pow((a.x - b.x), 2);
        double py = Math.Pow((a.y - b.y), 2);
        double pz = Math.Pow((a.z - b.z), 2);
        return Math.Sqrt(px + py + pz);
    }

    public double get_distance(in Vector2Int a, in Vector2Int b)
    {
        double px = Math.Pow((a.x - b.x), 2);
        double py = Math.Pow((a.y - b.y), 2);
        return Math.Sqrt(px + py);
    }

    public void swap_xy(ref Vector2Int vec)
    {
        int tmp = vec.x;
        vec.x = vec.y;
        vec.y = tmp;
    }

    /// <summary>
    /// Count the Surrounding walls on the grid.
    /// Return the number of surrounding walls
    /// </summary>
    /// <param name="x">mapX position</param>
    /// <param name="y">mapY position</param>
    /// <returns></returns>
    public int get_surrounding_wall_count(in int x, in int y)
    {
        int wallCount = 0;
        for (int neighbourX = x - 1; neighbourX <= x + 1; neighbourX++)
        {
            for (int neighbourY = y - 1; neighbourY <= y + 1; neighbourY++)
            {
                if (neighbourX >= 0 && neighbourX < roomWidth && neighbourY >= 0 && neighbourY < roomHeight)
                {
                    if (neighbourX != x || neighbourX != y)
                        wallCount += tileMap[neighbourX, neighbourY];
                }
                else
                    wallCount++;
            }
        }
        return wallCount;
    }

    private Vector3 to_world_pos(Coordinate tile)
    {
        Vector3 tilesWorldPos = tile.to_world_pos();// * 1.5f;
        return tilesWorldPos + this.worldPos;
    }

    private Coordinate get_random_room_tile(int roomNum = -1)
    {
        Room randRoom;
        if (roomNum >= 0 && roomNum < rooms.Count)
            randRoom = rooms[roomNum];                                                          //GrabSpecifiedRoom
        else
            randRoom = rooms[UnityEngine.Random.Range(0, rooms.Count - 1)];                     //Grab Random Room to place goal at
        foreach (Coordinate t in randRoom.edgeTiles)
        {
            if (get_surrounding_wall_count(t.pos.x, t.pos.y) == 0)
                return t;
        }
        return randRoom.edgeTiles[UnityEngine.Random.Range(0, randRoom.edgeTiles.Count - 1)];       //Get Tile Location
    }

    private Coordinate get_random_tile()
    {
        int region = UnityEngine.Random.Range(0, placeCoords.Count - 1);
        return placeCoords[region][UnityEngine.Random.Range(0, placeCoords[region].Count - 1)];
    }

    private Vector3 get_obj_position(Coordinate toUse)
    {
        float xAmout = roomWidth / 2 * squareSize;
        float xPos = Mathf.InverseLerp(-xAmout, xAmout, toUse.pos.x) * 10 + borderSize;
        float zPos = Mathf.InverseLerp(-xAmout, xAmout, toUse.pos.y) * 10 + borderSize;
        return new Vector3(xPos, roomYPos, zPos) + to_world_pos(toUse);
    }

    /// Room Creation Code //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// The Following Code Creates a Map of Rooms using marching squares
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void generate_rooms()
    {
        tileMap = new int[roomWidth, roomHeight];
        rand_fill_map();
        for (int i = 0; i < smoothTimes; i++)
            smooth_map();
        process_map();

        //debug_show_room_tiles();
        //CreateBorderLayout for MeshGeneration
        borderWidth = roomWidth + borderSize * 2;
        borderHeight = roomHeight + borderSize * 2;
        borderMap = new int[borderWidth, borderHeight];
        set_room_regions();
        this.transform.parent.SendMessage("room_finished");
    }

    /// <summary>
    /// Convert Room Sections if there is not enough space/tiles
    /// </summary>
    private void process_map()
    {
        List<Room> remainingRooms = new List<Room>();

        //Get Wall Regions and convert them to a floor tile if the Region of walls do not meet or exceed the threshold amount
        List<List<Coordinate>> wallRegions = get_regions(1);
        region_convert(in wallRegions, 0, wallThresholdSize, ref remainingRooms);

        //Get Floor Regions and convert them to wall tiles if the region does not meet or exceed the required threshold amount
        //Get the Room Regions back to connect the Rooms after
        List<List<Coordinate>> roomRegions = get_regions(0);
        region_convert(in roomRegions, 1, roomThresholdSize, ref remainingRooms, true);
        
        remainingRooms.Sort();
        remainingRooms[0].mainRoom = true;
        remainingRooms[0].accessibleFromMainRoom = true;

        connect_closest_rooms(ref remainingRooms);
        this.rooms = remainingRooms;
    }

    /// <summary>
    /// Convert Room tiles if room space threshold is not met
    /// </summary>
    /// <param name="Regions">List of Regions</param>
    /// <param name="conversion">To convert to</param>
    /// <param name="threshold">Size requirment</param>
    private void region_convert(in List<List<Coordinate>> Regions, int conversion, int threshold, ref List<Room> remainingRooms, bool setRemianing = false)
    {
        try
        {
            List<Room> toAdd = new List<Room>();
            Parallel.ForEach(Regions, region =>
            {
                if (region.Count < threshold)
                {
                    foreach (Coordinate tile in region)
                        tileMap[tile.pos.x, tile.pos.y] = conversion;
                }
                else if (setRemianing)
                {
                    lock (sharedLock)
                        toAdd.Add(new Room(region, tileMap));
                }
            });
            remainingRooms.AddRange(toAdd);
        }
        catch (Exception e)
        {
            print_exception_message("RegionConvert Exception: ", e);
        }
    }

    /// <summary>
    /// Get List of Coordinate Tiles that do not contain walls
    /// </summary>
    /// <param name="startX"></param>
    /// <param name="startY"></param>
    /// <returns></returns>
    private List<Coordinate> get_region_tiles(in int startX, in int startY)
    {
        List<Coordinate> tiles = new List<Coordinate>();
        int[,] mapFlags = new int[roomWidth, roomHeight];
        int tileType = tileMap[startX, startY];

        Queue<Coordinate> queue = new Queue<Coordinate>();
        queue.Enqueue(new Coordinate(new Vector2Int(startX, startY)));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Coordinate tile = queue.Dequeue();
            tiles.Add(tile);
            for (int posX = tile.pos.x - 1; posX <= tile.pos.x + 1; posX++)
            {
                for (int posY = tile.pos.y - 1; posY <= tile.pos.y + 1; posY++)
                {
                    if (in_map_range(posX, posY) && (posY == tile.pos.y || posX == tile.pos.x))
                    {
                        if (mapFlags[posX, posY] == 0 && tileMap[posX, posY] == tileType)
                        {
                            mapFlags[posX, posY] = 1;
                            queue.Enqueue(new Coordinate(new Vector2Int(posX, posY)));
                        }
                    }
                }
            }
        }
        return tiles;
    }

    /// <summary>
    /// Get Lists of Rooms/Regions based on tileType (open, or wall)
    /// </summary>
    /// <param name="tileType"></param>
    /// <returns></returns>
    private List<List<Coordinate>> get_regions(int tileType)
    {
        List<List<Coordinate>> regions = new List<List<Coordinate>>();
        int[,] mapFlags = new int[roomWidth, roomHeight];

        for (int posX = 0; posX < roomWidth; posX++)
        {
            for (int posY = 0; posY < roomHeight; posY++)
            {
                if (mapFlags[posX, posY] == 0 && tileMap[posX, posY] == tileType)
                {
                    List<Coordinate> region = get_region_tiles(in posX, in posY);
                    regions.Add(region);
                    foreach (Coordinate tile in region)
                        mapFlags[tile.pos.x, tile.pos.y] = 1;
                }
            }
        }
        return regions;
    }

    private bool in_map_range(in int posX, in int posY)
    {
        return posX >= 0 && posX < roomWidth && posY >= 0 && posY < roomHeight;
    }

    /// <summary>
    /// Randomly Fills the tileMap based on the random seed, and whether the random num generated is less than the fillPercent set from the settings file
    /// </summary>
    private void rand_fill_map()
    {
        if (useRandSeed) seed = DateTime.Now.Ticks.ToString();  //Base Random seed of Time

        UnityEngine.Random.InitState(seed.GetHashCode());
        for (int x = 0; x < roomWidth; x++)
        {
            for (int y = 0; y < roomHeight; y++)
            {
                if (x == 0 || x == roomWidth - 1 || y == 0 || y == roomHeight - 1)                  // Borders are always filled
                    tileMap[x, y] = 1;
                else
                    tileMap[x, y] = UnityEngine.Random.Range(0, 100) < randFillPercent ? 1 : 0;     // Fill the tile  if random num is less than fill percent
            }
        }

    }

    /// <summary>
    /// Adjust the map based on surrounding wall Count.
    /// </summary>
    private void smooth_map()
    {
        for (int x = 0; x < roomWidth; x++)
        {
            for (int y = 0; y < roomHeight; y++)
            {
                int neighboursWalls = get_surrounding_wall_count(x, y);

                if (neighboursWalls > 4) tileMap[x, y] = 1;
                else if (neighboursWalls < 4) tileMap[x, y] = 0;
            }
        }
    }

    /// Internall Room Connection and Hallway Code //////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// The Following Code Connects the internal rooms by their closest neighbors, and creates a hallway between the rooms.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Create Passage From closest Tile to the point passed.
    /// This function is externally called to connect this room to another.
    /// </summary>
    /// <param name="point"></param>
    public void connect_to_point(Vector2Int point)
    {
        Coordinate pointCoord = new Coordinate(point);
        Coordinate closestRoomTile = pointCoord;
        float closestRoomDistance = Mathf.Infinity;
        Parallel.ForEach(rooms, r =>
        {
            Parallel.ForEach(r.edgeTiles, tile =>
            {
                float distance = (float)get_distance(tile.pos, pointCoord.pos);
                lock (sharedLock)
                {
                    if (distance < closestRoomDistance)
                    {
                        closestRoomDistance = distance;
                        closestRoomTile = tile;
                    }
                }
            });
        });
        create_passage(closestRoomTile, pointCoord, false);
    }

    /// <summary>
    /// Clear a circle area at the coordinate specified with a specified radius
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    private void draw_circle(Coordinate center, int radius)
    {
        int dRadius = radius * radius;
        for(int x = -radius; x <= radius; x++)
        {
            int dX = x * x;
            for(int y = -radius; y <= radius; y++)
            {
                if(dX + y*y <= dRadius)
                {
                    int drawX = center.pos.x + x;
                    int drawY = center.pos.y + y;
                    
                    lock (sharedLock)
                    {
                        if (in_map_range(drawX, drawY))
                            tileMap[drawX, drawY] = 0;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Get Line from pointA to pointB
    /// </summary>
    /// <param name="pointA"></param>
    /// <param name="pointB"></param>
    /// <returns></returns>
    private List<Coordinate> get_line(in Coordinate pointA, in Coordinate pointB)
    {
        List<Coordinate> line = new List<Coordinate>();

        Vector2Int posA = pointA.pos;
        Vector2Int posB = pointB.pos - posA;
        Vector2Int step = new Vector2Int(Math.Sign(posB.x), Math.Sign(posB.y));
        Vector2Int long_short = new Vector2Int(Mathf.Abs(posB.x), Mathf.Abs(posB.y));
        bool inverted = false;

        if (long_short.x < long_short.y)
        {
            inverted = true;
            swap_xy(ref step);
            swap_xy(ref long_short);
        }

        int gradientAccumulation = long_short.x / 2;
        for(int i = 0; i < long_short.x; i++)
        {
            line.Add(new Coordinate(posA));

            if (inverted) posA.y += step.x;
            else posA.x += step.x;

            gradientAccumulation += long_short.y;
            if(gradientAccumulation >= long_short.x)
            {
                if (inverted) posA.x += step.y;
                else posA.y += step.y;

                gradientAccumulation -= long_short.x;
            }
        }
        return line;
    }

    /// <summary>
    /// Create a hallway between two coordinate spaces.
    /// </summary>
    /// <param name="tileA"></param>
    /// <param name="tileB"></param>
    /// <param name="showPassageDraw"></param>
    /// <param name="hallRadius"></param>
    private void create_passage(in Coordinate tileA, in Coordinate tileB, bool showPassageDraw = false, int hallRadius = -1)
    {
        if (hallRadius <= 0) hallRadius = hallWidth;
        if (showPassageDraw)
            Debug.DrawLine(to_world_pos(tileA), to_world_pos(tileB), Color.magenta, 100);

        List<Coordinate> line = get_line(in tileA, in tileB);
        Parallel.ForEach(line, c => draw_circle(c, hallRadius));
    }

    /// <summary>
    /// Create a hallway between two coordinate spaces of seperate rooms and connect the two.
    /// </summary>
    /// <param name="tileA"></param>
    /// <param name="tileB"></param>
    /// <param name="roomA"></param>
    /// <param name="roomB"></param>
    /// <param name="showPassageDraw"></param>
    /// <param name="hallRadius"></param>
    private void create_passage(in Coordinate tileA, in Coordinate tileB, ref Room roomA, ref Room roomB, bool showPassageDraw = false, int hallRadius = -1)
    {
        if (roomA != null && roomB != null)
            Room.connect_rooms(ref roomA, ref roomB);

        create_passage(in tileA, in tileB, showPassageDraw, hallRadius);
    }

    /// <summary>
    /// Find the closest room to current room region and connect the two
    /// </summary>
    /// <param name="regionRooms"></param>
    /// <param name="forceAccessibilityFromMainRoom"></param>
    /// <param name="showDrawRoom"></param>
    private void connect_closest_rooms(ref List<Room> regionRooms, bool forceAccessibilityFromMainRoom = false, bool showDrawRoom = false)
    {
        List<Room> A_rooms = new List<Room>();
        List<Room> B_rooms = new List<Room>();

        if (forceAccessibilityFromMainRoom)
        {
            Parallel.ForEach(regionRooms, room =>
            {
                if (room.accessibleFromMainRoom)
                {
                    lock (sharedLock) B_rooms.Add(room);
                }
                else
                {
                    lock (sharedLock) A_rooms.Add(room);
                }
            });
        }
        else
        {
            A_rooms = regionRooms;
            B_rooms = regionRooms;
        }

        int bestDistance = 0;
        Coordinate bestTileA = new Coordinate();
        Coordinate bestTileB = new Coordinate();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach (Room roomA in A_rooms)
        {
            if (!forceAccessibilityFromMainRoom)
            {
                possibleConnectionFound = false;
                if (roomA.connectedRooms.Count > 0)
                    continue;
            }

            try
            {
                Parallel.ForEach(B_rooms, roomB =>
                {
                    bool isConnected = false;
                    lock (sharedLock)
                    {
                        if (roomA == roomB || roomA.is_connected(roomB))
                            isConnected = true;
                    }

                    if (!isConnected)
                    {
                        for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                        {
                            for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                            {
                                Coordinate tileA = roomA.edgeTiles[tileIndexA];
                                Coordinate tileB = roomB.edgeTiles[tileIndexB];
                                int distanceBetweenRooms = (int)get_distance(tileA.pos, tileB.pos);

                                lock (sharedLock)
                                {
                                    if (distanceBetweenRooms < bestDistance || !possibleConnectionFound)
                                    {
                                        bestDistance = distanceBetweenRooms;
                                        possibleConnectionFound = true;
                                        bestTileA = tileA;
                                        bestTileB = tileB;
                                        bestRoomA = roomA;
                                        bestRoomB = roomB;
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception e)
            {
                print_exception_message("ConnectClosestRooms", e);
            }

            if (possibleConnectionFound && !forceAccessibilityFromMainRoom) //checkAgain if one of the rooms isnt connected to the main room
                create_passage(in bestTileA, in bestTileB, ref bestRoomA, ref bestRoomB, showDrawRoom);
        }//Foreach RoomA ends here

        if (possibleConnectionFound && forceAccessibilityFromMainRoom)
        {
            create_passage(in bestTileA, in bestTileB, ref bestRoomA, ref bestRoomB);
            connect_closest_rooms(ref regionRooms, true);
        }

        if (!forceAccessibilityFromMainRoom)
            connect_closest_rooms(ref regionRooms, true);
    }

    /// Spawn and Goal Set functions ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Functions for creating Goal and Spawn objects within the room.
    /// Which function is called depends on which Room Object is selected to have the set object generated in it from the MapGeneration code
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Tell this room to set its placement coordinates for placing objects
    /// </summary>
    private void set_room_regions()
    {
        placeCoords = get_regions(0);
    }

    private void set_goal(float val = -1)
    {
        Coordinate randTileInRoom = get_random_tile(); //get_random_room_tile();
        connect_to_point(randTileInRoom.pos);

        Vector3 tileLocation = get_obj_position(randTileInRoom);                                                    //Convert Tiles location to worldPosition
        //Debug.DrawLine(tileLocation, tileLocation + new Vector3(0, 20, 0), Color.blue, 5000);

        tileLocation.y = 12.5f;                                                                                     //OffsetGoalObject off the ground by its height
        GameObject goal = Instantiate(Goal_Prefab, tileLocation, Goal_Prefab.transform.rotation, this.transform);   //Create Goal object at location;
        float score = val;
        if (val < 0)
        {
            score = UnityEngine.Random.Range(100, 150);
            goal.transform.localScale = goalScale;
            tileLocation.y -= Goal_Prefab.transform.localScale.y*1.5f;
            goal.transform.position = tileLocation;
        }
        goal.SendMessage("set_value", score);
        this.transform.parent.SendMessage("room_finished");
    }

    private void set_spawn()
    {
        Coordinate randTileInRoom = get_random_tile();//get_random_room_tile();
        connect_to_point(randTileInRoom.pos);

        Vector3 tileLocation = get_obj_position(randTileInRoom);                                                    //Convert Tiles location to worldPosition
        //Debug.DrawLine(tileLocation, tileLocation + new Vector3(0, 20, 0), Color.blue, 5000);

        tileLocation.y = 10f;                                                                                       //OffsetGoalObject off the ground by its height
        Instantiate(Spawn_Prefab, tileLocation, Spawn_Prefab.transform.rotation, this.transform);                   //Create Goal object at location;

        this.transform.parent.SendMessage("room_finished");
    }

    /// Mesh Generation Function ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Function for Generating this Objects Mesh
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Creates the Rooms UV's and Meshes based on the wall tiles
    /// </summary>
    public void generate_room_mesh()
    {
        for (int x = 0; x < borderWidth; x++)
        {
            for (int y = 0; y < borderHeight; y++)
            {
                if (x >= borderSize && x < roomWidth + borderSize && y >= borderSize && y < roomHeight + borderSize)
                    borderMap[x, y] = tileMap[x - borderSize, y - borderSize];
                //else
                //   BorderMap[x, y] = 1;
            }
        }

        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.generate_mesh(borderMap, squareSize);
    }

    /// Debug Functions /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// The following code draws a line from the point befor the mesh is generated, 
    /// showing the maps layout from the edge tiles and the hallways being drawn to form connections between rooms.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void debug_print_room()
    {
        string line = "[\n";
        for(int z = 0; z < borderHeight; z++)
        {
            line = "\t";
            for(int x = 0; x < borderWidth; x++)
            {
                line += borderMap[z, x].ToString();
                line += z == borderHeight - 1 ? ",": "";
            }
            line += "\n";
        }
        line += "]\n";
        print(line);
    }

    private void debug_show_room_tiles()
    {
        Vector3 worldCoord = to_world_pos(worldTile);
        print("WorldCoord: " + worldCoord);
        Debug.DrawLine(worldCoord, worldCoord + new Vector3(0, 50, 0), Color.red, 5000);
        Vector3 heightUP = new Vector3(0, 5, 0);
        foreach(Room r in rooms)
        {
            foreach(Coordinate tile in r.edgeTiles)
            {
                Vector3 tilePos = to_world_pos(tile);
                Debug.DrawLine(tilePos, tilePos + heightUP, Color.green, 5000);
            }
        }
    }
}
