using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace ReadoutConsole
{
    class Readout
    {
        private static Readout instance;

        public const int BufferSize = 4096;
        public IPEndPoint FPGAEndPoint { private set; get; }
        private TcpClient tcpclient;        
        private NetworkStream networkstream;
        private byte[] buffer;        

        private Readout() 
        {
            var FPGAAddress = IPAddress.Parse("192.168.10.16");
            var port = 24;
            FPGAEndPoint = new IPEndPoint(FPGAAddress, port);                                    
            buffer = new byte[BufferSize];
            for (int i = 0; i < BufferSize; i++)
                buffer[i] = 0x00;            
        }
        
        public static Readout Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Readout();
                }
                return instance;
            }
        }

        public void Connect()
        {
            tcpclient = new TcpClient();
            //tcpclient.SendTimeout = 100;
            tcpclient.SendTimeout = 500;
            tcpclient.ReceiveBufferSize = 104857600;
            Console.WriteLine("Buffer size: {0}", tcpclient.ReceiveBufferSize);
            tcpclient.Connect(FPGAEndPoint);
            networkstream = tcpclient.GetStream();
            //networkstream.ReadTimeout = 100;             
            networkstream.ReadTimeout = 2000;
            
        }

        public byte[] Read(int size)
        {            
            var tmpsize = size;
            System.IO.MemoryStream ms = new System.IO.MemoryStream();

            while (tmpsize > 0)
            {
                try
                {
                    var ressize = networkstream.Read(buffer, 0, Math.Min(BufferSize, tmpsize));
                    ms.Write(buffer, 0, ressize);
                    /*
                    if(Program.MY_DEBUG)
                        Console.WriteLine("Ressize: {0}", ressize);
                    */
                    tmpsize -= ressize;
                } catch (Exception e)
                {
                    throw e;
                }            
            }

            var ret_bytes = ms.ToArray();
            ms.Close();

            return ret_bytes;
        }

        public void Read(DataContainer dc, int length)
        {
            var tmpsize = length*Program.DataUnit;
            while (tmpsize > 0)
            {
                try
                {
                    var ressize = networkstream.Read(buffer, 0, Math.Min(BufferSize, tmpsize));
                    dc.Write(buffer, ressize);
                    //if (Program.MY_DEBUG)
                    //Console.WriteLine("Ressize: {0}", ressize);
                    tmpsize -= ressize;
                } catch (Exception e)
                {
                    throw e;
                    /*
                    if (Program.MY_DEBUG)
                    {
                        Console.WriteLine("Connection failed, RETRY!");
                        Console.WriteLine(e.ToString());
                    }*/
                }                
            }            
        }
        
        public void Clean_slow()
        {
            try
            {
                while (true)
                    networkstream.Read(buffer, 0, BufferSize);
            }
            catch (Exception e)
            {
                if (Program.MY_DEBUG)
                    Console.WriteLine("Buffer cleared");
                return;
            }                
        }

        public void Clean()
        {
            int nres;
            int total_count = 0;
            while (networkstream.DataAvailable)
            {
                nres = networkstream.Read(buffer, 0, buffer.Length);
                total_count += nres;
            }
            if(Program.MY_DEBUG)
                Console.WriteLine("Clean count: {0}", total_count);
            buffer = new byte[BufferSize];
        }

        public void Clean2()
        {
            int nres;
            int total_count = 0;
            do
            {
                nres = networkstream.Read(buffer, 0, buffer.Length);
                total_count += nres;
            } while (nres > 0);
            if (Program.MY_DEBUG)
                Console.WriteLine("Clean 2 count: {0}", total_count);
            buffer = new byte[BufferSize];
        }

        public void Close()
        {
            networkstream.Close();
            tcpclient.Close();
        }
    }
}
