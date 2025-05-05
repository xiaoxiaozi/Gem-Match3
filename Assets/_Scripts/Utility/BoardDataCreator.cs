using System;
using System.Collections.Generic;
using _Scripts.Data_Classes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _Scripts.Utility
{
    /// <summary>
    /// 游戏板数据创建器，用于创建和管理三消游戏的板数据
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))][ExecuteInEditMode]
    public class BoardDataCreator:MonoBehaviour
    {
        /// <summary>游戏板ID</summary>
        public int BoardID;
        
        /// <summary>物品数据库引用</summary>
        [SerializeField] private ItemDatabaseSO itemDatabase;
        
        /// <summary>普通物品（棋子与障碍）ID矩阵创建器</summary>
        [SerializeField] ItemIDMatrixCreator normalItemIDMatrixCreator;
        
        /// <summary>底层物品ID矩阵创建器</summary>
        [SerializeField] ItemIDMatrixCreator underlayItemIDMatrixCreator;
        
        /// <summary>上层物品ID矩阵创建器</summary>
        [SerializeField] ItemIDMatrixCreator overlayItemIDMatrixCreator;
        
        /// <summary>游戏板宽度(只读)</summary>
        [ShowInInspector][ReadOnly] private int _boardWidth;
        
        /// <summary>游戏板高度(只读)</summary>
        [ShowInInspector][ReadOnly] private int _boardHeight;
        
        /// <summary>精灵渲染器组件</summary>
        private SpriteRenderer _spriteRenderer;
        
        /// <summary>游戏板精灵保存数据</summary>
        private BoardSpriteSaveData _boardSpriteSaveData;
        
        /// <summary>游戏板数据</summary>
        private BoardData _board;
    
        /// <summary>
        /// 初始化方法，检查数据库并获取组件
        /// </summary>
        private void Awake()
        {
            if(itemDatabase==null)
                throw new Exception("Item Database is null");
            _spriteRenderer = GetComponent<SpriteRenderer>();
            GetBoardSpriteData();
            InitializeItemMatrices();
        }
    
        /// <summary>
        /// 从数据库获取游戏板精灵数据并更新显示
        /// </summary>
        [Button]
        public void GetBoardSpriteData()
        {
            _boardSpriteSaveData= itemDatabase.GetBoardSpriteData(BoardID);
            _spriteRenderer.sprite=_boardSpriteSaveData.Sprite;
            _boardWidth = _boardSpriteSaveData.Width;
            _boardHeight = _boardSpriteSaveData.Height;
            normalItemIDMatrixCreator.boardSpriteSaveData = _boardSpriteSaveData;
            underlayItemIDMatrixCreator.boardSpriteSaveData = _boardSpriteSaveData;
            overlayItemIDMatrixCreator.boardSpriteSaveData = _boardSpriteSaveData;
        }
    
        /// <summary>
        /// 重置所有瓦片地图
        /// </summary>
        [Button]
        public void ResetAllTilemaps()
        {
            normalItemIDMatrixCreator.ResetTilemap();
            underlayItemIDMatrixCreator.ResetTilemap();
            overlayItemIDMatrixCreator.ResetTilemap();
        }
    
        /// <summary>
        /// 初始化所有物品ID矩阵
        /// </summary>
        [Button]
        public void InitializeItemMatrices()
        {
            normalItemIDMatrixCreator.InitializeItemIDMatrix(_boardWidth, _boardHeight);
            underlayItemIDMatrixCreator.InitializeItemIDMatrix(_boardWidth, _boardHeight);
            overlayItemIDMatrixCreator.InitializeItemIDMatrix(_boardWidth, _boardHeight);
        }
    
        /// <summary>
        /// 创建游戏板数据对象
        /// </summary>
        /// <returns>包含所有层物品数据的BoardData对象</returns>
        public BoardData CreateBoardData()
        {
            int[,] normalItemIds = normalItemIDMatrixCreator.ItemIDMatrix;
            Dictionary<Vector2Int,int> underlayItemIds= new Dictionary<Vector2Int,int>();
            Dictionary<Vector2Int,int> overlayItemIds= new Dictionary<Vector2Int,int>();
            CreateDictionaryFromMatrix(overlayItemIDMatrixCreator.ItemIDMatrix,overlayItemIds);
            CreateDictionaryFromMatrix(underlayItemIDMatrixCreator.ItemIDMatrix,underlayItemIds);
            _board = new BoardData(BoardID,Vector3.zero, normalItemIds, underlayItemIds, overlayItemIds);
            return _board;
        }

        public void SetBoardData(BoardData bd, Dictionary<int, ItemTileDataSO> dictTileDatas)
        {
            _board = bd;
            _boardWidth = bd.NormalItemIds.GetLength(0);
            _boardHeight = bd.NormalItemIds.GetLength(1);
            InitializeItemMatrices();
            BoardID = bd.BoardSpriteID;
            normalItemIDMatrixCreator.ItemIDMatrix = bd.NormalItemIds;
            CreateMatrixFromDictionary(bd.UnderlayItemIds,underlayItemIDMatrixCreator.ItemIDMatrix);
            CreateMatrixFromDictionary(bd.OverlayItemIds,overlayItemIDMatrixCreator.ItemIDMatrix);
            GetBoardSpriteData();
            
            normalItemIDMatrixCreator.RefreshTilemapByItemIDMatrix(dictTileDatas);
            underlayItemIDMatrixCreator.RefreshTilemapByItemIDMatrix(dictTileDatas);
            overlayItemIDMatrixCreator.RefreshTilemapByItemIDMatrix(dictTileDatas);
        }
    
        /// <summary>
        /// 将矩阵数据转换为字典格式
        /// </summary>
        /// <param name="matrix">源矩阵数据</param>
        /// <param name="dictionary">目标字典</param>
        private void CreateDictionaryFromMatrix(int[,] matrix, Dictionary<Vector2Int,int> dictionary)
        {
            dictionary.Clear();
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (matrix[i, j] != -1)
                    {
                        dictionary.Add( new Vector2Int(i,j),matrix[i, j]);
                    }
                }
            }
        }

        private void CreateMatrixFromDictionary(Dictionary<Vector2Int, int> dictionary, int[,] matrix)
        {
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (dictionary.TryGetValue(new Vector2Int(i,j),out int value))
                    {
                        matrix[i, j] = value;
                    }
                    else
                    {
                        matrix[i, j] = -1;
                    }
                }
            }
        }
        
        [Button]
        public void Test()
        {
            normalItemIDMatrixCreator.Test();
        }
    }
}
