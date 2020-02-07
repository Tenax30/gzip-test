using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GzipTest
{
    class Writer
    {
        private readonly FileStream _resultStream;
        private NumberedQueue _writerQueue;

        private object locker = new object();

        private bool isStopped = false;

        public Writer(FileStream resultStream, NumberedQueue writerQueue)
        {
            _resultStream = resultStream;
            _writerQueue = writerQueue;
        }

        public void StartWrite()
        {
            new Thread(() => Write()).Start();
        }

        private void Write()
        {
            while(!isStopped || _writerQueue.GetCount() > 0)
            {
                var block = _writerQueue.Dequeue();
                _resultStream.Write(block.Buffer, 0, block.Buffer.Length);
            }
        }

        public void Stop()
        {

        }
    }
}
