using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoalScript : MonoBehaviour
{

    const float twoPi = 2 * Mathf.PI;
    const float oneEightyOverPi = 180 / Mathf.PI;

    private const float maxDis = 10;

    private bool paused = false;
    private float value = 1000;
    private bool destroy = false;
    private float angleSpeed = 0.75f;
    private float moveSpeed = 1;
    private Vector3 Goal_Angle;
    private Vector3 Goal_Pos;
    private Vector3 Goal_Dir;
    private Vector3 startPos;

    private float angleY;
    AudioSource audioS;
    // Start is called before the first frame update
    void Start()
    {
        angleY = UnityEngine.Random.Range(0.1f, 1);
        Goal_Angle = Vector3.zero;
        audioS = this.gameObject.GetComponent<AudioSource>();
        startPos = this.transform.position;
        Goal_Pos = startPos;
        change_dir();
    }

    float get_distance(Vector3 newPos)
    {
        return Mathf.Sqrt(Mathf.Pow(newPos.x - startPos.x, 2) + Mathf.Pow(newPos.y - startPos.y, 2) + Mathf.Pow(newPos.z - startPos.z, 2));
    }

    void change_dir()
    {
        Goal_Dir = new Vector3(UnityEngine.Random.Range(-1, 1), 0, UnityEngine.Random.Range(-1, 1));
        if (Goal_Dir == Vector3.zero)
            Goal_Dir = new Vector3(1, 0, .25f);
    }

    Vector3 get_new_position()
    {
        Vector3 newPos = this.transform.position + Goal_Dir * moveSpeed * Time.deltaTime;

        // change direction and move in direction of starting point
        if (get_distance(newPos) >= maxDis)
        {
            Goal_Dir *= -1;
            newPos = this.transform.position + Goal_Dir * moveSpeed * Time.deltaTime;
        }
        return newPos;
    }

    // Update is called once per frame
    void Update()
    {
        if(!paused)
        {
            if (destroy && !audioS.isPlaying)
                Destroy(this.gameObject);

            Goal_Angle = new Vector3(0, update_rotation(ref this.angleY, -1), 0);
            Goal_Pos = get_new_position();
            this.transform.position = Goal_Pos;
        }
    }

    private void FixedUpdate()
    {
        this.transform.rotation = Quaternion.Euler(Goal_Angle);
        
    }

    /// <summary>
    /// Update the Angle by its specified axis and return the angle in degrees.
    /// </summary>
    /// <param name="anglePos"></param>
    /// <param name="axis"></param>
    /// <param name="invert"></param>
    /// <returns></returns>
    float update_rotation(ref float anglePos, int invert = 1)
    {
        anglePos += angleSpeed * Time.fixedDeltaTime;
        if (anglePos >= twoPi) anglePos -= twoPi;
        else if (anglePos <= -twoPi) anglePos += twoPi;
        return anglePos * oneEightyOverPi;
    }

    private void set_pause_state(bool state)
    {
        paused = state;
    }

    private void set_value(float scoreValue)
    {
        value = scoreValue;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Player")
        {
            collision.gameObject.SendMessage("update_score", value);
            this.gameObject.GetComponent<MeshRenderer>().enabled = false;
            this.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
            this.transform.GetChild(1).GetComponent<Light>().enabled = false;
            this.transform.GetChild(2).GetComponent<ParticleSystem>().Stop();
            this.gameObject.GetComponent<MeshCollider>().enabled = false;
            destroy = true;
            audioS.Play();
        }
    }
}
