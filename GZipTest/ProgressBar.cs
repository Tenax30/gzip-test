using System;
using System.Threading;

namespace GzipTest
{
    class ProgressBar
    {
        private TimerCallback timerCallback;
        private Timer timer;

        private bool _isReady;
        private object _locker;

        public ProgressBar()
        {
            timerCallback = new TimerCallback(SetReadiness);
            timer = new Timer(timerCallback, null, 0, 2000);

            _isReady = true;

            _locker = new object();
        }

        public void ShowProgress(long processedBytes, long totalBytes)
        {
            lock(_locker)
            {
                if (_isReady)
                {
                    Console.WriteLine($"Processed {processedBytes / 1024}/{totalBytes / 1024} KB");
                    _isReady = false;
                }
            }
        }

        private void SetReadiness(object _)
        {
            _isReady = true;
        }
    }
}
