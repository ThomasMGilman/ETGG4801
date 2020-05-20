﻿using UnityEngine;

public class SpawnerScript : MonoBehaviour
{
    public GameObject playerPrefab;
    private GameObject player;
    
    // Start is called before the first frame update
    void Start()
    {
        Vector3 spawnPosition = this.transform.position;
        spawnPosition.y += playerPrefab.transform.localScale.y+1;
        player = Instantiate(playerPrefab, spawnPosition, playerPrefab.transform.rotation);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}