using System.Collections.Generic;
using _Scripts.Core;
using _Scripts.Utility;
using Rimaethon.Scripts.Managers;
using Scripts;
using UnityEngine;

namespace _Scripts.Managers.Matching
{
    //First version I made  was  checking shapes but there are some shapes that can happen at runtime and this class is not enough to handle all of them.
    //Since items are dropping randomly there can be some instances such as + shape or 5+ same items in a row or column
    //Such as : https://youtu.be/KjkjjvClTGU?t=230   https://youtu.be/KjkjjvClTGU?t=222

    // Also sometimes match logic doesn't work as I assumed it would be. such as : https://youtu.be/KjkjjvClTGU?t=280  Which should be a TNT but it merges to a Rocket instead.

    // Max Possible Matches is 14(3.2*10^-9 probability, at least it is more probable than someone looking at this code) and in order to solve allocation i need to somehow manage to create some kind of cache
    //   0 0 0 1 0 0 0 0
    //   0 0 0 1 0 0 0 0
    //   0 0 0 1 0 0 0 0
    //   0 0 0 1 0 0 0 0
    //   0 1 1 1 1 1 0 0
    //   0 0 0 1 0 0 0 0
    //   0 0 0 1 0 0 0 0
    //   0 0 0 1 0 0 0 0
    //   0 0 0 1 0 0 0 0
    //   0 0 0 1 0 0 0 0

    /// <summary>
    /// Represents a match result with position information.
    /// </summary>
    public struct Match
    {
        /// <summary>
        /// Indicates whether a match was found at this position.
        /// </summary>
        public bool IsMatch;
        
        /// <summary>
        /// The grid position of the match.
        /// </summary>
        public Vector2Int Pos;
    }

    /// <summary>
    /// Responsible for checking and identifying matches on the game board.
    /// This class handles the logic for detecting different match patterns and determining match types.
    /// </summary>
    public class MatchChecker
    {
        // Reference to the game board
        private readonly Board _board;
        
        // Arrays to store match information in different directions
        private readonly Match[] _horizontalExtensions;  // Stores horizontal extensions of vertical matches
        private readonly Match[] _horizontalMatches;     // Stores horizontal matches from the origin point
        private readonly Match[] _verticalExtensions;    // Stores vertical extensions of horizontal matches
        private readonly Match[] _verticalMatches;       // Stores vertical matches from the origin point
        
        // List to store all matched positions
        List<Vector2Int> matches = new List<Vector2Int>();
        
        // ID of the item being checked for matches
        private int _itemID;
        
        // Queue of items that need to be checked for matches
        private Queue<Vector2Int> _itemsToCheckForMatches = new Queue<Vector2Int>();

        /// <summary>
        /// Initializes a new instance of the MatchChecker class.
        /// </summary>
        /// <param name="board">The game board to check matches on.</param>
        public MatchChecker(Board board)
        {
            _board = board;
            // Initialize arrays to store match information
            _verticalMatches = new Match[10];
            _horizontalMatches = new Match[10];
            _verticalExtensions = new Match[10];
            _horizontalExtensions = new Match[10];
            
            // Subscribe to the item movement end event to check for matches after items move
            EventManager.Instance.AddHandler<Vector2Int>(GameEvents.OnItemMovementEnd, AddItemToCheckForMatches);
        }

        /// <summary>
        /// Cleans up event subscriptions when the object is disabled.
        /// </summary>
        public void OnDisable()
        {
            if(EventManager.Instance==null)
                return;
            EventManager.Instance.RemoveHandler<Vector2Int>(GameEvents.OnItemMovementEnd, AddItemToCheckForMatches);
        }

        /// <summary>
        /// Adds an item position to the queue of items to check for matches.
        /// </summary>
        /// <param name="itemPos">The position of the item to check.</param>
        private void AddItemToCheckForMatches(Vector2Int itemPos)
        {
            // Avoid adding duplicate positions to the queue
            if(_itemsToCheckForMatches.Contains(itemPos))
                return;
            _itemsToCheckForMatches.Enqueue(itemPos);
        }

        /// <summary>
        /// Checks all items in the queue for matches.
        /// </summary>
        /// <returns>True if any matches were found, false otherwise.</returns>
        public bool CheckForMatches()
        {
            int count = _itemsToCheckForMatches.Count;
            bool hasMatch = false;
            
            // Process all items in the queue
            while (count > 0)
            {
                count--;
                Vector2Int pos = _itemsToCheckForMatches.Dequeue();

                // Check if the current position has a match
                if (CheckMatch(pos))
                {
                    hasMatch = true;
                }
            }
            return hasMatch;
        }

        /// <summary>
        /// Checks if there is a match at the specified position.
        /// </summary>
        /// <param name="pos">The position to check for matches.</param>
        /// <returns>True if a match was found, false otherwise.</returns>
        public bool CheckMatch(Vector2Int pos)
        {
            // Skip checking if the item is not valid for matching
            if (!_board.Cells[pos.x,pos.y].HasItem || !_board.GetItem(pos).IsMatchable || _board.GetItem(pos).IsMoving ||
                _board.GetItem(pos).IsExploding || _board.GetItem(pos).IsMatching)
                return false;
                
            // Create a new match data object to store match information
            MatchData matchedItems = new MatchData();
            var item = _board.GetItem(pos);
            _itemID = item.ItemID;
            
            // Check for matches in horizontal and vertical directions
            var horizontalMatchCount = CheckMatchesInDirection(_horizontalMatches, pos, new Vector2Int(1, 0));
            var verticalMatchCount = CheckMatchesInDirection(_verticalMatches, pos, new Vector2Int(0, 1));
            
            // Check for extensions of matches (for T and L shapes)
            var verticalExtensionCount = GetExtension(_horizontalMatches, _verticalExtensions, new Vector2Int(0, 1));
            var horizontalExtensionCount = GetExtension(_verticalMatches, _horizontalExtensions, new Vector2Int(1, 0));
            
            // Mark the current item as matching
            _board.Cells[pos.x,pos.y].BoardItem.IsMatching = true;
            matches.Add(pos);
            
            // Determine the type of match based on the match counts
            var matchType = CheckForMatchType(horizontalMatchCount, verticalMatchCount, verticalExtensionCount,
                horizontalExtensionCount);
                
            if (matchType != MatchType.None)
            {
                // If a match was found, create match data and broadcast it
                matchedItems.MatchType = matchType;
                matchedItems.matchID = _itemID;
                matchedItems.Matches.AddRange(matches);
                EventManager.Instance.Broadcast(GameEvents.AddMatchToHandle, matchedItems);
            }
            else
            {
                // If no match was found, unmark the item
                _board.Cells[pos.x,pos.y].BoardItem.IsMatching = false;
            }
            
            // Clear all match arrays for the next check
            ClearAllMatchesAndAddThemToArray(_horizontalMatches, false);
            ClearAllMatchesAndAddThemToArray(_verticalMatches, false);
            ClearAllMatchesAndAddThemToArray(_horizontalExtensions, false);
            ClearAllMatchesAndAddThemToArray(_verticalExtensions, false);
            matches.Clear();
            
            return matchType != MatchType.None;
        }

        /// <summary>
        /// Checks for matches in a specific direction from a position.
        /// </summary>
        /// <param name="matchArray">Array to store match information.</param>
        /// <param name="pos">Starting position.</param>
        /// <param name="direction">Direction to check (e.g., horizontal or vertical).</param>
        /// <returns>Number of matches found.</returns>
        private int CheckMatchesInDirection(Match[] matchArray, Vector2Int pos, Vector2Int direction)
        {
            var arrayIndex = 0;
            // Check in positive direction
            arrayIndex += CheckMatchesInDirectionHelper(matchArray, pos, direction, 1, arrayIndex);
            // Check in negative direction
            arrayIndex = CheckMatchesInDirectionHelper(matchArray, pos, direction, -1, arrayIndex);
            return arrayIndex;
        }

        /// <summary>
        /// Helper method to check for matches in a specific direction and multiplier.
        /// </summary>
        /// <param name="matchArray">Array to store match information.</param>
        /// <param name="pos">Starting position.</param>
        /// <param name="direction">Direction to check.</param>
        /// <param name="start">Direction multiplier (1 for positive, -1 for negative).</param>
        /// <param name="arrayIndex">Current index in the match array.</param>
        /// <returns>Number of matches found in this direction.</returns>
        private int CheckMatchesInDirectionHelper(Match[] matchArray, Vector2Int pos, Vector2Int direction, int start, int arrayIndex)
        {
            var index = start;
            // Continue checking in the direction until a non-matching item is found or board boundary is reached
            while (_board.IsInBoundaries(pos + direction * index) && _board.GetItem(pos + direction * index) != null)
            {
                Vector2Int cellPos = pos + direction * index;
                var cell = _board.Cells[cellPos.x,cellPos.y];
                
                // Check if the item matches and is valid for matching
                if (_itemID == cell.BoardItem.ItemID && !cell.BoardItem.IsMoving && !cell.BoardItem.IsExploding &&
                    !cell.IsGettingFilled && !cell.IsGettingEmptied && !cell.BoardItem.IsMatching && cell.BoardItem.IsMatchable && !cell.IsLocked)
                {
                    // Store match information
                    matchArray[arrayIndex].Pos = pos + direction * index;
                    matchArray[arrayIndex].IsMatch = true;
                    arrayIndex++;
                }
                else
                {
                    break;
                }
                index += start;
            }
            return arrayIndex;
        }
        
        // Temporary array used for checking extensions
        Match[] arrayToCheckCopy = new Match[10];

        /// <summary>
        /// Gets extensions of matches in a perpendicular direction.
        /// Used to detect T and L shaped matches.
        /// </summary>
        /// <param name="arrayToCheck">Array of matches to check for extensions.</param>
        /// <param name="matchArray">Array to store extension matches.</param>
        /// <param name="direction">Direction to check for extensions.</param>
        /// <returns>Maximum number of extension matches found.</returns>
        private int GetExtension(Match[] arrayToCheck, Match[] matchArray, Vector2Int direction)
        {
            int maxMatchCount = 0;
            // Check each match for extensions in the perpendicular direction
            foreach (var item in arrayToCheck)
            {
                if (!item.IsMatch)
                {
                    break;
                }
                // Check for matches from this position in the perpendicular direction
                var extensionMatchCount = CheckMatchesInDirection(arrayToCheckCopy, item.Pos, direction);
                if (extensionMatchCount > maxMatchCount)
                {
                    // If more matches are found, update the match array and count
                    arrayToCheckCopy.CopyTo(matchArray, 0);
                    maxMatchCount = extensionMatchCount;
                }
                arrayToCheckCopy = new Match[10];
            }
            return maxMatchCount;
        }

        /// <summary>
        /// Clears all matches in an array and optionally adds them to the matches list.
        /// </summary>
        /// <param name="matchArray">Array of matches to clear.</param>
        /// <param name="addToArray">Whether to add the matches to the matches list.</param>
        private void ClearAllMatchesAndAddThemToArray(Match[] matchArray, bool addToArray = true)
        {
            for (var i = 0; i < matchArray.Length; i++)
            {
                if (matchArray[i].IsMatch && addToArray)
                {
                    // Add the match to the matches list
                    matches.Add(matchArray[i].Pos);
                    _board.Cells[matchArray[i].Pos.x,matchArray[i].Pos.y].BoardItem.IsMatching = true;
                }

                // Reset the match flag
                matchArray[i].IsMatch = false;
            }
        }

        /// <summary>
        /// Determines the type of match based on the match counts in different directions.
        /// </summary>
        /// <param name="horizontalMatchCount">Number of horizontal matches.</param>
        /// <param name="verticalMatchCount">Number of vertical matches.</param>
        /// <param name="verticalExtensionCount">Number of vertical extension matches.</param>
        /// <param name="horizontalExtensionCount">Number of horizontal extension matches.</param>
        /// <returns>The type of match found.</returns>
        private MatchType CheckForMatchType(int horizontalMatchCount, int verticalMatchCount, int verticalExtensionCount, int horizontalExtensionCount)
        {
            // Check for different match types in order of priority
            if (CheckForLightBall(horizontalMatchCount, verticalMatchCount)) return MatchType.LightBall;
            if (CheckForTNT(horizontalMatchCount, verticalMatchCount, verticalExtensionCount, horizontalExtensionCount)) return MatchType.TNT;
            if (CheckForHorizontalRocket(verticalMatchCount)) return MatchType.HorizontalRocket;
            if (CheckForVerticalRocket(horizontalMatchCount)) return MatchType.VerticalRocket;
            if (CheckForMissile(horizontalMatchCount, verticalMatchCount, horizontalExtensionCount, verticalExtensionCount)) return MatchType.Missile;
            if (CheckForNormal(horizontalMatchCount, verticalMatchCount)) return MatchType.Normal;

            return MatchType.None;
        }

        /// <summary>
        /// Checks if the match pattern forms a Missile.
        /// </summary>
        /// <param name="horizontalMatchCount">Number of horizontal matches.</param>
        /// <param name="verticalMatchCount">Number of vertical matches.</param>
        /// <param name="horizontalExtensionCount">Number of horizontal extension matches.</param>
        /// <param name="verticalExtensionCount">Number of vertical extension matches.</param>
        /// <returns>True if a Missile match is found, false otherwise.</returns>
        private bool CheckForMissile(int horizontalMatchCount, int verticalMatchCount, int horizontalExtensionCount, int verticalExtensionCount)
        {
            // Check if there are matches in both directions and at least one extension
            if (horizontalMatchCount < 1 || verticalMatchCount < 1 || (verticalExtensionCount == 0 || horizontalExtensionCount == 0)) return false;
            
            // Check if the extensions form a T or L shape
            if (_verticalExtensions[0].Pos == _horizontalExtensions[0].Pos)
            {
                ClearAllMatchesAndAddThemToArray(_horizontalMatches);
                ClearAllMatchesAndAddThemToArray(_verticalMatches);
                ClearAllMatchesAndAddThemToArray(_horizontalExtensions);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the match pattern forms a Normal match (3 in a row/column).
        /// </summary>
        /// <param name="horizontalMatchCount">Number of horizontal matches.</param>
        /// <param name="verticalMatchCount">Number of vertical matches.</param>
        /// <returns>True if a Normal match is found, false otherwise.</returns>
        private bool CheckForNormal(int horizontalMatchCount, int verticalMatchCount)
        {
            // Check for 3 in a row horizontally
            if (horizontalMatchCount >= 2)
            {
                ClearAllMatchesAndAddThemToArray(_horizontalMatches);
                return true;
            }

            // Check for 3 in a row vertically
            if (verticalMatchCount < 2) return false;
            ClearAllMatchesAndAddThemToArray(_verticalMatches);

            return true;
        }

        /// <summary>
        /// Checks if the match pattern forms a LightBall (5+ in a row/column or large pattern).
        /// </summary>
        /// <param name="horizontalMatchCount">Number of horizontal matches.</param>
        /// <param name="verticalMatchCount">Number of vertical matches.</param>
        /// <returns>True if a LightBall match is found, false otherwise.</returns>
        private bool CheckForLightBall(int horizontalMatchCount, int verticalMatchCount)
        {
            // Check if there are enough matches to form a LightBall
            if (horizontalMatchCount <= 3 && verticalMatchCount <= 3) return false;
            
            // Add all matches to the matches list
            ClearAllMatchesAndAddThemToArray(_horizontalMatches);
            ClearAllMatchesAndAddThemToArray(_verticalMatches);
            
            // Add extensions if they form a valid pattern
            if(_horizontalExtensions[0].IsMatch && _horizontalExtensions[1].IsMatch)
                ClearAllMatchesAndAddThemToArray(_horizontalExtensions);
            if(_verticalExtensions[0].IsMatch && _verticalExtensions[1].IsMatch)
                ClearAllMatchesAndAddThemToArray(_verticalExtensions);

            return true;
        }

        /// <summary>
        /// Checks if the match pattern forms a TNT (T or L shape).
        /// </summary>
        /// <param name="horizontalMatchCount">Number of horizontal matches.</param>
        /// <param name="verticalMatchCount">Number of vertical matches.</param>
        /// <param name="verticalExtensionCount">Number of vertical extension matches.</param>
        /// <param name="horizontalExtensionCount">Number of horizontal extension matches.</param>
        /// <returns>True if a TNT match is found, false otherwise.</returns>
        private bool CheckForTNT(int horizontalMatchCount, int verticalMatchCount, int verticalExtensionCount,
            int horizontalExtensionCount)
        {
            // Check if there are enough matches in the right pattern to form a TNT
            if ((horizontalMatchCount <= 1 || (verticalMatchCount <= 1 && verticalExtensionCount <= 1)) &&
                (verticalMatchCount <= 1 || (horizontalMatchCount <= 1 && horizontalExtensionCount <= 1)))
                return false;
                
            // Add all matches to the matches list
            ClearAllMatchesAndAddThemToArray(_horizontalMatches);
            ClearAllMatchesAndAddThemToArray(_verticalMatches);
            
            // Add extensions if they form a valid pattern
            if(_horizontalExtensions[0].IsMatch && _horizontalExtensions[1].IsMatch)
                ClearAllMatchesAndAddThemToArray(_horizontalExtensions);
            if(_verticalExtensions[0].IsMatch && _verticalExtensions[1].IsMatch)
                ClearAllMatchesAndAddThemToArray(_verticalExtensions);
                
            return true;
        }

        /// <summary>
        /// Checks if the match pattern forms a Horizontal Rocket (4 in a column).
        /// </summary>
        /// <param name="verticalMatchCount">Number of vertical matches.</param>
        /// <returns>True if a Horizontal Rocket match is found, false otherwise.</returns>
        private bool CheckForHorizontalRocket(int verticalMatchCount)
        {
            // Check if there are enough vertical matches to form a Horizontal Rocket
            if (verticalMatchCount <= 2) return false;
            ClearAllMatchesAndAddThemToArray(_verticalMatches);
            return true;
        }

        /// <summary>
        /// Checks if the match pattern forms a Vertical Rocket (4 in a row).
        /// </summary>
        /// <param name="horizontalMatchCount">Number of horizontal matches.</param>
        /// <returns>True if a Vertical Rocket match is found, false otherwise.</returns>
        private bool CheckForVerticalRocket(int horizontalMatchCount)
        {
            // Check if there are enough horizontal matches to form a Vertical Rocket
            if (horizontalMatchCount <= 2) return false;
            ClearAllMatchesAndAddThemToArray(_horizontalMatches);
            return true;
        }
    }
}
