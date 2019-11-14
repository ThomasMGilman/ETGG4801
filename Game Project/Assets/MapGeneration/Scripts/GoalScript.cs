using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoalScript : MonoBehaviour
{

    private bool paused = false;
    private float value = 1000;
    private bool destroy = false;
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
        }
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
        this.gameObject.GetComponent<ParticleSystem>().Stop();
        this.gameObject.GetComponent<MeshCollider>().enabled = false;
        destroy = true;
        audioS.Play();
    }
}
