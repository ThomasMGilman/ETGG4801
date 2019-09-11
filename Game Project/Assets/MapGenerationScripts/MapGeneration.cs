using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGeneration : MonoBehaviour
{
    public int Width;
    public int Height;
    public int Depth;

    public float seed;
    public bool useRandomSeed = true;

    [Range(0, 100)]
    private float fillPercent;

    private float[,,] map;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void generateMap()
    {
        seed = Time.time;
        map = new float[Width, Height, Depth];
    }
}
