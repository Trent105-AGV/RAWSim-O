using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Geometrics;

/// <summary>
/// Represents on node of a quadtree. The node can either contain all elements itself or have four subnodes separating the elements by their position.
/// </summary>
/// <typeparam name="T">The type of elements contained by the node.</typeparam>
public class QuadNode<T> where T : Circle
{
    /// <summary>
    /// Creates a new quadnode.
    /// </summary>
    /// <param name="divisionThreshold"></param>
    /// <param name="combineThreshold"></param>
    /// <param name="x1">The lower bound of the x-values belonging to this sector.</param>
    /// <param name="x2">The upper bound of the x-values belonging to this sector.</param>
    /// <param name="y1">The lower bound of the y-values belonging to this sector.</param>
    /// <param name="y2">The upper bound of the y-values belonging to this sector.</param>
    public QuadNode(int divisionThreshold, int combineThreshold, double x1, double x2, double y1, double y2)
    {
        DivisionThreshold = divisionThreshold;
        CombineThreshold = combineThreshold;
        X1 = x1;
        X2 = x2;
        Y1 = y1;
        Y2 = y2;
        _midX = X1 + (X2 - X1) / 2;
        _midY = Y1 + (Y2 - Y1) / 2;
        _midXs = [X1 + (X2 - X1) * (1.0 / 4.0), X1 + (X2 - X1) * (3.0 / 4.0), X1 + (X2 - X1) * (1.0 / 4.0), X1 + (X2 - X1) * (3.0 / 4.0)
        ];
        _midYs = [Y1 + (Y2 - Y1) * (1.0 / 4.0), Y1 + (Y2 - Y1) * (1.0 / 4.0), Y1 + (Y2 - Y1) * (3.0 / 4.0), Y1 + (Y2 - Y1) * (3.0 / 4.0)
        ];
    }

    /// <summary>
    /// The sub-nodes having this node as a parent. These nodes subdivide the space of this node into 4 equally sized parts.
    /// </summary>
    internal readonly QuadNode<T>[] Children = new QuadNode<T>[4];

    /// <summary>
    /// The IDs of all children as an array.
    /// </summary>
    private readonly int[] _childrenIDs = [0, 1, 2, 3];

    /// <summary>
    /// The lower bound of the x-values belonging to this sector.
    /// </summary>
    private readonly double X1;
    /// <summary>
    /// The upper bound of the x-values belonging to this sector.
    /// </summary>
    private readonly double X2;
    /// <summary>
    /// The lower bound of the y-values belonging to this sector.
    /// </summary>
    private readonly double Y1;
    /// <summary>
    /// The upper bound of the y-values belonging to this sector.
    /// </summary>
    private readonly double Y2;

    /// <summary>
    /// The middle x-value of this node.
    /// </summary>
    private readonly double _midX;
    /// <summary>
    /// The middle y-value of this node.
    /// </summary>
    private readonly double _midY;

    /// <summary>
    /// The middle x-values of the children.
    /// </summary>
    private readonly double[] _midXs;
    /// <summary>
    /// The middle y-values of the children.
    /// </summary>
    private readonly double[] _midYs;

    /// <summary>
    /// One QuadNode is subdivided, if it contains at least this count of objects.
    /// </summary>
    private readonly int DivisionThreshold;

    /// <summary>
    /// If fewer objects are contained in the four children of a QuadNode, they are recombined.
    /// </summary>
    private readonly int CombineThreshold;

    /// <summary>
    /// Contains all objects attached to this node.
    /// </summary>
    internal readonly HashSet<T> Objects = [];

    /// <summary>
    /// The largest object of this node.
    /// </summary>
    private T _largestObject;

    /// <summary>
    /// Returns the <code>QuadNode</code> belonging to the specified direction.
    /// </summary>
    /// <param name="direction">The direction of the child.</param>
    /// <returns>The desired <code>QuadNode</code> object.</returns>
    public QuadNode<T> this[QuadDirections direction]
    {
        get
        {
            return direction switch
            {
                QuadDirections.SW => Children[0],
                QuadDirections.SE => Children[1],
                QuadDirections.NW => Children[2],
                QuadDirections.NE => Children[3],
                _ => null
            };
        }
    }

    /// <summary>
    /// Returns true if Circle c moving to location x, y will not collide with another Circle, false if it will collide.
    /// </summary>
    /// <param name="c">Circle to check for collision.</param>
    /// <param name="x">The new x-coordinate.</param>
    /// <param name="y">The new y-coordinate.</param>
    /// <returns><code>true</code> if the move is valid, <code>false</code> otherwise.</returns>
    public bool IsValidMove(T c, double x, double y)
    {
        // If this is a leaf node, check all objects
        if (Children[0] == null)
        {
            foreach (var other in Objects)
            {
                if (other == c) { continue; }
                if (other.IsCollision(x, y, c.Radius)) { return false; }
            }
            return true;
        }

        // Use 2*diameter to account for another circle beyond the QuadtreeNode
        var diameter = 4 * c.Radius;

        // Check to see if it's in any of the four quadrants; be leniant by a diameter incase it's at the edge
        // See if in left half
        if (c.X - diameter <= _midX)
        {
            // See if in bottom half
            if (c.Y - diameter <= _midY)
                if (!Children[0].IsValidMove(c, x, y))
                    return false;

            // See if in top half
            if (c.Y + diameter >= _midY)
                if (!Children[2].IsValidMove(c, x, y))
                    return false;
        }

        // See if in right half
        if (c.X + diameter >= _midX)
        {
            // See if in bottom half
            if (c.Y - diameter <= _midY)
                if (!Children[1].IsValidMove(c, x, y))
                    return false;

            // See if in top half
            if (c.Y + diameter >= _midY)
                if (!Children[3].IsValidMove(c, x, y))
                    return false;
        }

        // Survived all the collision tests
        return true;
    }

    /// <summary>
    /// Checks an object for any collisions with other objects.
    /// </summary>
    /// <param name="c">The object to check for collisions.</param>
    /// <returns><code>true</code> if there are any collisions, <code>false</code> otherwise.</returns>
    public bool IsCollision(T c)
    {
        // If this is a leaf node, check all objects
        if (Children[0] == null)
        {
            foreach (var other in Objects)
            {
                if (other == c) { continue; }
                if (other.IsCollision(c)) { return true; }
            }
            return false;
        }

        // Use 2*diameter to account for another circle beyond the QuadtreeNode
        var diameter = 4 * c.Radius;

        // Check to see if it's in any of the four quadrants; be leniant by a diameter incase it's at the edge
        // See if in left half
        if (c.X - diameter <= _midX)
        {
            // See if in bottom half
            if (c.Y - diameter <= _midY)
                if (!Children[0].IsCollision(c))
                    return false;

            // See if in top half
            if (c.Y + diameter >= _midY)
                if (!Children[2].IsCollision(c))
                    return false;
        }

        // See if in right half
        if (c.X + diameter >= _midX)
        {
            // See if in bottom half
            if (c.Y - diameter <= _midY)
                if (!Children[1].IsCollision(c))
                    return false;

            // See if in top half
            if (c.Y + diameter >= _midY)
                if (!Children[3].IsCollision(c))
                    return false;
        }

        // Survived all the collision tests
        return false;
    }

    /// <summary>
    /// Adds the specified Circle to the tree.
    /// </summary>
    /// <param name="c">The Circle to add.</param>
    public void Add(T c)
    {
        // If it's bigger, then it's the largest circle
        if (_largestObject == null || c.Radius > _largestObject.Radius)
            _largestObject = c;

        if (Children[0] == null)
        {
            Objects.Add(c);
        }
        else
        {
            // See if in left half
            if (c.X < _midX)
            {
                // See if in bottom half
                if (c.Y < _midY)
                    Children[0].Add(c);
                // In top half
                else
                    Children[2].Add(c);
            }
            // In right half
            else
            {
                // See if in bottom half
                if (c.Y < _midY)
                    Children[1].Add(c);
                // In top half
                else
                    Children[3].Add(c);
            }
        }
    }

    /// <summary>
    /// Removes the specified Circle from the tree.
    /// </summary>
    /// <param name="c">The Circle to remove.</param>
    public void Remove(T c)
    {
        // If leaf node, remove circle, otherwise traverse subnodes
        if (Children[0] == null)
        {
            var removeSuccess = Objects.Remove(c);
            if (!removeSuccess)
                throw new InvalidOperationException("Cannot remove the object from the tree - it is not in the position where it was expected");

            // Get new largest circle
            var largest_size = 0.0;
            _largestObject = null;
            foreach (var i in Objects)
                if (i.Radius > largest_size)
                {
                    _largestObject = i;
                    largest_size = _largestObject.Radius;
                }
        }
        else
        {
            // See if in left half
            if (c.X < _midX)
            {
                // See if in bottom half
                if (c.Y < _midY)
                    Children[0].Remove(c);
                // In top half
                else
                    Children[2].Remove(c);
            }
            // In right half
            else
            {
                // See if in bottom half
                if (c.Y < _midY)
                    Children[1].Remove(c);
                // In top half
                else
                    Children[3].Remove(c);
            }

            // Get new largest circle
            var largest_size = 0.0;
            _largestObject = null;
            for (var i = 0; i < 4; i++)
                if (Children[i]._largestObject != null && Children[i]._largestObject.Radius > largest_size)
                {
                    _largestObject = Children[i]._largestObject;
                    largest_size = _largestObject.Radius;
                }
        }
    }

    /// <summary>
    /// Returns an enumeration of objects within the given distance around the given coordinates.
    /// </summary>
    /// <param name="x">The x-value of the coordinate.</param>
    /// <param name="y">The y-value of the coordinate.</param>
    /// <param name="distance">The distance for the search.</param>
    /// <returns>All objects within distance.</returns>
    public IEnumerable<T> GetObjectsWithinDistance(double x, double y, double distance)
    {
        // If this is a leaf node only check the attached objects and return
        if (Children[0] == null)
        {
            foreach (var o in Objects)
                if (o.IsCollision(x, y, distance))
                    yield return o;
            yield break;
        }

        // Find distance to search for another object (but make sure to include a padding to check for the nodes where an object may be overlapping two nodes (but don'task pass this distance on to the collision detection itself)
        var dist = distance;
        if (_largestObject != null)
            dist += _largestObject.Radius;

        // Check to see if it's in any of the four quadrants see if in left half
        if (x - dist <= _midX)
        {
            // See if in bottom half
            if (y - dist <= _midY)
                foreach (var child in Children[0].GetObjectsWithinDistance(x, y, distance))
                    yield return child;

            // See if in top half
            if (y + dist >= _midY)
                foreach (var child in Children[2].GetObjectsWithinDistance(x, y, distance))
                    yield return child;
        }

        // See if in right half
        if (x + dist >= _midX)
        {
            // See if in bottom half
            if (y - dist <= _midY)
                foreach (var child in Children[1].GetObjectsWithinDistance(x, y, distance))
                    yield return child;

            // See if in top half
            if (y + dist >= _midY)
                foreach (var child in Children[3].GetObjectsWithinDistance(x, y, distance))
                    yield return child;
        }
    }

    /// <summary>
    /// Gets the object nearest to the given coordinates.
    /// </summary>
    /// <param name="x">The x-value.</param>
    /// <param name="y">The y-value.</param>
    /// <param name="nearestObject">Is updated with the nearest object of the node, if there is a nearer one.</param>
    /// <param name="nearestDistance">This field is passed the current best distance which is updated, if a better one is found.</param>
    /// <returns>The object nearest to the given coordinates.</returns>
    public void GetNearestObject(double x, double y, ref T nearestObject, ref double nearestDistance)
    {
        // If this is a leaf node, only check the attached objects
        if (Children[0] == null)
        {
            // Obtain nearest object
            foreach (var child in Objects)
            {
                var distance = child.GetDistance(x, y);
                if (distance < nearestDistance)
                {
                    nearestObject = child;
                    nearestDistance = distance;
                }
            }
            return;
        }

        // Determine search order
        var first = -1; var firstValue = double.MaxValue;
        var second = -1; var secondValue = double.MaxValue;
        var third = -1; var thirdValue = double.MaxValue;
        var fourth = -1; var fourthValue = double.MaxValue;
        for (var i = 0; i < _childrenIDs.Length; i++)
        {
            var distance = Metrics.Distances.CalculateEuclid(_midXs[i], _midYs[i], x, y);
            if (distance < firstValue)
            {
                fourth = third; fourthValue = thirdValue;
                third = second; thirdValue = secondValue;
                second = first; secondValue = firstValue;
                first = i; firstValue = distance;
            }
            else if (distance < secondValue)
            {
                fourth = third; fourthValue = thirdValue;
                third = second; thirdValue = secondValue;
                second = i; secondValue = distance;
            }
            else if (distance < thirdValue)
            {
                fourth = third; fourthValue = thirdValue;
                third = i; thirdValue = distance;
            }
            else if (distance < fourthValue)
            {
                fourth = i; fourthValue = distance;
            }
        }
        // Check all children for the nearest object
        // Check first section
        Children[first].GetNearestObject(x, y, ref nearestObject, ref nearestDistance);
        // Check second section if necessary
        if (nearestObject == null || !CheckChildLookupUnnecessary(second, x, y, nearestDistance))
            Children[second].GetNearestObject(x, y, ref nearestObject, ref nearestDistance);
        // Check third section if necessary
        if (nearestObject == null || !CheckChildLookupUnnecessary(third, x, y, nearestDistance))
            Children[third].GetNearestObject(x, y, ref nearestObject, ref nearestDistance);
        // Check fourth section if necessary
        if (nearestObject == null || !CheckChildLookupUnnecessary(fourth, x, y, nearestDistance))
            Children[fourth].GetNearestObject(x, y, ref nearestObject, ref nearestDistance);
    }
    /// <summary>
    /// Checks whether the given section has to be checked considering the so far best distance.
    /// </summary>
    /// <param name="childID">The section to be checked.</param>
    /// <param name="x">The x-value to search for the nearest neigbor for.</param>
    /// <param name="y">The y-value to search for the nearest neigbor for.</param>
    /// <param name="bestDistance">The best distance so far.</param>
    /// <returns><code>true</code> if the lookup of the given section is unnecessary, <code>false</code> otherwise.</returns>
    private bool CheckChildLookupUnnecessary(int childID, double x, double y, double bestDistance)
    {
        return childID switch
        {
            // Check bottom left section
            0 => x - bestDistance >= _midXs[childID] || y - bestDistance >= _midYs[childID],
            // Check bottom right section
            1 => x + bestDistance <= _midXs[childID] || y - bestDistance >= _midYs[childID],
            // Check top left section
            2 => x - bestDistance >= _midXs[childID] || y + bestDistance <= _midYs[childID],
            // Check top right section
            3 => x + bestDistance <= _midXs[childID] || y + bestDistance <= _midYs[childID],
            _ => throw new ArgumentException("Unknown child ID: " + childID)
        };
    }

    /// <summary>
    /// Returns the number of objects beneath this node of the QuadTree.
    /// </summary>
    /// <returns>The number of objects at this node or the summed number of objects at the child-nodes of this node.</returns>
    public int Count()
    {
        if (Children[0] == null)
            return Objects.Count;
        return Children[0].Count() + Children[1].Count() + Children[2].Count() + Children[3].Count();
    }

    /// <summary>
    /// Validates this node in terms of checking whether all objects are within the nodes boundaries.
    /// </summary>
    /// <returns><code>true</code> if the node is valid, <code>false</code> otherwise.</returns>
    public bool Validate()
    {
        if (Children[0] == null)
            return Objects.All(o => o.X >= X1 && o.X <= X2 && o.Y >= Y1 && o.Y <= Y2);
        return Children.All(c => c.Validate());
    }

    /// <summary>
    /// Rearranges the tree by expanding and collapsing this node and all of its children, if necessary.
    /// </summary>
    public void Reoptimize()
    {
        // Growing the tree
        if (Objects.Count >= DivisionThreshold)
        {
            // Create child trees
            Children[0] = new QuadNode<T>(DivisionThreshold, CombineThreshold, X1, _midX, Y1, _midY);
            Children[1] = new QuadNode<T>(DivisionThreshold, CombineThreshold, _midX, X2, Y1, _midY);
            Children[2] = new QuadNode<T>(DivisionThreshold, CombineThreshold, X1, _midX, _midY, Y2);
            Children[3] = new QuadNode<T>(DivisionThreshold, CombineThreshold, _midX, X2, _midY, Y2);

            // Move all objects into the corresponding one
            foreach (var c in Objects)
                Add(c);

            // Clean up this node
            Objects.Clear();
        }

        // Update all children
        if (Children[0] != null)
            foreach (var q in Children)
                q.Reoptimize();

        // If has children, and each child is a leaf node, check to see if the sum of all of the objects within those 4 children is less than the QuadTree.CombineThreshold. If so, take all of the children's objects into this node and detach the children. 
        if (Children[0] != null &&
            Children[0].Children[0] == null &&
            Children[1].Children[0] == null &&
            Children[2].Children[0] == null &&
            Children[3].Children[0] == null &&
            Children[0].Objects.Count + Children[1].Objects.Count + Children[2].Objects.Count + Children[3].Objects.Count < CombineThreshold)
        {
            // Iterate the children
            for (var i = 0; i < 4; i++)
            {
                // Need to clear Children[0] first so that Add will know it has no children.
                var circles = Children[i].Objects;
                Children[i] = null;
                // Add all objects from child nodes
                foreach (var c in circles) Add(c);
            }
        }
    }

    /// <summary>
    /// Finds the shortest distance any object can move before a collision could happen.
    /// </summary>
    /// <returns>The shortest distance to a collision.</returns>
    public double GetShortestDistanceWithoutCollision()
    {
        // If not a leaf node, then get values from child nodes, and find the minimum
        if (Children[0] != null)
        {
            return Math.Min(
                Math.Min(Children[0].GetShortestDistanceWithoutCollision(), Children[1].GetShortestDistanceWithoutCollision()),
                Math.Min(Children[2].GetShortestDistanceWithoutCollision(), Children[3].GetShortestDistanceWithoutCollision()));
        }

        // Leaf node, so find shortest distances for each robot
        var objectArray = Objects.ToArray();
        var minDistance = Double.PositiveInfinity;
        for (var i = 0; i < Objects.Count - 1; i++)
        {
            var c1 = objectArray[i];
            // Only check moving objects
            if (!c1.Moving)
                continue;

            var minDistanceSquared = double.PositiveInfinity;

            // Find distance to closest other object
            for (var j = i + 1; j < Objects.Count; j++)
            {
                var c2 = objectArray[j];
                // Only check moving objects
                if (!c2.Moving)
                    continue;

                // Calculate distance
                var dist = (c1.X - c2.X) * (c1.X - c2.X) + (c1.Y - c2.Y) * (c1.Y - c2.Y);
                if (dist < minDistanceSquared)
                    minDistanceSquared = dist;
            }

            // Set new minimum, if necessary
            minDistance = Math.Min(minDistance, Math.Sqrt(minDistanceSquared));

            // Check distance to sides of collision pod
            // Use 2*diameter to account for another circle beyond the QuadNode
            var diameter = 2 * c1.Radius;

            // Left side
            minDistance = Math.Min(minDistance, c1.X - (X1 - diameter));
            // Right side
            minDistance = Math.Min(minDistance, X2 + diameter - c1.X);
            // Top side
            minDistance = Math.Min(minDistance, Y1 + diameter - c1.Y);
            // Bottom side
            minDistance = Math.Min(minDistance, c1.Y - (Y2 - diameter));
        }

        // Return the minimal distance between two objects of this node and all of its children
        return minDistance;
    }
}