using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GzipTest
{
    class Reader
    {
        private readonly FileStream _sourceStream;
        private readonly int _blockSize;

        private object _locker = new object();

        private NumberedQueue _readerQueue;
        private int _freeId = 0;

        public bool IsFinished { get; set; } = false;

        public Reader(FileStream sourceStream, NumberedQueue readerQueue, int blockSize)
        {
            _sourceStream = sourceStream;
            _readerQueue = readerQueue;
            _blockSize = blockSize;
        }

        public void StartRead()
        {
            new Thread(() => Read()).Start();
        }

        private void Read()
        {
            long unreadedBytesLength = _sourceStream.Length - _sourceStream.Position;

            while(unreadedBytesLength > 0)
            {
                var readBytes = new byte[Math.Min(unreadedBytesLength, _blockSize)];

                _sourceStream.Read(readBytes, 0, readBytes.Length);

                var block = new Block(_freeId++);
                block.Buffer = readBytes;
                 
                _readerQueue.Enqueue(block);
            }

            IsFinished = true;
        }
    }
}
