using GzipTest.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GzipTest
{
    class Reader
    {
        private const int BlockSize = 1048576;
        private const int CompressedBlockInfoLength = 8;
        private const int CheckBytesLength = 4;

        private readonly FileStream _sourceStream;
        private readonly CompressionMode _mode;

        private readonly ReaderQueue _readerQueue;

        private int _freeId = 0;

        public bool IsStopped { get; set; } = false;

        public Exception FatalException = null;

        public Reader(FileStream sourceStream,  CompressionMode mode)
        {
            _sourceStream = sourceStream;
            _mode = mode;

            _readerQueue = new ReaderQueue();
        }

        public void StartRead()
        {
            new Thread(() => Read()).Start();
        }

        private void Read()
        {
            try
            {
                byte[] readBytes;

                long unreadedBytesLength = _sourceStream.Length - _sourceStream.Position;

                while (unreadedBytesLength > 0 && !IsStopped)
                {
                    if (_mode == CompressionMode.Compress)
                    {
                        readBytes = new byte[Math.Min(unreadedBytesLength, BlockSize)];
                        _sourceStream.Read(readBytes, 0, readBytes.Length);
                    }
                    else
                    {
                        byte[] blockInfoBytes = new byte[CompressedBlockInfoLength];
                        _sourceStream.Read(blockInfoBytes, 0, blockInfoBytes.Length);

                        bool isValidBlock = CheckCompressedBlock(blockInfoBytes.Take(CheckBytesLength).ToArray());

                        if (!isValidBlock)
                        {
                            throw new FileCorruptException();
                        }

                        int readBytesLength = BitConverter.ToInt32(blockInfoBytes, 4);

                        readBytes = new byte[readBytesLength];
                        blockInfoBytes.CopyTo(readBytes, 0);
                        _sourceStream.Read(readBytes, 8, readBytes.Length - 8);
                    }

                    var block = new Block(_freeId++);
                    block.Buffer = readBytes;

                    _readerQueue.Enqueue(block);

                    unreadedBytesLength = _sourceStream.Length - _sourceStream.Position;
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

        public Block GetBlock()
        {
            return _readerQueue.Dequeue();
        }

        public int GetBlockCount()
        {
            return _readerQueue.GetCount();
        }

        public void Stop()
        {
            IsStopped = true;
            _readerQueue.Unlock();
        }

        private bool CheckCompressedBlock(byte[] checkInfoBytes)
        {
            const int validCompressedBlockInfo = 559903;

            int currentCompressedBlockInfo = BitConverter.ToInt32(checkInfoBytes, 0);

            if (validCompressedBlockInfo != currentCompressedBlockInfo)
            {
                return false;
            }

            return true;
        }

        
    }
}
