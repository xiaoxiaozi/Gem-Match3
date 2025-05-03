using System;
using System.Collections.Generic;
using _Scripts.Core;
using _Scripts.Utility;
using DG.Tweening;
using Rimaethon.Scripts.Managers;
using Scripts;
using UnityEngine;

namespace _Scripts.Managers.Matching
{
    /// <summary>
    /// Handles the processing and execution of matches in the game.
    /// Responsible for managing match data, processing different match types,
    /// and triggering appropriate effects when matches occur.
    /// </summary>
    public class MatchHandler
    {
        // Reference to the game board
        private Board _board;
        // Sequence for animations
        private Sequence _sequence;
        // Collection of matches waiting to be processed
        private readonly HashSet<MatchData> _matchesToHandle= new HashSet<MatchData>();
        // Matches that will be processed in the current frame
        private List<MatchData> _matchesToHandleThisFrame= new List<MatchData>();
        // Directions used for checking adjacent cells during explosions
        private List<Vector2Int> _directions = new List<Vector2Int>();
        // Flag to indicate if the handler is disabled
        private bool _isDisabled;

        /// <summary>
        /// Initializes a new instance of the MatchHandler class.
        /// </summary>
        /// <param name="board">The game board to handle matches on.</param>
        public MatchHandler(Board board)
        {
            _board = board;
            // Subscribe to the AddMatchToHandle event to receive match data
            EventManager.Instance.AddHandler<MatchData>(GameEvents.AddMatchToHandle, AddMatchToHandle);
        }

        /// <summary>
        /// Cleans up resources and unsubscribes from events when the handler is disabled.
        /// </summary>
        public void OnDisable()
        {
            _board = null;
            _isDisabled = true;
            _matchesToHandle.Clear();
            _matchesToHandleThisFrame.Clear();
            _directions.Clear();
            GC.Collect();
            if (EventManager.Instance == null) return;
            EventManager.Instance.RemoveHandler<MatchData>(GameEvents.AddMatchToHandle, AddMatchToHandle);
        }

        /// <summary>
        /// Adds a match to the collection of matches to be handled.
        /// </summary>
        /// <param name="matchData">The match data to add.</param>
        private void AddMatchToHandle(MatchData matchData)
        {
            if (_isDisabled)
            {
                Debug.LogWarning("MatchHandler is disabled");
                return;
            }
            _matchesToHandle.Add(matchData);
        }

        /// <summary>
        /// Processes all pending matches in the current frame.
        /// </summary>
        /// <returns>True if there are still matches to handle, false otherwise.</returns>
        public bool HandleMatches()
        {
            if (_isDisabled)
            {
                Debug.LogWarning("MatchHandler is disabled");
                return false;
            }
            // Copy matches to a temporary list to avoid modification during iteration
            _matchesToHandleThisFrame.AddRange(_matchesToHandle);
            foreach (MatchData matchData in _matchesToHandleThisFrame)
            {
                CheckForMatchType(matchData);
            }
            _matchesToHandleThisFrame.Clear();
            return _matchesToHandle.Count > 0;
        }

        /// <summary>
        /// Determines how to process a match based on its type.
        /// </summary>
        /// <param name="data">The match data to process.</param>
        private void CheckForMatchType(MatchData data)
        {
            // If no match, remove from the collection
            if (data.MatchType == MatchType.None)
            {
                _matchesToHandle.Remove(data);
                return;
            }
            // For normal matches, handle immediately
            if (data.MatchType == MatchType.Normal)
            {
                HandleMatch(data.Matches);
                _matchesToHandle.Remove(data);
                return;
            }
            // For special matches (power-ups), animate items moving together
            LerpAllItemsToPosition(data);
        }

        /// <summary>
        /// Processes a normal match by exploding matched items.
        /// </summary>
        /// <param name="matchedItems">List of positions of matched items.</param>
        private void  HandleMatch(List<Vector2Int> matchedItems)
        {
            foreach (var pos in matchedItems)
            {
                // Skip invalid positions or items that can't be matched
                if(!_board.IsInBoundaries(pos)||!_board.Cells[pos.x,pos.y].HasItem||!_board.GetItem(pos).IsMatching)
                    continue;
                // Explode in all directions from this position
                ExplodeAllDirections(pos);
                Cell cell=_board.Cells[pos.x,pos.y];
                cell.BoardItem.OnMatch();
            }
            // Play match sound effect
            AudioManager.Instance.PlaySFX(SFXClips.MatchItemExplodeSound);
            // Get the first matched item for event broadcasting
            IBoardItem item = _board.GetItem(matchedItems[0]);
            // Broadcast match event with position, item ID, and match count
            EventManager.Instance.Broadcast<Vector3,int,int>(GameEvents.OnMainEventGoalMatch,item.Transform.position,item.ItemID,matchedItems.Count);
        }

        /// <summary>
        /// Animates items moving together for special matches (power-ups).
        /// </summary>
        /// <param name="matchData">The match data containing positions to animate.</param>
        private void LerpAllItemsToPosition(MatchData matchData)
        {
            // Initialize the animation if not already done
            if (!matchData.IsInitialized)
            {
                InitializeLerp(matchData);
            }
            
            bool isAllMerged = true;
            // Get the target position (first match position)
            Vector2Int mergeVector2IntPos = matchData.Matches[0];
            Vector3 mergePos = LevelGrid.Instance.GetCellCenterLocalVector2(mergeVector2IntPos);
            
            // Move all matched items toward the target position
            foreach (Vector2Int pos in matchData.Matches)
            {
                if (!_board.IsInBoundaries(pos) || !_board.Cells[pos.x,pos.y].HasItem)
                    continue;
                Cell cell = _board.Cells[pos.x,pos.y];
                cell.BoardItem.Transform.localPosition = Vector3.MoveTowards(cell.BoardItem.Transform.localPosition, mergePos, 0.09f);
                // Check if any item hasn't reached the target yet
                if (Vector3.Distance(cell.BoardItem.Transform.localPosition, mergePos) > 0.05f)
                {
                    isAllMerged = false;
                }
            }
            
            // If not all items have reached the target, wait for next frame
            if(!isAllMerged)
                return;
                
            // Once all items have reached the target, process the match
            foreach (Vector2Int match in matchData.Matches)
            {
                if (!_board.IsInBoundaries(match) || !_board.Cells[match.x,match.y].HasItem)
                    continue;
                ExplodeAllDirections(match);
                Cell cell = _board.Cells[match.x,match.y];
                cell.BoardItem.OnRemove();
            }

            // Play match sound effect
            AudioManager.Instance.PlaySFX(SFXClips.MatchSound);
            // Get the item at the merge position for event broadcasting
            IBoardItem item = _board.GetItem(mergeVector2IntPos);
            // Broadcast match event with position, item ID, and match count
            EventManager.Instance.Broadcast<Vector3,int,int>(GameEvents.OnMainEventGoalMatch,item.Transform.position,item.ItemID,matchData.Matches.Count);
            // Add a new power-up item at the merge position
            EventManager.Instance.Broadcast(GameEvents.AddItemToAddToBoard, mergeVector2IntPos, (int)matchData.MatchType);
            // Remove the processed match from the collection
            _matchesToHandle.Remove(matchData);
        }

        /// <summary>
        /// Initializes the animation for a special match.
        /// </summary>
        /// <param name="matchData">The match data to initialize.</param>
        private void InitializeLerp(MatchData matchData)
        {
            foreach (Vector2Int match in matchData.Matches)
            {
                if (!_board.IsInBoundaries(match) || !_board.Cells[match.x,match.y].HasItem)
                    continue;
                Cell cell = _board.Cells[match.x,match.y];
                // Mark items as matching and inactive
                cell.BoardItem.IsMatching = true;
                cell.BoardItem.IsActive= false;
            }
            matchData.IsInitialized = true;
        }

        /// <summary>
        /// Triggers explosions in all four cardinal directions from a position.
        /// </summary>
        /// <param name="pos">The center position to explode from.</param>
        private void ExplodeAllDirections(Vector2Int pos)
        {
           _directions = pos.GetFourDirections();
            for (int i = 0; i < 4; i++)
            {
                // Check if the adjacent item can be exploded
                if (_board.IsInBoundaries(_directions[i]) && _board.Cells[_directions[i].x,_directions[i].y].HasItem &&
                    !_board.GetItem(_directions[i]).IsExploding && !_board.GetItem(_directions[i]).IsMatching &&
                    _board.GetItem(_directions[i]).IsExplodeAbleByNearMatches)
                    _board.GetItem(_directions[i]).OnExplode();
            }
        }
    }
}
