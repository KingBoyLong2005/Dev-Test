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

    // private bool m_isDragging;

    private Camera m_cam;

    // private Collider2D m_hitCollider;

    private GameSettings m_gameSettings;

    // private List<Cell> m_potentialMatch;

    // private float m_timeAfterFill;

    // private bool m_hintIsShown;

    private bool m_gameOver;

    private readonly List<Item> m_bottomItems = new List<Item>();

    private readonly List<Transform> m_bottomCells = new List<Transform>();

    private Coroutine m_autoplayCoroutine;

    public void StartGame(GameManager gameManager, GameSettings gameSettings)
    {
        m_gameManager = gameManager;

        m_gameSettings = gameSettings;

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

    // private void Fill()
    // {
    //     m_board.Fill();
    //     FindMatchesAndCollapse();
    // }

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
                // StopHints();
                break;
        }
    }


    public void Update()
    {
        if (m_gameOver) return;
        if (IsBusy) return;

        // if (!m_hintIsShown)
        // {
        //     m_timeAfterFill += Time.deltaTime;
        //     if (m_timeAfterFill > m_gameSettings.TimeForHint)
        //     {
        //         m_timeAfterFill = 0f;
        //         ShowHint();
        //     }
        // }

        if (Input.GetMouseButtonDown(0))
        {
            var hit = Physics2D.Raycast(m_cam.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit.collider != null)
            {
                // m_isDragging = true;
                // m_hitCollider = hit.collider;
                Cell cell = hit.collider.GetComponent<Cell>();
                if (m_board.ContainsCell(cell))
                {
                    MoveItemToBottom(cell);
                }
            }
        }

        // if (Input.GetMouseButtonUp(0))
        // {
        //     ResetRayCast();
        // }

        // if (Input.GetMouseButton(0) && m_isDragging)
        // {
        //     var hit = Physics2D.Raycast(m_cam.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        //     if (hit.collider != null)
        //     {
        //         if (m_hitCollider != null && m_hitCollider != hit.collider)
        //         {
        //             StopHints();

        //             Cell c1 = m_hitCollider.GetComponent<Cell>();
        //             Cell c2 = hit.collider.GetComponent<Cell>();
        //             if (AreItemsNeighbor(c1, c2))
        //             {
        //                 IsBusy = true;
        //                 SetSortingLayer(c1, c2);
        //                 m_board.Swap(c1, c2, () =>
        //                 {
        //                     FindMatchesAndCollapse(c1, c2);
        //                 });

        //                 ResetRayCast();
        //             }
        //         }
        //     }
        //     else
        //     {
        //         ResetRayCast();
        //     }
        // }
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

        m_bottomItems.Add(item);

        IsBusy = true;

        OnMoveEvent();

        int bottomIndex = m_bottomItems.Count - 1;

        item.View.DOMove(
            m_bottomCells[bottomIndex].position,
            0.2f
        ).OnComplete(() =>
        {
            ClearBottomTriples();

            IsBusy = false;

            CheckEndGame();
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
        else if (m_bottomItems.Count >=
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
    // private void ResetRayCast()
    // {
    //     m_isDragging = false;
    //     m_hitCollider = null;
    // }

    // private void FindMatchesAndCollapse(Cell cell1, Cell cell2)
    // {
    //     if (cell1.Item is BonusItem)
    //     {
    //         cell1.ExplodeItem();
    //         StartCoroutine(ShiftDownItemsCoroutine());
    //     }
    //     else if (cell2.Item is BonusItem)
    //     {
    //         cell2.ExplodeItem();
    //         StartCoroutine(ShiftDownItemsCoroutine());
    //     }
    //     else
    //     {
    //         List<Cell> cells1 = GetMatches(cell1);
    //         List<Cell> cells2 = GetMatches(cell2);

    //         List<Cell> matches = new List<Cell>();
    //         matches.AddRange(cells1);
    //         matches.AddRange(cells2);
    //         matches = matches.Distinct().ToList();

    //         if (matches.Count < m_gameSettings.MatchesMin)
    //         {
    //             m_board.Swap(cell1, cell2, () =>
    //             {
    //                 IsBusy = false;
    //             });
    //         }
    //         else
    //         {
    //             OnMoveEvent();

    //             CollapseMatches(matches, cell2);
    //         }
    //     }
    // }

    // private void FindMatchesAndCollapse()
    // {
    //     List<Cell> matches = m_board.FindFirstMatch();

    //     if (matches.Count > 0)
    //     {
    //         CollapseMatches(matches, null);
    //     }
    //     else
    //     {
    //         m_potentialMatch = m_board.GetPotentialMatches();
    //         if (m_potentialMatch.Count > 0)
    //         {
    //             IsBusy = false;

    //             m_timeAfterFill = 0f;
    //         }
    //         else
    //         {
    //             //StartCoroutine(RefillBoardCoroutine());
    //             StartCoroutine(ShuffleBoardCoroutine());
    //         }
    //     }
    // }

    // private List<Cell> GetMatches(Cell cell)
    // {
    //     List<Cell> listHor = m_board.GetHorizontalMatches(cell);
    //     if (listHor.Count < m_gameSettings.MatchesMin)
    //     {
    //         listHor.Clear();
    //     }

    //     List<Cell> listVert = m_board.GetVerticalMatches(cell);
    //     if (listVert.Count < m_gameSettings.MatchesMin)
    //     {
    //         listVert.Clear();
    //     }

    //     return listHor.Concat(listVert).Distinct().ToList();
    // }

    // private void CollapseMatches(List<Cell> matches, Cell cellEnd)
    // {
    //     for (int i = 0; i < matches.Count; i++)
    //     {
    //         matches[i].ExplodeItem();
    //     }

    //     if(matches.Count > m_gameSettings.MatchesMin)
    //     {
    //         m_board.ConvertNormalToBonus(matches, cellEnd);
    //     }

    //     StartCoroutine(ShiftDownItemsCoroutine());
    // }

    // private IEnumerator ShiftDownItemsCoroutine()
    // {
    //     m_board.ShiftDownItems();

    //     yield return new WaitForSeconds(0.2f);

    //     m_board.FillGapsWithNewItems();

    //     yield return new WaitForSeconds(0.2f);

    //     FindMatchesAndCollapse();
    // }

    // private IEnumerator RefillBoardCoroutine()
    // {
    //     m_board.ExplodeAllItems();

    //     yield return new WaitForSeconds(0.2f);

    //     m_board.Fill();

    //     yield return new WaitForSeconds(0.2f);

    //     FindMatchesAndCollapse();
    // }

    // private IEnumerator ShuffleBoardCoroutine()
    // {
    //     m_board.Shuffle();

    //     yield return new WaitForSeconds(0.3f);

    //     FindMatchesAndCollapse();
    // }


    // private void SetSortingLayer(Cell cell1, Cell cell2)
    // {
    //     if (cell1.Item != null) cell1.Item.SetSortingLayerHigher();
    //     if (cell2.Item != null) cell2.Item.SetSortingLayerLower();
    // }

    // private bool AreItemsNeighbor(Cell cell1, Cell cell2)
    // {
    //     return cell1.IsNeighbour(cell2);
    // }

    internal void Clear()
    {
        m_board.Clear();
        m_bottomItems.Clear();
        m_bottomCells.Clear();
    }

    // private void ShowHint()
    // {
    //     m_hintIsShown = true;
    //     foreach (var cell in m_potentialMatch)
    //     {
    //         cell.AnimateItemForHint();
    //     }
    // }

    // private void StopHints()
    // {
    //     m_hintIsShown = false;
    //     foreach (var cell in m_potentialMatch)
    //     {
    //         cell.StopHintAnimation();
    //     }

    //     m_potentialMatch.Clear();
    // }
}
