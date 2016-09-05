using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReadoutConsole
{
    enum SnapSrc : byte
    {
        DDS, DAC, ADC, IQ, TEST
    }

    class Snapshot
    {
        private static Snapshot instance;
        private RBCP _rbcp;

        private Snapshot()
        {
            _rbcp = RBCP.Instance;
        }

        public static Snapshot Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Snapshot();
                }
                return instance;
            }
        }

        public void ToggleSnap(bool on)
        {
            byte[] snap_switch_addr = { 0x30, 0x00, 0x00, 0x00 };
            _rbcp.Write(snap_switch_addr, new byte[] { Convert.ToByte(on) });
        }

        public void FIFORest()
        {
            byte[] fifo_reset_addr = { 0x30, 0x00, 0x00, 0x01 };
            _rbcp.Write(fifo_reset_addr, new byte[] { 1 });
        }

        public void SetSrc(SnapSrc src)
        {
            byte[] src_addr = { 0x31, 0x00, 0x00, 0x00 };
            _rbcp.Write(src_addr, new byte[] { (byte)src });
        }

        public void SetSrc(SnapSrc src, byte channel)
        {
            SetSrc(src);
            byte[] ch_addr = { 0x31, 0x00, 0x00, 0x01 };
            _rbcp.Write(ch_addr, new byte[] { channel });
        }

        public SnapSrc GetSrc()
        {
            byte[] src_addr = { 0x31, 0x00, 0x00, 0x00 };
            var rb = _rbcp.Read(src_addr)[0];
            return (SnapSrc)rb;
        }

        public byte GetChannel()
        {
            byte[] ch_addr = { 0x31, 0x00, 0x00, 0x01 };
            return _rbcp.Read(ch_addr)[0];
        }
    }
}
