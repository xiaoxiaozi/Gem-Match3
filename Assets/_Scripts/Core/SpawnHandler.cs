/*
 * SpawnHandler.cs
 * 
 * This class is responsible for handling the spawning of items on the game board.
 * It manages two main types of spawning:
 * 1. Booster items - Special items that are added to specific positions on the board
 * 2. Regular filler items - Items that fill empty cells on the board
 * 
 * The class works closely with the Board class and uses an object pooling system
 * to efficiently manage game objects.
 */

using System.Collections.Generic;
using _Scripts.Utility;
using DG.Tweening;
using Rimaethon.Scripts.Managers;
using Scripts;
using UnityEngine;

namespace _Scripts.Core
{
    public class SpawnHandler
    {
        // Dictionary to track items that need to be added to the board at specific positions
        private Dictionary<Vector2Int,int> _itemsToAddToBoard = new Dictionary<Vector2Int,int> ();
        
        // Reference to the main board
        private readonly Board _board;
        
        // Board dimensions
        private readonly int _width;
        private readonly int _height;
        
        // Reference to the GameObject that represents the board in the scene
        private GameObject _boardInstance;
        
        // Array to track which columns have been modified and need updating
        private bool[] _dirtyColumns;
        
        // List of item IDs that can be spawned as filler items
        private readonly List<int> _spawnAbleFillerItemIds;
        
        // List of positions where items can be spawned
        private readonly List<Vector2Int> _spawnCells;

        /*
         * Constructor initializes the SpawnHandler with necessary references and data
         * 
         * Parameters:
         * - boardInstance: GameObject representing the board in the scene
         * - board: Reference to the Board class that manages the game grid
         * - dirtyColumns: Array tracking which columns need updating
         * - spawnAbleFillerItemIds: List of item IDs that can be spawned as fillers
         * - spawnCells: List of positions where items can be spawned
         */
        public SpawnHandler(GameObject boardInstance,Board board,bool[] dirtyColumns, List<int> spawnAbleFillerItemIds,List<Vector2Int> spawnCells)
        {
            _board = board;
            _width = _board.Width;
            _height = _board.Height;
            _boardInstance = boardInstance;
            _dirtyColumns = dirtyColumns;
            _spawnAbleFillerItemIds = spawnAbleFillerItemIds;
            _spawnCells = spawnCells;
            
            // Subscribe to the event for adding items to the board
            EventManager.Instance.AddHandler<Vector2Int,int>(GameEvents.AddItemToAddToBoard, AddBoosterToAddToTheBoard);
        }

        /*
         * Cleanup method to unsubscribe from events when the handler is disabled
         */
        public void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.RemoveHandler<Vector2Int,int>(GameEvents.AddItemToAddToBoard, AddBoosterToAddToTheBoard);
        }

        /*
         * Event handler method that adds a booster item to the list of items to be added to the board
         * 
         * Parameters:
         * - itemPos: Position on the board where the item should be added
         * - itemID: ID of the item to add
         */
        private void AddBoosterToAddToTheBoard(Vector2Int itemPos,int itemID)
        {
            // Try to add the item to the dictionary, return if it already exists
            if(!_itemsToAddToBoard.TryAdd(itemPos, itemID)) return;
        }

        /*
         * Handles the spawning of booster items that were queued to be added to the board
         * 
         * Returns:
         * - bool: True if any boosters were spawned, false otherwise
         */
        public bool HandleBoosterSpawn()
        {
            bool isThereBoosterToSpawn = false;
            
            // Process each booster item in the queue
            foreach (KeyValuePair<Vector2Int,int> itemData in _itemsToAddToBoard)
            {
                // Lock the cell temporarily while we modify it
                _board.Cells[itemData.Key.x,itemData.Key.y].SetIsLocked(true);
                
                // Get the world position for the item
                Vector3 pos = LevelGrid.Instance.GetCellCenterWorld(itemData.Key);
                
                // Get a booster item from the object pool
                IBoardItem boardItem = ObjectPool.Instance.GetBoosterItem(itemData.Value, pos, _board);
                
                // Set up the item's transform
                boardItem.Transform.parent = _boardInstance.transform;
                boardItem.Transform.position = pos;
                
                // If there's already an item in this cell, return it to the pool
                if (_board.Cells[itemData.Key.x,itemData.Key.y].HasItem)
                {
                    ObjectPool.Instance.ReturnItem(_board.GetItem(itemData.Key), _board.GetItem(itemData.Key).ItemID);
                }
                
                // Update cell state
                _board.Cells[itemData.Key.x,itemData.Key.y].SetIsGettingEmptied(false);
                _board.Cells[itemData.Key.x,itemData.Key.y].SetIsGettingFilled(false);
                _board.Cells[itemData.Key.x,itemData.Key.y].SetItem(boardItem);
                
                // Set the target position for the item
                _board.GetItem(itemData.Key).TargetToMove = itemData.Key;
                
                // Mark the column as dirty so it gets updated
                _dirtyColumns[itemData.Key.x] = true;
                
                // Unlock the cell
                _board.Cells[itemData.Key.x,itemData.Key.y].SetIsLocked(false);
                
                // Add a scale animation to highlight the new booster
                boardItem.Transform.DOScale(Vector3.one * 1.4f, 0.15f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetUpdate(UpdateType.Fixed);
                
                isThereBoosterToSpawn = true;
            }
            
            // Clear the queue after processing
            _itemsToAddToBoard.Clear();
            
            return isThereBoosterToSpawn;
        }

        /*
         * Handles the spawning of regular filler items in empty cells
         * 
         * Returns:
         * - bool: True if any cells were filled, false otherwise
         */
        public bool HandleFillSpawn()
        {
            bool isAnyCellEmpty = false;

            // Check each spawn cell
            foreach (var cell in _spawnCells)
            {
                // Skip if the cell already has an item or is locked
                if (_board.Cells[cell.x,cell.y].HasItem||_board.Cells[cell.x,cell.y].IsLocked) continue;
                
                // Choose a random item type from the available filler items
                int randomType = _spawnAbleFillerItemIds[Random.Range(0, _spawnAbleFillerItemIds.Count)];
                
                // Get an item from the object pool and place it above the target cell
                _board.Cells[cell.x,cell.y].SetItem(ObjectPool.Instance.GetItem(randomType, LevelGrid.Grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y+1, 0)),_board));
                
                // Set up the item's transform and movement properties
                _board.GetItem(cell.x, cell.y).Transform.parent = _boardInstance.transform;
                _board.GetItem(cell.x, cell.y).TargetToMove = new Vector2Int(cell.x, cell.y);
                _board.GetItem(cell.x, cell.y).IsMoving = true;
                
                // Mark the column as dirty so it gets updated
                _dirtyColumns[cell.x] = true;
                
                // Update cell state
                _board.Cells[cell.x,cell.y].SetIsGettingEmptied(false);
                _board.Cells[cell.x,cell.y].SetIsGettingFilled(true);
                
                isAnyCellEmpty = true;
            }
            
            return isAnyCellEmpty;
        }
    }
}
