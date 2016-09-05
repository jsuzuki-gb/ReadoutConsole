using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReadoutConsole
{
    class DACControl
    {
        private static DACControl instance;
        private RBCP _rbcp;

        private byte[] dac_address = { 0x00, 0x01, 0x02, 0x03, 0x04,       0x06, 0x07,
                                       0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
                                       0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x14,  // 0x14 appears twice because of the requirement of the DAC chip
                                       0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1B, 0x1F}; // 0x1B appears twice because of the requirement of the DAC chip

        private byte[] dac_i_data  = { 0x70, 0x01, 0x00, 0x13, 0xff,       0x00, 0x00,
                                       0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                       0x00, 0x24, 0x02, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00,
                                       0x83, 0x00, 0x00, 0x00, 0x00, 0x00, 0x24, 0x00, 0x12 };

        private byte[] dac_addr_prefix_wr = { 0x20, 0x00, 0x00 };
        private byte[] dac_addr_prefix_rd = { 0x21, 0x00, 0x00 };
        
        private DACControl()
        {
            _rbcp = RBCP.Instance;
        }

        public static DACControl Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DACControl();
                }
                return instance;
            }
        }

        private byte[] DACAddress(byte internal_addresss, bool isWrite = true)
        {
            var addr = new byte[4];
            if(isWrite)
                Array.Copy(dac_addr_prefix_wr, addr, dac_addr_prefix_wr.Length);
            else
                Array.Copy(dac_addr_prefix_rd, addr, dac_addr_prefix_wr.Length);

            addr[3] = internal_addresss;
            return addr;
        }

        public void DAC_4ena()
        {
            _rbcp.Write(DACAddress(0x17), new byte[] { 0x04 });
        }

        public void TestmodeOff()
        {
            var rddata = RBCP.Interpret(_rbcp.Read(DACAddress(0x01)));
            byte pt = (byte)(rddata.Item2[0] & 0xfb);
            _rbcp.Write(DACAddress(0x01), new byte[] { pt });
        }

        public void TXEnableOn()
        {
            byte[] addr = { 0x22, 0x00, 0x00, 0x00 };
            _rbcp.Write(addr, new byte[] { 0x01 });
        }

        public void TXEnableOff()
        {
            byte[] addr = { 0x22, 0x00, 0x00, 0x00 };
            _rbcp.Write(addr, new byte[] { 0x00 });
        }

        public void TrigFrame()
        {
            byte[] addr = { 0x22, 0x00, 0x00, 0x01 };
            _rbcp.Write(addr, new byte[] { 0x01 });
        }

        public void DACChannelSwap()
        {
            byte[] channel_swap_addr = { 0x22, 0x00, 0x00, 0x02 };
            var data = _rbcp.Read(channel_swap_addr);
            byte[] write_data = new byte[1];
            Console.WriteLine("Swap data: {0}", RBCP.Interpret(data).Item2[0]);
            Console.WriteLine("Swap data bool: {0}", RBCP.Interpret(data).Item2[0] == 0);
            if (RBCP.Interpret(data).Item2[0] == 0)
                write_data[0] = 1;
            else
                write_data[0] = 0;
            _rbcp.Write(channel_swap_addr, write_data);
        }

        public void DACChannelSwap(bool swap)
        {
            byte[] channel_swap_addr = { 0x22, 0x00, 0x00, 0x02 };
            if (swap)
                _rbcp.Write(channel_swap_addr, new byte[] { 0x01 });
            else
                _rbcp.Write(channel_swap_addr, new byte[] { 0x00 });
        }

        public void DACNormalInput()
        {
            byte[] addr = { 0x22, 0x00, 0x00, 0x03 };
            _rbcp.Write(addr, new byte[] { 0x00 });
        }

        public List<byte[]> Write(byte[] reg_addr, byte[] reg_data)
        {
            DAC_4ena();
            if (reg_addr.Length != reg_data.Length)
                throw new Exception("Invarid use of DAC write method");
            var length = reg_addr.Length;
            var result_list = new List<byte[]>();
            for (int i = 0; i < length; i++)
            {
                var tmpaddr = DACAddress(reg_addr[i]);
                var result = _rbcp.Write(tmpaddr, new byte[]{reg_data[i]});
                result_list.Add(result);
            }
            return result_list;
        }

        public byte[] Write(byte reg_addr, byte[] reg_data)
        {
            return _rbcp.Write(DACAddress(reg_addr), reg_data);
        }

        public List<byte[]> Read(byte[] reg_addr)
        {
            DAC_4ena();
            var length = reg_addr.Length;
            var result_list = new List<byte[]>();
            for (int i = 0; i < length; i++)
            {
                var tmpaddr = DACAddress(reg_addr[i], false);
                var result = _rbcp.Read(tmpaddr);
                result_list.Add(result);
            }
            return result_list;
        }

        public void DACReset() 
        {
            Write(dac_address, dac_i_data);
            DAC_4ena();
            TestmodeOff();

            TXEnableOff();
            TrigFrame();

            DACChannelSwap(false);
            DACNormalInput();
        }

        public void DACRegisterInitialize()
        {
            Write(new byte[] { 0x01, 0x13 }, new byte[] { 0x01, 0xc0 });
        }

        public int DACTemperature()
        {
            var temperature = Read(new byte[]{0x05})[0];
            return (int)temperature[8];
        }
    }
}
