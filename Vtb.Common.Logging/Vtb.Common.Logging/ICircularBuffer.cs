namespace Vtb.Common.Logging
{
    using System;
    public interface ICircularBuffer<T> where T : struct, IEquatable<T>
    {
        int Count { get; }
        int Capacity { get; set; }
        //T Enqueue(T item);
        void Enqueue(T item);
        T Dequeue();
        void Clear();
        void Remove(int count);
        T this[int index] { get; set; }
        int IndexOf(T item, int start = 0);
        int IndexOf(T[] items, int start = 0);
        void Insert(int index, T item);
        void RemoveAt(int index);
    }
}
