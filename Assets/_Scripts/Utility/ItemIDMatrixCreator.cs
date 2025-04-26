using _Scripts.Data_Classes;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Scripts.Utility
{
    /// <summary>
    /// 用于创建和管理游戏板上的物品ID矩阵的工具类
    /// </summary>
    public class ItemIDMatrixCreator:MonoBehaviour
    {
        /// <summary>
        /// 二维数组，存储每个网格位置对应的物品ID
        /// </summary>
        [ShowInInspector] [TableMatrix( DrawElementMethod = "DrawElement",RowHeight = 20,IsReadOnly = true,HideColumnIndices = true,HideRowIndices = true)]
        public int[,] ItemIDMatrix;
        
        /// <summary>
        /// 存储棋盘精灵数据的引用
        /// </summary>
        [HideInInspector]public BoardSpriteSaveData boardSpriteSaveData;
        
        /// <summary>
        /// 棋盘的宽度（只读）
        /// </summary>
        [ShowInInspector][ReadOnly]
        private int _boardWidth;
        
        /// <summary>
        /// 棋盘的高度（只读）
        /// </summary>
        [ShowInInspector][ReadOnly]
        private  int _boardHeight;
        
        /// <summary>
        /// 用于操作的Tilemap组件
        /// </summary>
        private Tilemap _tilemap;
    
        /// <summary>
        /// 重置Tilemap，清除所有瓦片
        /// </summary>
        [Button]
        public void ResetTilemap()
        {
            _tilemap = GetComponent<Tilemap>();
            _tilemap.ClearAllTiles();
        }
    
        /// <summary>
        /// 从Tilemap获取物品ID并填充到矩阵中
        /// </summary>
        [Button]
        public void GetItemIDFromTilemap()
        {
            _tilemap = GetComponent<Tilemap>();
            _tilemap.CompressBounds();
            InitializeItemIDMatrix(_boardWidth, _boardHeight);
            foreach (Vector3Int pos in _tilemap.cellBounds.allPositionsWithin)
            {
    
                TileBase tile = _tilemap.GetTile(pos);
                if(tile==null)
                    continue;
                var tileItem=tile as ItemTileDataSO;
                Debug.Log("Tile Item: "+pos);
                ItemIDMatrix[pos.x, pos.y] = tileItem.gameObject.GetComponent<BoardItemBase>().ItemID;
            }
        }
        
        /// <summary>
        /// 初始化物品ID矩阵
        /// </summary>
        /// <param name="width">矩阵宽度</param>
        /// <param name="height">矩阵高度</param>
        public void InitializeItemIDMatrix(int width, int height)
        {
            _boardWidth = width;
            _boardHeight = height;
            ItemIDMatrix = new int[_boardWidth, _boardHeight];
            for (int i = 0; i < _boardWidth; i++)
            {
                for (int j = 0; j < _boardHeight; j++)
                {
                    ItemIDMatrix[i, j] = -1;
                }
            }
        }
    
        /// <summary>
        /// 检查指定位置是否在有效范围内
        /// </summary>
        /// <param name="position">要检查的位置</param>
        /// <returns>如果在范围内且不是空白或移动格则返回true</returns>
        public bool IsInBounds(Vector3Int position)
        {
            if (position.x < 0 || position.y < 0 || position.x >= _boardWidth || position.y >= _boardHeight|| boardSpriteSaveData.CellTypeMatrix[position.x, _boardHeight - position.y - 1] == CellType.BLANK|| boardSpriteSaveData.CellTypeMatrix[position.x, _boardHeight - position.y - 1] == CellType.SHIFTER)
                return false;
            return true;
        }
        
        /// <summary>
        /// 绘制矩阵元素的GUI方法
        /// </summary>
        /// <param name="rect">绘制区域</param>
        /// <param name="x">x坐标</param>
        /// <param name="y">y坐标</param>
        /// <returns>固定返回1</returns>
        private int DrawElement(Rect rect, int x, int y)
        {
            GUI.Box(rect,ItemIDMatrix[x,_boardHeight-y-1].ToString());
            return 1;
    
        }
    }
}
