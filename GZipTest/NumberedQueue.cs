using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GzipTest
{
    class NumberedQueue
    {
        private const int MaxBlockCount = 50;

        private readonly Queue<Block> _queue;
        private object _locker = new object();
        private int currentId = 0;

        private AutoResetEvent _maxCountEvent = new AutoResetEvent(true);

        public NumberedQueue()
        {
            _queue = new Queue<Block>();
        }

        public void Enqueue(Block block)
        {
            while (true)
            {
                lock (_locker)
                {
                    if(_queue.Count >= MaxBlockCount)
                    {
                        _maxCountEvent.WaitOne();
                    }
                    if (block.Id == currentId)
                    {
                        _queue.Enqueue(block);
                        currentId++;

                        Monitor.PulseAll(_locker);
                        break;
                    }
                    else
                    {
                        Monitor.Wait(_locker);
                    }
                }
            }
        }

        public Block Dequeue()
        {
            while(true)
            {
                lock (_locker)
                {
                    if (_queue.Count > 0)
                    {
                        if(_queue.Count >= 50)
                        {
                            _maxCountEvent.Set();
                        }
                        return _queue.Dequeue();
                    }
                    else
                    {
                        Monitor.Wait(_locker);
                    }
                }
            }
        }

        public int GetCount()
        {
            return _queue.Count;
        }
    }
}
