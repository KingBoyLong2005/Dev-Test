using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public event Action<eStateGame> StateChangedAction = delegate { };

    public enum eLevelMode
    {
        TIMEATTACK,
        TIMER,
        MOVES
    }

    public enum eStateGame
    {
        SETUP,
        MAIN_MENU,
        GAME_STARTED,
        PAUSE,
        GAME_OVER,
        WIN,
        LOSE
    }

    private eStateGame m_state;
    public eStateGame State
    {
        get { return m_state; }
        private set
        {
            m_state = value;

            StateChangedAction(m_state);
        }
    }


    private GameSettings m_gameSettings;


    private BoardController m_boardController;

    private UIMainManager m_uiMenu;

    private LevelCondition m_levelCondition;

    private void Awake()
    {
        State = eStateGame.SETUP;

        m_gameSettings = Resources.Load<GameSettings>(Constants.GAME_SETTINGS_PATH);

        m_uiMenu = FindObjectOfType<UIMainManager>();
        m_uiMenu.Setup(this);
    }

    void Start()
    {
        State = eStateGame.MAIN_MENU;
    }

    // Update is called once per frame
    void Update()
    {
        if (m_boardController != null) m_boardController.Update();
    }


    internal void SetState(eStateGame state)
    {
        State = state;

        if(State == eStateGame.PAUSE)
        {
            DOTween.PauseAll();
        }
        else
        {
            DOTween.PlayAll();
        }
    }

    public void LoadLevel(eLevelMode mode, bool createCondition = true)
    {
        if (m_boardController != null)
        {
            ClearLevel();
        }

        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent -= GameOver;
            Destroy(m_levelCondition);
            m_levelCondition = null;
        }

        m_boardController = new GameObject("BoardController").AddComponent<BoardController>();
        m_boardController.StartGame(
            this,
            m_gameSettings,
            mode == eLevelMode.TIMEATTACK
        );

        if (createCondition && mode == eLevelMode.MOVES)
        {
            m_levelCondition = this.gameObject.AddComponent<LevelMoves>();
            m_levelCondition.Setup(m_gameSettings.LevelMoves, m_uiMenu.GetLevelConditionView(), m_boardController);
        }
        else if (createCondition && mode == eLevelMode.TIMER)
        {
            m_levelCondition = this.gameObject.AddComponent<LevelTime>();
            m_levelCondition.Setup(m_gameSettings.LevelTime, m_uiMenu.GetLevelConditionView(), this);
        }
        else if (createCondition && mode == eLevelMode.TIMEATTACK)
        {
            m_levelCondition = this.gameObject.AddComponent<LevelTime>();
            m_levelCondition.Setup(m_gameSettings.TimeAttackDuration, m_uiMenu.GetLevelConditionView(), this);
        }


        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent += GameOver;
        }

        State = eStateGame.GAME_STARTED;
    }

    public void StartAutoplayWin()
    {
        if (m_boardController != null) m_boardController.StartAutoplayWin();
    }

    public void StartAutoplayLose()
    {
        if (m_boardController != null) m_boardController.StartAutoplayLose();
    }

    public void GameOver()
    {
        StartCoroutine(WaitBoardController());
    }

    internal void ClearLevel()
    {
        if (m_boardController)
        {
            m_boardController.Clear();
            Destroy(m_boardController.gameObject);
            m_boardController = null;
        }
    }

    private IEnumerator WaitBoardController()
    {
        if (m_state == eStateGame.WIN || m_state == eStateGame.LOSE)
        {
            yield break;
        }

        while (m_boardController.IsBusy)
        {
            yield return new WaitForEndOfFrame();
        }

        yield return new WaitForSeconds(1f);

        if (m_state != eStateGame.WIN && m_state != eStateGame.LOSE)
        {
            State = eStateGame.GAME_OVER;
        }

        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent -= GameOver;

            Destroy(m_levelCondition);
            m_levelCondition = null;
        }
    }
}
