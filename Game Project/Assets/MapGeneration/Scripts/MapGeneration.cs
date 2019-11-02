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
    public static int HalfWidth, HalfHeight;

    public static int HallWidth = 1;
    public static int SquareSize = 1;
    public static int BorderSize = 1;
    public static int SmoothTimes = 5;                              //Times to smooth the map out

    public static int WallThresholdSize = 50;
    public static int RoomThresholdSize = 50;

    public static string Seed = "Random";                           //When generating map using seed, uses Hash of string
    public static bool UseRandSeed = false;

    public static int MaxFillPercent = 90;
    public static int MinFillPercent = 10;
    public static int RandFillPercent;
    public static bool UseRandFillPercent = false;

    private Map_Room[,] Map;

    // Start is called before the first frame update
    void Start()
    {
        importMapSettings(mapSettingsName);
        HalfWidth = RoomWidth / 2;
        HalfHeight = RoomHeight / 2;
        Map = new Map_Room[WorldWidth, WorldHeight];
        createWorld();
        processWorld();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void createWorld()
    {
        if (UseRandSeed) Seed = DateTime.Now.Ticks.ToString();

        int xPos, zPos;
        for(int x = 0; x < WorldWidth; x++)
        {
            xPos = getRoomPosX(x);
            for(int z = 0; z < WorldHeight; z++)
            {
                zPos = getRoomPosZ(z);
                createRoom(x, z, new Vector3(xPos, 0, zPos), (UnityEngine.Random.Range(0, 100) < RandFillPercent || (x==0 && z==0)) ? true : false);
            }
        }
    }

    private void processWorld()
    {

    }

    private int getRoomPosX(int x)
    {
        return x * (RoomWidth + HalfWidth);
    }

    private int getRoomPosZ(int z)
    {
        return z * (RoomHeight + HalfHeight);
    }

    public bool isRoomGenerated(int x, int z)
    {
        return Map[x,z].Generated;
    }
    
    Map_Room createRoomStruct(bool generated, bool isConnected, GameObject newRoom = null)
    {
        return new Map_Room() { Room = newRoom, Generated = generated, ConnectedToMainRoom = isConnected };
    }

    private void createRoom(int x, int z, Vector3 roomPosition, bool doGeneration)
    {
        if (doGeneration)
        {
            Map[x, z] = createRoomStruct(doGeneration, (x == 0 && z == 0), Instantiate(Room_prefab, roomPosition, Room_prefab.transform.rotation, this.transform));
            Map[x, z].Room.name = "Room: [" + x.ToString() + "," + z.ToString() + "]";
        }
        else
            Map[x, z] = createRoomStruct(doGeneration, false);
    }

    public struct Map_Room
    {
        public GameObject Room;
        public List<Map_Room> NeighbourRooms;
        public bool Generated;
        public bool ConnectedToMainRoom;
    };

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
                        break;
                    case ("roomheight"):
                        RoomHeight = Int32.Parse(line[1].Trim());
                        break;
                    case ("hallwidth"):
                        HallWidth = Int32.Parse(line[1].Trim());
                        break;

                    case ("squaresize"):
                        SquareSize = Int32.Parse(line[1].Trim());
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
                    default:
                        print("\nSettingsException: " + line[0] + " doesnt exist as a valid setting!!\n\tLine: " + lines[i] + "\n\tLineLength: " + lines[i].Length + "\n");
                        break;
                }
            }
        }
    }
}
