using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerScript : MonoBehaviour
{
    public GameObject playerPrefab;
    // Start is called before the first frame update
    void Start()
    {
        Vector3 spawnPosition = this.transform.position;
        spawnPosition.y += playerPrefab.transform.localScale.y;
        Instantiate(playerPrefab, spawnPosition, playerPrefab.transform.rotation);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
