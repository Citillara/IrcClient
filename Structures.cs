using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Irc
{
    public struct ModeChange
    {
        public string Name;
        public bool IsAdded;
        public char Mode;
        public bool IsGlobalMode;
    }
}
