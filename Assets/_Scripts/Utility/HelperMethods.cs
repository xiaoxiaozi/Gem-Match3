using System.Collections.Generic;
using Scripts;
using UnityEngine;

namespace _Scripts.Utility
{
    /// <summary>
    /// 提供棋盘和网格相关的实用方法
    /// </summary>
    public static class HelperMethods
    {
        /// <summary>
        /// 获取棋盘左边界X坐标
        /// </summary>
        /// <param name="array">单元格二维数组</param>
        /// <returns>左边界X坐标</returns>
        public static float GetBoardBoundaryLeftX(this Cell[,] array)
        {
            return LevelGrid.Instance.GetCellCenterWorld(array[0,0].CellPosition).x-LevelGrid.Grid.cellSize.x;
        }

        /// <summary>
        /// 获取棋盘右边界X坐标
        /// </summary>
        /// <param name="array">单元格二维数组</param>
        /// <returns>右边界X坐标</returns>
        public static float GetBoardBoundaryRightX(this Cell[,] array)
        {
            return LevelGrid.Instance.GetCellCenterWorld(array[array.GetLength(0)-1,0].CellPosition).x+LevelGrid.Grid.cellSize.x;
        }

        /// <summary>
        /// 获取棋盘上边界Y坐标
        /// </summary>
        /// <param name="array">单元格二维数组</param>
        /// <returns>上边界Y坐标</returns>
        public static float GetBoardBoundaryTopY(this Cell[,] array)
        {
            return LevelGrid.Instance.GetCellCenterWorld(array[0,array.GetLength(1)-1].CellPosition).y+LevelGrid.Grid.cellSize.y;
        }

        /// <summary>
        /// 获取棋盘下边界Y坐标
        /// </summary>
        /// <param name="array">单元格二维数组</param>
        /// <returns>下边界Y坐标</returns>
        public static float GetBoardBoundaryBottomY(this Cell[,] array)
        {
            return LevelGrid.Instance.GetCellCenterWorld(array[0,0].CellPosition).y-LevelGrid.Grid.cellSize.y;
        }

        /// <summary>
        /// 检查位置是否在棋盘边界内
        /// </summary>
        /// <param name="board">棋盘对象</param>
        /// <param name="pos">要检查的位置</param>
        /// <returns>是否在边界内</returns>
        public static bool IsInBoundaries(this Board board, Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < board.Width&& pos.y >= 0 && pos.y < board.Height;
        }

        /// <summary>
        /// 检查位置是否在棋盘边界内
        /// </summary>
        /// <param name="board">棋盘对象</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>是否在边界内</returns>
        public static bool IsInBoundaries(this Board board, int x,int y)
        {
            return x >= 0 && x < board.Width&& y >= 0 && y < board.Height;
        }

        /// <summary>
        /// 获取棋盘指定位置的物品
        /// </summary>
        /// <param name="board">棋盘对象</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>物品对象，如果没有则返回null</returns>
        public static IBoardItem GetItem(this Board board, int x,int y)
        {
            if(board.Cells[x,y].HasItem)
            {
                return board.Cells[x,y].BoardItem;
            }

            return null;
        }

        /// <summary>
        /// 获取棋盘指定位置的物品
        /// </summary>
        /// <param name="board">棋盘对象</param>
        /// <param name="position">位置坐标</param>
        /// <returns>物品对象，如果没有则返回null</returns>
        public static IBoardItem GetItem(this Board board ,Vector2Int position)
        {
            if(board.IsInBoundaries(position)&&board.Cells[position.x,position.y].HasItem)
            {
                return board.Cells[position.x,position.y].BoardItem;
            }

            return null;
        }

        /// <summary>
        /// 设置棋盘上所有物品的父物体
        /// </summary>
        /// <param name="board">棋盘对象</param>
        /// <param name="parent">要设置的父物体</param>
        public static void SetBoardItemsParent(this Board board, Transform parent)
        {
            foreach (var cell in board.Cells)
            {
                if (cell.HasItem)
                {
                    cell.BoardItem.Transform.SetParent(parent);
                }
                if(cell.HasOverLayItem)
                {
                    cell.OverLayBoardItem.Transform.SetParent(parent);
                }
                if(cell.HasUnderLayItem)
                {
                    cell.UnderLayBoardItem.Transform.SetParent(parent);
                }
            }
        }

        #region Directions
        /// <summary>
        /// 获取当前位置的四个方向(上、下、左、右)
        /// </summary>
        /// <param name="pos">当前位置</param>
        /// <returns>四个方向的坐标列表</returns>
        public static List<Vector2Int> GetFourDirections(this Vector2Int pos)
        {
            return new List<Vector2Int>()
            {
                new Vector2Int(pos.x, pos.y + 1), // Up
                new Vector2Int(pos.x, pos.y - 1), // Down
                new Vector2Int(pos.x + 1, pos.y), // Right
                new Vector2Int(pos.x - 1, pos.y)  // Left
            };
        }
        #endregion
    }
}
