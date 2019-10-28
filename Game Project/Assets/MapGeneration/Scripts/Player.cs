using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    const float twoPi = 2 * Mathf.PI;
    const float oneEightyOverPi = 180 / Mathf.PI;

    CursorLockMode wantMode;    //referenced https://docs.unity3d.com/ScriptReference/Cursor-lockState.html

    Rigidbody Player_Rigidbody;
    Camera Player_Cam;
    Vector3 Player_Velocity, Player_Angle;
    
    float angleX, angleY;

    void Start()
    {
        this.Player_Rigidbody = GetComponent<Rigidbody>();
        this.Player_Cam = this.transform.GetChild(0).GetComponent<Camera>();
        angleX = 0; angleY = 0;

        SetCursorState(CursorLockMode.Locked);          //Lock Mouse Movement
    }

    private void checkInput()
    {
        if (Input.GetKeyDown(KeyCode.Q))
            SetCursorState(CursorLockMode.None);

        if (Input.GetKeyDown(KeyCode.Mouse0))
            SetCursorState(CursorLockMode.Locked);
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
        checkInput();
        Player_Velocity = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized * 10;
        Player_Angle = new Vector3(updateRotation(ref this.angleY, "MouseY", -1), updateRotation(ref this.angleX, "MouseX"), 0);
    }

    void FixedUpdate()
    {
        this.transform.rotation = Quaternion.Euler(Player_Angle);
        this.transform.position += Player_Velocity != Vector3.zero ? Player_Cam.transform.TransformDirection(Player_Velocity * Time.fixedDeltaTime) : Vector3.zero;
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
}
