using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;

//using reference material from https://learn.unity.com/project/procedural-cave-generation-tutorial
public class RoomsGeneration : MapGeneration
{
    private int[,] TileMap;
    private int[,] BorderMap;
    private int borderWidth, borderHeight;
    private int roomX, roomZ;
    private List<Room> Rooms;
    public Vector3 worldPos;
    object sharedLock;

    // Start is called before the first frame update
    void Start()
    {
        worldPos = this.transform.position;
        Debug.DrawLine(worldPos, worldPos + new Vector3(0, 20, 0), Color.cyan, 5000);
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

    /// <summary>
    /// Create Passage From closest Tile to the point
    /// </summary>
    /// <param name="point"></param>
    public void connectToPoint(in pointToSend pointPack)
    {
        Vector3 point = pointPack.v;
        Color drawColor = pointPack.c;

        Coordinate pointCoord = new Coordinate((int)point.x, (int)point.y);
        Coordinate closestRoomTile = pointCoord;
        Vector3 tilePos = Vector3.zero;
        Vector3 heightUP = new Vector3(0, 5, 0);
        int closestRoomDistance = RoomHeight * RoomWidth;
        foreach (Room r in Rooms)
        {
            foreach(Coordinate tile in r.edgeTiles)
            {
                tilePos = toWorldPos(tile);
                
                int distanceBetweenRooms = (int)(Mathf.Pow(tilePos.x - pointCoord.tileX, 2) + Mathf.Pow(tilePos.z - pointCoord.tileY, 2));
                //float distance = (float)getDistance(tilePos, point);
                if(distanceBetweenRooms < closestRoomDistance)
                {
                    closestRoomDistance = distanceBetweenRooms;
                    closestRoomTile = tile;
                }
            }
        }
        createPassage(closestRoomTile, pointCoord,null,null, false);
        Debug.DrawLine(toWorldPos(closestRoomTile), toWorldPos(pointCoord), drawColor, 5000);
        print("\t"+this.name+" ClosestTilePos: " + tilePos);
    }

    public void generateRoomMesh()
    {
        for (int x = 0; x < borderWidth; x++)
        {
            for (int y = 0; y < borderHeight; y++)
            {
                if (x >= BorderSize && x < RoomWidth + BorderSize && y >= BorderSize && y < RoomHeight + BorderSize)
                    BorderMap[x, y] = TileMap[x - BorderSize, y - BorderSize];
                //else
                    //BorderMap[x, y] = 1;
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
        //MeshGenerator meshGen = GetComponent<MeshGenerator>();
        //meshGen.GenerateMesh(BorderMap, SquareSize);
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

    private void connectClosestRooms(ref List<Room> regionRooms, bool forceAccessibilityFromMainRoom = false, bool showDrawRoom = true)
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
                Parallel.ForEach(B_rooms, roomB => //Parallelize Here
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

    private void createPassage(in Coordinate tileA, in Coordinate tileB, in Room roomA = null, in Room roomB = null, bool showPassageDraw = true)
    {
        if(roomA != null && roomB != null)
            Room.connectRooms(roomA, roomB);

        if(showPassageDraw)
            Debug.DrawLine(toWorldPos(tileA), toWorldPos(tileB), Color.green, 100);

        List<Coordinate> line = getLine(in tileA, in tileB);
        Parallel.ForEach(line, c => drawCircle(c, HallWidth));
    }

    private Vector3 toWorldPos(Coordinate tile)/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    {
        Vector3 tilesWorldPos = tile.toWorldPos() * 1.5f;
        return tilesWorldPos + this.worldPos;
    }

    private void drawCircle(Coordinate center, int radius)
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
                    TileMap[x, y] = UnityEngine.Random.Range(0, 100) < RandFillPercent ? 1 : 0;
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
            return new Vector3(-HalfHalfWidth -halfWallThreshold + 5 - 1.5f + tileX, 
                                2, 
                                -HalfHalfHeight -halfWallThreshold + 5 - 1.5f + tileY);
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

    private void setRoomPos((int,int) pos)
    {
        print("Room: " + this.name +" origin worldPos: "+worldPos);
        roomX = pos.Item1;
        roomZ = pos.Item2;
        worldPos.x -= roomX * HalfWidth;
        worldPos.z -= roomZ * HalfHeight;
        print("new WorldPos: " + worldPos);
    }

    private void debugShowRoomTiles()
    {
        Coordinate worldTile = new Coordinate((int)this.worldPos.x, (int)this.worldPos.z);
        Vector3 worldCoord = worldTile.toWorldPos()*1.5f;
        print("WorldCoord: " + worldCoord);
        Debug.DrawLine(worldCoord, worldCoord + new Vector3(0, 50, 0), Color.cyan, 5000);
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
