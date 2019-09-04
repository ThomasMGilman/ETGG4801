using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class InnerRoomSpawn : MonoBehaviour
{
    public int numRooms;
    public GameObject room_Prefab;

    private Bounds roomBounds;
    private double parentRadius;
    // Start is called before the first frame update
    void Start()
    {
        parentRadius = transform.localScale.x / 2;    //parent is a sphere, get scale which is diameter and divide by 2 for radius
        roomBounds = room_Prefab.GetComponent<MeshRenderer>().bounds;

        int roomsToMake = numRooms / 2;
        double innerRoomAngleInc = 360 / numRooms;
        createHorizontalRooms(roomsToMake, parentRadius, innerRoomAngleInc);

    }

    //sphere point references
    //Reference: https://math.stackexchange.com/questions/1301500/how-to-find-point-on-sphere-from-pitch-and-heading
    //Reference2:https://stackoverflow.com/questions/969798/plotting-a-point-on-the-edge-of-a-sphere
    void createHorizontalRooms(int roomsToMake, double radius, double angleInc)
    {
        double angle = 0;
        for (int i = 0; i < roomsToMake; i++, angle += angleInc)
        {
            float angleS = (float)((angle * Math.PI) / 180);                          //Calculate angle into radians
            
            float posX = (float)(radius * Math.Cos(angleS));
            float posZ = (float)(radius * Math.Sin(angleS));

            Vector3 pos = new Vector3(posX, 0, posZ);
            Vector3 otherPos = new Vector3(-posX, 0, -posZ);                         //Mirror room on -x axis around sphere

            createRoom(pos);
            createRoom(otherPos);

            print("anlgeS: " + angleS);
            print("new room at pos: " + pos + " and at pos: " + otherPos);
        }
    }

    void createRoom(Vector3 pos)
    {
        GameObject newRoom = Instantiate(room_Prefab, pos + transform.position, room_Prefab.transform.rotation);
        newRoom.transform.localScale = room_Prefab.transform.localScale;
        newRoom.transform.parent = this.transform;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
