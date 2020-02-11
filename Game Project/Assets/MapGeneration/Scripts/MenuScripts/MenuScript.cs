using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public TextAsset scoreBoard;
    private Text scoreText;
    private string scorePath = "Assets/Resources/scoreBoard.txt";

    private SortedDictionary<float, List<string>> player_scores;

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

        SettingsButton.interactable = false;
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

        player_scores = new SortedDictionary<float, List<string>>();
    }

    private bool key_input()
    {
        return (Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Mouse0)) == true;
    }

    // Update is called once per frame
    void Update()
    {
        if(winPanel.activeSelf && key_input())
        {
            winPanel.SetActive(false);
            menuPanel.SetActive(true);
        }
        else if(losePanel.activeSelf && key_input())
        {
            losePanel.SetActive(false);
            menuPanel.SetActive(true);
        }
    }

    public void start_game()
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
    public void continue_game()
    {
        paused = false;
        menuPanel.SetActive(paused);
        GameObject.FindGameObjectWithTag("Player").SendMessage("set_pause_state", paused);
        GameObject.FindGameObjectWithTag("Goal").SendMessage("set_pause_state", paused);
    }

    public void set_settings()
    {

    }

    public void quit_game()
    {
        Application.Quit();
    }

    private void set_pause_state(bool state)
    {
        paused = state;
        menuPanel.SetActive(paused);
        GameObject.FindGameObjectWithTag("Player").SendMessage("set_pause_state", paused);
        GameObject Goal = GameObject.FindGameObjectWithTag("Goal");
        if(Goal != null) Goal.SendMessage("set_pause_state", paused);
    }

    private void win_screen(int score)
    {
        set_pause_state(true);
        winPanel.SetActive(true);
        scoreText.text = score.ToString();
        ContinueGameButton.interactable = false;
        set_cursor_state(CursorLockMode.None);
    }

    private void lose_screen()
    {
        set_pause_state(true);
        losePanel.SetActive(true);
        ContinueGameButton.interactable = false;
        set_cursor_state(CursorLockMode.None);
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

    private void add_name_to_dictionary(ref float value, ref string name)
    {
        if (!player_scores[value].Contains(name))
            player_scores[value].Add(name);
    }

    private void display_score()
    {
        StreamReader scoreReader = new StreamReader(scorePath, true); //open Score Text
        while(!scoreReader.EndOfStream)
        {
            string[] line = scoreReader.ReadLine().Trim().Split();
            string nameToCheck = line[0].Trim().ToLower();
            float scoreVal = float.Parse(line[1].Trim());
            if (player_scores.ContainsKey(scoreVal))
                add_name_to_dictionary(ref scoreVal, ref nameToCheck);

        }
    }

    /*
    private void updateWorldWidth(int width)
    {
        for(int i = 0; i < settingsLines.Length; i++)
        {
            string[] line = settingsLines[i].Split();
            if(line[0].Trim().ToLower() == "worldwidth")
            {
                line[1] = width.ToString();
            }
        }
        
    }

    private void updateWorldHight(int hight)
    {

    }
    */
}
