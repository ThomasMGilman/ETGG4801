using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    const float twoPi = 2 * Mathf.PI;
    const float oneEightyOverPi = 180 / Mathf.PI;
    const float PiOverOneEighty = Mathf.PI / 180;

    CursorLockMode wantMode;    //referenced https://docs.unity3d.com/ScriptReference/Cursor-lockState.html

    Rigidbody Player_Rigidbody;
    Camera Player_Cam;
    Camera MiniMap_Cam;
    Vector3 Player_Velocity, Player_Angle, Camera_Angle;

    private GameObject menuObject, overLay;
    bool paused = false;
    bool gotGoal = false;
    bool jumping = false;
    float goalValue = 1000;
    float angleX, angleY;
    float score = 500;
    float maxCamXAngle = 45.0f * PiOverOneEighty;
    float minCamXAngle = -45.0f * PiOverOneEighty;

    Text scoreText;

    public GameObject trailPrefab;
    uint maxTrail = 400000000;
    bool useMaxTrail = true;
    float travelDistancePerTrailMarker = 300;
    float jumpAmount = .15f;
    float jumpDis = 0;
    float groundHight;
    private float steps = 0;
    private Queue<GameObject> trail;

    public int targetFPS = 60;

    void Start()
    {
        trail = new Queue<GameObject>();
        steps = 0;
        groundHight = this.transform.position.y - this.transform.localScale.y*1.5f;

        //if (useMaxTrail) maxTrail = 2 ^ (sizeof(uint) * 8) - 1;
        maxTrail = 100000;//2 ^ (sizeof(uint) * 8) - 1;

        this.Player_Rigidbody = GetComponent<Rigidbody>();
        this.Player_Cam = this.transform.GetChild(0).GetComponent<Camera>();
        angleX = 0; angleY = 0;

        //Lock Mouse Movement
        set_cursor_state(CursorLockMode.Locked);

        menuObject  = GameObject.Find("Canvas").transform.GetChild(1).gameObject;
        overLay     = GameObject.Find("Canvas").transform.GetChild(0).gameObject;
        MiniMap_Cam = GameObject.FindGameObjectWithTag("mapCamera").GetComponent<Camera>();

        overLay.SetActive(true);
        scoreText = GameObject.FindGameObjectWithTag("Score").GetComponent<Text>();
        scoreText.text = ((int)score).ToString();
        Vector3 miniMapPos = this.transform.position;
        miniMapPos.y += 100;
        MiniMap_Cam.transform.position = miniMapPos;
    }

    private void checkInput()
    {
        //if (Input.GetKeyDown(KeyCode.Q))  //For testing
        //    SetCursorState(CursorLockMode.None);

        if (Input.GetKeyDown(KeyCode.Mouse0))
            set_cursor_state(CursorLockMode.Locked);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            set_cursor_state(CursorLockMode.None);
            menuObject.SetActive(true);
            overLay.SetActive(false);
            GameObject.FindGameObjectWithTag("Menu").SendMessage("set_pause_state", !paused);
        }

        if(Input.GetKeyDown(KeyCode.Space) && !jumping)
        {
            jumping = true;
            jumpDis = jumpAmount;
        }

    }

    /// <summary>
    /// Update the Angle by its specified axis and return the angle in degrees.
    /// </summary>
    /// <param name="anglePos"></param>
    /// <param name="axis"></param>
    /// <param name="invert"></param>
    /// <returns></returns>
    float updateRotation(ref float anglePos, string axis, int invert = 1, bool camAngle = false)
    {
        float inputVal = Input.GetAxis(axis);

        //If Input do movement computation
        if (inputVal != 0)
        {
            float amnt = invert * Input.GetAxis(axis) * Time.fixedDeltaTime;
            if (camAngle)
            {
                float newVal = anglePos + amnt;
                if (newVal > minCamXAngle && newVal < maxCamXAngle)
                    anglePos = newVal;
            }
            else
                anglePos += amnt;
            if (anglePos >= twoPi) anglePos -= twoPi;
            else if (anglePos <= -twoPi) anglePos += twoPi;
        }
        return anglePos * oneEightyOverPi;
    }

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFPS;
    }

    void Update()
    {

        if (Application.targetFrameRate != targetFPS)
            Application.targetFrameRate = targetFPS;

        if (!paused)
        {
            checkInput();
            Player_Velocity = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized * 10;
            Player_Angle = new Vector3(0, updateRotation(ref this.angleX, "MouseX"), 0);
            Camera_Angle = new Vector3(updateRotation(ref this.angleY, "MouseY", -1, true), 0, 0);

            score -= 10 * Time.deltaTime;
            if (score <= 0)
                GameObject.FindGameObjectWithTag("Menu").SendMessage("lose_screen");
            scoreText.text = ((int)score).ToString();

            steps += Mathf.Abs(Player_Velocity.x) + Mathf.Abs(Player_Velocity.z);
            leave_trail();
        }
    }

    void FixedUpdate()
    {
        if (!paused)
        {
            if (Player_Angle.x >= 75) Player_Angle.x = 75; if (Player_Angle.x <= -75) Player_Angle.x = -75;
            this.transform.rotation = Quaternion.Euler(Player_Angle);
            Player_Cam.transform.rotation = Quaternion.Euler(Camera_Angle + Player_Angle);
            Vector3 newPosOffset = Player_Velocity != Vector3.zero ? Player_Cam.transform.TransformDirection(Player_Velocity * Time.fixedDeltaTime) : Vector3.zero;
            newPosOffset.y = 0;
            if (jumping && jumpDis > 0)
            {
                jumpDis -= jumpAmount * Time.fixedDeltaTime;
                newPosOffset.y = jumpDis;
            }
            else
                jumping = false;

            if (this.transform.position.y + newPosOffset.y < groundHight) newPosOffset.y = 0;
            this.transform.position += newPosOffset;
            update_camera_position();

        }
    }

    private void update_camera_position()
    {
        MiniMap_Cam.transform.position = new Vector3(this.transform.position.x, MiniMap_Cam.transform.position.y,  this.transform.position.z);
    }

    /// <summary>
    /// Leave a trail behind the player
    /// </summary>
    private void leave_trail()
    {
        if(steps >= travelDistancePerTrailMarker && maxTrail > 0)
        {
            steps = 0;

            if (trail.Count >= maxTrail - 1)
                Destroy(trail.Dequeue());
            Vector3 crumPos = this.transform.position; crumPos.y = 10 + trailPrefab.transform.localScale.y;
            trail.Enqueue(Instantiate(trailPrefab, crumPos, trailPrefab.transform.rotation));
        }
    }

    /// <summary>
    /// Sets the Cursor State While inGame
    /// </summary>
    /// <param name="state"></param>
    private void set_cursor_state(CursorLockMode state)
    {
        Cursor.lockState = wantMode = state;
        Cursor.visible = (CursorLockMode.Locked != wantMode);
    }

    private void set_pause_state(bool state)
    {
        paused = state;
        //overLay.SetActive(paused);
        if (!overLay.activeSelf) overLay.SetActive(true);
        if(!paused)
            set_cursor_state(CursorLockMode.Locked);
    }

    private void update_score(float val)
    {
        score += val;
    }

    private void OnCollisionEnter(Collision collision)
    {
        GameObject other = collision.gameObject;
        if(other.tag == "Goal" || other.name == "TempGoal(Clone)")
        {
            gotGoal = true;
        }
        if(other.tag == "Spawner" && gotGoal)
        {
            GameObject.FindGameObjectWithTag("Menu").SendMessage("win_screen", (int)score);
        }
        if(other.tag == "Roof")
        {
            jumping = false;
            jumpDis = 0;
        }
    }
}
