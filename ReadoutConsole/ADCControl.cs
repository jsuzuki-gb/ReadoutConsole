using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReadoutConsole
{
    class ADCControl
    {
        private static ADCControl instance;
        private RBCP _rbcp;

        private byte[] adc_address = { 0x00, 0x01, 0x03, 0x25, 0x29, 0x2b, 0x3d, 0x3f, 0x40, 0x41, 0x42, 0x45, 0x4a, 0x58, 0xbf, 0xc1, 0xcf, 0xef, 0xf1, 0xf2, 0x02, 0xd5, 0xd7, 0xdb };
        private byte[] adc_addr_prefix_wr = { 0x10, 0x00, 0x00 };
        private byte[] adc_addr_prefix_rd = { 0x11, 0x00, 0x00 };

        public bool ADCReadEnabled { private set; get; }
        public bool ADCWriteEnabled { private set; get; }        

        private ADCControl()
        {
            _rbcp = RBCP.Instance;
            ADCReadEnabled = false;
            ADCWriteEnabled = false;
        }

        public static ADCControl Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ADCControl();
                }
                return instance;
            }
        }

        private byte[] ADCAddress(byte internal_addresss, bool isWrite=true)
        {
            var addr = new byte[4];
            if(isWrite)
                Array.Copy(adc_addr_prefix_wr, addr, adc_addr_prefix_wr.Length);
            else
                Array.Copy(adc_addr_prefix_rd, addr, adc_addr_prefix_wr.Length);

            addr[3] = internal_addresss;
            return addr;
        }

        public void ADCReset()
        {            
            _rbcp.Write(ADCAddress(0x00), new byte[] { 0x02 });
            ADCWriteEnabled = true;
            ADCChannelSwap(false);
        }

        public void ADCReadEnable() 
        {
            if (!ADCReadEnabled) 
            {
                byte[] addr = ADCAddress(0x00);
                byte[] data = { 0x01 };
                _rbcp.Write(addr, data);
                ADCReadEnabled = true; ADCWriteEnabled = false;
            }            
        }        

        public void ADCWriteEnable()
        {
            if (!ADCWriteEnabled)
            {
                byte[] addr = ADCAddress(0x00);
                byte[] data = { 0x00 };
                _rbcp.Write(addr, data);
                ADCWriteEnabled = true; ADCReadEnabled = false;
            }
        }

        public List<byte[]> Write(byte[] reg_addr, byte[] reg_data)
        {            
            if (reg_addr.Length != reg_data.Length)
                throw new Exception("Invarid use of ADC write method");

            ADCWriteEnable();
            var length = reg_addr.Length;
            var result_list = new List<byte[]>();
            for (int i = 0; i < length; i++)
            {
                var tmpaddr = ADCAddress(reg_addr[i]);
                var result = _rbcp.Write(tmpaddr, new byte[] {reg_data[i]});
                result_list.Add(result);
            }
            return result_list;
        }

        public byte[] Write(byte reg_addr, byte[] reg_data)
        {
            return _rbcp.Write(ADCAddress(reg_addr), reg_data);
        }

        public List<byte[]> Read(byte[] reg_addr)
        {
            var length = reg_addr.Length;
            var result_list = new List<byte[]>();
            for (int i = 0; i < length; i++)
            {
                var tmpaddr = ADCAddress(reg_addr[i], false);
                var result = _rbcp.Read(tmpaddr);
                result_list.Add(result);
            }
            return result_list;
        }

        public void Clean() 
        {
            var clean_data = new byte[adc_address.Length];
            for (int i = 0; i < clean_data.Length; i++)
                clean_data[i] = 0;
            Write(adc_address.Skip(1).ToArray(), 
                  clean_data.Skip(1).ToArray());
        }

        public void ADCRegisterInitialize()
        {
            Write(0x42, new byte[] { 0xf8 });
        }

        public void ADCChannelSwap()
        {
            byte[] channel_swap_addr = { 0x12, 0x00, 0x00, 0x00 };
            var data = _rbcp.Read(channel_swap_addr);
            byte[] write_data = new byte[1];
            if ( RBCP.Interpret(data).Item2[0] == 0)
                write_data[0] = 1;
            else
                write_data[0] = 0;
            _rbcp.Write(channel_swap_addr, write_data);
        }

        public void ADCChannelSwap(bool swap)
        {
            byte[] channel_swap_addr = { 0x12, 0x00, 0x00, 0x00 };
            if (swap)
                _rbcp.Write(channel_swap_addr, new byte[] { 0x01 });
            else
                _rbcp.Write(channel_swap_addr, new byte[] { 0x00 });
        }
    }
}
