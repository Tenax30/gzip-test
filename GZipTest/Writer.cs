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
        private WriterQueue _writerQueue;

        public bool IsStopped { get; private set; }

        public Exception FatalException { get; set; } = null;

        public Writer(FileStream resultStream)
        {
            _resultStream = resultStream;

            _writerQueue = new WriterQueue();
        }

        public void StartWrite()
        {
            new Thread(() => Write()).Start();
        }

        private void Write()
        {
            try
            {
                while (!IsStopped || _writerQueue.GetCount() > 0)
                {
                    var block = _writerQueue.Dequeue();
                    if(block != null)
                    {
                        _resultStream.Write(block.Buffer, 0, block.Buffer.Length);
                    }
                }
            }
            catch(Exception ex)
            {
                FatalException = ex;
            }
            finally
            {
                Stop();
            }
        }

        public void PushBlock(Block block)
        {
            _writerQueue.Enqueue(block);
        }

        public void Stop()
        {
            _writerQueue.Unlock();
            IsStopped = true;
        }
    }
}
