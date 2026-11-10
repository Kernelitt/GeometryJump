using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryJump.Level
{
    struct Block()
    {
        public int X, Y, Layer, Id;
        public float Rotation;
    }

    struct Level()
    {
        public required string Name, Description, Author;
        public int SongId = 0;
        public required Block[] blocks;
    }
}
