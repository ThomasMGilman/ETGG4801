﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    const float twoPi = 2 * Mathf.PI;
    const float oneEightyOverPi = 180 / Mathf.PI;

    CursorLockMode wantMode;    //referenced https://docs.unity3d.com/ScriptReference/Cursor-lockState.html

    Rigidbody Player_Rigidbody;
    Camera Player_Cam;
    Camera MiniMap_Cam;
    Vector3 Player_Velocity, Player_Angle;

    private GameObject menuObject, overLay;
    bool paused = false;
    bool gotGoal = false;
    float goalValue = 1000;
    float angleX, angleY;
    float score;

    Text scoreText;

    void Start()
    {
        this.Player_Rigidbody = GetComponent<Rigidbody>();
        this.Player_Cam = this.transform.GetChild(0).GetComponent<Camera>();
        angleX = 0; angleY = 0;
        score = 1000;

        SetCursorState(CursorLockMode.Locked);          //Lock Mouse Movement

        menuObject = GameObject.Find("Canvas").transform.GetChild(1).gameObject;
        overLay = GameObject.Find("Canvas").transform.GetChild(0).gameObject;
        MiniMap_Cam = GameObject.FindGameObjectWithTag("mapCamera").GetComponent<Camera>();

        overLay.SetActive(true);
        scoreText = GameObject.FindGameObjectWithTag("Score").GetComponent<Text>();
        scoreText.text = score.ToString();
        Vector3 miniMapPos = this.transform.position;
        miniMapPos.y += 100;
        MiniMap_Cam.transform.position = miniMapPos;
    }

    private void checkInput()
    {
        if (Input.GetKeyDown(KeyCode.Q))
            SetCursorState(CursorLockMode.None);

        if (Input.GetKeyDown(KeyCode.Mouse0))
            SetCursorState(CursorLockMode.Locked);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorState(CursorLockMode.None);
            menuObject.SendMessage("setPauseState", !paused);
        }

    }

    /// <summary>
    /// Update the Angle by its specified axis and return the angle in degrees.
    /// </summary>
    /// <param name="anglePos"></param>
    /// <param name="axis"></param>
    /// <param name="invert"></param>
    /// <returns></returns>
    float updateRotation(ref float anglePos, string axis, int invert = 1)
    {
        float inputVal = Input.GetAxis(axis);
        anglePos += inputVal != 0 ? invert * Input.GetAxis(axis) * Time.fixedDeltaTime : 0; //If Input do movement computation
        if (anglePos >= twoPi) anglePos -= twoPi;
        else if (anglePos <= -twoPi) anglePos += twoPi;
        return anglePos * oneEightyOverPi;
    }

    void Update()
    {
        if(!paused)
        {
            checkInput();
            Player_Velocity = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized * 10;
            Player_Angle = new Vector3(updateRotation(ref this.angleY, "MouseY", -1), updateRotation(ref this.angleX, "MouseX"), 0);
            score -= 1 * Time.deltaTime;
            if(score <= 0)
            {
                //GameOver
            }
            scoreText.text = score.ToString();
        }
        
    }

    void FixedUpdate()
    {
        if(!paused)
        {
            this.transform.rotation = Quaternion.Euler(Player_Angle);
            Vector3 newPosOffset = Player_Velocity != Vector3.zero ? Player_Cam.transform.TransformDirection(Player_Velocity * Time.fixedDeltaTime) : Vector3.zero;
            this.transform.position += newPosOffset;
            MiniMap_Cam.transform.position += newPosOffset;

        }
    }

    /// <summary>
    /// Sets the Cursor State While inGame
    /// </summary>
    /// <param name="state"></param>
    private void SetCursorState(CursorLockMode state)
    {
        Cursor.lockState = wantMode = state;
        Cursor.visible = (CursorLockMode.Locked != wantMode);
    }

    private void setPauseState(bool state)
    {
        paused = state;
        overLay.SetActive(paused);
        if(!paused)
            SetCursorState(CursorLockMode.Locked);
    }

    private void OnCollisionEnter(Collision collision)
    {
        GameObject other = collision.gameObject;
        if(other.tag == "Goal")
        {
            score += goalValue;
            gotGoal = true;
            Destroy(other);
        }
        if(other.tag == "Spawner")
        {
            if(gotGoal)
            {
                //WinGAME!
            }
        }
    }
}
