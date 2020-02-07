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
        private const int BlockSize = 1048576;

        private readonly ProgressBar _progressBar;

        private readonly Reader _reader;
        private readonly Writer _writer;

        private readonly NumberedQueue _readerQueue;
        private readonly NumberedQueue _writerQueue;

        public Exception FatalException { get; private set; }

        public Compressor(FileStream sourceStream, FileStream resultStream, ProgressBar progressBar)
        {
            _readerQueue = new NumberedQueue();
            _writerQueue = new NumberedQueue();

            _reader = new Reader(sourceStream, _readerQueue, BlockSize);
            _writer = new Writer(resultStream, _writerQueue);

            _progressBar = progressBar;

            FatalException = null;
        }

        #region Compression

        public void StartCompression()
        {
            _reader.StartRead();
            _writer.StartWrite();

            var compressionThreads = new List<Thread>();

            for (int i = 0; i < Environment.ProcessorCount / 4; i++)
            {
                var compressionThread = new Thread(() => Compress());

                compressionThreads.Add(compressionThread);
            }

            compressionThreads.ForEach(t => t.Start());
            compressionThreads.ForEach(t => t.Join());
        }

        private void TryCompress(Synchronizer synchronizer, List<Thread> compressionThreads)
        {
            try
            {
                try
                {
                    //Compress(synchronizer);
                }
                catch (Exception ex)
                {
                    if(ex is ThreadAbortException)
                    {
                        throw;
                    }

                    lock(synchronizer.ExceptionLocker)
                    {
                        if(Thread.CurrentThread.ThreadState == ThreadState.AbortRequested)
                        {
                            return;
                        }

                        FatalException = ex;

                        foreach(var thread in compressionThreads)
                        {
                           if(thread != Thread.CurrentThread)
                           {
                                thread.Abort();
                           }
                        }
                    }
                }
            }
            catch(ThreadAbortException)
            {
            }
        }

        private void Compress()
        {
            while (!_reader.IsFinished && _readerQueue.GetCount() > 0)
            {
                var block = _readerQueue.Dequeue();

                byte[] compressedBytes;

                using (var memoryStream = new MemoryStream())
                {
                    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                    {
                        gzipStream.Write(block.Buffer, 0, block.Buffer.Length);
                    }

                    compressedBytes = memoryStream.ToArray();
                }

                BitConverter.GetBytes(compressedBytes.Length).CopyTo(compressedBytes, 4);

                block.Buffer = compressedBytes;

                _writerQueue.Enqueue(block);

                //_progressBar.ShowProgress(_sourceStream.Position, _sourceStream.Length);
            }
        }

        #endregion

        //#region Decompression

        //private const int CompressedBlockInfoLength = 8;
        //private const int CheckBytesLength = 4;

        //public void StartDecompression()
        //{
        //    var synchronizer = new Synchronizer();

        //    var decompressionThreads = new List<Thread>();

        //    for (int i = 0; i < Environment.ProcessorCount; i++)
        //    {
        //        var decompressionThread = new Thread(() => TryDecompress(synchronizer, decompressionThreads));

        //        decompressionThreads.Add(decompressionThread);
        //    }

        //    decompressionThreads.ForEach(t => t.Start());
        //    decompressionThreads.ForEach(t => t.Join());
        //}

        //private void TryDecompress(Synchronizer synchronizer, List<Thread> decompressionThreads)
        //{
        //    try
        //    {
        //        try
        //        {
        //            Decompress(synchronizer);
        //        }
        //        catch (Exception ex)
        //        {
        //            if (ex is ThreadAbortException)
        //            {
        //                throw;
        //            }

        //            lock (synchronizer.ExceptionLocker)
        //            {
        //                if (Thread.CurrentThread.ThreadState == ThreadState.AbortRequested)
        //                {
        //                    return;
        //                }

        //                FatalException = ex;

        //                foreach (var thread in decompressionThreads)
        //                {
        //                    if (thread != Thread.CurrentThread)
        //                    {
        //                        thread.Abort();
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (ThreadAbortException)
        //    {
        //    }
        //}

        //private void Decompress(Synchronizer synchronizer)
        //{
        //    while (true)
        //    {
        //        int currentId;

        //        byte[] compressedBytes;
        //        byte[] blockInfoBytes = new byte[CompressedBlockInfoLength];

        //        lock (synchronizer.ReadLocker)
        //        {
        //            long ureadedBytesLength = _sourceStream.Length - _sourceStream.Position;

        //            if (ureadedBytesLength == 0)
        //            {
        //                break;
        //            }

        //            _sourceStream.Read(blockInfoBytes, 0, blockInfoBytes.Length);

        //            bool isValidBlock = CheckCompressedBlock(blockInfoBytes.Take(CheckBytesLength).ToArray());

        //            if (!isValidBlock)
        //            {
        //                throw new FileCorruptException();
        //            }

        //            int compressedBytesLength = BitConverter.ToInt32(blockInfoBytes, 4);
        //            compressedBytes = new byte[compressedBytesLength];
        //            blockInfoBytes.CopyTo(compressedBytes, 0);
        //            _sourceStream.Read(compressedBytes, 8, compressedBytes.Length - 8);

        //            currentId = synchronizer.GetFreeId();
        //        }

        //        byte[] decompressedBytes;

        //        using (var compressedMemoryStream = new MemoryStream(compressedBytes))
        //        using (var gzipStream = new GZipStream(compressedMemoryStream, CompressionMode.Decompress))
        //        using (var decompressedMemoryStream = new MemoryStream())
        //        {
        //            gzipStream.CopyTo(decompressedMemoryStream);
        //            decompressedBytes = decompressedMemoryStream.ToArray();
        //        }

        //        while (true)
        //        {
        //            lock (synchronizer.WriteLocker)
        //            {
        //                if (currentId == synchronizer.NextId)
        //                {
        //                    _resultStream.Write(decompressedBytes, 0, decompressedBytes.Length);

        //                    synchronizer.IncreaseNextId();

        //                    Monitor.PulseAll(synchronizer.WriteLocker);

        //                    break;
        //                }
        //                else
        //                {
        //                    Monitor.Wait(synchronizer.WriteLocker);
        //                }
        //            }
        //        }

        //        _progressBar.ShowProgress(_sourceStream.Position, _sourceStream.Length);
        //    }
        //}

        //private bool CheckCompressedBlock(byte[] checkInfoBytes)
        //{
        //    const int validCompressedBlockInfo = 559903;

        //    int currentCompressedBlockInfo = BitConverter.ToInt32(checkInfoBytes, 0);

        //    if(validCompressedBlockInfo != currentCompressedBlockInfo)
        //    {
        //        return false;
        //    }

        //    return true;
        //}

        //#endregion
    }
}