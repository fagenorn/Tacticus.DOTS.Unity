﻿using System;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Sandbox.ECS.KNN
{
// Sorted heap with a self balancing tree
    // Can act as either a min or max heap
    public unsafe struct MinMaxHeap<T> : IDisposable where T : unmanaged
    {
        [NativeDisableContainerSafetyRestriction]
        T* keys; //objects

        [NativeDisableContainerSafetyRestriction]
        float* values;

        public int Count;

        int m_capacity;

        public float HeadValue => values[1];

        T HeadKey => keys[1];

        public bool IsFull => Count == m_capacity;

        Allocator m_allocator;

        public MinMaxHeap(int startCapacity, Allocator allocator)
        {
            Count       = 0;
            m_allocator = allocator;

            // Now alloc starting arrays
            m_capacity = startCapacity;
            values     = UnsafeUtilityEx.AllocArray<float>(startCapacity + 1, m_allocator);
            keys       = UnsafeUtilityEx.AllocArray<T>(startCapacity + 1, m_allocator);
        }

        void Swap(int indexA, int indexB)
        {
            (values[indexA], values[indexB]) = (values[indexB], values[indexA]);
            (keys[indexA], keys[indexB])     = (keys[indexB], keys[indexA]);
        }

        public void Dispose()
        {
            UnsafeUtility.Free(values, m_allocator);
            UnsafeUtility.Free(keys, m_allocator);
            values = null;
            keys   = null;
        }

        public void Resize(int newSize)
        {
            // Allocate more space
            var newValues = UnsafeUtilityEx.AllocArray<float>(newSize + 1, m_allocator);
            var newKeys   = UnsafeUtilityEx.AllocArray<T>(newSize + 1, m_allocator);

            // Copy over old arrays
            UnsafeUtility.MemCpy(newValues, values, (m_capacity + 1) * sizeof(int));
            UnsafeUtility.MemCpy(newKeys, keys, (m_capacity + 1) * sizeof(int));

            // Get rid of old arrays
            Dispose();

            // And now use old arrays
            values     = newValues;
            keys       = newKeys;
            m_capacity = newSize;
        }

        // bubble down, MaxHeap version
        void BubbleDownMax(int index)
        {
            int l = HeapUtils.Left(index);
            int r = HeapUtils.Right(index);

            // bubbling down, 2 kids
            while ( r <= Count )
            {
                // if heap property is violated between index and Left child
                if ( values[index] < values[l] )
                {
                    if ( values[l] < values[r] )
                    {
                        Swap(index, r); // left has bigger priority
                        index = r;
                    }
                    else
                    {
                        Swap(index, l); // right has bigger priority
                        index = l;
                    }
                }
                else
                {
                    // if heap property is violated between index and R
                    if ( values[index] < values[r] )
                    {
                        Swap(index, r);
                        index = r;
                    }
                    else
                    {
                        index = l;
                        l     = HeapUtils.Left(index);

                        break;
                    }
                }

                l = HeapUtils.Left(index);
                r = HeapUtils.Right(index);
            }

            // only left & last children available to test and swap
            if ( l <= Count && values[index] < values[l] )
            {
                Swap(index, l);
            }
        }

        void BubbleDownMin(int index)
        {
            int l = HeapUtils.Left(index);
            int r = HeapUtils.Right(index);

            // bubbling down, 2 kids
            while ( r <= Count )
            {
                // if heap property is violated between index and Left child
                if ( values[index] > values[l] )
                {
                    if ( values[l] > values[r] )
                    {
                        Swap(index, r); // right has smaller priority
                        index = r;
                    }
                    else
                    {
                        Swap(index, l); // left has smaller priority
                        index = l;
                    }
                }
                else
                {
                    // if heap property is violated between index and R
                    if ( values[index] > values[r] )
                    {
                        Swap(index, r);
                        index = r;
                    }
                    else
                    {
                        index = l;
                        l     = HeapUtils.Left(index);

                        break;
                    }
                }

                l = HeapUtils.Left(index);
                r = HeapUtils.Right(index);
            }

            // only left & last children available to test and swap
            if ( l <= Count && values[index] > values[l] )
            {
                Swap(index, l);
            }
        }

        void BubbleUpMax(int index)
        {
            int p = HeapUtils.Parent(index);

            //swap, until Heap property isn't violated anymore
            while ( p > 0 && values[p] < values[index] )
            {
                Swap(p, index);
                index = p;
                p     = HeapUtils.Parent(index);
            }
        }

        void BubbleUpMin(int index)
        {
            int p = HeapUtils.Parent(index);

            //swap, until Heap property isn't violated anymore
            while ( p > 0 && values[p] > values[index] )
            {
                Swap(p, index);
                index = p;
                p     = HeapUtils.Parent(index);
            }
        }

        public void PushObjMax(T key, float val)
        {
            // if heap full
            if ( Count == m_capacity )
            {
                // if Heads priority is smaller than input priority, then ignore that item
                if ( HeadValue > val )
                {
                    values[1] = val; // remove top element
                    keys[1]   = key;
                    BubbleDownMax(1); // bubble it down
                }
            }
            else
            {
                Count++;
                values[Count] = val;
                keys[Count]   = key;
                BubbleUpMax(Count);
            }
        }

        public void PushObjMin(T key, float val)
        {
            // if heap full
            if ( Count == m_capacity )
            {
                // if Heads priority is smaller than input priority, then ignore that item
                if ( HeadValue < val )
                {
                    values[1] = val; // remove top element
                    keys[1]   = key;
                    BubbleDownMin(1); // bubble it down
                }
            }
            else
            {
                Count++;
                values[Count] = val;
                keys[Count]   = key;
                BubbleUpMin(Count);
            }
        }

        T PopHeadObj()
        {
            T result = HeadKey;

            values[1] = values[Count];
            keys[1]   = keys[Count];
            Count--;

            return result;
        }

        public T PopObjMax()
        {
            T result = PopHeadObj();
            BubbleDownMax(1);

            return result;
        }

        public T PopObjMin()
        {
            T result = PopHeadObj();
            BubbleDownMin(1);

            return result;
        }
    }
}