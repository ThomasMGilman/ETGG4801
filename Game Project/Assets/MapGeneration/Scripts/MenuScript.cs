using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuScript : MonoBehaviour
{
    public GameObject GamePrefab;
    public GameObject menuPanel;
    public GameObject winPanel;
    public GameObject losePanel;
    public RawImage GameName;
    public Button NewGameButton;
    public Button ContinueGameButton;
    public Button SettingsButton;
    public Button QuitButton;

    private Text scoreText;

    CursorLockMode wantMode;

    private GameObject GameState, menuObject, overLay;

    private bool firstStart;
    private bool paused = false;

    // Start is called before the first frame update
    void Start()
    {
        NewGameButton.interactable = true;
        NewGameButton.transform.localScale = NewGameButton.transform.localScale / 2;

        ContinueGameButton.interactable = false;
        ContinueGameButton.transform.localScale = ContinueGameButton.transform.localScale / 2;

        SettingsButton.interactable = true;
        SettingsButton.transform.localScale = SettingsButton.transform.localScale / 2;

        QuitButton.interactable = true;
        QuitButton.transform.localScale = QuitButton.transform.localScale / 2;

        firstStart = true;
        paused = false;
        menuObject = GameObject.Find("Canvas").transform.GetChild(1).gameObject;
        overLay = GameObject.Find("Canvas").transform.GetChild(0).gameObject;
        overLay.SetActive(false);
        menuObject.SetActive(true);

        scoreText = winPanel.transform.GetChild(2).gameObject.GetComponent<Text>(); //ScoreText
        winPanel.SetActive(false);

        losePanel.SetActive(false);
    }

    private bool keyInput()
    {
        return (Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Mouse0)) == true;
    }

    // Update is called once per frame
    void Update()
    {
        if(winPanel.activeSelf && keyInput())
        {
            winPanel.SetActive(false);
            menuPanel.SetActive(true);
        }
        else if(losePanel.activeSelf && keyInput())
        {
            losePanel.SetActive(false);
            menuPanel.SetActive(true);
        }
    }

    public void startGame()
    {
        if (firstStart)
        {
            GameState = Instantiate(GamePrefab, Vector3.zero, GamePrefab.transform.rotation);
            firstStart = false;

        }
        else
            SceneManager.LoadScene(0, LoadSceneMode.Single);
        ContinueGameButton.interactable = true;
        menuPanel.SetActive(paused);
    }

    ///Tell Everyone to wake up and start playing again and hide the menu again
    public void continueGame()
    {
        paused = false;
        menuPanel.SetActive(paused);
        GameObject.FindGameObjectWithTag("Player").SendMessage("setPauseState", paused);
        GameObject.FindGameObjectWithTag("Goal").SendMessage("setPauseState", paused);
    }

    public void setSettings()
    {

    }

    public void quitGame()
    {
        Application.Quit();
    }

    private void setPauseState(bool state)
    {
        paused = state;
        menuPanel.SetActive(paused);
        GameObject.FindGameObjectWithTag("Player").SendMessage("setPauseState", paused);
        GameObject Goal = GameObject.FindGameObjectWithTag("Goal");
        if(Goal != null) Goal.SendMessage("setPauseState", paused);
    }

    private void winScreen(float score)
    {
        setPauseState(true);
        winPanel.SetActive(true);
        scoreText.text = score.ToString();
        ContinueGameButton.interactable = false;
        SetCursorState(CursorLockMode.None);
    }

    private void loseScreen()
    {
        setPauseState(true);
        losePanel.SetActive(true);
        ContinueGameButton.interactable = false;
        SetCursorState(CursorLockMode.None);
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
