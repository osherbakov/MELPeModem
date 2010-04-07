using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    [Flags]
    public enum SYNC_TYPE : byte
    {
        NONE = 0x00,
        GARDNER_NDA = 0x01,
        GARDNER_DD = 0x02,
        DIFF_NDA = 0x04,
        ZERODET_NDA = 0x08,
        QAMLD_NDA = 0x10,
        PEAK_NDA = 0x20,
        CORR_NDA = 0x40,
        MUELLER_NDA = 0x80
    }

    class SymbolSync : DataProcessingModule
    {
        InputPin<IQ> DataIn;
        OutputPin<IQ> DataOut;

        SYNC_TYPE SyncType = SYNC_TYPE.GARDNER_NDA | SYNC_TYPE.ZERODET_NDA | SYNC_TYPE.QAMLD_NDA | SYNC_TYPE.CORR_NDA ;
        int DecimFactor;
        int HalfPoint;
        int DataSize;
        int NumSymbols;
        int PutIndex;
        int SlicingIndex;
        int PreviousSlicingIndex;
        bool SkipSample;
        IQ[] DataBuffer;

        float IQEnergyDifference = 5;

        Queue<IQ> OutputData =  new Queue<IQ>();

        public SymbolSync(int interpDecimFactor)
        {
            DecimFactor = interpDecimFactor;
            HalfPoint = DecimFactor / 2; 
            Init();
        }

        public SymbolSync(SYNC_TYPE sType, int interpDecimFactor )
        {
            SyncType = sType;
            DecimFactor = interpDecimFactor;
            HalfPoint = DecimFactor / 2;
            Init();
        }

        public override void Init()
        {
            PutIndex = 0;
            SkipSample = false;
            OutputData.Clear();
        }

        public void StartCorrectionProcess(SYNC_TYPE sType, int numberOfSymbols, int currentIndex)
        {
            SyncType = sType;
            NumSymbols = numberOfSymbols;
            PreviousSlicingIndex = (DecimFactor - 1) - currentIndex;

            // If array is NOT same size - create it, otherwise re-use it
            if (numberOfSymbols * DecimFactor != DataSize)
            {
                DataSize = numberOfSymbols * DecimFactor;
                DataBuffer = new IQ[DataSize];
            }
            else
            {
                Array.Clear(DataBuffer, 0, DataSize);
            }
            Init();
        }

        public void StartCorrectionProcess(int numberOfSymbols, int currentIndex)
        {
            StartCorrectionProcess(SyncType, numberOfSymbols, currentIndex);
        }

        bool CalculateIndexCorrection(out bool skipNext)
        {
            bool getCurrent = false;
            skipNext = false;
            // if points are close to each other - no correction is needed
            if (Math.Abs(SlicingIndex - PreviousSlicingIndex) * 2 < DecimFactor)
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

        public int Process(IQ inData)
        {
            if (PutIndex < DataSize)
            {
                DataBuffer[PutIndex] = inData;
            }else if ((PutIndex % DecimFactor) == SlicingIndex)
            {
                OutputData.Enqueue(inData);
            }
            PutIndex++;
            if (PutIndex == DataSize)
            {
                CalculateCorrections();
            }
            return OutputData.Count;
        }

        public void Process(CNTRL_MSG controlParam,  IQ inData)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                if (PutIndex < DataSize)
                {
                    DataBuffer[PutIndex] = inData;
                }else if ((PutIndex % DecimFactor) == SlicingIndex)
                {
                    DataOut.Process(inData);
                }
                PutIndex++;
                if (PutIndex == DataSize)
                {
                    CalculateCorrections();
                    foreach (IQ IQData in OutputData) DataOut.Process(IQData);
                }
            }
        }


        public int Process(IQ []inDataArray, int startIndex, int numToProcess)
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

        const float a = 0.508f;       // Coefficient for RRC mid-point
        const float b = -0.1f;        //   at the end of the symbol interval

        int [] CalculateSyncOffset(float [] inputData)
        {
            float minvalGNDA = float.MaxValue;
            float minvalGDD = float.MaxValue;
            float minvalDiff = float.MaxValue;
            float minvalQAMLD = float.MaxValue;
            float minvalZDD = float.MaxValue;
            float maxvalPeak = float.MinValue;
            float maxvalCorr = float.MinValue;

            int BestOffsetGNDA = 0;
            int BestOffsetGDD = 0;
            int BestOffsetDiff = 0;
            int BestOffsetQAMLD = 0;
            int BestOffsetZDD = 0;
            int BestOffsetPeak = 0;
            int BestOffsetCorr = 0;

            for (int SamlplingIdx = 0; SamlplingIdx < DecimFactor; SamlplingIdx++)
            {
                float GardnerNDA = 0;
                float GardnerDD = 0;
                float Diff = 0;
                float ZDD = 0;
                float QAMLD = 0;
                float Peak = 0;
                float Corr = 0;
                int nTran = 0;

                for (int EvenIdx = SamlplingIdx + DecimFactor; EvenIdx < DataSize; EvenIdx += DecimFactor)
                {
                    int OddIdx = EvenIdx - HalfPoint;
                    float PrevEven = inputData[EvenIdx - DecimFactor];  // Previous Samples are at y(n-2)
                    float Odd = inputData[OddIdx];         // Odd samples are y(n-1)
                    float Even = inputData[EvenIdx];                    // Even samples are y(n)
                    float EstPrevEven = Math.Sign(PrevEven);            // Estimated previous
                    float EstEven = Math.Sign(Even);                    // Estimated current
                    // PrevEven = y(n-2)
                    // The correct sample decision point will be  EVEN!!!!!!!!
                    // When you see Sign(x) - that means that it is a decision (estimate)
                    // Techically, you can replace it with decision result, which can be
                    //  +1, -1 in case of BPSK, or anything in case of QAM and QPSK.

                    Corr += 2 * Even * PrevEven * b +
                                 // Even * Even +       // Do not include the Energy total
                                Odd * (Even + PrevEven) * a;
                    // Calculate correction on transitions/sign changes only
                    if ((Even * PrevEven) < 0)
                    {
                        float AB = 0;
                        float AA = 0;
                        for (int j = 0; j < HalfPoint; j++)
                        {
                            AB += inputData[OddIdx - j];    // Add all samples Before Odd
                            AA += inputData[OddIdx + j];    // Add all samples After Odd
                        }
                        // Data-directed (DD)
                        Peak += (AB * EstPrevEven + AA * EstEven);
                        GardnerDD += Math.Abs(Odd * (EstEven - EstPrevEven));

                        // Non-data assisted (NDA)
                        GardnerNDA += Math.Abs(Odd * (Even - PrevEven));
                        QAMLD += Math.Abs(0.5f * (Even + PrevEven) - Odd);
                        Diff += Math.Abs(AB * Even - AA * PrevEven);
                        ZDD += Math.Abs(Odd);
                        nTran++;                            // Increment the number of transitions
                    }
                }

                GardnerNDA /= nTran;
                GardnerDD /= nTran;
                QAMLD /= nTran;
                Diff /= nTran;
                Peak /= nTran;
                ZDD /= nTran;

                if (Math.Abs(GardnerDD) < minvalGDD)
                {
                    minvalGDD = Math.Abs(GardnerDD);
                    BestOffsetGDD = SamlplingIdx;
                }
                if (Math.Abs(GardnerNDA) < minvalGNDA)
                {
                    minvalGNDA = Math.Abs(GardnerNDA);
                    BestOffsetGNDA = SamlplingIdx;
                }
                if (Math.Abs(Diff) < minvalDiff)
                {
                    minvalDiff = Math.Abs(Diff);
                    BestOffsetDiff = SamlplingIdx;
                }
                if (Math.Abs(ZDD) < minvalZDD)
                {
                    minvalZDD = Math.Abs(ZDD);
                    BestOffsetZDD = SamlplingIdx;
                }
                if (Math.Abs(QAMLD) < minvalQAMLD)
                {
                    minvalQAMLD = Math.Abs(QAMLD);
                    BestOffsetQAMLD = SamlplingIdx;
                }
                if (Math.Abs(Peak) > maxvalPeak)
                {
                    maxvalPeak = Math.Abs(Peak);
                    BestOffsetPeak = SamlplingIdx;
                }
                if (Corr > maxvalCorr)
                {
                    maxvalCorr = Corr;
                    BestOffsetCorr = SamlplingIdx;
                }
            }

            List<int> li = new List<int>();
            if ( (SyncType & SYNC_TYPE.GARDNER_DD) != 0) li.Add(BestOffsetGDD);
            if ( (SyncType & SYNC_TYPE.GARDNER_NDA) != 0) li.Add(BestOffsetGNDA);
            if ( (SyncType & SYNC_TYPE.DIFF_NDA) != 0) li.Add(BestOffsetDiff);
            if ( (SyncType & SYNC_TYPE.PEAK_NDA) != 0) li.Add(BestOffsetPeak);
            if ( (SyncType & SYNC_TYPE.QAMLD_NDA) != 0) li.Add(BestOffsetQAMLD);
            if ( (SyncType & SYNC_TYPE.ZERODET_NDA) != 0) li.Add(BestOffsetZDD);
            if ( (SyncType & SYNC_TYPE.CORR_NDA) != 0) li.Add(BestOffsetCorr);

            return li.ToArray();
        }

        int Average(int [] values)
        {
            // Find the average value that has the smallest total distance from all points (best fit)
            int numElem = values.Length;
            int result = 0;
            int MinDist = int.MaxValue;
            for (int TestVal = 0; TestVal < DecimFactor; TestVal++)
            {
                int Sum = 0;
                for (int i = 0; i < numElem; i++)
                {
                    int Dist = Math.Abs(TestVal - values[i]);
                    if (Dist > HalfPoint) Dist = DecimFactor - Dist;
                    Sum += Dist;
                }
                if (Sum < MinDist)
                {
                    MinDist = Sum;
                    result = TestVal;
                }
            }
            return result;
        }

        void CalculateCorrections()
        {
            int result;
            float EnergyI, EnergyQ;
            float I, Q;
            float[] ProcessDataI = new float[DataSize];
            float[] ProcessDataQ = new float[DataSize];

            EnergyI = 0;
            EnergyQ = 0;

            for (int i = 0; i < DataSize; i++)
            {
                IQ val = DataBuffer[i];
                I = val.I;
                Q = val.Q;
                EnergyI += I * I;
                EnergyQ += Q * Q;
                ProcessDataI[i] = I;
                ProcessDataQ[i] = Q;
            }

            // Use either I or Q component with the highest energy, or combine both
            if (EnergyI > IQEnergyDifference * EnergyQ)
            {
                result = Average(CalculateSyncOffset(ProcessDataI));
            }
            else if (EnergyQ > IQEnergyDifference * EnergyI)
            {
                result = Average(CalculateSyncOffset(ProcessDataQ));
            }
            else
            {
                // When I and Q are pretty close - combine them
                int [] rI = CalculateSyncOffset(ProcessDataI);
                int [] rQ = CalculateSyncOffset(ProcessDataQ);
                int []r = new int[rI.Length + rQ.Length];
                Array.Copy(rI, 0, r, 0, rI.Length);
                Array.Copy(rQ, 0, r, rI.Length, rQ.Length);
                result = Average(r);
            }
            SlicingIndex = result;
            if (CalculateIndexCorrection(out SkipSample))
            {
                OutputData.Enqueue(DataBuffer[0]);
            }
            for (int i = SlicingIndex; i < DataSize; i += DecimFactor)
            {
                if (SkipSample)
                {
                    SkipSample = false;
                }
                else
                {
                    OutputData.Enqueue(DataBuffer[i]);
                }
            }
        }

        public float IQEnergyDiff
        {
            get { return IQEnergyDifference; }
            set { IQEnergyDifference = value; }
        }

        public int SymbolCorrection
        {
            get 
            {
                return ((PutIndex - 1) - SlicingIndex ) % DecimFactor; 
            }
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<IQ>("DataIn", this.Process);
            DataOut = new OutputPin<IQ>("DataOut");
            base.SetIOParameters("SymbolSyncSlicer", new DataPin[] { DataIn, DataOut });
        }
    }


    class SymbolSlicer : DataProcessingModule
    {
        InputPin<IQ> DataIn;
        OutputPin<IQ> DataOut;

        SYNC_TYPE SyncType;
        int DecimFactor;
        int HalfPoint;
        int DataSize;
        int NumSymbols;
        Index PutIndex;
        int PutCounter;
        bool SkipSample;
        int SlicingIndex;      // The index at which we will take a slice 
        IQ FrameEnergy;

        float IQEnergyDifference = 5;

        IQ[] DataBuffer;
        Queue<IQ> OutputData;

        // Best values for the slicer
        float []GNDAI;
        float []GDDI;
        float []QAMLDI;
        float []ZDDI;
        float []CorrI;

        float []GNDAQ;
        float []GDDQ;
        float []QAMLDQ;
        float []ZDDQ;
        float []CorrQ;


        public SymbolSlicer(int interpDecimFactor, int numberOfSymbols)
            : this(SYNC_TYPE.GARDNER_NDA | SYNC_TYPE.ZERODET_NDA | SYNC_TYPE.QAMLD_NDA | SYNC_TYPE.CORR_NDA, interpDecimFactor, numberOfSymbols)
        {
        }

        public SymbolSlicer(SYNC_TYPE sType, int interpDecimFactor, int numberOfSymbols)
        {
            SyncType = sType;
            DecimFactor = interpDecimFactor;
            HalfPoint = DecimFactor / 2;
            NumSymbols = numberOfSymbols;
            DataSize = numberOfSymbols * DecimFactor;
            DataBuffer = new IQ[DataSize];
            OutputData = new Queue<IQ>();
            Init();
        }

        public override void Init()
        {
            SlicingIndex = 0;
            PutIndex = new Index(0, DataSize);
            PutCounter = 0;
            Array.Clear(DataBuffer, 0, DataSize);
            OutputData.Clear();
            SkipSample = false;

            FrameEnergy = IQ.ZERO;
                        
            GNDAI = new float[DecimFactor];
            GDDI = new float[DecimFactor]; ;
            QAMLDI = new float[DecimFactor]; 
            ZDDI = new float[DecimFactor];
            CorrI = new float[DecimFactor];

            GNDAQ = new float[DecimFactor];
            GDDQ = new float[DecimFactor];
            QAMLDQ = new float[DecimFactor];
            ZDDQ = new float[DecimFactor];
            CorrQ = new float[DecimFactor];
        }

        public int Process(IQ []inDataArray, int startIndex, int numToProcess)
        {
            for (int i = 0; i < numToProcess; i++)
            {
                Process(inDataArray[startIndex++]);
            }
            return OutputData.Count;
        }

        public void Process(CNTRL_MSG controlParam,  IQ inData)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                Process(inData);
            }
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
            get { return ( OutputData.Count > 0); }
        }

        public int Count
        {
            get { return OutputData.Count; }

        }

        public int SymbolsCorrection
        {
            get
            {
                return ((PutIndex - 1) - SlicingIndex) % DecimFactor;
            }
        }

        public float IQEnergyDiff
        {
            get { return IQEnergyDifference; }
            set { IQEnergyDifference = value; }
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

        const float a = 0.508f;       // Coefficient for RRC mid-point
        const float b = -0.1f;        //   at the end of the symbol interval

        void ProcessIQ()
        {
            for (int i = 0; i < DecimFactor; i++)
            {
                ProcessI(i);
                ProcessQ(i);
            }
        }

        void ProcessI(int startingIndex)
        {
            float GardnerNDA = 0;
            float GardnerDD = 0;
            float ZDD = 0;
            float QAMLD = 0;
            float Corr = 0;

            int nTran = 0;      // Transitions counter (from 0 to 1 and from 1 to 0)
            int Offset = startingIndex % DecimFactor;

            IQ PrevEvenIQ;
            IQ EvenIQ;

            // Start from the oldest samples
            PrevEvenIQ = DataBuffer[startingIndex];     // Previous Samples are at y(n-2)
            Index EvenIdx = new Index(startingIndex + DecimFactor, DataSize);
            Index OddIdx = new Index(startingIndex + HalfPoint, DataSize);

            for (int i = 0; i < (NumSymbols - 1); i++ )
            {
                EvenIQ = DataBuffer[EvenIdx];
                float Even = EvenIQ.I;                      // Even samples are y(n)
                float PrevEven = PrevEvenIQ.I;
                float Odd = DataBuffer[OddIdx].I;           // Odd samples are y(n-1)
                float EstPrevEven = Math.Sign(PrevEven);    // Estimated previous
                float EstEven = Math.Sign(Even);            // Estimated current
                // The correct sample decision point will be  EVEN!!!!!!!!
                // When you see Sign(x) - that means that it is a decision (estimate)

                Corr += 2 * Even * PrevEven * b +
                    // Even * Even +       // Do not include the Energy total
                            Odd * (Even + PrevEven) * a;
                // Calculate correction on transitions/sign changes only
                if ((Even * PrevEven) < 0)
                {
                    // Data-directed (DD)
                    GardnerDD += Math.Abs(Odd * (EstEven - EstPrevEven));
                    // Non-data assisted (NDA)
                    GardnerNDA += Math.Abs(Odd * (Even - PrevEven));
                    QAMLD += Math.Abs(0.5f * (Even + PrevEven) - Odd);
                    ZDD += Math.Abs(Odd);
                    nTran++;                            // Increment the number of transitions
                }
                EvenIdx += DecimFactor;
                OddIdx += DecimFactor;
                PrevEvenIQ = EvenIQ;
            }
            // If transitions occured - normalize the result
            if (nTran > 0)
            {
                GDDI[Offset] = GardnerDD/nTran;
                GNDAI[Offset] = GardnerNDA/nTran;
                QAMLDI[Offset] = QAMLD/nTran;
                ZDDI[Offset] = ZDD/nTran;
            }
            CorrI[Offset] = Corr;
        }

        void ProcessQ(int startingIndex)
        {
            float GardnerNDA = 0;
            float GardnerDD = 0;
            float ZDD = 0;
            float QAMLD = 0;
            float Corr = 0;

            int nTran = 0;      // Transitions counter (from 0 to 1 and from 1 to 0)
            int Offset = startingIndex % DecimFactor;

            IQ PrevEvenIQ;
            IQ EvenIQ;

            // Start from the oldest samples
            PrevEvenIQ = DataBuffer[startingIndex];     // Previous Samples are at y(n-2)
            Index EvenIdx = new Index(startingIndex + DecimFactor, DataSize);
            Index OddIdx = new Index(startingIndex + HalfPoint, DataSize);

            for (int i = 0; i < (NumSymbols - 1); i++)
            {
                EvenIQ = DataBuffer[EvenIdx];
                float Even = EvenIQ.Q;                      // Even samples are y(n)
                float PrevEven = PrevEvenIQ.Q;
                float Odd = DataBuffer[OddIdx].Q;           // Odd samples are y(n-1)
                float EstPrevEven = Math.Sign(PrevEven);    // Estimated previous
                float EstEven = Math.Sign(Even);            // Estimated current
                // The correct sample decision point will be  EVEN!!!!!!!!
                // When you see Sign(x) - that means that it is a decision (estimate)
                // Techically, you can replace it with decision result, which can be
                //  +1, -1 in case of BPSK, or anything in case of QAM and QPSK.

                Corr += 2 * Even * PrevEven * b +
                    // Even * Even +       // Do not include the Energy total
                            Odd * (Even + PrevEven) * a;
                // Calculate correction on transitions/sign changes only
                if ((Even * PrevEven) < 0)
                {
                    // Data-directed (DD)
                    GardnerDD += Math.Abs(Odd * (EstEven - EstPrevEven));
                    // Non-data assisted (NDA)
                    GardnerNDA += Math.Abs(Odd * (Even - PrevEven));
                    QAMLD += Math.Abs(0.5f * (Even + PrevEven) - Odd);
                    ZDD += Math.Abs(Odd);
                    nTran++;                            // Increment the number of transitions
                }
                EvenIdx += DecimFactor;
                OddIdx += DecimFactor;
                PrevEvenIQ = EvenIQ;
            }
            // If transitions occured - normalize the result
            if (nTran > 0)
            {
                GDDQ[Offset] = GardnerDD / nTran;
                GNDAQ[Offset] = GardnerNDA / nTran;
                QAMLDQ[Offset] = QAMLD / nTran;
                ZDDQ[Offset] = ZDD / nTran;
            }
            CorrQ[Offset] = Corr;
        }


        int Average(int [] values)
        {
            // Find the average value that has the smallest total distance from all points (best fit)
            int numElem = values.Length;
            int result = 0;
            int MinDist = int.MaxValue;
            for (int TestVal = 0; TestVal < DecimFactor; TestVal++)
            {
                int Sum = 0;
                for (int i = 0; i < numElem; i++)
                {
                    int Dist = Math.Abs(TestVal - values[i]);
                    if (Dist > HalfPoint) Dist = DecimFactor - Dist;
                    Sum += Dist;
                }
                if (Sum < MinDist)
                {
                    MinDist = Sum;
                    result = TestVal;
                }
            }
            return result;
        }


        static int MaxIndex(float[] dataArray)
        {
            float maxval = float.MinValue;
            int ret = 0;
            int Len = dataArray.Length;
            float data;
            for (int i = 0; i < Len; i++ )
            {
                data = dataArray[i];
                if (Math.Abs(data) > maxval)
                {
                    ret = i;
                    maxval = data;
                }
            }
            return ret;
        }

        static int MinIndex(float[] dataArray)
        {
            float minval = float.MaxValue;
            int ret = 0;
            int Len = dataArray.Length;
            float data;
            for (int i = 0; i < Len; i++)
            {
                data = dataArray[i];
                if (Math.Abs(data) < minval)
                {
                    ret = i;
                    minval = data;
                }
            }
            return ret;
        }


        int [] CalculateCorrectionsI()
        {
            List<int> li = new List<int>();

            if ((SyncType & SYNC_TYPE.GARDNER_DD) != 0) li.Add(MinIndex(GDDI));
            if ((SyncType & SYNC_TYPE.GARDNER_NDA) != 0) li.Add(MinIndex(GNDAI));
            if ((SyncType & SYNC_TYPE.QAMLD_NDA) != 0) li.Add(MinIndex(QAMLDI));
            if ((SyncType & SYNC_TYPE.ZERODET_NDA) != 0) li.Add(MinIndex(ZDDI));

            if ((SyncType & SYNC_TYPE.CORR_NDA) != 0) li.Add(MaxIndex(CorrI));

            return li.ToArray();
        }

        int[] CalculateCorrectionsQ()
        {
            List<int> li = new List<int>();

            if ((SyncType & SYNC_TYPE.GARDNER_DD) != 0) li.Add(MinIndex(GDDQ));
            if ((SyncType & SYNC_TYPE.GARDNER_NDA) != 0) li.Add(MinIndex(GNDAQ));
            if ((SyncType & SYNC_TYPE.QAMLD_NDA) != 0) li.Add(MinIndex(QAMLDQ));
            if ((SyncType & SYNC_TYPE.ZERODET_NDA) != 0) li.Add(MinIndex(ZDDQ));

            if ((SyncType & SYNC_TYPE.CORR_NDA) != 0) li.Add(MaxIndex(CorrQ));

            return li.ToArray();
        }


        int CalculateCorrections()
        {
            int result;
            // Use either I or Q component with the highest energy, or combine both
            if (FrameEnergy.I > (IQEnergyDifference * FrameEnergy.Q))
            {
                result = Average(CalculateCorrectionsI());
            }
            else if (FrameEnergy.Q > (IQEnergyDifference * FrameEnergy.I))
            {
                result = Average(CalculateCorrectionsQ());
            }
            else
            {
                // When I and Q are pretty close - combine them
                int[] rI = CalculateCorrectionsI();
                int[] rQ = CalculateCorrectionsQ();
                int[] r = new int[rI.Length + rQ.Length];
                Array.Copy(rI, 0, r, 0, rI.Length);
                Array.Copy(rQ, 0, r, rI.Length, rQ.Length);
                result = Average(r);
            }
            return result;
        }

        
        public int Process(IQ inData)
        {
            FrameEnergy += (inData.P - DataBuffer[PutIndex].P);
            if (PutCounter == DataSize)
            {
                // when the buffer is filled - calculate the optimum sampling offset
                DataOut.Process(CNTRL_MSG.SYMBOL_DETECTED);
                ProcessIQ();
                SlicingIndex = CalculateCorrections();

                // Now send out accumulated symbols with correct slicing
                for (int i = 0; i < NumSymbols; i++)
                {
                    IQ IQData = DataBuffer[DecimFactor * i + SlicingIndex];
                    OutputData.Enqueue(IQData);
                    DataOut.Process(IQData);
                }
                if (0 == SlicingIndex)     // Remember - currently we are at index 0
                {
                    OutputData.Enqueue(inData);
                    DataOut.Process(inData);
                }
            }

            if (PutCounter > DataSize)
            {
                // Do re-calculation on boundaries
                if ((PutCounter % DataSize) == 0)
                {
                    DataOut.Process(CNTRL_MSG.SYMBOL_DETECTED);
                    ProcessIQ();
                    int NewSamplingIndex = CalculateCorrections();
                    // Remember - currently we are at index 0
                    if (CalculateIndexCorrection(SlicingIndex, NewSamplingIndex, out SkipSample))
                    {
                        OutputData.Enqueue(inData);
                        DataOut.Process(inData);
                    }
                    SlicingIndex = NewSamplingIndex;
                }
                // On the DecimFactor boundaries: 
                if ((PutIndex % DecimFactor) == SlicingIndex)
                {
                    if (SkipSample)
                    {
                        SkipSample = false; // deactivate flag
                    }
                    else
                    {
                        // 1. send the data 
                        OutputData.Enqueue(inData);
                        DataOut.Process(inData);
                    }
                }
            }
            DataBuffer[PutIndex++] = inData;
            PutCounter++;
            return OutputData.Count;
        }

        bool CalculateIndexCorrection(int oldSamplingIdx, int newSamplingIdx, out bool skipNext)
        {
            bool getCurrent = false;
            skipNext = false;
            // if points are close to each other - no correction is needed
            if (Math.Abs(newSamplingIdx - oldSamplingIdx) * 2 < DecimFactor)
            {
            }
            else if (newSamplingIdx > oldSamplingIdx)   // Samples are too far from each other
            {   
                getCurrent = true;
            }
            else       //  Samples are too close to each other
            {
                skipNext = true;
            }
            return getCurrent;
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<IQ>("DataIn", this.Process);
            DataOut = new OutputPin<IQ>("DataOut");
            base.SetIOParameters("SymbolSlicer", new DataPin[] { DataIn, DataOut });
        }
    }

    class IntegrateAndDump : DataProcessingModule
    {
        InputPin<IQ> DataIn;
        OutputPin<IQ> DataOut;

        SYNC_TYPE SyncType;
        int DecimFactor;
        int HalfPoint;
        int ActiveSamples;
        
        int StartActive;
        int EndActive;
        
        int StartHalfPoint;
        int EndHalfPoint;

        int PutCounter;

        float IQEnergyDifference = 5;

        IQ PrevEvenIQ;
        IQ OddIQ;
        IQ EvenIQ;

        float SignalEn;
        float FrequencyEn;

        IQ OddAccum;
        IQ EvenAccum;
        IQ EnergyAccum;
        float FrameEnergyAccum;

        int SamplingOffset;

        Queue<IQ> OutputData;

        public IntegrateAndDump(int interpDecimFactor) :
            this(SYNC_TYPE.GARDNER_NDA | SYNC_TYPE.ZERODET_NDA | SYNC_TYPE.QAMLD_NDA | SYNC_TYPE.MUELLER_NDA, interpDecimFactor, interpDecimFactor)
        {
        }

        public IntegrateAndDump(SYNC_TYPE sType, int interpDecimFactor):
            this(sType, interpDecimFactor, interpDecimFactor)
        {
        }

        public IntegrateAndDump(SYNC_TYPE sType, int interpDecimFactor, int activeSamples)
        {
            SyncType = sType;
            DecimFactor = interpDecimFactor;
            ActiveSamples = activeSamples;
            HalfPoint = interpDecimFactor / 2;

            StartActive = interpDecimFactor - ActiveSamples;
            EndActive = StartActive + ActiveSamples;

            StartHalfPoint = StartActive + HalfPoint;
            EndHalfPoint = ActiveSamples / 2;

            OutputData = new Queue<IQ>();
            Init();
        }

        public override void Init()
        {
            PutCounter = 0;
            OddAccum = IQ.ZERO;
            EvenAccum = IQ.ZERO;
            PrevEvenIQ = IQ.ZERO;

            OutputData.Clear();
            EnergyAccum = IQ.ZERO;
            FrameEnergyAccum = 0;
            SamplingOffset = 0;

            FrequencyEn = 0;
            SignalEn = 0;
        }

        public int Process(IQ[] inDataArray, int startIndex, int numToProcess)
        {
            for (int i = 0; i < numToProcess; i++)
            {
                Process(inDataArray[startIndex++]);
            }
            return OutputData.Count;
        }

        public void Process(CNTRL_MSG controlParam, IQ inData)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                Process(inData);
            }
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

        public int Count
        {
            get { return OutputData.Count; }

        }

        public float IQEnergyDiff
        {
            get { return IQEnergyDifference; }
            set { IQEnergyDifference = value; }
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

        float CalculateCorrectionsI()
        {
            float result = 0;
            float Even = EvenIQ.I;
            float PrevEven = PrevEvenIQ.I;
            if ((Even * PrevEven) < 0)  // Sign change
            {
                float Odd = OddIQ.I;
                float EstPrevEven = Math.Sign(PrevEven);    // Estimated previous
                float EstEven = Math.Sign(Even);            // Estimated current
                result += ((SyncType & (SYNC_TYPE.GARDNER_NDA | SYNC_TYPE.GARDNER_DD)) != 0) ? (Odd * (EstEven - EstPrevEven)) : 0;
                result += ((SyncType & SYNC_TYPE.QAMLD_NDA) != 0) ? (Odd - 0.5f * (Even + PrevEven) ) : 0;
                result += ((SyncType & SYNC_TYPE.ZERODET_NDA) != 0) ? (Odd * EstEven) : 0;
                result += ((SyncType & SYNC_TYPE.MUELLER_NDA) != 0) ? (EstPrevEven * Even - EstEven * PrevEven) : 0;
            }
            return result;
        }

        float CalculateCorrectionsQ()
        {
            float result = 0;
            float Even = EvenIQ.Q;
            float PrevEven = PrevEvenIQ.Q;
            if ((Even * PrevEven) < 0) // Sign change
            {
                float Odd = OddIQ.Q;
                float EstPrevEven = Math.Sign(PrevEven);    // Estimated previous
                float EstEven = Math.Sign(Even);            // Estimated current
                result += ((SyncType & (SYNC_TYPE.GARDNER_NDA | SYNC_TYPE.GARDNER_DD)) != 0) ? (Odd * (EstEven - EstPrevEven)) : 0;
                result += ((SyncType & SYNC_TYPE.QAMLD_NDA) != 0) ? (Odd - 0.5f * (Even + PrevEven)) : 0;
                result += ((SyncType & SYNC_TYPE.ZERODET_NDA) != 0) ? (Odd * EstEven) : 0;
                result += ((SyncType & SYNC_TYPE.MUELLER_NDA) != 0) ? (EstPrevEven * Even - EstEven * PrevEven) : 0;
            }
            return result;
        }

        int CalculateCorrections()
        {
            float result;
            // Use either I or Q component with the highest energy, or combine both
            if (EnergyAccum.I > (IQEnergyDifference * EnergyAccum.Q))
            {
                result = CalculateCorrectionsI();
            }
            else if (EnergyAccum.Q > (IQEnergyDifference * EnergyAccum.I))
            {
                result = CalculateCorrectionsQ();
            }
            else
            {
                // When I and Q are pretty close - combine them
                result = CalculateCorrectionsI() + CalculateCorrectionsQ();
            }
            return Math.Sign(result);
        }


        public int Process(IQ inData)
        {
            // Calculate Even interval (sampling point)
            if ((PutCounter >= StartActive) && (PutCounter < EndActive))
            {
                EvenAccum += inData;
                EnergyAccum += inData.P;
                FrameEnergyAccum += inData.R2;
            }

            // Calculate Odd interval (correction)
            if ((PutCounter >= StartHalfPoint) || (PutCounter < EndHalfPoint))
            {
                OddAccum += inData;
            }

            PutCounter++;

            if (PutCounter == HalfPoint)
            {
                OddIQ = 2 * OddAccum / ActiveSamples;
                OddAccum = IQ.ZERO;
            }else if (PutCounter >= DecimFactor)
            {
                EvenIQ = 2 * EvenAccum / ActiveSamples;
                OutputData.Enqueue(EvenIQ);
                DataOut.Process(EvenIQ);
                
                FrequencyEn = EvenIQ.R2;
                SignalEn = 2 * FrameEnergyAccum / ActiveSamples;

                PutCounter = CalculateCorrections();
                SamplingOffset += PutCounter;

                PrevEvenIQ = EvenIQ;

                EvenAccum = IQ.ZERO;
                EnergyAccum = IQ.ZERO;
                FrameEnergyAccum = 0;
            }
            return OutputData.Count;
        }

        public int StartingOffset
        {
            get { return SamplingOffset; }
            set { Init();  PutCounter = value;}
        }

        public float SignalEnergy { get { return SignalEn; } }

        public float FrequencyEnergy { get { return FrequencyEn; } }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<IQ>("DataIn", this.Process);
            DataOut = new OutputPin<IQ>("DataOut");
            base.SetIOParameters("Integrate and Dump Symbol slicer", new DataPin[] { DataIn, DataOut });
        }
    }
}
