using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GzipTest
{
    class Block
    {
        public byte[] Buffer;
        public int Id { get; private set; }

        public Block(int id)
        {
            Id = id;
        }
    }
}
