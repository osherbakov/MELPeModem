using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MELPeModem
{
    class OFDMSync : DataProcessingModule
    {
        InputPin<IQ> DataIn;
        OutputPin<IQ> DataOut;

        int BlockSize;
        int FFTSize;
        int CPSize;

        int DataSize;

        int PutIndex;
        int SlicingIndex = 0;
        int PreviousSlicingIndex;
        
        IQ[] DataBuffer;
        float []CorrBuffer;
        float[] DiffFIRBuffer;
        float[] DiffIIRBuffer;
        float PrevValue = 0;
        FIR DiffFIR;

        float DiffIIR;
        float Alpha, OneMinusAlpha;

        float SNRValue;

        Queue<IQ> OutputData = new Queue<IQ>();

        public OFDMSync(int interpDecimFactor)
        {
            BlockSize = interpDecimFactor;

            // Calculate the size of the FFT Windows.
            // Guard Time will be BlockSize - FFTSize
            FFTSize = 1;
            while (FFTSize < BlockSize)
            {
                FFTSize <<= 1;
            }
            FFTSize >>= 1;
            CPSize = BlockSize - FFTSize;
            Init();
        }


        public float SNR
        {
            get { return SNRValue; }
            set { SNRValue = value; }
        }
        public override void Init()
        {
            PutIndex = 0;
            SNRValue = 1.0f;
            DataSize = 2 * BlockSize + CPSize;
            DataBuffer = new IQ[DataSize];

            Alpha = 2.0f / CPSize;
            OneMinusAlpha = 1.0f - Alpha;

            CorrBuffer = new float[BlockSize];
            DiffFIRBuffer = new float[BlockSize];
            DiffIIRBuffer = new float[BlockSize];

            PrevValue = 0;
            DiffIIR = 0;

            float[] coeffs = new float[CPSize / 2];
            for (int i = 0; i < CPSize / 2; i++)
            {
                coeffs[i] = 2.0f / CPSize;
            }
            DiffFIR = new FIR(coeffs, 1);

            OutputData.Clear();
        }

        public void StartCorrectionProcess(int currentIndex)
        {
            PreviousSlicingIndex = currentIndex;
            Array.Clear(DataBuffer, 0, DataSize);
            Init();
        }

        bool CalculateIndexCorrection(out bool skipNext)
        {
            bool getCurrent = false;
            skipNext = false;
            // if points are close to each other - no correction is needed
            if (Math.Abs(SlicingIndex - PreviousSlicingIndex) * 2 < BlockSize)
            {
            }
            else if (SlicingIndex > PreviousSlicingIndex)   // Samples are too far from each other
            {
                getCurrent = true;
            }
            else       //  Samples are too close to each other
            {
                skipNext = true;
            }
            return getCurrent;
        }

        void CalculateCorrections()
        {
            // Calculate the initial correlation value
            float Gamma = 0;
            float Energy = 0;
            IQ Sample;
            IQ ShiftedSample;

            IQ PrevSample, PrevShiftedSample;
            float Value;
            float Diff;

            int Idx = 0;
            int ShiftedIdx = FFTSize;
            for (; Idx < CPSize; Idx++, ShiftedIdx++)
            {
                Sample = DataBuffer[Idx];
                ShiftedSample = DataBuffer[ShiftedIdx];
                Gamma += (Sample / ShiftedSample).R2;
                Energy += (Sample * Sample).R2 + (ShiftedSample * ShiftedSample).R2;
            }

            Value = Gamma - 0.5f * SNRValue * Energy;
            Diff = Value - PrevValue;
            CorrBuffer[0] = Value;

            DiffFIR.Process(Diff, out DiffFIRBuffer[0]);
            DiffIIR = OneMinusAlpha * DiffIIR + Alpha * Diff;
            DiffIIRBuffer[0] = DiffIIR;

            PrevValue = Value;
            // Now, calculate all other correlations
            Idx = CPSize;
            ShiftedIdx = BlockSize;
            for (int i = 1; i < BlockSize; i++, Idx++, ShiftedIdx++)
            {
                Sample = DataBuffer[Idx]; 
                ShiftedSample = DataBuffer[ShiftedIdx];
                PrevSample = DataBuffer[i-1]; 
                PrevShiftedSample = DataBuffer[i - 1 + FFTSize];
                Gamma += (Sample / ShiftedSample).R2 - 
                                (PrevSample / PrevShiftedSample).R2;
                Energy += (Sample * Sample).R2 + (ShiftedSample * ShiftedSample).R2 - 
                                ((PrevSample * PrevSample).R2 + (PrevShiftedSample * PrevShiftedSample).R2);
                Value = Gamma - 0.5f * SNRValue * Energy;
                Diff = Value - PrevValue;
                
                CorrBuffer[i] = Value;
                DiffFIR.Process(Diff, out DiffFIRBuffer[i]);

                DiffIIR = OneMinusAlpha * DiffIIR + Alpha * Diff;
                DiffIIRBuffer[i] = DiffIIR;

                PrevValue = Value;
            }
        }

        public int Process(IQ inData)
        {
            if (PutIndex < DataSize)
            {
                DataBuffer[PutIndex] = inData;
            }
            PutIndex++;
            if (PutIndex == DataSize)
            {
                CalculateCorrections();
            }
            return OutputData.Count;
        }

        public void Process(CNTRL_MSG controlParam, IQ inData)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                if (PutIndex < DataSize)
                {
                    DataBuffer[PutIndex] = inData;
                }
                PutIndex++;
                if (PutIndex == DataSize)
                {
                    CalculateCorrections();
                    foreach (IQ IQData in OutputData) DataOut.Process(IQData);
                }
            }
        }


        public int Process(IQ[] inDataArray, int startIndex, int numToProcess)
        {
            for (int i = 0; i < numToProcess; i++)
            {
                Process(inDataArray[startIndex++]);
            }
            return OutputData.Count;
        }

        public int Process(float[] inDataArrayI, float[] inDataArrayQ, int startIndex, int numToProcess)
        {
            IQ Data;
            for (int i = 0; i < numToProcess; i++, startIndex++)
            {
                Data.I = inDataArrayI[startIndex];
                Data.Q = inDataArrayQ[startIndex];
                Process(Data);
            }
            return OutputData.Count;
        }

        public bool IsSyncReady
        {
            get { return (PutIndex >= DataSize); }
        }

        public int Count
        {
            get
            {
                return OutputData.Count;
            }
        }

        public IQ GetData()
        {
            return OutputData.Dequeue();
        }

        public int GetData(IQ[] outputArray)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outputArray, 0);
            OutputData.Clear();
            return ret;
        }

        public int GetData(IQ[] outputArray, int arrayIndex)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outputArray, arrayIndex);
            OutputData.Clear();
            return ret;
        }


        public int SymbolCorrection
        {
            get
            {
                return ((PutIndex - 1) - SlicingIndex) % BlockSize;
            }
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<IQ>("DataIn", this.Process);
            DataOut = new OutputPin<IQ>("DataOut");
            base.SetIOParameters("OFDM Timing Offset Estimator", new DataPin[] { DataIn, DataOut });
        }
    }
}
