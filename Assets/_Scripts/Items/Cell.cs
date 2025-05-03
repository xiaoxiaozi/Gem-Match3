using _Scripts.Data_Classes;
using UnityEngine;

namespace Scripts
{
    /// <summary>
    /// Represents a cell in the match-3 game board.
    /// Each cell can contain a main board item, an underlay item, and an overlay item.
    /// Cells have different types (normal, spawner, blank, shifter) that affect gameplay mechanics.
    /// </summary>
    public class Cell
    {
        /// <summary>
        /// Constructor for creating a new cell.
        /// </summary>
        /// <param name="cellPosition">The 2D grid position of the cell</param>
        /// <param name="cellType">The type of cell (normal, spawner, blank, shifter)</param>
        /// <param name="boardItem">The main item contained in this cell (optional)</param>
        /// <param name="underLayBoardItem">The item underneath the main item (optional)</param>
        /// <param name="overLayBoardItem">The item overlaying the main item (optional)</param>
        public Cell(Vector2Int cellPosition,CellType cellType=CellType.NORMAL,IBoardItem boardItem=null,IBoardItem underLayBoardItem=null,IBoardItem overLayBoardItem=null)
        {
            CellPosition = cellPosition;
            SetItem(boardItem);
            _underLayBoardItem = underLayBoardItem;
            _overLayBoardItem= overLayBoardItem;
            CellType = cellType;
        }

        /// <summary>The type of this cell (normal, spawner, blank, shifter)</summary>
        public CellType CellType;

        /// <summary>Returns true if the cell contains a main board item</summary>
        public bool HasItem => BoardItem != null;

        /// <summary>Returns true if the cell contains an underlay item</summary>
        public bool HasUnderLayItem => UnderLayBoardItem != null;

        /// <summary>Returns true if the cell contains an overlay item</summary>
        public bool HasOverLayItem => OverLayBoardItem != null;

        /// <summary>The 2D grid position of this cell</summary>
        public Vector2Int CellPosition;

        /// <summary>Returns true if the cell is currently being filled with an item</summary>
        public bool IsGettingFilled => _isGettingFilled;

        /// <summary>Returns true if an item is currently being removed from this cell</summary>
        public bool IsGettingEmptied => _isGettingEmptied;

        /// <summary>Returns true if the cell is locked and cannot be interacted with</summary>
        public bool IsLocked => _isLocked;

        /// <summary>Gets the main board item in this cell</summary>
        public IBoardItem BoardItem=>_boardItem;

        /// <summary>Gets the underlay item in this cell (items that appear beneath the main item)</summary>
        public IBoardItem UnderLayBoardItem=>_underLayBoardItem;

        /// <summary>Gets the overlay item in this cell (items that appear on top of the main item)</summary>
        public IBoardItem OverLayBoardItem=>_overLayBoardItem;

        // Private fields
        /// <summary>The underlay board item reference</summary>
        private IBoardItem _underLayBoardItem;

        /// <summary>The main board item reference</summary>
        private IBoardItem _boardItem;

        /// <summary>The overlay board item reference</summary>
        private IBoardItem _overLayBoardItem;

        /// <summary>Flag indicating if the cell is locked</summary>
        private bool _isLocked;

        /// <summary>Flag indicating if the cell is currently being filled</summary>
        private bool _isGettingFilled ;

        /// <summary>Flag indicating if the cell is currently being emptied</summary>
        private bool _isGettingEmptied;

        /// <summary>Counter for tracking how many times the cell has been locked</summary>
        private int _lockCount=0;

        /// <summary>
        /// Sets the main board item for this cell and updates the item's position.
        /// </summary>
        /// <param name="boardItem">The board item to place in this cell</param>
        public void SetItem(IBoardItem boardItem)
        {
            _boardItem = boardItem;
            if (BoardItem != null)
            {
                BoardItem.Position = CellPosition;
            }
        }

        /// <summary>
        /// Sets the underlay board item for this cell and updates the item's position.
        /// Underlay items appear beneath the main item.
        /// </summary>
        /// <param name="boardItem">The underlay board item to place in this cell</param>
        public void SetUnderLayItem(IBoardItem boardItem)
        {
            _underLayBoardItem = boardItem;
            if (UnderLayBoardItem != null)
            {
                UnderLayBoardItem.Position = CellPosition;
            }
        }

        /// <summary>
        /// Sets the overlay board item for this cell and updates the item's position.
        /// Overlay items appear on top of the main item.
        /// </summary>
        /// <param name="boardItem">The overlay board item to place in this cell</param>
        public void SetOverLayItem(IBoardItem boardItem)
        {
            _overLayBoardItem = boardItem;
            if (OverLayBoardItem != null)
            {
                OverLayBoardItem.Position = CellPosition;
            }
        }

        /// <summary>
        /// Sets whether this cell is currently being filled with an item.
        /// Used during item movement and board updates.
        /// </summary>
        /// <param name="value">True if the cell is being filled, false otherwise</param>
        public void SetIsGettingFilled(bool value)
        {
            _isGettingFilled = value;
        }

        /// <summary>
        /// Sets whether an item is currently being removed from this cell.
        /// Used during item movement and board updates.
        /// </summary>
        /// <param name="value">True if an item is being removed, false otherwise</param>
        public void SetIsGettingEmptied(bool value)
        {
            _isGettingEmptied = value;
        }

        /// <summary>
        /// Sets whether this cell is locked and cannot be interacted with.
        /// Uses a counter system to track multiple locks/unlocks.
        /// The cell remains locked until all locks are removed.
        /// </summary>
        /// <param name="value">True to lock the cell, false to unlock it</param>
        public void SetIsLocked(bool value)
        {
            _lockCount=value?_lockCount+1:_lockCount-1;
            if (_lockCount <= 0)
            {
                _isLocked = false;

            }else
            {
                _isLocked = true;
            }
        }
    }
}
