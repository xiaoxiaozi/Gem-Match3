using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Scripts.Data_Classes;
using _Scripts.Utility;
using DefaultNamespace;
using Scripts;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;
using SerializationUtility = Sirenix.Serialization.SerializationUtility;

namespace _Scripts.Editor
{
    // 关卡创建器类，用于在编辑模式下创建关卡数据
    [ExecuteInEditMode]
    public class LevelCreator : MonoBehaviour
    {
        [FoldoutGroup("Save Settıngs")]
        [SerializeField] private string tilePath = "Assets/Art/Tilemaps/Level Tiles/"; // 瓦片资源保存路径
        [FoldoutGroup("Save Settıngs")]
        [SerializeField] private string levelDataPath = "Assets/Data/Levels/"; // 关卡数据保存路径
        [FoldoutGroup("Save Settıngs")]
        [SerializeField] private string extension = ".json"; // 文件扩展名
        [FoldoutGroup("Save Settıngs")]
        [SerializeField] DataFormat dataFormat=DataFormat.JSON; // 数据格式
        [FoldoutGroup("Save Settıngs")]
        [SerializeField] private bool ifExistsDoNotGenerate; // 如果文件已存在则不生成
        
        [FoldoutGroup("References")]
        private List<BoardDataCreator> _boardDataCreators; // 棋盘数据创建器列表
        [FoldoutGroup("References")]
        [SerializeField] private ItemDatabaseSO itemDatabase; // 物品数据库
        
        // 目标物品ID列表
        [ValueDropdown("GetNormalItemIds")]
        [SerializeField] private List<int> goalIds = new List<int>();
        
        [SerializeField] private List<int> goalCounts = new List<int>(); // 目标物品数量列表
        
        // 可生成的填充物品ID列表
        [ValueDropdown("GetNormalItemIds")]
        [SerializeField] private List<int> spawnAbleFillerItemIds = new List<int>();
        
        // 可生成的填充物品数量列表
        [Tooltip("Write -1 if unlimited, this is specifically for spawning certain amount goals ")]
        [SerializeField] private List<int> spawnAbleFillerItemCounts = new List<int>();
        
        [SerializeField] private int levelID; // 关卡ID
        [SerializeField] private int moveCount; // 移动次数限制
        [SerializeField] private int backgroundID; // 背景ID

        // 获取普通物品ID的下拉菜单选项
        private IEnumerable<ValueDropdownItem<int>> GetNormalItemIds()
        {
            foreach (var item in itemDatabase.NormalItems)
            {
                yield return new ValueDropdownItem<int>(item.Value.ItemPrefab.name, item.Key);
            }
        }

        [Button("加载关卡数据")]
        public void LoadLevelData()
        {
            string path = levelDataPath + levelID + extension;
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Error", $"File Not Exist", "OK");
                return;
            }

            LevelData ld = LoadFromJson(path);
            goalIds = ld.GoalSaveData.GoalIDs.ToList();
            goalCounts = ld.GoalSaveData.GoalAmounts.ToList();
            backgroundID = ld.backgroundID;
            moveCount = ld.MoveCount;
            spawnAbleFillerItemIds = ld.SpawnAbleFillerItemIds.ToList();
            spawnAbleFillerItemCounts = new List<int>(spawnAbleFillerItemIds.Count);
            for (int i = 0; i < spawnAbleFillerItemIds.Count; i++)
            {
                spawnAbleFillerItemCounts.Add(-1);
            }

            Dictionary<int, ItemTileDataSO> dictTileDatas = new();
            foreach (var item in itemDatabase.NormalItems)
            {
                string subPath = "NormalTiles/";
                if (item.Value.ItemPrefab.TryGetComponent(typeof(UnderlayBoardItem), out _))
                {
                    subPath = "UnderlayTiles/";
                }
                else if (item.Value.ItemPrefab.TryGetComponent(typeof(OverlayBoardItem), out _))
                {
                    subPath = "OverlayTiles/";
                }
                
                string itemPath = "Assets/Art/Tilemaps/Level Tiles/" + subPath + item.Value.ItemPrefab.name + ".asset";
                var itemTileDataSO = AssetDatabase.LoadAssetAtPath<ItemTileDataSO>(itemPath);
                if (itemTileDataSO == null)
                    continue;
                
                dictTileDatas.Add(item.Key, itemTileDataSO);
            }
            
            _boardDataCreators = gameObject.GetComponentsInChildren<BoardDataCreator>().ToList();
            for (int i = 0; i < _boardDataCreators.Count; ++i)
            {
                _boardDataCreators[i].SetBoardData(ld.Boards[i], dictTileDatas);
            }
        }

        // 创建关卡数据
        [Button("保存关卡数据")]
        public void CreateLevelData()
        {
            _boardDataCreators = gameObject.GetComponentsInChildren<BoardDataCreator>().ToList();
            
            // 检查填充物品ID和数量是否匹配
            if(spawnAbleFillerItemIds.Count!=spawnAbleFillerItemCounts.Count)
            {
                // 弹出错误提示
                EditorUtility.DisplayDialog("Error", "Spawnable Filler Item Ids and Counts are not equal", "OK");
                return;
            }
            
            // 检查目标ID和数量是否匹配
            if(goalIds.Count!=goalCounts.Count)
            {
                EditorUtility.DisplayDialog("Error", "Goal Ids and Counts are not equal", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Confirm", $"Are you sure you want to create level {levelID}?", "OK", "CANCEL"))
            {
                return;
            }
            
            // 检查文件是否已存在
            if (File.Exists(levelDataPath + levelID + extension) && !EditorUtility.DisplayDialog("Confirm", $"File Exist, Overwrite?", "OK", "CANCEL"))
            {
                return;
            }
            
            // 获取棋盘数据并创建关卡数据
            List<BoardData> boards = GetBoardData();
            LevelData levelData = new LevelData(boards, spawnAbleFillerItemIds, GetGoalData(boards), backgroundID, moveCount);
            string levelDataFinalPath = levelDataPath + levelID;
            SaveToJson(levelDataFinalPath, levelData);
        }

        // 获取目标数据
        private GoalSaveData GetGoalData(List<BoardData> boards)
        {
            int[] goalIDs = goalIds.ToArray();
            int[] goalCounts = this.goalCounts.ToArray();
            return new GoalSaveData(goalIDs, goalCounts);
        }

        // 清除棋盘
        [Button("清除棋子")]
        public void ClearBoards()
        {
            _boardDataCreators = gameObject.GetComponentsInChildren<BoardDataCreator>().ToList();
            foreach (var boardDataCreator in _boardDataCreators)
            {
                boardDataCreator.ResetAllTilemaps();
            }
        }

        // 获取棋盘中的目标数量
        [Button]
        public void GetNumberOfGoalsInBoard()
        {
            _boardDataCreators = gameObject.GetComponentsInChildren<BoardDataCreator>().ToList();
            goalCounts = new List<int>(goalIds.Count);
            for (int i = 0; i < goalIds.Count; i++)
            {
                goalCounts.Add(0);
            }
            
            foreach (var boardDataCreator in _boardDataCreators)
            {
                BoardData boardData = boardDataCreator.CreateBoardData();
                
                // 遍历棋盘上的所有单元格
                for (int i = 0; i < itemDatabase.Boards[boardData.BoardSpriteID].Width; i++)
                {
                    for (int j = 0; j < boardData.NormalItemIds.GetLength(1); j++)
                    {
                        // 检查普通物品
                        int itemId = boardData.NormalItemIds[i, j];
                        int index = goalIds.IndexOf(itemId);
                        if (index != -1)
                        {
                            goalCounts[index]++;
                        }
                        
                        // 检查底层物品
                        if(boardData.UnderlayItemIds.TryGetValue(new Vector2Int(i,j),out itemId))
                        {
                            index =  goalIds.IndexOf(itemId);
                            if (index != -1)
                            {
                                goalCounts[index]++;
                            }
                        }

                        // 检查覆盖层物品
                        if (boardData.OverlayItemIds.TryGetValue(new Vector2Int(i, j), out itemId))
                        {
                            index =  goalIds.IndexOf(itemId);
                            if (index != -1)
                            {
                                goalCounts[index]++;
                            }
                        }
                    }
                }
            }
        }

        // 获取所有棋盘数据
        private List<BoardData> GetBoardData()
        {
            List<BoardData> boards = new List<BoardData>();
            foreach (var boardDataCreator in _boardDataCreators)
            {
                boards.Add(boardDataCreator.CreateBoardData());
            }
            return boards;
        }

        private LevelData LoadFromJson(string path)
        {
            var bytes = File.ReadAllBytes(path);
            return SerializationUtility.DeserializeValue<LevelData>(bytes, dataFormat);
        }

        // 保存为JSON文件
        private void SaveToJson(string path, LevelData levelData)
        {
            var serializedData = SerializationUtility.SerializeValue(levelData, dataFormat);
            File.WriteAllBytes(path+extension, serializedData);
        }

        // 从物品数据库初始化瓦片脚本对象
        // 有问题，暂时屏蔽
        // [Button]
        public void InitializeTileScriptableObjectsFromItemDataBase()
        {
            if (itemDatabase == null)
            {
                Debug.LogError("Item Database is null");
                return;
            }
            
            // 为每个普通物品创建瓦片数据
            foreach (var itemData in itemDatabase.NormalItems.Values)
            {
                var itemTileDataSO = ScriptableObject.CreateInstance<ItemTileDataSO>();
                itemTileDataSO.gameObject = itemData.ItemPrefab;
                itemTileDataSO.sprite = itemData.ItemPrefab.GetComponent<SpriteRenderer>().sprite;

                string subPath = "NormalTiles/";
                if (itemData.ItemPrefab.TryGetComponent(typeof(UnderlayBoardItem), out _))
                {
                    subPath = "UnderlayTiles/";
                }else if (itemData.ItemPrefab.TryGetComponent(typeof(OverlayBoardItem), out _))
                {
                    subPath = "OverlayTiles/";
                }
                AssetDatabase.CreateAsset(itemTileDataSO, tilePath +subPath+ itemData.ItemPrefab.name + ".asset");
            }
        }
    }
}
