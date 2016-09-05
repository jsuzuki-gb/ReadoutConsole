using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReadoutConsole
{
    class DCWrapper : DataContainer
    {
        // SD means Software Downsampling
        private int sdCount;

        public int SDCount
        {
            set
            {
                if (sdCount == value)
                    return;
                sdCount = value;
                tmpI = new double[sdCount];
                tmpQ = new double[sdCount];
            }
            get
            {
                return sdCount;
            }
        }

        public IQInfo[] SDIQArray;
        public int SDIndex;

        private double[] tmpI;
        private double[] tmpQ;       

        public DCWrapper(int length) : base(length)
        {
            SDCount = Program.SoftwareDownsampleCount;
            
            SDIndex = 0;
            SDIQArray = new IQInfo[Program.NumberOfChannels];
            Enumerable.Range(0, Program.NumberOfChannels).ToList().ForEach(i => { SDIQArray[i] = new IQInfo(length); });
        }

        public void SoftwareDownsample(bool useMedian = false)
        {
            if (SDIndex == ConvertedLength/SDCount )
                return;
            for (; SDIndex < ConvertedLength / SDCount; SDIndex++)
            {
                Enumerable.Range(0, Program.NumberOfChannels).ToList().ForEach(i => {
                    double[] iq;
                    if (useMedian)
                        iq = SDValueMedian(SDIndex, i);
                    else
                        iq = SDValue(SDIndex, i);
                    
                    SDIQArray[i].Is[SDIndex] = iq[0];
                    SDIQArray[i].Qs[SDIndex] = iq[1];
                });
            }            
        }

        private double[] SDValue(int sd_index, int channel)
        {
            double tmpI = 0;
            double tmpQ = 0;
            for(int i = 0; i < SDCount; i++)
            {
                tmpI += this.IQArray[channel].Is[sd_index * SDCount + i];
                tmpQ += this.IQArray[channel].Qs[sd_index * SDCount + i];
            }
            return new double[]{tmpI/SDCount, tmpQ/SDCount};
        }

        private double[] SDValueMedian(int sd_index, int channel)
        {
            
            for (int i = 0; i < SDCount; i++)
            {
                tmpI[i] = this.IQArray[channel].Is[sd_index * SDCount + i];                
                tmpQ[i] = this.IQArray[channel].Qs[sd_index * SDCount + i];                
            }            
            int halfIndex = sdCount / 2;
            var sortedIs = tmpI.OrderBy(n => n);
            var sortedQs = tmpQ.OrderBy(n => n);
            
            if ((sdCount % 2) == 0)
            {
                return new double[] {(sortedIs.ElementAt(halfIndex) + sortedIs.ElementAt(halfIndex + 1))/ 2.0 , 
                                     (sortedQs.ElementAt(halfIndex) + sortedQs.ElementAt(halfIndex + 1))/ 2.0};
            }
            else
            {
                return new double[] { sortedIs.ElementAt(halfIndex), sortedQs.ElementAt(halfIndex) };
            }
        }
    }
}
