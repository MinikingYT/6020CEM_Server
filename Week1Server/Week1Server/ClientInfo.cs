using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Week1Server
{
    public class ClientInfo
    {
        public DateTime LastHeartbeat { get; set; }
        public byte[] GameState { get; set; }


    }
}
