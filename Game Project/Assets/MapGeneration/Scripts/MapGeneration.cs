using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class MapGeneration : MonoBehaviour
{
    public GameObject Room_prefab;
    public string mapSettingsName;

    public static int WorldWidth = 5, WorldHeight = 5;              //dimensions of Wold Space
    public static int RoomWidth = 100, RoomHeight = 100;            //dimensions of Room space
    public static float HalfWidth, HalfHeight;
    public static float HalfHalfWidth, HalfHalfHeight;

    public static int HallWidth = 1;
    public static int SquareSize = 1;
    public static float halfSquareSize = .5f;
    public static int BorderSize = 1;
    public static int SmoothTimes = 5;                              //Times to smooth the map out

    public static int WallThresholdSize = 50;
    public static float halfWallThreshold = 25;
    public static int RoomThresholdSize = 50;
    public static float halfRoomThreshold = 25;

    public static string Seed = "Random";                           //When generating map using seed, uses Hash of string
    public static bool UseRandSeed = false;

    public static int MaxFillPercent = 90;
    public static int MinFillPercent = 10;
    public static int RandFillPercent;
    public static bool UseRandFillPercent = false;

    private static int roomProcessCount = 0;
    private static int numRooms = 0;

    private Map_Room[,] Map;

    // Start is called before the first frame update
    void Start()
    {
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
                createRoom(x, z, new Vector3(xPos, 0, zPos), true);//(UnityEngine.Random.Range(0, 100) < RandFillPercent || (x == 0 && z == 0)) ? true : false);
            }
        }
    }

    private void connectRooms()
    {
        List<Map_Room> notConnected = new List<Map_Room>();
        System.Random rand = new System.Random();

        foreach (Map_Room room in Map)
        {
            notConnected.Add(room);
        }
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

            if (Map[x,z].ConnectedToMainRoom)                                    //Is current Room mainRoom or connected to mainRoom?
            {
                notConnected.RemoveAt(ri);
                print("CurRoom: " + Map[x,z].Room.name+" is Connected to Rooms: ");
                foreach ((int, int) r in Map[x,z].connections)
                    print("\t"+Map[r.Item1, r.Item1].Room.name);
            }
            else
            {
                bool gotRoom = false;
                do///////////////////////////////////////////////////////////////////////////////////////////Get Random Surrounding Room
                {

                    oX = rand.Next(x - 1, x + 1);
                    oZ = rand.Next(z - 1, z + 1);
                    do////////////////////////////////////////////////////////////////////////////////////////////////////////////Handle Room Index Boundaries and make sure other room isnt already connected to
                    {
                        if (oX == x && oZ == z)                       //Handle Room same as curRoom
                        {
                            oX += rand.Next(0, 1) == 0 ? 1 : -1;//UnityEngine.Random.Range(0, 1) == 0 ? 1 : -1;
                            oZ += rand.Next(0, 1) == 0 ? 1 : -1;//UnityEngine.Random.Range(0, 1) == 0 ? 1 : -1;
                            rand = new System.Random((int)DateTime.Now.Ticks);
                        };
                        if (oX > WorldWidth - 1) oX -= 1;                                              //Handle RoomX greater than worldWidth
                        else if (oX < 0) oX += 1;                                                      //Handle RoomX less than 0
                        if (oZ > WorldHeight - 1) oZ -= 1;                                             //Handle RoomZ greater than worldHeight
                        else if (oZ < 0) oZ += 1;                                                      //Handle RoomZ less than 0
                    } while (oX == x && oZ == z);

                    if (!Map[x, z].connections.Contains((oX, oZ)))
                        gotRoom = true;
                } while (!gotRoom);

                //Add Connected Rooms lists to each other connecting all rooms together
                if(Map[oX,oZ].ConnectedToMainRoom)
                {
                    Map[x, z].ConnectedToMainRoom = true;
                    foreach((int,int) coord in Map[x,z].connections)
                        Map[coord.Item1, coord.Item2].ConnectedToMainRoom = true;
                }
                Map[x, z].connections.Add((oX, oZ));
                Map[x, z].connections.UnionWith(Map[oX, oZ].connections);
                Map[oX, oZ].connections.UnionWith(Map[x, z].connections);

                //SetPoint between Rooms to create a passage to
                Vector3 connectionPoint = new Vector3(0, 0, 0);
                //Set point X Pos
                if (oX < x)
                { 
                    connectionPoint.x = Map[x, z].worldPos.x - HalfWidth;
                }
                else if (oX > x)
                { 
                    connectionPoint.x = Map[x, z].worldPos.x + RoomWidth;
                }
                else
                { 
                    connectionPoint.x = UnityEngine.Random.Range(Map[x, z].worldPos.x-HalfWidth, Map[x, z].worldPos.x + RoomWidth);
                }
                //Set point Z Pos
                if (oZ < z)
                { 
                    connectionPoint.z = Map[x, z].worldPos.z - HalfHeight;
                }
                else if (oZ > z)
                { 
                    connectionPoint.z = Map[x, z].worldPos.z + RoomHeight;
                }
                else
                { 
                    connectionPoint.z = UnityEngine.Random.Range(Map[x, z].worldPos.z - HalfHeight, Map[x, z].worldPos.z + RoomHeight);
                }

                pointToSend p1 = new pointToSend() { c = Color.red, v = connectionPoint }; //Correct
                pointToSend p2 = new pointToSend() { c = Color.blue, v = connectionPoint };
                //Tell Rooms to create passage to point specified
                print("connecting " + Map[x, z].Room.name + " to " + Map[oX, oZ].Room.name);
                print("their Origins are: " + Map[x, z].worldPos + " and " + Map[oX, oZ].worldPos);
                print("Connecting at point: " + connectionPoint);

                Map[x, z].Room.SendMessage("connectToPoint", p1);
                Map[oX, oZ].Room.SendMessage("connectToPoint", p2);
                print("\n");
            }
        }
    }

    private void generateMeshes()
    {
        foreach (Map_Room room in Map)
        {
            room.Room.SendMessage("debugShowRoomTiles");
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
        return new Map_Room() { Room = newRoom, mapIndex_X = x, mapIndex_Z = z, connections = new HashSet<(int, int)>(), Generated = generated, ReadyToGenMesh = false, ConnectedToMainRoom = isConnected };
    }

    private void createRoom(int x, int z, Vector3 roomPosition, bool doGeneration)
    {
        if (doGeneration)
        {
            Map[x, z] = createRoomStruct(x, z, doGeneration, (x == 0 && z == 0), Instantiate(Room_prefab, roomPosition, Room_prefab.transform.rotation, this.transform));
            Map[x, z].worldPos = roomPosition;
            Map[x, z].Room.name = "Room: [" + x.ToString() + "," + z.ToString() + "]";
        }
        else
            Map[x, z] = createRoomStruct(x, z, doGeneration, false);
    }

    public struct Map_Room
    {
        public GameObject Room;
        public int mapIndex_X, mapIndex_Z;
        public HashSet<(int, int)> connections;
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

    private void roomFinished()
    {
        roomProcessCount++;
        if (roomProcessCount == numRooms)
        {
            connectRooms();
            generateMeshes();
        }
    }

    public struct pointToSend
    {
        public Color c;
        public Vector3 v;
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
        if(!(lines.Length > 1))
        {
            print("Settings File contains ZERO settings!!!");
            return;
        }
        for(int i = 0; i < lines.Length; i++)
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
                        HalfHalfWidth = HalfWidth / 2;
                        break;
                    case ("roomheight"):
                        RoomHeight = Int32.Parse(line[1].Trim());
                        HalfHeight = RoomHeight / 2;
                        HalfHalfHeight = HalfHeight / 2;
                        break;
                    case ("hallwidth"):
                        HallWidth = Int32.Parse(line[1].Trim());
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
                        halfWallThreshold = WallThresholdSize / 2;
                        break;
                    case ("roomthresholdsize"):
                        RoomThresholdSize = Int32.Parse(line[1].Trim());
                        halfRoomThreshold = RoomThresholdSize / 2;
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
                    default:
                        print("\nSettingsException: " + line[0] + " doesnt exist as a valid setting!!\n\tLine: " + lines[i] + "\n\tLineLength: " + lines[i].Length + "\n");
                        break;
                }
            }
        }
    }
}
