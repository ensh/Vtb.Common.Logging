using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vtb.Common.Logging
{
    using System.Collections.Concurrent;
    using System.Threading;

    public class PacketProcessing<T>
    {
        public readonly ConcurrentBag<T> m_items;

        public readonly int Delay;
        public readonly int MaxCount;
        public readonly int TryEnter;

        public PacketProcessing(int delay, int maxCount)
        {
            Delay = delay;
            MaxCount = maxCount;
            m_items = new ConcurrentBag<T>();
        }

        public void Add(T element)
        {            
            m_items.Add(element);
        }

        private IEnumerable<T> Items
        {
            get
            {
                for (int i = 0; i < MaxCount; i++)
                {
                    if (m_items.TryTake(out var item))
                        yield return item;
                    else
                        break;
                }
            }
        }

        public void Run(Action<IEnumerable<T>> processing)
        {
            processing(Items);
        }
    }
}
