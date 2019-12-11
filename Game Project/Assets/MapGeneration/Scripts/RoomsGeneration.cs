using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class RoomsGeneration : MapGeneration
{
    private int[,] TileMap;
    private int[,] BorderMap;
    private int borderWidth, borderHeight;
    private int RandFillPercent;
    private List<Room> Rooms;
    public Vector3 worldPos;
    public Coordinate worldTile;
    object sharedLock;
    Vector3 goalScale = new Vector3(.25f, .25f, .25f);

    private GameObject goalObj = null, startObj = null;

    // Start is called before the first frame update
    void Start()
    {
        if (!UseRandFillPercent)
        {
            RandFillPercent = UnityEngine.Random.Range(MinFillPercent, MaxFillPercent);
        }
        else
            RandFillPercent = FillPercent;
        worldPos = this.transform.position;
        worldTile = new Coordinate() { tileX = (int)worldPos.x, tileY = (int)worldPos.z };
        //Debug.DrawLine(worldPos, worldPos + new Vector3(0, 20, 0), Color.cyan, 5000);
        sharedLock = new object();
        GenerateRooms();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public double getDistance(in Vector3 a, in Vector3 b)
    {
        double px = Math.Pow((a.x - b.x), 2);
        double py = Math.Pow((a.y - b.y), 2);
        double pz = Math.Pow((a.z - b.z), 2);
        return Math.Sqrt(px + py + pz);
    }

    public double getDistance(in (int,int) a, in (int,int) b)
    {
        double px = Math.Pow((a.Item1 - b.Item1), 2);
        double py = Math.Pow((a.Item2 - b.Item2), 2);
        return Math.Sqrt(px + py);
    }


    /// <summary>
    /// Create Passage From closest Tile to the point
    /// </summary>
    /// <param name="point"></param>
    public void connectToPoint(in (int,int) point)
    {
        Coordinate pointCoord = new Coordinate(point.Item1, point.Item2);
        Coordinate closestRoomTile = pointCoord;
        float closestRoomDistance = Mathf.Infinity;
        Parallel.ForEach(Rooms, r =>
        {
            Parallel.ForEach(r.edgeTiles, tile =>
            {
                float distance = (float)getDistance((tile.tileX, tile.tileY), (pointCoord.tileX, pointCoord.tileY));
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

        createPassage(closestRoomTile, pointCoord, null, null, false);
    }

    /// <summary>
    /// Creates the Rooms UV's and Meshes based on the wall tiles
    /// </summary>
    public void generateRoomMesh()
    {
        for (int x = 0; x < borderWidth; x++)
        {
            for (int y = 0; y < borderHeight; y++)
            {
                if (x >= BorderSize && x < RoomWidth + BorderSize && y >= BorderSize && y < RoomHeight + BorderSize)
                    BorderMap[x, y] = TileMap[x - BorderSize, y - BorderSize];
                //else
                //   BorderMap[x, y] = 1;
            }
        }
        
        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(BorderMap, SquareSize);
    }

    private void GenerateRooms()
    {
        TileMap = new int[RoomWidth, RoomHeight];
        RandFillMap();
        for (int i = 0; i < SmoothTimes; i++)
            SmoothMap();
        ProcessMap();
        //CreateBorderLayout for MeshGeneration
        borderWidth = RoomWidth + BorderSize * 2;
        borderHeight = RoomHeight + BorderSize * 2;
        BorderMap = new int[borderWidth, borderHeight];
        this.transform.parent.SendMessage("roomFinished");
    }

    /// <summary>
    /// Convert Room Sections if their is not enough space/tiles
    /// </summary>
    private void ProcessMap()
    {
        List<Room> remainingRooms = new List<Room>();

        //Get Wall Regions and convert them to a floor tile if the Region of walls do not meet or exceed the threshold amount
        List<List<Coordinate>> wallRegions = GetRegions(1);
        regionConvert(in wallRegions, 0, WallThresholdSize, ref remainingRooms);

        //Get Floor Regions and convert them to wall tiles if the region does not meet or exceed the required threshold amount
        //Get the Room Regions back to connect the Rooms after
        List<List<Coordinate>> roomRegions = GetRegions(0);
        regionConvert(in roomRegions, 1, RoomThresholdSize, ref remainingRooms, true);

        remainingRooms.Sort();
        remainingRooms[0].MainRoom = true;
        remainingRooms[0].AccessibleFromMainRoom = true;

        connectClosestRooms(ref remainingRooms);
        this.Rooms = remainingRooms;
    }

    /// <summary>
    /// Convert Room tiles if room space threshold is not met
    /// </summary>
    /// <param name="Regions">List of Regions</param>
    /// <param name="conversion">To convert to</param>
    /// <param name="threshold">Size requirment</param>
    private void regionConvert(in List<List<Coordinate>> Regions, int conversion, int threshold, ref List<Room> remainingRooms, bool setRemianing = false)
    {
        try
        {
            List<Room> toAdd = new List<Room>();
            Parallel.ForEach(Regions, region =>
            {
                if (region.Count < threshold)
                {
                    foreach (Coordinate tile in region)
                        TileMap[tile.tileX, tile.tileY] = conversion;
                }
                else if (setRemianing)
                {
                    lock (sharedLock)
                        toAdd.Add(new Room(region, TileMap));
                }
            });
            remainingRooms.AddRange(toAdd);
        }
        catch (Exception e)
        {
            printExceptionMessage("RegionConvert Exception: ", e);
        }
    }

    private void connectClosestRooms(ref List<Room> regionRooms, bool forceAccessibilityFromMainRoom = false, bool showDrawRoom = false)
    {
        List<Room> A_rooms = new List<Room>();
        List<Room> B_rooms = new List<Room>();

        if(forceAccessibilityFromMainRoom)
        {
            Parallel.ForEach(regionRooms, room =>
            {
                if (room.AccessibleFromMainRoom)
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
            if(!forceAccessibilityFromMainRoom)
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
                        if (roomA == roomB || roomA.IsConnected(roomB))
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
                                int distanceBetweenRooms = (int)(Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));

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
            catch(Exception e)
            {
                printExceptionMessage("ConnectClosestRooms", e);
            }

            if (possibleConnectionFound && !forceAccessibilityFromMainRoom) //checkAgain if one of the rooms isnt connected to the main room
                createPassage(in bestTileA, in bestTileB, in bestRoomA, in bestRoomB, showDrawRoom);
        }//Foreach RoomA ends here

        if (possibleConnectionFound && forceAccessibilityFromMainRoom)
        {
            createPassage(in bestTileA, in bestTileB, in bestRoomA, in bestRoomB);
            connectClosestRooms(ref regionRooms, true);
        }

        if (!forceAccessibilityFromMainRoom)
            connectClosestRooms(ref regionRooms, true);
    }

    private void createPassage(in Coordinate tileA, in Coordinate tileB, in Room roomA = null, in Room roomB = null, bool showPassageDraw = false, int hallRadius = -1)
    {
        if (hallRadius <= 0) hallRadius = HallWidth;
        if(roomA != null && roomB != null)
            Room.connectRooms(roomA, roomB);

        if(showPassageDraw)
            Debug.DrawLine(toWorldPos(tileA), toWorldPos(tileB), Color.magenta, 100);

        List<Coordinate> line = getLine(in tileA, in tileB);
        Parallel.ForEach(line, c => drawCircle(c, hallRadius));
    }

    private void drawCircle(Coordinate center, int radius, bool color = false)
    {
        int dRadius = radius * radius;
        for(int x = -radius; x <= radius; x++)
        {
            int dX = x * x;
            for(int y = -radius; y <= radius; y++)
            {
                if(dX + y*y <= dRadius)
                {
                    int drawX = center.tileX + x;
                    int drawY = center.tileY + y;
                    
                    lock (sharedLock)
                    {
                        if (inMapRange(drawX, drawY))
                            TileMap[drawX, drawY] = 0;
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
    private List<Coordinate> getLine(in Coordinate pointA, in Coordinate pointB)
    {
        List<Coordinate> line = new List<Coordinate>();
        int pX              = pointA.tileX;
        int pY              = pointA.tileY;
        int dx              = pointB.tileX - pX;
        int dy              = pointB.tileY - pY;
        int stepX           = Math.Sign(dx);
        int stepY           = Math.Sign(dy);
        int longest         = Mathf.Abs(dx);
        int shortest        = Mathf.Abs(dy);
        bool inverted       = false;
        if (longest < shortest)
        {
            inverted    = true;
            longest     = Mathf.Abs(dy);
            shortest    = Mathf.Abs(dx);
            stepX       = Math.Sign(dy);
            stepY       = Math.Sign(dx);
        }

        int gradAccumulation = longest / 2;
        for(int i = 0; i < longest; i++)
        {
            line.Add(new Coordinate(pX, pY));

            if (inverted)   pY += stepX;
            else            pX += stepX;

            gradAccumulation += shortest;
            if(gradAccumulation >= longest)
            {
                if (inverted)   pX += stepY;
                else            pY += stepY;

                gradAccumulation -= longest;
            }
        }
        return line;
    }

    /// <summary>
    /// Get Lists of Rooms/Regions based on tileType (open, or wall)
    /// </summary>
    /// <param name="tileType"></param>
    /// <returns></returns>
    private List<List<Coordinate>> GetRegions(int tileType)
    {
        List<List<Coordinate>> regions = new List<List<Coordinate>>();
        int[,] mapFlags = new int[RoomWidth, RoomHeight];

        for(int posX = 0; posX < RoomWidth; posX++)
        {
            for(int posY = 0; posY < RoomHeight; posY++)
            {
                if(mapFlags[posX, posY] == 0 && TileMap[posX, posY] == tileType)
                {
                    List<Coordinate> region = GetRegionTiles(in posX, in posY);
                    regions.Add(region);
                    foreach (Coordinate tile in region)
                        mapFlags[tile.tileX, tile.tileY] = 1;
                }
            }
        }
        return regions;
    }

    /// <summary>
    /// Get List of Coordinate Tiles that do not contain walls
    /// </summary>
    /// <param name="startX"></param>
    /// <param name="startY"></param>
    /// <returns></returns>
    private List<Coordinate> GetRegionTiles(in int startX, in int startY)
    {
        List<Coordinate> tiles = new List<Coordinate>();
        int[,] mapFlags = new int[RoomWidth, RoomHeight];
        int tileType = TileMap[startX, startY];

        Queue<Coordinate> queue = new Queue<Coordinate>();
        queue.Enqueue(new Coordinate(startX, startY));
        mapFlags[startX, startY] = 1;

        while(queue.Count > 0)
        {
            Coordinate tile = queue.Dequeue();
            tiles.Add(tile);
            for(int posX = tile.tileX - 1; posX <= tile.tileX + 1; posX++)
            {
                for(int posY = tile.tileY - 1; posY <= tile.tileY + 1; posY++)
                {
                    if(inMapRange(posX, posY) && (posY == tile.tileY || posX == tile.tileX))
                    {
                        if (mapFlags[posX, posY] == 0 && TileMap[posX, posY] == tileType)
                        {
                            mapFlags[posX, posY] = 1;
                            queue.Enqueue(new Coordinate(posX, posY));
                        }
                    }
                }
            }
        }
        return tiles;
    }

    private bool inMapRange(in int posX, in int posY)
    {
        return posX >= 0 && posX < RoomWidth && posY >= 0 && posY < RoomHeight;
    }

    private void RandFillMap()
    {
        if (UseRandSeed) Seed = DateTime.Now.Ticks.ToString();  //Base Random seed of Time

        UnityEngine.Random.InitState(Seed.GetHashCode());
        for(int x = 0; x < RoomWidth; x++)
        {
            for(int y = 0; y < RoomHeight; y++)
            {
                if (x == 0 || x == RoomWidth - 1 || y == 0 || y == RoomHeight - 1)
                    TileMap[x, y] = 1;
                else
                    TileMap[x, y] = UnityEngine.Random.Range(0, 100) < RandFillPercent ? 0 : 1;
            }
        }
    }

    /// <summary>
    /// Adjust the map based on surrounding wall Count.
    /// </summary>
    private void SmoothMap()
    {
        for(int x = 0; x < RoomWidth; x++)
        {
            for(int y = 0; y < RoomHeight; y++)
            {
                int neighboursWalls = GetSurroundingWallCount(x, y);

                if (neighboursWalls > 4)        TileMap[x, y] = 1;
                else if (neighboursWalls < 4)   TileMap[x, y] = 0;
            }
        }
    }

    /// <summary>
    /// Count the Surrounding walls on the grid.
    /// Return the number of surrounding walls
    /// </summary>
    /// <param name="x">mapX position</param>
    /// <param name="y">mapY position</param>
    /// <returns></returns>
    public int GetSurroundingWallCount(in int x, in int y)
    {
        int wallCount = 0;
        for(int neighbourX = x-1; neighbourX <= x + 1; neighbourX++)
        {
            for(int neighbourY = y-1; neighbourY <= y + 1; neighbourY++)
            {
                if (neighbourX >= 0 && neighbourX < RoomWidth && neighbourY >= 0 && neighbourY < RoomHeight)
                {
                    if (neighbourX != x || neighbourX != y)
                        wallCount += TileMap[neighbourX, neighbourY];
                }
                else
                    wallCount++;
            }
        }
        return wallCount;
    }

    private Vector3 toWorldPos(Coordinate tile)/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    {
        Vector3 tilesWorldPos = tile.toWorldPos();// * 1.5f;
        return tilesWorldPos + this.worldPos;
    }

    public struct Coordinate
    {
        public int tileX, tileY;
        public Coordinate(int posX, int posY)
        {
            tileX = posX;
            tileY = posY;
        }
        public Vector3 toWorldPos()
        {
            return new Vector3(-HalfWidth + halfSquareSize + tileX, 2, -HalfHeight + halfSquareSize + tileY);
        }
    }
    
    public class Room : IComparable<Room>
    {
        public List<Coordinate> tiles;
        public List<Coordinate> edgeTiles;
        public HashSet<Room>    connectedRooms;
        public int              roomSize;
        public bool             AccessibleFromMainRoom;
        public bool             MainRoom;

        object sharedLock = new object();

        public Room()
        { }

        public Room(List<Coordinate> roomTiles, int[,] map)
        {
            this.tiles          = roomTiles;
            this.roomSize       = roomTiles.Count;
            this.connectedRooms = new HashSet<Room>();
            this.edgeTiles      = new List<Coordinate>();

            try
            {
                Parallel.ForEach(this.tiles, tile =>
                {
                    for (int posX = tile.tileX - 1; posX <= tile.tileX + 1; posX++)
                    {
                        for (int posY = tile.tileY - 1; posY <= tile.tileY + 1; posY++)
                        {
                            if ((posX == tile.tileX || posY == tile.tileY) && map[posX, posY] == 1)
                            {
                                lock (sharedLock)
                                    this.edgeTiles.Add(tile);
                            }
                        }
                    }
                });
            }
            catch(Exception e)
            {
                print("RoomCreation Exception!!!:\n\t" + e.Message +
                    "\n\tStackTrace: " + e.StackTrace +
                    "\n\tSource: " + e.Source +
                    "\n\tInnerMessage: " + e.InnerException.Message);
            }
        }

        public void setAccessibleFromMainRoom()
        {
            if(!this.AccessibleFromMainRoom)
            {
                this.AccessibleFromMainRoom = true;
                Parallel.ForEach (this.connectedRooms, connectedRoom =>
                    connectedRoom.setAccessibleFromMainRoom());
            }
        }

        public void connectRoom(in Room room)
        {
            this.connectedRooms.Add(room);
        }

        public static void connectRooms(in Room roomA, in Room roomB)
        {
            if (roomA.AccessibleFromMainRoom)
                roomB.setAccessibleFromMainRoom();
            else if (roomB.AccessibleFromMainRoom)
                roomA.setAccessibleFromMainRoom();

            roomA.connectRoom(in roomB);
            roomA.connectRoom(in roomA);
        }

        public bool IsConnected(Room otherRoom)
        {
            return this.connectedRooms.Contains(otherRoom);
        }

        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }
    }

    private Coordinate getRandomRoomTile(int roomNum = -1)
    {
        Room randRoom;
        if(roomNum >= 0 && roomNum < Rooms.Count)
            randRoom = Rooms[roomNum];                                                          //GrabSpecifiedRoom
        else
            randRoom = Rooms[UnityEngine.Random.Range(0, Rooms.Count - 1)];                     //Grab Random Room to place goal at
        foreach(Coordinate t in randRoom.tiles)
        {
            if (GetSurroundingWallCount(t.tileX, t.tileY) == 0)
                return t;
        }
        return randRoom.edgeTiles[UnityEngine.Random.Range(0, randRoom.edgeTiles.Count - 1)];       //Get Tile Location
    }

    private Vector3 getObjPosition(Coordinate toUse)
    {
        float xAmout = RoomWidth / 2 * SquareSize;
        float xPos = Mathf.InverseLerp(-xAmout, xAmout, toUse.tileX) * 10 + BorderSize;
        float zPos = Mathf.InverseLerp(-xAmout, xAmout, toUse.tileY) * 10 + BorderSize;
        return new Vector3(xPos, 0, zPos) + toWorldPos(toUse);
    }

    private void setGoal(float val = -1)
    {
        Coordinate randTileInRoom = getRandomRoomTile();
        Vector3 tileLocation = getObjPosition(randTileInRoom);                                                  //Convert Tiles location to worldPosition
        //Debug.DrawLine(tileLocation, tileLocation + new Vector3(0, 20, 0), Color.red, 5000);
        tileLocation.y = 12.5f;                                                                                   //OffsetGoalObject off the ground by its height
        drawCircle(randTileInRoom, 4);
        GameObject goal = Instantiate(Goal_prefab, tileLocation, Goal_prefab.transform.rotation);//, this.transform);             //Create Goal object at location;
        float score = val;
        if (val < 0)
        {
            score = UnityEngine.Random.Range(50, 125);
            goal.transform.localScale = goalScale;
            tileLocation.y -= Goal_prefab.transform.localScale.y*1.5f;
            goal.transform.position = tileLocation;
        }
        goal.SendMessage("setValue", score);
        this.transform.parent.SendMessage("roomFinished");
    }

    private void setSpawn()
    {
        Coordinate randTileInRoom = getRandomRoomTile();
        Vector3 tileLocation = getObjPosition(randTileInRoom);                                                  //Convert Tiles location to worldPosition
        //Debug.DrawLine(tileLocation, tileLocation + new Vector3(0, 20, 0), Color.red, 5000);
        tileLocation.y = 10f;                                                                                   //OffsetGoalObject off the ground by its height
        drawCircle(randTileInRoom, 4);
        Instantiate(Spawn_prefab, tileLocation, Spawn_prefab.transform.rotation);//, this.transform);           //Create Goal object at location;
        this.transform.parent.SendMessage("roomFinished");
    }

    private void debugPrintRoom()
    {
        string line = "[\n";
        for(int z = 0; z < borderHeight; z++)
        {
            line = "\t";
            for(int x = 0; x < borderWidth; x++)
            {
                line += BorderMap[z, x].ToString();
                line += z == borderHeight - 1 ? ",": "";
            }
            line += "\n";
        }
        line += "]\n";
        print(line);
    }

    private void debugShowRoomTiles()
    {
        Vector3 worldCoord = worldTile.toWorldPos();
        print("WorldCoord: " + worldCoord);
        Debug.DrawLine(worldCoord, worldCoord + new Vector3(0, 50, 0), Color.red, 5000);
        Vector3 heightUP = new Vector3(0, 5, 0);
        foreach(Room r in Rooms)
        {
            foreach(Coordinate tile in r.edgeTiles)
            {
                Vector3 tilePos = toWorldPos(tile);
                Debug.DrawLine(tilePos, tilePos + heightUP, Color.black, 5000);
            }
        }
    }
}
