using System.Collections.Generic;

namespace RAWSimO.MultiAgentPathFinding.DataStructures;

public class BinaryHeap<T>
{
    private readonly List<double> priorities;
    private readonly List<T> items;

    public HeapType Type { get; }

    public int Size => priorities.Count;

    public T Root => items[0];

    public BinaryHeap(HeapType type)
    {
        priorities  = [];
        items = [];
        Type = type;
    }

    public void Insert(double priority, T item)
    {
        items.Add(item);
        priorities.Add(priority);

        var i = items.Count - 1;

        var flag = true;
        if (Type == HeapType.MaxHeap)
            flag = false;

        while (i > 0)
        {
            if ((priorities[i].CompareTo(priorities[(i - 1) / 2]) > 0) ^ flag)
            {
                var temp = items[i];
                items[i] = items[(i - 1) / 2];
                items[(i - 1) / 2] = temp;

                var tempd = priorities[i];
                priorities[i] = priorities[(i - 1) / 2];
                priorities[(i - 1) / 2] = tempd;

                i = (i - 1) / 2;
            }
            else
                break;
        }
    }

    public void DeleteRoot()
    {
        var i = priorities.Count - 1;

        items[0] = items[i];
        priorities[0] = priorities[i];
        items.RemoveAt(i);
        priorities.RemoveAt(i);

        i = 0;

        var flag = true;
        if (Type == HeapType.MaxHeap)
            flag = false;

        while (true)
        {
            var leftInd = 2 * i + 1;
            var rightInd = 2 * i + 2;
            var largest = i;

            if (leftInd < priorities.Count)
            {
                if ((priorities[leftInd].CompareTo(priorities[largest]) > 0) ^ flag)
                    largest = leftInd;
            }

            if (rightInd < priorities.Count)
            {
                if ((priorities[rightInd].CompareTo(priorities[largest]) > 0) ^ flag)
                    largest = rightInd;
            }

            if (largest != i)
            {
                var temp = items[largest];
                items[largest] = items[i];
                items[i] = temp;

                var tempd = priorities[largest];
                priorities[largest] = priorities[i];
                priorities[i] = tempd;

                i = largest;
            }
            else
                break;
        }
    }

    public T PopRoot()
    {
        var result = items[0];

        DeleteRoot();

        return result;
    }

    public enum HeapType
    {
        MinHeap,
        MaxHeap
    }

}