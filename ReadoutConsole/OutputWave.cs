using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReadoutConsole
{
    class OutputWave
    {
        private static OutputWave instance;
        private RBCP _rbcp;
        private long width_factor = (long)1 << 32;


        private OutputWave()
        {
            _rbcp = RBCP.Instance;
        }

        public static OutputWave Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new OutputWave();
                }
                return instance;
            }
        }        

        public void WritePhase(int channel, double frequency, double phase)
        {
            /*
            // Address
            byte mae = (byte)(channel >> 4);
            byte ato = (byte)((channel - (mae << 4)) << 4);
            var address = new byte[] {0x40, 0x00, mae, ato};
            // Phase offset
            var phase_offset = (long)((phase / 360.0) * width_factor);
            var poff_bytes = System.BitConverter.GetBytes(phase_offset).Take(4).ToList();            
            // Phase increment
            var phase_increment = (long)(frequency % Program.ADCSampleRate / Program.ADCSampleRate * width_factor);
            var pinc_bytes = System.BitConverter.GetBytes(phase_increment).Take(4).ToList();                        
            */
            short towrite = (short)(4 * (channel) + 1);                        
            var address = new List<byte> { 0x40, 0x00 };
            address.AddRange(BitConverter.GetBytes(towrite).Reverse());
            // Write
            var target_freq = frequency > 0 ? frequency : (frequency % Program.ADCSampleRate) + Program.ADCSampleRate;
            Console.WriteLine("DEBUG freq: {0} Hz", target_freq);
            var phase_increment = (long)( target_freq/Program.ADCSampleRate * width_factor);
            Console.WriteLine("DEBUG pinc: {0}", phase_increment);
            var pinc_bytes = System.BitConverter.GetBytes(phase_increment).Take(4).ToList();
            pinc_bytes.Reverse();
            Console.WriteLine("DEBUG pinc bytes: {0}", BitConverter.ToString(pinc_bytes.ToArray()));
            

            var wr_data = pinc_bytes.ToArray();
            var ret_data = _rbcp.Write(address.ToArray(), wr_data);
            var intprt_data = RBCP.Interpret(ret_data);
            if (!address.SequenceEqual(intprt_data.Item1) || 
                !wr_data.SequenceEqual(intprt_data.Item2))
                throw new Exception("Wrote <-> Read do not match");
            _rbcp.Write(new byte[] { 0x70, 0, 0, 0 }, new byte[] { 0x01 });
        }

        public void SetFrequency(int channel, double frequency, double phase = 0, bool dds_en = true)
        {
            WritePhase(channel, frequency, phase);
            if (dds_en)
                _rbcp.DDSEnable();
        }

        public void SetFrequency(double frequency, double phase = 0, bool dds_en = true)
        {
            for (int i = 0; i < Program.NumberOfChannels; i++)
            {
                WritePhase(i, frequency, phase);
            }
            if (dds_en)
                _rbcp.DDSEnable();
        }
    }
}
