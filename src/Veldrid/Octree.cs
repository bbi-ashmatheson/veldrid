﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace Veldrid
{
    public static class Octree
    {
        public static OctreeNode<T> CreateNewTree<T>(ref BoundingBox bounds, int maxChildren)
        {
            return OctreeNode<T>.CreateNewTree(ref bounds, maxChildren);
        }
    }

    /// <summary>
    /// Maintains a reference to the current root node of a dynamic octree.
    /// The root node may change as items are added and removed from contained nodes.
    /// </summary>
    /// <typeparam name="T">The type stored in the octree.</typeparam>
    public class Octree<T>
    {
        private OctreeNode<T> _currentRoot;

        public Octree(BoundingBox boundingBox, int maxChildre)
        {
            _currentRoot = new OctreeNode<T>(boundingBox, maxChildre);
        }

        /// <summary>
        /// The current root node of the octree. This may change when items are added and removed.
        /// </summary>
        public OctreeNode<T> CurrentRoot => _currentRoot;

        public void AddItem(BoundingBox itemBounds, T item)
        {
            _currentRoot = _currentRoot.AddItem(ref itemBounds, item);
        }

        public void GetContainedObjects(BoundingFrustum frustum, List<T> results)
        {
            _currentRoot.GetContainedObjects(ref frustum, results);
        }

        public void GetContainedObjects(BoundingFrustum frustum, List<T> results, Func<T, bool> filter)
        {
            _currentRoot.GetContainedObjects(ref frustum, results, filter);
        }

        public int RayCast(Ray ray, List<T> hits)
        {
            return _currentRoot.RayCast(ray, hits);
        }

        public void GetAllContainedObjects(List<T> results)
        {
            _currentRoot.GetAllContainedObjects(results);
        }

        public void GetAllContainedObjects(List<T> results, Func<T, bool> filter)
        {
            _currentRoot.GetAllContainedObjects(results, filter);
        }
    }

    [DebuggerDisplay("{DebuggerDisplayString,nq}")]
    public class OctreeNode<T>
    {
        private readonly List<OctreeItem> _items = new List<OctreeItem>();
        private readonly OctreeNodeCache _nodeCache;

        public BoundingBox Bounds { get; private set; }
        public int MaxChildren { get; private set; }
        public OctreeNode<T>[] Children { get; private set; } = Array.Empty<OctreeNode<T>>();
        public OctreeNode<T> Parent { get; private set; }

        private const int ChildCount = 8;

        public static OctreeNode<T> CreateNewTree(ref BoundingBox bounds, int maxChildren)
        {
            return new OctreeNode<T>(ref bounds, maxChildren, new OctreeNodeCache(maxChildren), null);
        }

        public OctreeNode<T> AddItem(BoundingBox itemBounds, T item)
        {
            return AddItem(ref itemBounds, item);
        }

        public OctreeNode<T> AddItem(ref BoundingBox itemBounds, T item)
        {
            if (Parent != null)
            {
                throw new InvalidOperationException("Can only add items to the root Octree node.");
            }

            OctreeNode<T> root = this;

            OctreeItem octreeItem = new OctreeItem(ref itemBounds, item);
            bool result = AddItem(ref octreeItem);
            if (!result)
            {
                root = ResizeAndAdd(ref octreeItem);
            }

            return root;
        }

        public void GetContainedObjects(BoundingFrustum frustum, List<T> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            frustum = CoreGetContainedObjects(ref frustum, results, null);
        }

        public void GetContainedObjects(ref BoundingFrustum frustum, List<T> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            frustum = CoreGetContainedObjects(ref frustum, results, null);
        }

        public void GetContainedObjects(ref BoundingFrustum frustum, List<T> results, Func<T, bool> filter)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            frustum = CoreGetContainedObjects(ref frustum, results, filter);
        }

        public void GetAllContainedObjects(List<T> results) => GetAllContainedObjects(results);
        public void GetAllContainedObjects(List<T> results, Func<T, bool> filter)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            CoreGetAllContainedObjects(results, filter);
        }

        public int RayCast(Ray ray, List<T> hits)
        {
            if (!ray.Intersects(Bounds))
            {
                return 0;
            }
            else
            {
                int numHits = 0;
                foreach (OctreeItem item in _items)
                {
                    if (ray.Intersects(item.Bounds))
                    {
                        numHits++;
                        hits.Add(item.Item);
                    }
                }
                foreach (var child in Children)
                {
                    numHits += child.RayCast(ray, hits);
                }

                return numHits;
            }
        }

        public BoundingBox GetPreciseBounds()
        {
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            return CoreGetPreciseBounds(ref min, ref max);
        }

        private BoundingBox CoreGetPreciseBounds(ref Vector3 min, ref Vector3 max)
        {
            foreach (var item in _items)
            {
                min = Vector3.Min(min, item.Bounds.Min);
                max = Vector3.Max(max, item.Bounds.Max);
            }

            foreach (var child in Children)
            {
                child.CoreGetPreciseBounds(ref min, ref max);
            }

            return new BoundingBox(min, max);
        }

        public int GetItemCount()
        {
            return _items.Count + Children.Sum(on => on.GetItemCount());
        }

        private BoundingFrustum CoreGetContainedObjects(ref BoundingFrustum frustum, List<T> results, Func<T, bool> filter)
        {
            ContainmentType ct = frustum.Contains(Bounds);
            if (ct == ContainmentType.Contains)
            {
                CoreGetAllContainedObjects(results, filter);
            }
            else if (ct == ContainmentType.Intersects)
            {
                foreach (var octreeItem in _items)
                {
                    if (frustum.Contains(octreeItem.Bounds) != ContainmentType.Disjoint)
                    {
                        if (filter == null || filter(octreeItem.Item))
                        {
                            results.Add(octreeItem.Item);
                        }
                    }
                }
                foreach (var child in Children)
                {
                    child.CoreGetContainedObjects(ref frustum, results, filter);
                }
            }

            return frustum;
        }

        private bool AnyChild(Func<OctreeItem, bool> filter)
        {
            if (_items.Any(filter))
            {
                return true;
            }

            return Children.Any(node => node.AnyChild(filter));
        }

        private void CoreGetAllContainedObjects(List<T> results, Func<T, bool> filter)
        {
            foreach (var octreeItem in _items)
            {
                if (filter == null || filter(octreeItem.Item))
                {
                    results.Add(octreeItem.Item);
                }
            }
            foreach (var child in Children)
            {
                child.CoreGetAllContainedObjects(results, filter);
            }
        }

        public void Clear()
        {
            if (Parent != null)
            {
                throw new InvalidOperationException("Can only clear the root OctreeNode.");
            }

            RecycleTree();
        }

        private void RecycleTree()
        {
            if (Children.Length != 0)
            {
                foreach (var child in Children)
                {
                    child.RecycleTree();
                    _nodeCache.AddNode(child);
                }

                _nodeCache.AddAndClearChildrenArray(Children);
                Children = Array.Empty<OctreeNode<T>>();
            }

            _items.Clear();
        }

        private bool AddItem(ref OctreeItem item)
        {
            if (Bounds.Contains(ref item.Bounds) != ContainmentType.Contains)
            {
                return false;
            }

            if (_items.Count >= MaxChildren && Children.Length == 0)
            {
                OctreeNode<T> newNode = SplitChildren(ref item.Bounds, null);
                if (newNode != null)
                {
                    bool succeeded = newNode.AddItem(ref item);
                    Debug.Assert(succeeded, "Octree node returned from SplitChildren must fit the item given to it.");
                    return true;
                }
            }
            else if (Children.Length > 0)
            {
                foreach (var child in Children)
                {
                    if (child.AddItem(ref item))
                    {
                        return true;
                    }
                }
            }

            // Couldn't fit in any children.
            _items.Add(item);
            return true;
        }

        // Splits the node into 8 children
        private OctreeNode<T> SplitChildren(ref BoundingBox itemBounds, OctreeNode<T> existingChild)
        {
            Debug.Assert(Children.Length == 0, "Children must be empty before SplitChildren is called.");

            OctreeNode<T> childBigEnough = null;
            Children = _nodeCache.GetChildrenArray();
            Vector3 center = Bounds.GetCenter();
            Vector3 dimensions = Bounds.GetDimensions();

            Vector3 quaterDimensions = dimensions * 0.25f;

            int i = 0;
            for (float x = -1f; x <= 1f; x += 2f)
            {
                for (float y = -1f; y <= 1f; y += 2f)
                {
                    for (float z = -1f; z <= 1f; z += 2f)
                    {
                        Vector3 childCenter = center + (quaterDimensions * new Vector3(x, y, z));
                        Vector3 min = childCenter - quaterDimensions;
                        Vector3 max = childCenter + quaterDimensions;
                        BoundingBox childBounds = new BoundingBox(min, max);
                        OctreeNode<T> newChild;

                        if (existingChild != null && existingChild.Bounds == childBounds)
                        {
                            newChild = existingChild;
                        }
                        else
                        {
                            newChild = _nodeCache.GetNode(ref childBounds);
                        }

                        if (childBounds.Contains(ref itemBounds) == ContainmentType.Contains)
                        {
                            childBigEnough = newChild;
                        }

                        newChild.Parent = this;
                        Children[i] = newChild;
                        i++;
                    }
                }
            }

            PushItemsToChildren();
            return childBigEnough;
        }

        private void PushItemsToChildren()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                foreach (var child in Children)
                {
                    if (child.AddItem(ref item))
                    {
                        _items.Remove(item);
                        i--;
                        continue;
                    }
                }
            }
        }

        private OctreeNode<T> ResizeAndAdd(ref OctreeItem octreeItem)
        {
            OctreeNode<T> oldRoot = this;
            Vector3 oldRootCenter = Bounds.GetCenter();
            Vector3 oldRootHalfExtents = Bounds.GetDimensions() * 0.5f;

            Vector3 expandDirection = Vector3.Normalize(octreeItem.Bounds.GetCenter() - oldRootCenter);
            Vector3 newCenter = oldRootCenter;
            if (expandDirection.X >= 0) // oldRoot = Left
            {
                newCenter.X += oldRootHalfExtents.X;
            }
            else
            {
                newCenter.X -= oldRootHalfExtents.X;
            }

            if (expandDirection.Y >= 0) // oldRoot = Bottom
            {
                newCenter.Y += oldRootHalfExtents.Y;
            }
            else
            {
                newCenter.Y -= oldRootHalfExtents.Y;
            }

            if (expandDirection.Z >= 0) // oldRoot = Far
            {
                newCenter.Z += oldRootHalfExtents.Z;
            }
            else
            {
                newCenter.Z -= oldRootHalfExtents.Z;
            }

            BoundingBox newRootBounds = new BoundingBox(newCenter - oldRootHalfExtents * 2f, newCenter + oldRootHalfExtents * 2f);
            OctreeNode<T> newRoot = _nodeCache.GetNode(ref newRootBounds);
            OctreeNode<T> fittingNode = newRoot.SplitChildren(ref octreeItem.Bounds, oldRoot);
            if (fittingNode != null)
            {
                bool succeeded = fittingNode.AddItem(ref octreeItem);
                Debug.Assert(succeeded, "Octree node returned from SplitChildren must fit the item given to it.");
                return newRoot;
            }
            else
            {
                return newRoot.AddItem(ref octreeItem.Bounds, octreeItem.Item);
            }
        }

        public OctreeNode(BoundingBox box, int maxChildren)
            : this(ref box, maxChildren, new OctreeNodeCache(maxChildren), null)
        {
        }

        private OctreeNode(ref BoundingBox bounds, int maxChildren, OctreeNodeCache nodeCache, OctreeNode<T> parent)
        {
            Bounds = bounds;
            MaxChildren = maxChildren;
            _nodeCache = nodeCache;
            Parent = parent;
        }

        private void Reset(ref BoundingBox newBounds)
        {
            Bounds = newBounds;

            _items.Clear();
            Parent = null;

            if (Children.Length != 0)
            {
                _nodeCache.AddAndClearChildrenArray(Children);
                Children = Array.Empty<OctreeNode<T>>();
            }
        }

        private string DebuggerDisplayString
        {
            get
            {
                return string.Format("{0} - {1}, Items:{2}", Bounds.Min, Bounds.Max, _items.Count);
            }
        }

        private class OctreeNodeCache
        {
            private readonly Stack<OctreeNode<T>> _nodes = new Stack<OctreeNode<T>>();
            private readonly Stack<OctreeNode<T>[]> _cachedChildren = new Stack<OctreeNode<T>[]>();

            public int MaxChildren { get; private set; }

            public OctreeNodeCache(int maxChildren)
            {
                MaxChildren = maxChildren;
            }

            public void AddNode(OctreeNode<T> child)
            {
                _nodes.Push(child);
            }

            public OctreeNode<T> GetNode(ref BoundingBox bounds)
            {
                if (_nodes.Count > 0)
                {
                    var node = _nodes.Pop();
                    node.Reset(ref bounds);
                    return node;
                }
                else
                {
                    return CreateNewNode(ref bounds);
                }
            }

            public void AddAndClearChildrenArray(OctreeNode<T>[] children)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    children[i] = null;
                }

                _cachedChildren.Push(children);
            }

            public OctreeNode<T>[] GetChildrenArray()
            {
                if (_cachedChildren.Count > 0)
                {
                    var children = _cachedChildren.Pop();
#if DEBUG
                    for (int i = 0; i < children.Length; i++)
                    {
                        Debug.Assert(children[i] == null);
                    }
#endif

                    return children;
                }
                else
                {
                    return new OctreeNode<T>[ChildCount];
                }
            }

            private OctreeNode<T> CreateNewNode(ref BoundingBox bounds)
            {
                OctreeNode<T> node = new OctreeNode<T>(ref bounds, MaxChildren, this, null);
                return node;
            }
        }

        private struct OctreeItem
        {
            public BoundingBox Bounds;
            public T Item;

            public OctreeItem(ref BoundingBox bounds, T item)
            {
                Bounds = bounds;
                Item = item;
            }

            public override string ToString()
            {
                return string.Format("{0}, {1}", Bounds, Item);
            }
        }
    }
}