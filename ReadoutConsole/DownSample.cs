using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReadoutConsole
{
    class DownSample
    {
        private static DownSample instance;
        private RBCP _rbcp;

        private DownSample()
        {
            _rbcp = RBCP.Instance;
        }

        public static DownSample Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DownSample();
                }
                return instance;
            }
        }

        public void SetRate(int rate_num)
        {
            byte[] ds_addr = { 0x61, 0x00, 0x00, 0x00 };
            _rbcp.Write(ds_addr, BitConverter.GetBytes(rate_num).Reverse().ToArray());
        }
    }
}
