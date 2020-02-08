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

        private Reader _reader;
        private Writer _writer;

        private bool _isAborted = false;
        private object _locker = new object();
        private CompressionMode _mode;

        public Exception FatalException { get; set; } = null;

        public Compressor(FileStream sourceStream, FileStream resultStream, ProgressBar progressBar)
        {
            _sourceStream = sourceStream;
            _resultStream = resultStream;
            _progressBar = progressBar;
        }

        public void StartCompression()
        {
            _mode = CompressionMode.Compress;
            Start();
        }

        public void StartDecompression()
        {
            _mode = CompressionMode.Decompress;
            Start();
        }

        private void Start()
        {
            _reader = new Reader(_sourceStream, _mode);
            _writer = new Writer(_resultStream);

            _reader.StartRead();
            _writer.StartWrite();

            var threads = new List<Thread>();

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                var thread = new Thread(() => TryWork(threads));

                threads.Add(thread);
            }

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join());

            SetExceptions();
            if (FatalException == null)
                _writer.Stop();
        }

        private void TryWork(List<Thread> compressionThreads)
        {
            try
            {
                if(_mode == CompressionMode.Compress)
                {
                    Compress();
                }
                else if(_mode == CompressionMode.Decompress)
                {
                    Decompress();
                }
            }
            catch(ThreadAbortException)
            {
            }
            catch(Exception ex)
            {
                lock(_locker)
                {
                    if(_isAborted)
                    {
                        return;
                    }

                    FatalException = ex;
                    _reader.Stop();
                    _writer.Stop();
                    _isAborted = true;

                    foreach (var thread in compressionThreads)
                    {
                        if (thread != Thread.CurrentThread)
                        {
                            thread.Abort();
                        }
                    }
                }
            }
        }

        private void Compress()
        {
            while ((!_reader.IsStopped || _reader.GetBlockCount() > 0) && !_writer.IsStopped)
            {
                var block = _reader.GetBlock();

                if (block == null)
                    break;

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

                _writer.PushBlock(block);

                _progressBar.ShowProgress(_sourceStream.Position, _sourceStream.Length);
            }
        }

        private void Decompress()
        {
            while ((!_reader.IsStopped || _reader.GetBlockCount() > 0) && !_writer.IsStopped)
            {
                var block = _reader.GetBlock();

                if (block == null)
                    break;

                byte[] decompressedBytes;

                using (var compressedMemoryStream = new MemoryStream(block.Buffer))
                using (var gzipStream = new GZipStream(compressedMemoryStream, CompressionMode.Decompress))
                using (var decompressedMemoryStream = new MemoryStream())
                {
                    gzipStream.CopyTo(decompressedMemoryStream);
                    decompressedBytes = decompressedMemoryStream.ToArray();
                }

                block.Buffer = decompressedBytes;

                _writer.PushBlock(block);

                _progressBar.ShowProgress(_sourceStream.Position, _sourceStream.Length);
            }
        }

        private void SetExceptions()
        {
            if(_reader.FatalException != null)
            {
                FatalException = _reader.FatalException;
                _writer.Stop();
            }
            else if(_writer.FatalException != null)
            {
                FatalException = _writer.FatalException;
                _reader.Stop();
            }
        }
    }
}