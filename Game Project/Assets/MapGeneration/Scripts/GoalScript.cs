using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoalScript : MonoBehaviour
{

    const float twoPi = 2 * Mathf.PI;
    const float oneEightyOverPi = 180 / Mathf.PI;

    private bool paused = false;
    private float value = 1000;
    private bool destroy = false;
    private float angleSpeed = 1;
    private Vector3 Goal_Angle;
    private float angleY;
    AudioSource audioS;
    // Start is called before the first frame update
    void Start()
    {
        audioS = this.gameObject.GetComponent<AudioSource>();
        
    }

    // Update is called once per frame
    void Update()
    {
        if(!paused)
        {
            if (destroy && !audioS.isPlaying)
                Destroy(this.gameObject);

            Goal_Angle = new Vector3(0, updateRotation(ref this.angleY, -1), 0);
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
    float updateRotation(ref float anglePos, int invert = 1)
    {
        anglePos += angleSpeed * Time.fixedDeltaTime;
        if (anglePos >= twoPi) anglePos -= twoPi;
        else if (anglePos <= -twoPi) anglePos += twoPi;
        return anglePos * oneEightyOverPi;
    }

    private void setPauseState(bool state)
    {
        paused = state;
    }

    private void setValue(float scoreValue)
    {
        value = scoreValue;
    }

    private void getVal(GameObject other)
    {
        other.SendMessage("updateScore", value);
        this.gameObject.GetComponent<MeshRenderer>().enabled = false;
        this.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        this.transform.GetChild(1).GetComponent<Light>().enabled = false;
        this.transform.GetChild(2).GetComponent<ParticleSystem>().Stop();
        this.gameObject.GetComponent<MeshCollider>().enabled = false;
        destroy = true;
        audioS.Play();
    }
}
