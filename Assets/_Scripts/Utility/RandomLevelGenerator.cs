using System;
using System.Collections.Generic;
using _Scripts.Data_Classes;
using Scripts;
using Sirenix.OdinInspector;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace _Scripts.Utility
{
    // 随机关卡生成器类，继承自MonoBehaviour
    public class RandomLevelGenerator:MonoBehaviour
    {
        [SerializeField] private ItemDatabaseSO itemDatabase; // 物品数据库
        [SerializeField] private Vector3 boardPosition; // 棋盘位置
        private int _boardID; // 棋盘ID
        private int boardSpriteID; // 棋盘精灵ID
        
        // 通过下拉菜单选择棋盘精灵
        [ValueDropdown("GetBoardSpriteID")]
        [SerializeField] private Sprite boardSprite;
        
        // 通过下拉菜单选择背景精灵
        [ValueDropdown("GetBackgroundSprite")]
        [SerializeField] private Sprite backgroundSprite;
        
        // 通过下拉菜单选择要生成的普通物品类型
        [ValueDropdown("GetNormalItemIds")]
        [SerializeField] private List<int> normalItemTypesToSpawn = new List<int>();
        
        [SerializeField] private int totalOverlayItemCount; // 覆盖层物品总数
        
        // 通过下拉菜单选择要生成的覆盖层物品类型
        [ValueDropdown("GetNormalItemIds")]
        [SerializeField] private List<int> overlayItemTypesToSpawn = new List<int>();
        
        [SerializeField] private int totalUnderlayItemCount; // 底层物品总数
        
        // 通过下拉菜单选择要生成的底层物品类型
        [ValueDropdown("GetNormalItemIds")]
        [SerializeField] private List<int> underlayItemTypesToSpawn = new List<int>();
        
        // 通过下拉菜单选择目标物品ID
        [Tooltip("The counts will be depending on random generation")]
        [ValueDropdown("GetNormalItemIds")]
        [SerializeField] private List<int> goalIds = new List<int>();
        
        public Dictionary<int,List<IBoardItem>> _goalPositions = new Dictionary<int, List<IBoardItem>>(); // 目标物品位置字典
        public Dictionary<int,int> _goalCounts= new Dictionary<int, int>(); // 目标物品数量字典
        private List<int> _spawnAbleFillerItemIds=new List<int>(){0,1,2,3,4}; // 可生成的填充物品ID列表

        // 获取普通物品ID的下拉菜单选项
        private IEnumerable<ValueDropdownItem<int>> GetNormalItemIds()
        {
            foreach (var item in itemDatabase.NormalItems)
            {
                yield return new ValueDropdownItem<int>(item.Value.ItemPrefab.name, item.Key);
            }
        }

        // 获取棋盘精灵ID的下拉菜单选项
        private IEnumerable<ValueDropdownItem<Sprite>> GetBoardSpriteID()
        {
            foreach (var item in itemDatabase.Boards)
            {
                _boardID = item.Key;
                yield return new ValueDropdownItem<Sprite>(item.Key.ToString(), item.Value.Sprite);
            }
        }

        // 获取背景精灵的下拉菜单选项
        private IEnumerable<ValueDropdownItem<Sprite>> GetBackgroundSprite()
        {
            foreach (var item in itemDatabase.Backgrounds)
            {
                yield return new ValueDropdownItem<Sprite>(item.Value.name, item.Value);
            }
        }

        // 生成随机棋盘
        [BurstCompile]
        public Board GenerateRandomBoard(SpriteRenderer backgroundSpriteRenderer,GameObject boardInstance)
        {
            Random random = new Random((uint)DateTime.Now.Ticks);
            BoardSpriteSaveData boardSpriteSaveData= itemDatabase.Boards[_boardID];
            
            // 创建生成棋盘的Job
            var job = new GenerateBoardJob
            {
                Width = boardSpriteSaveData.Width,
                Height = boardSpriteSaveData.Height,
                Board = new NativeArray<int>(boardSpriteSaveData.Width * boardSpriteSaveData.Height, Allocator.TempJob),
                Random = random,
                NormalItemTypes = normalItemTypesToSpawn.ToNativeArray(Allocator.TempJob),
            };
            
            job.Schedule().Complete();
            int[,] normalItemArray =Create2DArrayFromNativeArray(job.Board, boardSpriteSaveData.Width, boardSpriteSaveData.Height);
            
            // 生成底层和覆盖层物品
            Dictionary<Vector2Int,int> underlayItemIds= GenerateItemsAtRandomPositions(boardSpriteSaveData,underlayItemTypesToSpawn,totalUnderlayItemCount);
            Dictionary<Vector2Int,int> overlayItemIds= GenerateItemsAtRandomPositions(boardSpriteSaveData,overlayItemTypesToSpawn,totalOverlayItemCount);
            
            // 创建棋盘数据和棋盘对象
            BoardData boardData = new BoardData(_boardID,boardPosition,normalItemArray,underlayItemIds,overlayItemIds);
            Board board = new Board(boardSpriteSaveData,boardData,boardInstance,_spawnAbleFillerItemIds);
            
            // 初始化目标字典
            InitializeGoalDictionaries(board, goalIds, _goalPositions, _goalCounts);
            backgroundSpriteRenderer.sprite = backgroundSprite;
            
            // 释放NativeArray资源
            job.Board.Dispose();
            job.NormalItemTypes.Dispose();
            return board;
        }

        // 生成棋盘的Job结构体
        [BurstCompile]
        struct GenerateBoardJob : IJob
        {
            public int Height; // 棋盘高度
            public int Width; // 棋盘宽度
            public NativeArray<int> Board; // 棋盘数据
            public Random Random; // 随机数生成器
            public NativeArray<int> NormalItemTypes; // 普通物品类型
            
            public void Execute()
            {
                do
                {
                    GenerateBoardWithNoTriplets(Width,Height, Random,NormalItemTypes, ref Board);
                } while (!HasMatchableSwap(Board,Width,Height));
            }
        }

        // 生成没有三连的棋盘
        [BurstCompile]
        private static void GenerateBoardWithNoTriplets(int width, int height, Random random, NativeArray<int> itemTypes,ref NativeArray<int> board)
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int randomIndex = itemTypes[random.NextInt(0, itemTypes.Length)];
                board[y * width + x] = randomIndex;

                // 检查是否有三连或方块匹配，如果有则改变单元格类型
                while ((y >= 1 && x >= 1 && board[(y - 1) * width + x] == randomIndex && board[y * width + x - 1] == randomIndex &&
                        board[(y - 1) * width + x - 1] == randomIndex) ||
                       (x >= 2 && board[y * width + x - 1] == randomIndex && board[y * width + x - 2] == randomIndex) ||
                       (y >= 2 && board[(y - 1) * width + x] == randomIndex && board[(y - 2) * width + x] == randomIndex))
                {
                    randomIndex = itemTypes[random.NextInt(0, itemTypes.Length)];
                    board[y * width + x] = randomIndex;
                }
            }
        }

        // 检查是否有可匹配的交换
        [BurstCompile]
        private static bool HasMatchableSwap(NativeArray<int> board,int width, int height)
        {
            for (int y = 0; y < width; y++)
            for (int x = 0; x < height; x++)
            for (int i = 0; i < 4; i++)
            {
                // 交换逻辑，检查四个方向
                int dx = i == 0 ? 0 : i == 1 ? 1 : i == 2 ? 0 : -1;
                int dy = i == 0 ? 1 : i == 1 ? 0 : i == 2 ? -1 : 0;
                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                
                // 交换
                int temp = board[y * width + x];
                board[y * width + x] = board[ny * width + nx];
                board[ny * width + nx] = temp;

                // 检查是否有三连
                if (HasTriplet(board, x, y, width,height) || HasTriplet(board, nx, ny,width, height))
                {
                    // 交换回来
                    board[ny * width + nx] = board[y * width + x];
                    board[y * width + x] = temp;
                    return true;
                }

                // 交换回来
                board[ny * width + nx] = board[y * width + x];
                board[y * width + x] = temp;
            }

            return false;
        }

        // 检查指定位置是否有三连
        [BurstCompile]
        private static bool HasTriplet(NativeArray<int> board, int x, int y, int width, int height)
        {
            int value = board[y * width + x];
            return (x >= 2 && board[y * width + x - 1] == value && board[y * width + x - 2] == value) ||
                   (x < width - 2 && board[y * width + x + 1] == value && board[y * width + x + 2] == value) ||
                   (y >= 2 && board[(y - 1) * width + x] == value && board[(y - 2) * width + x] == value) ||
                   (y < height - 2 && board[(y + 1) * width + x] == value && board[(y + 2) * width + x] == value);
        }

        // 在随机位置生成物品
        private Dictionary<Vector2Int, int> GenerateItemsAtRandomPositions(BoardSpriteSaveData spriteSaveData,List<int> itemIDs,int maxItem)
        {
            Dictionary<Vector2Int, int> itemPositions = new Dictionary<Vector2Int, int>();
            int maxTries = 100; // 最大尝试次数

            while(maxItem>0)
            {
                int x = UnityEngine.Random.Range(0, spriteSaveData.Width);
                int y = UnityEngine.Random.Range(0, spriteSaveData.Height);
                Vector2Int pos = new Vector2Int(x, y);
                if ( itemPositions.ContainsKey(pos))
                {
                    maxTries--;
                    if (maxTries <= 0)
                    {
                        break;
                    }
                    continue;
                }

                itemPositions.Add(pos, itemIDs[UnityEngine.Random.Range(0, itemIDs.Count)]);
                maxItem--;
            }

            return itemPositions;
        }

        // 初始化目标字典
        public  void InitializeGoalDictionaries(Board board, List<int> goalIds, Dictionary<int, List<IBoardItem>> goalPositions, Dictionary<int, int> goalCounts)
        {
            foreach (var goalId in goalIds)
            {
                goalPositions.Add(goalId, new List<IBoardItem>());
                goalCounts.Add(goalId, 0);
            }
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Cells[x, y].HasItem && goalIds.Contains(board.Cells[x, y].BoardItem.ItemID))
                    {
                        goalPositions[board.Cells[x, y].BoardItem.ItemID].Add(board.Cells[x, y].BoardItem);
                        goalCounts[board.Cells[x, y].BoardItem.ItemID]++;
                    }
                    if(board.Cells[x, y].HasUnderLayItem && goalIds.Contains(board.Cells[x, y].UnderLayBoardItem.ItemID))
                    {
                        goalPositions[board.Cells[x, y].UnderLayBoardItem.ItemID].Add(board.Cells[x, y].UnderLayBoardItem);
                        goalCounts[board.Cells[x, y].UnderLayBoardItem.ItemID]++;
                    }
                    if(board.Cells[x, y].HasOverLayItem && goalIds.Contains(board.Cells[x, y].OverLayBoardItem.ItemID))
                    {
                        goalPositions[board.Cells[x, y].OverLayBoardItem.ItemID].Add(board.Cells[x, y].OverLayBoardItem);
                        goalCounts[board.Cells[x, y].OverLayBoardItem.ItemID]++;
                    }
                }
            }
        }

        // 从NativeArray创建二维数组
        private static int[,] Create2DArrayFromNativeArray(NativeArray<int> array, int width, int height)
        {
            int[,] result = new int[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    result[x, y] = array[y * width + x];
                }
            }
            return result;
        }
    }
}
