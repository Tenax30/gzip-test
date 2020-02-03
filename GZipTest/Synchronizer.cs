namespace GzipTest
{
    class Synchronizer
    {
        public object ReadLocker { get; private set; }
        public object WriteLocker { get; private set; }
        public int NextId { get; private set; }
        private int _freeId;

        public Synchronizer()
        {
            ReadLocker = new object();
            WriteLocker = new object();
            NextId = _freeId = 0;
        }

        public int GetFreeId()
        {
            return _freeId++;
        }

        public void IncreaseNextId()
        {
            NextId++;
        }
    }
}
