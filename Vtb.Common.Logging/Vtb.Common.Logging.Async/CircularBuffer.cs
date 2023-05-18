using System;
using System.Collections;
using System.Collections.Generic;

namespace Vtb.Common.Logging
{
    public class CircularBuffer<T> : ICircularBuffer<T>, IEnumerable<T> where T : struct, IEquatable<T>
    {
        public CircularBuffer(int capacity)
		{
			if (capacity < 0)
				throw new ArgumentOutOfRangeException ("capacity", "must be positive");

            m_buffer = new T[capacity];
			m_head = capacity - 1;
		}

		public int Count { get; private set; }

		public int Capacity
        {
            get { return m_buffer.Length; }
			set
			{
				if (value < 0)
					throw new ArgumentOutOfRangeException ("value", "must be positive");

                if (value == m_buffer.Length)
					return;

				var buffer = new T[value];
				var count = 0;
				while (Count > 0 && count < value)
					buffer [count++] = Dequeue ();

				m_buffer = buffer;
				Count = count;
				m_head = count - 1;
				m_tail = 0;
			}
		}

		public void Enqueue (T item)
		{
			m_head = (m_head + 1) % Capacity;
			m_buffer [m_head] = item;
			if (Count == Capacity)
				m_tail = (m_tail + 1) % Capacity;
			else
				++Count;
		}

        /*
        public T Enqueue (T item)
		{
			m_head = (m_head + 1) % Capacity;
			var overwritten = m_buffer [m_head];
			m_buffer [m_head] = item;
			if (Count == Capacity)
				m_tail = (m_tail + 1) % Capacity;
			else
				++Count;
			return overwritten;
		}
        */

        public T Dequeue ()
		{
			if (Count == 0)
				throw new InvalidOperationException ("queue exhausted");

			var dequeued = m_buffer [m_tail];
			//m_buffer [m_tail] = default(T);
			m_tail = (m_tail + 1) % Capacity;
			--Count;
			return dequeued;
		}

        public void Remove(int count)
        {
            if (count >= Count)
                Clear();
            else
            {
                m_tail = (m_tail + count) % Capacity;
                Count -= count;
            }
        }

		public void Clear ()
		{
			m_head = Capacity - 1;
			m_tail = 0;
			Count = 0;
		}

		public T this [int index]
        {
			get
            {
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException ("index");

				return m_buffer [(m_tail + index) % Capacity];
			}
			set
            {
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException ("index");

				m_buffer [(m_tail + index) % Capacity] = value;
			}
		}

		public int IndexOf (T item, int start = 0)
		{
			for (var i = start; i < Count; ++i)
				if (item.Equals(this [i]))
					return i;
			return -1;
		}

        public int IndexOf (T[] items, int start = 0)
        {
            for (int i = start, j = 0, N = Count; i < N; ++i)
            {
                for (j = 0; i + j < N && j < items.Length; ++j)
                {
                    if (!items[j].Equals(this[i + j]))
                        break;
                }
                if (j == items.Length)
                    return i;
            }
            return -1;
        }

        public void Insert (int index, T item)
		{
			if (index < 0 || index > Count)
				throw new ArgumentOutOfRangeException ("index");

			if (Count == index)
				Enqueue (item);
			else
			{
				var last = this [Count - 1];
				for (var i = index; i < Count - 2; ++i)
					this [i + 1] = this [i];
				this [index] = item;
				Enqueue (last);
			}
		}

		public void RemoveAt (int index)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException ("index");

			for (var i = index; i > 0; --i)
				this [i] = this [i - 1];
			Dequeue ();
		}

		public IEnumerator<T> GetEnumerator ()
		{
			if (Count == 0 || Capacity == 0)
				yield break;

			for (var i = 0; i < Count; ++i)
				yield return this [i];
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

        private T[] m_buffer;
        private int m_head;
        private int m_tail;

    }
}
