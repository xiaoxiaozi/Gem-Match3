using System;
using System.Collections.Generic;
using System.Linq;
using _Scripts.Data_Classes;
using _Scripts.Utility;
using Cysharp.Threading.Tasks;
using DefaultNamespace;
using DG.Tweening;
using Rimaethon.Scripts.Managers;
using Rimaethon.Scripts.Utility;
using Scripts;
using Sirenix.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

// 导入必要的命名空间，包括系统集合、LINQ、自定义数据类、工具类、异步任务库、DOTween动画库等

namespace _Scripts.Managers
{
    /// <summary>
    /// 关卡管理器类，负责初始化、管理和监控游戏关卡的状态
    /// 继承自Singleton模式，确保全局只有一个实例
    /// </summary>
    public class LevelManager:Singleton<LevelManager>
    {
        // 物品数据库，包含所有可用的游戏物品信息
        [SerializeField] private ItemDatabaseSO itemDatabase;
        // 游戏板预制体
        [SerializeField] private GameObject boardPrefab;
        // 背景预制体
        [SerializeField] private GameObject backgroundPrefab;
        // 是否生成随机关卡
        [SerializeField] private bool shouldGenerateRandomLevel;
        // 随机关卡生成器
        [SerializeField] private RandomLevelGenerator randomLevelGenerator;
        // 存储目标物品位置的字典，键为物品ID，值为物品列表
        private Dictionary<int,List<IBoardItem>> _goalPositions = new Dictionary<int, List<IBoardItem>>();
        // 存储目标物品数量的字典，键为物品ID，值为剩余数量
        private Dictionary<int,int> _goalCounts= new Dictionary<int, int>();
        // 游戏板列表，支持多板块游戏
        private readonly List<Board> _boards=new List<Board>();
        // 剩余移动次数
        private int _moveCount=20;
        // 关卡是否已设置
        private bool _isLevelSet;
        // 当前关卡数据
        private LevelData _levelData;
        // 被光球匹配的物品ID集合
        public readonly HashSet<int> ItemsGettingMatchedByLightBall = new HashSet<int>();
        // 板块伸缩动画的幅度
        private const float BoardStretchAmount = -1f;
        // 初始X坐标位置
        private float initialXPos;
        // 标记板块是否有待处理的操作
        public bool DoesBoardHasThingsToDo;
        // 关卡是否已完成
        private bool isLevelCompleted;
        // 标准高度
        private int normalHeight=10;
        // 额外高度的精灵遮罩Y轴偏移量
        private float _spriteMaskYOffsetForAdditionalHeight=0.5f;
        // 标准宽度
        private int normalWidth=8;
        // 额外宽度的板块X轴偏移量
        private float _boardXOffsetForAdditionalWidth=0.25f;

        /// <summary>
        /// 启用组件时注册事件处理程序
        /// </summary>
        private void OnEnable()
        {
            // 注册物品爆炸事件处理
            EventManager.Instance.AddHandler<Vector2Int,int>(GameEvents.OnItemExplosion, HandleItemExplosion);
            // 注册移动次数变化事件处理
            EventManager.Instance.AddHandler<int>(GameEvents.OnMoveCountChanged, HandleMoveCount);
        }
        
        /// <summary>
        /// 禁用组件时移除事件处理程序
        /// </summary>
        private void OnDisable()
        {
            if(EventManager.Instance==null)
                return;
            // 移除物品爆炸事件处理
            EventManager.Instance.RemoveHandler<Vector2Int,int>(GameEvents.OnItemExplosion, HandleItemExplosion);
            // 移除移动次数变化事件处理
            EventManager.Instance.RemoveHandler<int>(GameEvents.OnMoveCountChanged, HandleMoveCount);
        }

        /// <summary>
        /// 组件启动时初始化关卡
        /// </summary>
        private void Start()
        {
            // 从存档管理器获取当前关卡数据
            _levelData=SaveManager.Instance.GetCurrentLevelData();
            // 标记关卡已设置
            _isLevelSet = true;
            // 异步初始化关卡，使用Forget()方法避免等待任务完成
            InitializeLevel().Forget();
        }

        /// <summary>
        /// 实际上，为放置Cloche或用户添加的助推器设置特定逻辑会更好，但这不在本项目范围内
        /// </summary>

        /// <summary>
        /// 异步初始化关卡，设置背景、游戏板和目标
        /// </summary>
        /// <returns>异步任务</returns>
        private async UniTask InitializeLevel()
        {
            EventManager.Instance.Broadcast(GameEvents.OnPlayerInputLock);

            SpriteRenderer backgroundSpriteRenderer= Instantiate(backgroundPrefab, Vector3.zero,Quaternion.identity).GetComponent<SpriteRenderer>();
            if(shouldGenerateRandomLevel)
            {
                GameObject boardInstance=Instantiate(boardPrefab, transform.position, Quaternion.identity);
                boardInstance.transform.SetParent(transform);
                Board board = randomLevelGenerator.GenerateRandomBoard(backgroundSpriteRenderer, boardInstance);
                _boards.Add(board);
                _goalPositions = randomLevelGenerator._goalPositions;
                _goalCounts = randomLevelGenerator._goalCounts;
                boardInstance.GetComponent<BoardManager>().InitializeBoard(board);
            }
            else if(_isLevelSet)
            {
                HashSet<int> spawnAbleItems =_levelData.SpawnAbleFillerItemIds.ToHashSet();
                spawnAbleItems.AddRange(_levelData.GoalSaveData.GoalIDs.ToList());
                backgroundSpriteRenderer.sprite= itemDatabase.Backgrounds[_levelData.backgroundID];
                await ObjectPool.Instance.InitializeStacks(spawnAbleItems,25,15);
                foreach (var  boardData in _levelData.Boards)
                {
                    GameObject boardInstance=Instantiate(boardPrefab, transform.position, Quaternion.identity);

                    boardInstance.transform.SetParent(transform);
                    Board board= new Board(itemDatabase.GetBoardSpriteData(boardData.BoardSpriteID),boardData,boardInstance,_levelData.SpawnAbleFillerItemIds);

                    _boards.Add(board);
                    boardInstance.GetComponent<SpriteRenderer>().sprite=itemDatabase.GetBoardSpriteData(boardData.BoardSpriteID).Sprite;
                    BoardManager boardManager= boardInstance.GetComponent<BoardManager>();
                    boardManager.InitializeBoard(board);
                    if(board.Height>normalHeight)
                        boardManager._spriteMask.transform.localPosition=
                            new Vector3(boardManager._spriteMask.transform.localPosition.x,boardManager._spriteMask.transform.localPosition.y+_spriteMaskYOffsetForAdditionalHeight*(board.Height-normalHeight),boardManager._spriteMask.transform.localPosition.z);
                    if(board.Width>normalWidth)
                        transform.position=new Vector3(transform.position.x-(_boardXOffsetForAdditionalWidth*(board.Width-normalWidth)),transform.position.y,transform.position.z);

                    randomLevelGenerator.InitializeGoalDictionaries(board,_levelData.GoalSaveData.GoalIDs.ToList(),_goalPositions,_goalCounts);
                    for(int i=0;i<_levelData.GoalSaveData.GoalAmounts.Length;i++)
                    {
                        _goalCounts[_levelData.GoalSaveData.GoalIDs[i]]=_levelData.GoalSaveData.GoalAmounts[i];
                    }
                }
                _moveCount = _levelData.MoveCount;
            }
            Vector3 boardInitialOffset= new Vector3(3f, transform.position.y, transform.position.z);

            initialXPos = transform.position.x;
            transform.position=boardInitialOffset;
            await InGameUIManager.Instance.HandleGoalAndPowerUpUI(_goalCounts,_moveCount);
            await HandleBoardAnimation();
        }

        /// <summary>
        /// 处理游戏板的入场动画和助推器放置
        /// </summary>
        /// <returns>异步任务</returns>
        private async UniTask HandleBoardAnimation()
        {
            await UniTask.Delay(200);
            await transform.DOMoveX(initialXPos + BoardStretchAmount, 0.4f).SetUpdate(UpdateType.Fixed).SetEase(Ease.InOutSine).ToUniTask();
            await transform.DOMoveX(initialXPos, 0.2f).SetUpdate(UpdateType.Fixed).SetEase(Ease.InOutSine).ToUniTask();
            await UniTask.Delay(200);
            List<int> boostersUsedThisLevel=SceneController.Instance.GetBoostersUsedThisLevel();
            if (boostersUsedThisLevel != null)
            {
                HashSet<Vector2Int> spawnablePositions = GetRandomSpawnablePos(boostersUsedThisLevel.Count);

                foreach (var boosterID in boostersUsedThisLevel )
                {
                    Vector2Int spawnPos = spawnablePositions.First();
                    EventManager.Instance.Broadcast(GameEvents.OnItemRemoval, spawnPos);
                    EventManager.Instance.Broadcast<Vector2Int,int>(GameEvents.AddItemToAddToBoard, spawnPos, boosterID);
                    spawnablePositions.Remove(spawnPos);
                    await UniTask.Delay(200);
                }
                boostersUsedThisLevel.Clear();
            }
            EventManager.Instance.Broadcast(GameEvents.OnPlayerInputUnlock);
        }

        /// <summary>
        /// 处理移动次数变化，当次数用尽时锁定游戏板
        /// </summary>
        /// <param name="valueToAdd">要添加的移动次数值</param>
        private void HandleMoveCount(int valueToAdd)
        {
            _moveCount+=valueToAdd;
            if (_moveCount > 0) return;
            EventManager.Instance.Broadcast(GameEvents.OnPlayerInputLock);
            EventManager.Instance.Broadcast(GameEvents.OnBoardLock);
            WaitForBoardToFinish().Forget();
        }

        /// <summary>
        /// 等待游戏板完成所有操作，然后检查关卡是否完成
        /// </summary>
        /// <returns>异步任务</returns>
        private async UniTask WaitForBoardToFinish()
        {
            await UniTask.Delay(300);
            while (DoesBoardHasThingsToDo)
            {
                await UniTask.Delay(300);
            }
            await UniTask.Delay(400);

            if (CheckForLevelCompletion())
            {
                EventManager.Instance.Broadcast(GameEvents.OnPlayerInputUnlock);
                return;
            }
            AudioManager.Instance.PlaySFX(SFXClips.LevelLoseSound);
            EventManager.Instance.Broadcast(GameEvents.OnNoMovesLeft);
            EventManager.Instance.Broadcast(GameEvents.OnPlayerInputUnlock);
        }

        /// <summary>
        /// 检查指定ID的目标是否已达成
        /// </summary>
        /// <param name="itemID">物品ID</param>
        /// <returns>如果目标已达成返回true，否则返回false</returns>
        public bool IsGoalReached(int itemID)
        {
            return _goalCounts[itemID] <= 0;
        }

        /// <summary>
        /// 处理物品爆炸事件，更新目标计数并检查关卡完成情况
        /// </summary>
        /// <param name="pos">爆炸位置</param>
        /// <param name="itemID">爆炸物品ID</param>
        private void HandleItemExplosion(Vector2Int pos,int itemID)
        {
            if (_goalCounts.ContainsKey(itemID))
            {
                IBoardItem boardItem;
                if (_boards[0].Cells[pos.x, pos.y].HasItem && _boards[0].Cells[pos.x, pos.y].BoardItem.ItemID == itemID)
                {
                    boardItem = _boards[0].Cells[pos.x, pos.y].BoardItem;
                }else if (_boards[0].Cells[pos.x, pos.y].HasUnderLayItem && _boards[0].Cells[pos.x, pos.y].UnderLayBoardItem.ItemID == itemID)
                {
                    boardItem = _boards[0].Cells[pos.x, pos.y].UnderLayBoardItem;
                }
                else
                {
                    return;
                }
                if (_goalCounts[itemID] > 0)
                {
                    _goalCounts[itemID]--;
                    if (!boardItem.IsGeneratorItem)
                    {
                        _goalPositions[boardItem.ItemID].Remove(boardItem);
                    }
                    EventManager.Instance.Broadcast<int,int>(GameEvents.OnGoalUIUpdate, boardItem.ItemID, _goalCounts[boardItem.ItemID]);
                    if (_goalCounts[boardItem.ItemID] == 0)
                    {
                        if (boardItem.IsGeneratorItem)
                        {
                            _goalPositions[boardItem.ItemID].Clear();
                        }
                        CheckForLevelCompletion();

                    }

                }
            }

        }

        /// <summary>
        /// 检查关卡是否已完成（所有目标都已达成）
        /// </summary>
        /// <returns>如果关卡已完成返回true，否则返回false</returns>
        private bool CheckForLevelCompletion()
        {
            if(isLevelCompleted)
                return true;
            if (_goalCounts.Values.All(count => count == 0))
            {
                isLevelCompleted = true;
                HandleCompletion().Forget();
                return true;
            }

            return false;

        }

        /// <summary>
        /// 处理关卡完成后的逻辑，等待游戏板完成所有操作后触发关卡完成事件
        /// </summary>
        /// <returns>异步任务</returns>
        private async UniTask HandleCompletion()
        {
            EventManager.Instance.Broadcast(GameEvents.OnPlayerInputLock);
            EventManager.Instance.Broadcast(GameEvents.OnBoardLock);
            await UniTask.Delay( 500);
            while (DoesBoardHasThingsToDo)
            {
                await UniTask.Delay( 200);
            }
            await UniTask.Delay( 500);
            EventManager.Instance.Broadcast(GameEvents.OnLevelCompleted);
            EventManager.Instance.Broadcast(GameEvents.OnPlayerInputUnlock);
        }

        /// <summary>
        /// 为了适应多板块关卡中的能力提升使用，这些方法放在这里而不是BoardManager中更合适
        /// </summary>

        /// <summary>
        /// 检查指定位置是否在任何游戏板的有效范围内
        /// </summary>
        /// <param name="pos">要检查的位置</param>
        /// <returns>如果位置有效返回true，否则返回false</returns>
        public bool IsValidPosition(Vector2Int pos)
        {
            foreach (var board in _boards)
            {
                if (board.IsInBoundaries(pos))
                {

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取包含指定位置的游戏板
        /// </summary>
        /// <param name="pos">要查找的位置</param>
        /// <returns>包含该位置的游戏板，如果没有找到则返回null</returns>
        public Board GetBoard(Vector2Int pos)
        {
            foreach (var board in _boards)
            {
                if (board.IsInBoundaries(pos))
                {
                    return board;
                }
            }

            return null;
        }

        /// <summary>
        /// 检查锤子道具击中位置的效果
        /// </summary>
        /// <param name="pos">锤子击中的位置</param>
        public void CheckHammerHit(Vector2Int pos)
        {
            foreach (var board in _boards)
            {
                if (!board.IsInBoundaries(pos)) continue;
                if (board.Cells[pos.x, pos.y].HasItem)
                {

                    IBoardItem item = board.GetItem(pos);
                    item.OnExplode();
                }
            }
        }

        /// <summary>
        /// 参考视频 https://youtu.be/iSaTx0T9GFw?t=3697 中可以看到，有底层物品的单元格被视为有效位置，所以这里也采用相同的逻辑
        /// </summary>

        /// <summary>
        /// 获取指定数量的随机可生成位置
        /// </summary>
        /// <param name="numberOfPositions">需要的位置数量</param>
        /// <returns>可生成位置的集合</returns>
        public HashSet<Vector2Int> GetRandomSpawnablePos(int numberOfPositions)
        {
            int maxTries = 30 * numberOfPositions; // Increase max tries based on number of positions needed
            HashSet<Vector2Int> spawnablePositions = new HashSet<Vector2Int>(); // Use HashSet to ensure uniqueness

            foreach (var board in _boards)
            {
                while (maxTries > 0 && spawnablePositions.Count < numberOfPositions)
                {
                    maxTries--;
                    Cell cell = board.Cells[Random.Range(0, board.Width), Random.Range(0, board.Height)];
                    if (cell.HasItem && cell.BoardItem.IsShuffleAble && !cell.HasOverLayItem&&!cell.BoardItem.IsMoving && !cell.BoardItem.IsBooster&&
                        !cell.BoardItem.IsExploding && !cell.BoardItem.IsMoving &&
                        !cell.IsLocked && !cell.BoardItem.IsMatching&&cell.BoardItem.IsActive)
                    {
                        spawnablePositions.Add(cell.CellPosition); // Add position to HashSet
                    }
                }
            }
            if (spawnablePositions.Count < numberOfPositions)
            {
                Debug.LogWarning("Could not find enough unique spawnable positions.");
            }
            return spawnablePositions;
        }

        /// <summary>
        /// 实际上，这部分应该有自己的类，包含为TNT和火箭寻找合适目标区域的专门方法，例如为垂直火箭寻找一列中最多目标的方法
        /// </summary>

        /// <summary>
        /// 这样玩家就不会感觉导弹做出了糟糕和不可预测的决定
        /// </summary>

        /// <summary>
        /// 获取随机目标位置，优先选择目标物品的位置
        /// </summary>
        /// <returns>随机目标位置，如果没有找到则返回(-1,-1)</returns>
        public Vector2Int GetRandomGoalPos()
        {
            foreach (var goalList in  _goalPositions.Values)
            {
               if (goalList.Count > 0)
               {
                   Vector2Int goalPos = goalList[UnityEngine.Random.Range(0, goalList.Count)].Position;
                   return goalPos;
               }
            }
            foreach (var board in _boards)
            {
                foreach (var cell in board.Cells)
                {
                    if(cell.HasItem)
                        return cell.CellPosition;
                }
            }
            return new Vector2Int(-1, -1);
        }
    }
}
