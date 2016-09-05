using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ReadoutConsole
{
    class IQInfo
    {
        public double[] Is;
        public double[] Qs;
        public IQInfo(int length)
        {
            Is = new double[length];
            Qs = new double[length];
        }
    }

    class DataContainer
    {
        public byte[] data;
        public int WriteIndex { private set; get; }
        public int Length { private set; get; }

        public DateTime StartDateTime;                        
        public IQInfo[] IQArray;

        public long[] TSArray;

        public int DataLength
        {
            get
            {
                return WriteIndex/Program.DataUnit;
            }
        }
        public int ConvertedLength { private set; get; }

        public DataContainer(int length)
        {
            Length = length;
            data = new byte[length*Program.DataUnit];
            WriteIndex = 0;
            ConvertedLength = 0;           
            IQArray = new IQInfo[Program.NumberOfChannels];
            Enumerable.Range(0, Program.NumberOfChannels).ToList().ForEach(i => { IQArray[i] = new IQInfo(length); });
            TSArray = new long[length];
        }

        public void Write(byte[] buff, int byte_length)
        {
            Array.Copy(buff, 0, data, WriteIndex, byte_length);
            WriteIndex += byte_length;
        }

        public void Convert()
        {
            var tmpdl = DataLength;            
            if (tmpdl == ConvertedLength)
                return;
            for(;ConvertedLength < tmpdl; ConvertedLength++)
            {
                //DataUtils.BinaryToIQ(data, ConvertedLength * Program.DataUnit, Is, Qs, ConvertedLength);
                DataUtils.BinaryToIQ(data, ConvertedLength * Program.DataUnit, IQArray, ConvertedLength);
                TSArray[ConvertedLength] = DataUtils.BEByte5ToLong(data, ConvertedLength * Program.DataUnit + 1);
            }
        }

        public void Convert_multi()
        {
            var tmpdl = DataLength;
            if (tmpdl == ConvertedLength)
                return;            
            Parallel.For(ConvertedLength, tmpdl, i =>
            {                
                DataUtils.BinaryToIQ(data, i * Program.DataUnit, IQArray, i);
                TSArray[i] = DataUtils.BEByte5ToLong(data, i * Program.DataUnit + 1);
            });            
            ConvertedLength = tmpdl;
        }

        public bool NeedToBeConverted()
        {
            return !(DataLength == ConvertedLength);
        }

        public void WriteConvert(byte[] buff, int byte_length)
        {
            Write(buff, byte_length);
            Convert();
        }

        public List<string> Express()
        {
            Convert();
            var expression = Enumerable.Range(0, ConvertedLength).Select(i =>
            {
                List<string> ts = new List<string> { TSArray[i].ToString() } ;
                List<string> Is_str = Enumerable.Range(0, Program.NumberOfChannels).Select(ch => IQArray[ch].Is[i].ToString()).ToList();
                List<string> Qs_str = Enumerable.Range(0, Program.NumberOfChannels).Select(ch => IQArray[ch].Qs[i].ToString()).ToList();
                ts.AddRange(Is_str); ts.AddRange(Qs_str);
                return string.Join(" ", ts);
            });

            return expression.ToList();
        }
    }

    class DataUtils
    {
        public static long BEByte7ToLong(byte[] d, int start_index)
        {
            byte[] done = new byte[8];
            if ((d[start_index] & 0x80) == 0x80)
            {
                done[0] = 0xff;
                Array.Copy(d, start_index, done, 1, 7);
            }
            else
            {
                done[0] = 0x00;
                Array.Copy(d, start_index, done, 1, 7);
            }
            return System.BitConverter.ToInt64(done.Reverse().ToArray(), 0);
        }

        public static long BEByte5ToLong(byte[] d, int start_index)
        {
            byte[] done = new byte[8];
            if ((d[start_index] & 0x80) == 0x80)
            {
                done[0] = 0xff; done[1] = 0xff; done[2] = 0xff;
                Array.Copy(d, start_index, done, 3, 5);
            }
            else
            {
                done[0] = 0x00; done[0] = 0x00; done[0] = 0x00;
                Array.Copy(d, start_index, done, 3, 5);
            }
            return System.BitConverter.ToInt64(done.Reverse().ToArray(), 0);
        }

        public static double[][] BinaryToIQ(byte[] d, int offset)
        {
            var Is = new double[Program.NumberOfChannels];
            var Qs = new double[Program.NumberOfChannels];
            Enumerable.Range(0, Program.NumberOfChannels).ToList().ForEach(i =>
            {
                Is[i] = BEByte7ToLong(d, offset + 6 + 7 * Program.NumberOfChannels * i) / Program.DownSampleRate;
                Qs[i] = BEByte7ToLong(d, offset + 13 + 7 * Program.NumberOfChannels * i) / Program.DownSampleRate;
            });
            return new double[][] { Is, Qs };
        }

        public static void BinaryToIQ(byte[] d, int offset, double[,] target_I, double[,] target_Q, int target_offset)
        {
            Enumerable.Range(0, Program.NumberOfChannels).ToList().ForEach(i =>
            {
                target_I[i,target_offset] = BEByte7ToLong(d, offset + 6 + 7 * Program.NumberOfChannels * i) / Program.DownSampleRate;
                target_Q[i,target_offset] = BEByte7ToLong(d, offset + 13 + 7 * Program.NumberOfChannels * i) / Program.DownSampleRate;
            });
        }

        /*
        public static void BinaryToIQ(byte[] d, int offset, IQInfo[] IQArray)
        {
            Enumerable.Range(0, Program.NumberOfChannels).ToList().ForEach(i =>
            {
                IQArray[i].Is.Add(BEByte7ToLong(d, offset + 6 + 7 * Program.NumberOfChannels * i) / Program.DownSampleRate);
                IQArray[i].Qs.Add(BEByte7ToLong(d, offset + 13 + 7 * Program.NumberOfChannels * i) / Program.DownSampleRate);                
            });
        }*/

        public static void BinaryToIQ(byte[] d, int offset, IQInfo[] IQArray, int index)
        {
            Enumerable.Range(0, Program.NumberOfChannels).ToList().ForEach(i =>
            {
                IQArray[i].Is[index] = BEByte7ToLong(d, offset + 6 + 7 * Program.NumberOfChannels * i);
                IQArray[i].Qs[index] = BEByte7ToLong(d, offset + 13 + 7 * Program.NumberOfChannels * i);
            });
        }

    }
}
