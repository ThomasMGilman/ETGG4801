using System.Collections;
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
    bool jumping = false;
    float goalValue = 1000;
    float angleX, angleY;
    float score = 500;

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

        SetCursorState(CursorLockMode.Locked);          //Lock Mouse Movement

        menuObject = GameObject.Find("Canvas").transform.GetChild(1).gameObject;
        overLay = GameObject.Find("Canvas").transform.GetChild(0).gameObject;
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
            SetCursorState(CursorLockMode.Locked);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorState(CursorLockMode.None);
            menuObject.SetActive(true);
            overLay.SetActive(false);
            GameObject.FindGameObjectWithTag("Menu").SendMessage("setPauseState", !paused);
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
        if (!paused)
        {
            checkInput();
            Player_Velocity = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized * 10;
            Player_Angle = new Vector3(updateRotation(ref this.angleY, "MouseY", -1), updateRotation(ref this.angleX, "MouseX"), 0);
            score -= 10 * Time.deltaTime;
            if (score <= 0)
            {
                GameObject.FindGameObjectWithTag("Menu").SendMessage("loseScreen");
            }
            scoreText.text = ((int)score).ToString();

            steps += Mathf.Abs(Player_Velocity.x) + Mathf.Abs(Player_Velocity.z);
            leaveTrail();
        }
    }

    void FixedUpdate()
    {
        if (!paused)
        {
            if (Player_Angle.x >= 75) Player_Angle.x = 75; if (Player_Angle.x <= -75) Player_Angle.x = -75;
            this.transform.rotation = Quaternion.Euler(Player_Angle);
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
            updateCameraPosition();

        }
    }

    private void updateCameraPosition()
    {
        MiniMap_Cam.transform.position = new Vector3(this.transform.position.x, MiniMap_Cam.transform.position.y,  this.transform.position.z);
    }

    /// <summary>
    /// Leave a trail behind the player
    /// </summary>
    private void leaveTrail()
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
    private void SetCursorState(CursorLockMode state)
    {
        Cursor.lockState = wantMode = state;
        Cursor.visible = (CursorLockMode.Locked != wantMode);
    }

    private void setPauseState(bool state)
    {
        paused = state;
        //overLay.SetActive(paused);
        if (!overLay.activeSelf) overLay.SetActive(true);
        if(!paused)
            SetCursorState(CursorLockMode.Locked);
    }

    private void updateScore(float val)
    {
        score += val;
    }

    private void OnCollisionEnter(Collision collision)
    {
        GameObject other = collision.gameObject;
        if(other.tag == "Goal" || other.name == "TempGoal(Clone)")
        {
            other.SendMessage("getVal", this.gameObject);
            gotGoal = true;
        }
        if(other.tag == "Spawner" && gotGoal)
        {
            GameObject.FindGameObjectWithTag("Menu").SendMessage("winScreen", (int)score);
        }
        if(other.tag == "Roof")
        {
            jumping = false;
            jumpDis = 0;
        }
    }
}
