using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using GzipTest.Exceptions;

namespace GzipTest
{
    class Compressor
    {
        private readonly FileStream _sourceStream;
        private readonly FileStream _resultStream;
        private readonly ProgressBar _progressBar;

        public Compressor(FileStream sourceStream, FileStream resultStream, ProgressBar progressBar)
        {
            _sourceStream = sourceStream;
            _resultStream = resultStream;
            _progressBar = progressBar;
        }

        #region Compression

        private const int BlockSize = 1048576;

        public void StartCompression()
        {
            var synchronizer = new Synchronizer();

            var compressionThreads = new List<Thread>();

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                var compressionThread = new Thread(() => Compress(synchronizer));

                compressionThreads.Add(compressionThread);
                compressionThread.Start();
            }

            compressionThreads.ForEach(t => t.Join());
        }

        private void Compress(Synchronizer synchronizer)
        {
            while (true)
            {
                int currentId;

                byte[] readBytes;

                lock (synchronizer.ReadLocker)
                {
                    long ureadedBytesLength = _sourceStream.Length - _sourceStream.Position;

                    if (ureadedBytesLength == 0)
                    {
                        break;
                    }

                    readBytes = new byte[Math.Min(ureadedBytesLength, BlockSize)];
                    _sourceStream.Read(readBytes, 0, readBytes.Length);

                    currentId = synchronizer.GetFreeId();
                }

                byte[] compressedBytes;

                using (var memoryStream = new MemoryStream())
                {
                    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                    {
                        gzipStream.Write(readBytes, 0, readBytes.Length);
                    }

                    compressedBytes = memoryStream.ToArray();
                }

                BitConverter.GetBytes(compressedBytes.Length).CopyTo(compressedBytes, 4);

                while (true)
                {
                    lock (synchronizer.WriteLocker)
                    {
                        if (currentId == synchronizer.NextId)
                        {
                            _resultStream.Write(compressedBytes, 0, compressedBytes.Length);

                            synchronizer.IncreaseNextId();

                            Monitor.PulseAll(synchronizer.WriteLocker);

                            break;
                        }
                        else
                        {
                            Monitor.Wait(synchronizer.WriteLocker);
                        }
                    }
                }

                _progressBar.ShowProgress(_sourceStream.Position, _sourceStream.Length);
            }
        }

        #endregion

        #region Decompression

        private const int CompressedBlockInfoLength = 8;
        private const int CheckBytesLength = 4;

        public void StartDecompression()
        {
            var synchronizer = new Synchronizer();

            var decompressionThreads = new List<Thread>();

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                var decompressionThread = new Thread(() => Decompress(synchronizer));

                decompressionThreads.Add(decompressionThread);
                decompressionThread.Start();
            }

            decompressionThreads.ForEach(t => t.Join());
        }

        private void Decompress(Synchronizer synchronizer)
        {
            while (true)
            {
                int currentId;

                byte[] compressedBytes;
                byte[] blockInfoBytes = new byte[CompressedBlockInfoLength];

                lock (synchronizer.ReadLocker)
                {
                    long ureadedBytesLength = _sourceStream.Length - _sourceStream.Position;

                    if (ureadedBytesLength == 0)
                    {
                        break;
                    }

                    _sourceStream.Read(blockInfoBytes, 0, blockInfoBytes.Length);

                    bool isValidBlock = CheckCompressedBlock(blockInfoBytes.Take(CheckBytesLength).ToArray());

                    if (!isValidBlock)
                    {
                        throw new FileCorruptException();
                    }

                    int compressedBytesLength = BitConverter.ToInt32(blockInfoBytes, 4);
                    compressedBytes = new byte[compressedBytesLength];
                    blockInfoBytes.CopyTo(compressedBytes, 0);
                    _sourceStream.Read(compressedBytes, 8, compressedBytes.Length - 8);

                    currentId = synchronizer.GetFreeId();
                }

                byte[] decompressedBytes;

                using (var compressedMemoryStream = new MemoryStream(compressedBytes))
                using (var gzipStream = new GZipStream(compressedMemoryStream, CompressionMode.Decompress))
                using (var decompressedMemoryStream = new MemoryStream())
                {
                    gzipStream.CopyTo(decompressedMemoryStream);
                    decompressedBytes = decompressedMemoryStream.ToArray();
                }

                while (true)
                {
                    lock (synchronizer.WriteLocker)
                    {
                        if (currentId == synchronizer.NextId)
                        {
                            _resultStream.Write(decompressedBytes, 0, decompressedBytes.Length);

                            synchronizer.IncreaseNextId();

                            Monitor.PulseAll(synchronizer.WriteLocker);

                            break;
                        }
                        else
                        {
                            Monitor.Wait(synchronizer.WriteLocker);
                        }
                    }
                }

                _progressBar.ShowProgress(_sourceStream.Position, _sourceStream.Length);
            }
        }

        private bool CheckCompressedBlock(byte[] checkInfoBytes)
        {
            const int validCompressedBlockInfo = 559903;

            int currentCompressedBlockInfo = BitConverter.ToInt32(checkInfoBytes, 0);

            if(validCompressedBlockInfo != currentCompressedBlockInfo)
            {
                return false;
            }

            return true;
        }

        #endregion
    }
}