using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReadoutConsole
{
    public class Program
    {
        public static bool GUI_DEBUG = false;

        public static bool MY_DEBUG = true;
        public static int NumberOfChannels = 2;
        public static double ADCSampleRate = 200e6;
        public static double DownSampleRate = 2e5;
        public static int DataUnit = NumberOfChannels * 14 + 7;
        public static bool Verbose = true;
        public static int SoftwareDownsampleCount = 20;

        public static DateTime UnixEpoch = new DateTime(1970, 1, 1, 9, 0, 0);

        static List<DataContainer> DCList;

        public static void init()
        {
            var rbcp = RBCP.Instance;
            var adc = ADCControl.Instance;
            var dac = DACControl.Instance;
            var ds = DownSample.Instance;
            var ss = Snapshot.Instance;

            adc.ADCWriteEnable();
            dac.DAC_4ena();
            dac.TestmodeOff();

            //adc.ADCReset();
            //dac.DACReset();

            //dac.DACChannelSwap();
            //adc.ADCChannelSwap();
            dac.TXEnableOn();
            ds.SetRate((int)DownSampleRate);
            rbcp.ToggleIQDataGate(false);
            ss.ToggleSnap(false);

            var readout = Readout.Instance;
            readout.Connect();
            readout.Clean();

            DCList = new List<DataContainer>();
        }


        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            init();            

            var ow = OutputWave.Instance;
            var freq = -21.7e6;
            //int data_length = 10000;

            ow.SetFrequency(0, freq, dds_en: false);
            ow.SetFrequency(1, freq, dds_en: true);

            var readout = Readout.Instance;
            var rbcp = RBCP.Instance;
            readout.Clean();
            /*
            var dc = new DataContainer(data_length);
            dc.StartDateTime = DateTime.Now;
            rbcp.ToggleIQDataGate(true);
            readout.Read(dc, data_length);
            rbcp.ToggleIQDataGate(false);

            Console.WriteLine("DAQ Elapsed time: {0}", (DateTime.Now - dc.StartDateTime).TotalSeconds);

            var conv_start = DateTime.Now;
            dc.Convert();
            var conv_end = DateTime.Now;

            Console.WriteLine("Convert Elapsed time: {0}", (conv_end - conv_start).TotalSeconds);
            using(var sw = new System.IO.StreamWriter("tod.dat"))
            {
                for(int i = 0; i < dc.ConvertedLength; i++)
                {
                    sw.WriteLine("{0} {1} {2}", dc.TSList[0], dc.IQArray[0].Is[i], dc.IQArray[0].Qs[i]);
                }                
            }
            */

            /*
            List<double> freq_data = new List<double>();
            List<double> sweep_data = new List<double>();

            var bw = new System.IO.BinaryWriter(System.IO.File.OpenWrite("sweep.bin"));

            for(double f = -100e6; f < 100e6; f += 1e6)
            {
                Console.WriteLine("Sweep: {0} MHz", f/1e6);
                ow.SetFrequency(0, f, dds_en: false);
                ow.SetFrequency(1, f, dds_en: true);
                
                //readout.Clean();
                //readout.Clean();
                System.Threading.Thread.Sleep(20);
                readout.Clean();
                
                //readout.Clean_slow();
                var tmpdc = new DataContainer(10);
                tmpdc.StartDateTime = DateTime.Now;
                rbcp.ToggleIQDataGate(true);
                readout.Read(tmpdc, 10);
                rbcp.ToggleIQDataGate(false);
                tmpdc.Convert();

                var Is = tmpdc.IQArray[0].Is;
                var Qs = tmpdc.IQArray[0].Qs;
                var power = Enumerable.Range(0, Is.Count).Select(i => Math.Sqrt(Is[i] * Is[i] + Qs[i] * Qs[i])).Average();
                sweep_data.Add(power);
                freq_data.Add(f);
                bw.Write(tmpdc.data);
            }

            using(var sw = new System.IO.StreamWriter("sweep.dat"))
            {
                for (int i = 0; i < freq_data.Count; i++)
                    sw.WriteLine("{0} {1}", freq_data[i], sweep_data[i]);
            }
            */
            
            var ds = DownSample.Instance;
            ds.SetRate(200);

            readout.Clean();
            System.Threading.Thread.Sleep(200);
            readout.Clean();
            /*
            var dc = new DataContainer(data_length * 10);
            dc.StartDateTime = DateTime.Now;
            
            rbcp.ToggleIQDataGate(true);
            readout.Read(dc, data_length * 10);
            rbcp.ToggleIQDataGate(false);
            */
            var startdt = DateTime.Now;
            //Task.Run(()=>Reader());
            ReaderAsync().Wait();
            
            Console.WriteLine("DAQ Elapsed time: {0}", (DateTime.Now - startdt).TotalSeconds);

            
            //var conv_start = DateTime.Now;
            //dc.Convert();
            /*
            Parallel.ForEach(DCList, dc =>
            {
                dc.Convert();
            });
            */
            
            //var conv_end = DateTime.Now;

            //Console.WriteLine("Convert Elapsed time: {0}", (conv_end - conv_start).TotalSeconds);
            
            
            var da_start = DateTime.Now;
            foreach (var dc in DCList)
            {
                var fname = String.Format("tod_{0:F2}.dat", dc.StartDateTime - new DateTime(1970, 1, 1, 9, 0, 0));
                using (var sw = new System.IO.StreamWriter("tod.dat"))
                {
                    for (int i = 0; i < dc.ConvertedLength; i++)
                    {
                        sw.WriteLine("{0} {1} {2}", dc.TSArray[i], dc.IQArray[0].Is[i], dc.IQArray[0].Qs[i]);
                    }
                }
            }

            var da_end = DateTime.Now;
            Console.WriteLine("Disk access time: {0}", (da_end - da_start).TotalSeconds);
            
        }

        public static int totake = 5;
        public static int datacount = 0;
        public static int dclength = 1000000;
        public static int analyzecount = 0;

        public static List<Task> tasklist;

        static async Task ReaderAsync()
        {
            tasklist = new List<Task>();
            var rbcp = RBCP.Instance;
            var readout = Readout.Instance;

            while(datacount < totake)
            {
                try
                {
                    var dc = new DataContainer(dclength);
                    dc.StartDateTime = DateTime.Now;
                    rbcp.ToggleIQDataGate(true);
                    readout.Read(dc, dc.Length);                    
                    rbcp.ToggleIQDataGate(false);
                    DCList.Add(dc);
                    tasklist.Add(Task.Run(() => dc.Convert()));                    
                    datacount++;
                    Console.WriteLine(datacount);
                } catch
                {                    
                    Console.WriteLine("Retry connection...");
                    rbcp.ToggleIQDataGate(false);
                    readout.Close();
                    System.Threading.Thread.Sleep(500);
                    readout.Connect();
                    readout.Clean();
                }                
            }
            await Task.WhenAll(tasklist);
            Console.WriteLine("Finished!!");
        }
    }
}
