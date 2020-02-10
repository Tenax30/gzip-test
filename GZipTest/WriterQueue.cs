using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GzipTest
{
    class WriterQueue
    {
        private readonly Queue<Block> _queue;
        private object _locker = new object();
        private int _nextId = 0;
        private bool _isStopped = false;

        public WriterQueue()
        {
            _queue = new Queue<Block>();
        }

        public void Enqueue(Block block)
        {
            while (true)
            {
                lock (_locker)
                {
                    if (block.Id == _nextId)
                    {
                        _queue.Enqueue(block);
                        _nextId++;

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
            while (true)
            {
                lock (_locker)
                {
                    if (_queue.Count > 0)
                    {
                        Monitor.PulseAll(_locker);
                        return _queue.Dequeue();
                    }
                    else
                    {
                        if (_isStopped) return null;
                        Monitor.Wait(_locker);
                    }
                }
            }
        }

        public void Unlock()
        {
            lock(_locker)
            {
                Monitor.PulseAll(_locker);
                _isStopped = true;
            }
        }

        public int GetCount()
        {
            return _queue.Count;
        }
    }
}
