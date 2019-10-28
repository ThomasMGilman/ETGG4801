using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class MapGeneration : MonoBehaviour
{
    public GameObject Room_prefab;

    public int WorldWidth, WorldHeight;         //dimensions of Wold Space
    public int RoomWidth, RoomHeight;           //dimensions of Room space

    [HideInInspector]
    public int HalfWidth, HalfHeight;

    public int HallWidth = 1;
    public int SquareSize = 1;
    public int BorderSize = 1;
    public int SmoothTimes = 5;         //Times to smooth the map out

    public int WallThresholdSize = 50;
    public int RoomThresholdSize = 50;

    public string Seed = "Random";      //When generating map using seed, uses Hash of string
    public bool UseRandSeed = false;

    [Range(0, 100)]
    public int RandFillPercent;

    private Map_Room[,] Map;

    // Start is called before the first frame update
    void Start()
    {
        this.HalfWidth = RoomWidth / 2;
        this.HalfHeight = RoomHeight / 2;
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
}
