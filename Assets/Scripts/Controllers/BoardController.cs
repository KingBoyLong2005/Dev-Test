using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardController : MonoBehaviour
{
    private const float AutoplayDelay = 0.5f;

    public event Action OnMoveEvent = delegate { };

    public bool IsBusy { get; private set; }

    private Board m_board;

    private GameManager m_gameManager;


    private Camera m_cam;


    private GameSettings m_gameSettings;

    private bool m_isTimeAttack;

    private bool m_gameOver;

    private readonly List<Item> m_bottomItems = new List<Item>();

    private readonly List<Transform> m_bottomCells = new List<Transform>();

    private readonly Dictionary<Item, Cell> m_initialCells = new Dictionary<Item, Cell>();

    private Coroutine m_autoplayCoroutine;

    public void StartGame(
        GameManager gameManager,
        GameSettings gameSettings,
        bool isTimeAttack = false)
    {
        m_gameManager = gameManager;

        m_gameSettings = gameSettings;
        m_isTimeAttack = isTimeAttack;

        m_gameManager.StateChangedAction += OnGameStateChange;

        m_cam = Camera.main;

        int remainder =
            (gameSettings.BoardSizeX * gameSettings.BoardSizeY) % 3;

        if (remainder != 0)
        {
            gameSettings.BoardSizeY += 3 - remainder;
            Debug.LogWarning(
                "Board height was adjusted so item count is divisible by 3.");
        }

        m_board = new Board(this.transform, gameSettings);

        CreateBottomCells();

        m_board.Fill();
    }

    private void OnGameStateChange(GameManager.eStateGame state)
    {
        switch (state)
        {
            case GameManager.eStateGame.GAME_STARTED:
                IsBusy = false;
                break;
            case GameManager.eStateGame.PAUSE:
                IsBusy = true;
                break;
            case GameManager.eStateGame.GAME_OVER:
            case GameManager.eStateGame.WIN:
            case GameManager.eStateGame.LOSE:
                m_gameOver = true;
                break;
        }
    }


    public void Update()
    {
        if (m_gameOver) return;
        if (IsBusy) return;

        if (Input.GetMouseButtonDown(0))
        {
            var hit = Physics2D.Raycast(m_cam.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit.collider != null)
            {

                Cell cell = hit.collider.GetComponent<Cell>();
                if (m_board.ContainsCell(cell))
                {
                    MoveItemToBottom(cell);
                }
                else
                {
                    int bottomIndex = GetBottomItemIndex(hit.transform);
                    if (bottomIndex < 0)
                    {
                        bottomIndex = GetBottomCellIndex(cell);
                    }

                    if (bottomIndex >= 0)
                    {
                        ReturnItemToBoard(bottomIndex);
                    }
                }
            }
        }

    }

    private void CreateBottomCells()
    {
        GameObject prefab =
            Resources.Load<GameObject>(
                Constants.PREFAB_CELL_BACKGROUND
            );

        float firstX =
            -(m_gameSettings.BottomCellCount - 1) * 0.5f;

        float y =
            -(m_gameSettings.BoardSizeY * 0.5f) - 1.25f;

        for (int i = 0;
             i < m_gameSettings.BottomCellCount;
             i++)
        {
            GameObject cell = Instantiate(
                prefab,
                new Vector3(firstX + i, y, 0f),
                Quaternion.identity,
                transform
            );

            m_bottomCells.Add(cell.transform);
        }
    }

    private void MoveItemToBottom(Cell cell)
    {
        if (cell == null ||
            cell.Item == null ||
            m_bottomItems.Count >= m_bottomCells.Count)
        {
            return;
        }

        Item item = m_board.TakeItem(cell);

        if (item == null || item.View == null)
        {
            return;
        }

        m_initialCells[item] = cell;

        m_bottomItems.Add(item);

        IsBusy = true;

        OnMoveEvent();

        int bottomIndex = m_bottomItems.Count - 1;

        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Append(item.View.DOMove(
            m_bottomCells[bottomIndex].position,
            0.25f
        ));
        moveSequence.Join(item.View.DOPunchScale(
            Vector3.one * 0.15f,
            0.25f,
            1,
            0.5f
        ));
        moveSequence.SetEase(Ease.InOutQuad);
        moveSequence.OnComplete(() =>
        {
            ClearBottomTriples();

            IsBusy = false;

            CheckEndGame();
        });
    }

    private int GetBottomCellIndex(Cell cell)
    {
        if (cell == null)
        {
            return -1;
        }

        for (int i = 0; i < m_bottomCells.Count; i++)
        {
            if (m_bottomCells[i] == cell.transform)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetBottomItemIndex(Transform hitTransform)
    {
        for (int i = 0; i < m_bottomItems.Count; i++)
        {
            Transform itemView = m_bottomItems[i].View;
            if (itemView == hitTransform ||
                (itemView != null && hitTransform.IsChildOf(itemView)))
            {
                return i;
            }
        }

        return -1;
    }

    private void ReturnItemToBoard(int bottomIndex)
    {
        if (bottomIndex < 0 || bottomIndex >= m_bottomItems.Count)
        {
            return;
        }

        Item item = m_bottomItems[bottomIndex];
        if (!m_initialCells.TryGetValue(item, out Cell targetCell) ||
            !targetCell.IsEmpty || item.View == null)
        {
            return;
        }

        m_bottomItems.RemoveAt(bottomIndex);
        targetCell.Assign(item);
        IsBusy = true;

        item.View.DOMove(targetCell.transform.position, 0.25f)
            .OnComplete(() =>
            {
                MoveBottomItemsIntoEmptyCells();
                IsBusy = false;
            });
    }

    private void ClearBottomTriples()
    {
        for (int i = 0; i < m_bottomItems.Count; i++)
        {
            List<int> matchingIndexes =
                new List<int> { i };

            for (int j = i + 1;
                 j < m_bottomItems.Count;
                 j++)
            {
                if (m_bottomItems[i].IsSameType(
                    m_bottomItems[j]))
                {
                    matchingIndexes.Add(j);
                }

                if (matchingIndexes.Count ==
                    m_gameSettings.MatchesMin)
                {
                    break;
                }
            }

            if (matchingIndexes.Count <
                m_gameSettings.MatchesMin)
            {
                continue;
            }

            for (int j = matchingIndexes.Count - 1;
                 j >= 0;
                 j--)
            {
                int index = matchingIndexes[j];

                m_bottomItems[index].ExplodeView();
                m_bottomItems.RemoveAt(index);
            }

            MoveBottomItemsIntoEmptyCells();

            return;
        }
    }

    private void MoveBottomItemsIntoEmptyCells()
    {
        for (int i = 0;
             i < m_bottomItems.Count;
             i++)
        {
            if (m_bottomItems[i].View == null)
            {
                continue;
            }

            m_bottomItems[i].View.DOMove(
                m_bottomCells[i].position,
                0.15f
            );
        }
    }

    private void CheckEndGame()
    {
        bool boardIsEmpty =
            m_board.GetCells().All(cell => cell.IsEmpty);

        if (boardIsEmpty)
        {
            m_gameManager.SetState(
                GameManager.eStateGame.WIN
            );
        }
        else if (!m_isTimeAttack && m_bottomItems.Count >=
                 m_bottomCells.Count)
        {
            m_gameManager.SetState(
                GameManager.eStateGame.LOSE
            );
        }
    }

    public void StartAutoplayWin()
    {
        StartAutoplay(true);
    }

    public void StartAutoplayLose()
    {
        StartAutoplay(false);
    }

    private void StartAutoplay(bool shouldWin)
    {
        if (m_autoplayCoroutine != null)
        {
            StopCoroutine(m_autoplayCoroutine);
        }

        m_gameOver = false;
        m_autoplayCoroutine = StartCoroutine(AutoplayCoroutine(shouldWin));
    }

    private IEnumerator AutoplayCoroutine(bool shouldWin)
    {
        while (!m_gameOver)
        {
            Cell cell = FindAutoplayCell(shouldWin);

            if (cell == null)
            {
                break;
            }

            MoveItemToBottom(cell);

            while (IsBusy && !m_gameOver)
            {
                yield return null;
            }

            yield return new WaitForSeconds(AutoplayDelay);
        }

        m_autoplayCoroutine = null;
    }

    private Cell FindAutoplayCell(bool shouldWin)
    {
        List<Cell> availableCells = m_board.GetCells()
            .Where(cell => cell.Item is NormalItem)
            .ToList();

        if (availableCells.Count == 0)
        {
            return null;
        }

        if (shouldWin)
        {
            foreach (NormalItem bottomItem in m_bottomItems.OfType<NormalItem>())
            {
                Cell matchingCell = availableCells.FirstOrDefault(cell =>
                    ((NormalItem)cell.Item).ItemType == bottomItem.ItemType);

                if (matchingCell != null)
                {
                    return matchingCell;
                }
            }

            IGrouping<NormalItem.eNormalType, Cell> group = availableCells
                .GroupBy(cell => ((NormalItem)cell.Item).ItemType)
                .FirstOrDefault(groupItems =>
                    groupItems.Count() >= m_gameSettings.MatchesMin);

            if (group != null)
            {
                return group.First();
            }

            return availableCells.First();
        }

        foreach (Cell cell in availableCells)
        {
            NormalItem normalItem = (NormalItem)cell.Item;

            int sameTypeCount = m_bottomItems
                .OfType<NormalItem>()
                .Count(item => item.ItemType == normalItem.ItemType);

            if (sameTypeCount <
                m_gameSettings.MatchesMin - 1)
            {
                return cell;
            }
        }

        return availableCells.First();
    }

    internal void Clear()
    {
        m_board.Clear();
        m_bottomItems.Clear();
        m_bottomCells.Clear();
        m_initialCells.Clear();
    }
}
