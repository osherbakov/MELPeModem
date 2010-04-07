using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    enum CORR_TYPE
    {
        NONE,
        AMPLITUDE,
        AMPLITUDE_DIFF,
        PHASE_DIFF,
        DELTASIN_DIFF,
        DELTACOS_DIFF,
        DELTA_DIFF,
        I,
        Q,
        IQ
    }

    /// <summary>
    /// Class that performs Differential Correlation.
    /// </summary>
    /// <remarks>
    /// Correlate received IQ symbols to the Frame/Probe pattern.
    /// Because initially there is a phase ambiguity in a received QPSK/QAM signal,
    /// we can only reliably detect a phase difference between ajoining symbols.
    /// </remarks>
    class Correlator : DataProcessingModule
    {
        InputPin<IQ> DataIn;
        OutputPin<IQ> DataOut;

        int DataLength;
        int TargetLength;
        int TargetMargin;
        int NumSymbols;

        // All the programmable criteria forCorrelation Max detection
        int MinNumSymbols;              // Minimum number of symbols received
        int MinSymbolsAfterMax;         // Minimum number of symbols the Maximum should hold
        float MaxToAverageThreshold;    // The Maximum-over-average threshold
        float MaxToEnergyThreshold;     // The Maximum-over-energy threshold
        float MaxToTargetThreshold;     // The Maximum-over-Target threshold

        CORR_TYPE CorrelationType;
        bool CorrelationMaxFound;

        IQ[] Data;
        float[] IQDiff;
        float[] IQDiff1;

        IQ[] Target;
        float[] TargetIQDiff;
        float[] TargetIQDiff1;
        float TargetEnergy;
        float TargetAutoCorr;

        IQ CorrRotate = IQ.UNITY;      // Correction factor to rotate constellation
        IQ CorrFreq = IQ.UNITY;        // Correction factor to adjust for frequency offset
        IQ PrevIQ = IQ.ZERO;           // Previous Rotation value to calculate Frequency offset

        int CurrentIndex = 0;           // The current position where next symbol will go
        int CorrMaxIndex = 0;           // The index of the correlation peak in Data array
        int CorrelationPosition = 0;    // The index of the correlation peak relative to the last symbol
        float Correlation;              // Current correlation value
        float CorrMaxValue;             // The maximum correlation value
        float CorrMaxEnergy;            // The maximum correlation energy
        float PrevMaxValue;             // Previous correlation maximum value
        float CorrAverage;              // The average value for the full correlation set
        float CorrEnergyAverage;        // The average Energy for the full correlation set
        float DataEnergy;               // The energy of the correlated frame
        float PrevDataEnergy = 0;       // The energy of the first symbol used in correlation

        /// <summary>
        /// Constructor for the Correlator.
        /// </summary>
        /// <param name="bufferSize">Size of the buffer on which the correlation will be computed.</param>
        public Correlator(CORR_TYPE corrType, int symbolBufferSize, int minSymbols, int minPeakHold, float targetThreshold, float averageThreshold, float energyThreshold)
        {
            DataLength = Math.Max(symbolBufferSize, (minSymbols + minPeakHold));
            CorrelationType = corrType;
            MinNumSymbols = minSymbols;
            MinSymbolsAfterMax = minPeakHold;
            MaxToTargetThreshold = targetThreshold;
            MaxToAverageThreshold = averageThreshold;
            MaxToEnergyThreshold = energyThreshold;

            Data = new IQ[DataLength];
            IQDiff = new float[DataLength];
            IQDiff1 = new float[DataLength];
            Init();
        }

        /// <summary>
        /// Initialize the correlator. Correlation Target will stay the same.
        /// </summary>
        public override void Init()
        {
            Array.Clear(Data, 0, DataLength);
            Array.Clear(IQDiff, 0, DataLength);
            Array.Clear(IQDiff1, 0, DataLength);

            CurrentIndex = 0;
            CorrelationPosition = 0;
            CorrMaxIndex = 0;
            CorrMaxValue = float.MinValue;
            CorrAverage = 0;
            CorrEnergyAverage = 0;
            PrevMaxValue = 0;
            NumSymbols = 0;
            DataEnergy = 0;
            PrevIQ = IQ.ZERO;
            CorrRotate = IQ.UNITY;
            CorrFreq = IQ.UNITY; 
            PrevDataEnergy = 0;
            CorrelationMaxFound = false;
        }


        public void AddTarget(IQ[] targetArray)
        {
            AddTarget(targetArray, targetArray.Length);
        }

        /// <summary>
        /// Adds corralation target. This target will be used as the matching target for all data in the buffer.
        /// </summary>
        /// <param name="targetArray">Array that defines the target IQ components.</param>
        /// <param name="numberOfTargetSymbols">Number of target symbols in IQ array.</param>
        public void AddTarget(IQ[] targetArray, int numberOfTargetSymbols)
        {
            IQ SavedPrevIQ = PrevIQ;

            TargetLength = numberOfTargetSymbols;
            TargetMargin = TargetLength + MinSymbolsAfterMax;
            Target = new IQ[TargetLength];
            TargetIQDiff = new float[TargetLength];
            TargetIQDiff1 = new float[TargetLength];
            if (TargetMargin > DataLength)
            {
                DataLength = TargetMargin;
                Data = new IQ[DataLength];
                IQDiff = new float[DataLength];
                IQDiff1 = new float[DataLength];
                Init();
            }
            MinNumSymbols = Math.Max(MinNumSymbols, TargetLength);
            DataLength = Math.Max(DataLength, (MinNumSymbols + MinSymbolsAfterMax));
            if (DataLength > Data.Length)
            {
                Data = new IQ[DataLength];
                IQDiff = new float[DataLength];
                IQDiff1 = new float[DataLength];
            }

            targetArray.CopyTo(Target, 0);
            TargetEnergy = 0;
            IQ CurrIQ;
            for (int i = 0; i < TargetLength; i++)
            {
                CurrIQ = targetArray[i];
                TargetEnergy += CurrIQ.R2;
            }

            PrevIQ = IQ.ZERO;
            TargetAutoCorr = 0;
            float val;
            if (CorrelationType == CORR_TYPE.PHASE_DIFF)
            {
                for (int i = 0; i < TargetLength; i++)
                {
                    CurrIQ = targetArray[i];
                    val = (CurrIQ.Phase - PrevIQ.Phase);
                    TargetAutoCorr += val * val;
                    TargetIQDiff[i] = val;
                    PrevIQ = CurrIQ;
                }
            }
            else if (CorrelationType == CORR_TYPE.DELTASIN_DIFF)
            {
                for (int i = 0; i < TargetLength; i++)
                {
                    CurrIQ = targetArray[i];
                    val = CurrIQ.DeltaSin(PrevIQ);
                    TargetAutoCorr += val * val;
                    TargetIQDiff[i] = val;
                    PrevIQ = CurrIQ;
                }
            }
            else if (CorrelationType == CORR_TYPE.DELTACOS_DIFF)
            {
                for (int i = 0; i < TargetLength; i++)
                {
                    CurrIQ = targetArray[i];
                    val = CurrIQ.DeltaCos(PrevIQ);
                    TargetAutoCorr += val * val;
                    TargetIQDiff[i] = val;
                    PrevIQ = CurrIQ;
                }
            }
            else if (CorrelationType == CORR_TYPE.AMPLITUDE_DIFF)
            {
                for (int i = 0; i < TargetLength; i++)
                {
                    CurrIQ = targetArray[i];
                    val = (CurrIQ - PrevIQ).R2;
                    TargetAutoCorr += val * val;
                    TargetIQDiff[i] = val;
                    PrevIQ = CurrIQ;
                }
            }
            else if (CorrelationType == CORR_TYPE.AMPLITUDE)
            {
                for (int i = 0; i < TargetLength; i++)
                {
                    val = targetArray[i].R2;
                    TargetAutoCorr += val * val;
                    TargetIQDiff[i] = val;
                }
            }
            else if (CorrelationType == CORR_TYPE.DELTA_DIFF)
            {
                for (int i = 0; i < TargetLength; i++)
                {
                    CurrIQ = targetArray[i];
                    val = CurrIQ.DeltaSin(PrevIQ);
                    TargetAutoCorr += val * val;
                    TargetIQDiff[i] = val;
                    val = CurrIQ.DeltaCos(PrevIQ);
                    TargetAutoCorr += val * val;
                    TargetIQDiff1[i] = val;
                    PrevIQ = CurrIQ;
                }
            }
            else if (CorrelationType == CORR_TYPE.I)
            {
                for (int i = 0; i < TargetLength; i++)
                {
                    val = targetArray[i].I;
                    TargetAutoCorr += val * val;
                    TargetIQDiff[i] = val;
                }
            }
            else if (CorrelationType == CORR_TYPE.Q)
            {
                for (int i = 0; i < TargetLength; i++)
                {
                    val = targetArray[i].Q;
                    TargetAutoCorr += val * val;
                    TargetIQDiff[i] = val;
                }
            }
            else if (CorrelationType == CORR_TYPE.IQ)
            {
                for (int i = 0; i < TargetLength; i++)
                {
                    val = targetArray[i].I;
                    TargetAutoCorr += val * val;
                    TargetIQDiff[i] = val;
                    val = targetArray[i].Q;
                    TargetAutoCorr += val * val;
                    TargetIQDiff1[i] = val;
                }
            }
            else if (CorrelationType == CORR_TYPE.NONE)
            {
            }

            PrevIQ = SavedPrevIQ;
        }

        public void StartCorrectionProcess()
        {
            Init();
        }

        /// <summary>
        /// Size of the data stored in the Correlator.
        /// </summary>
        public int Length { get { return DataLength; } }

        /// <summary>
        /// Position in the correlatior buffer where the correlation maximum is detected.
        /// </summary>
        public int CorrelationMaxIndex { get { return CorrelationPosition; } }

        public int CorrelationMaxLength { get { return TargetLength; } }

        public int SymbolsCount { get { return NumSymbols; } }

        /// <summary>
        /// Correction (Rotation) factor for the next IQ data.
        /// </summary>
        public IQ RotateCorrection
        {
//            get
//            {
//                Quad freq = new Quad(CorrRotate, CorrFreq, CorrelationMaxIndex - CorrelationMaxLength / 2);
//                return freq.Value;
//            }
            get{ return this.CorrRotate; }
            set { this.CorrRotate = value; }
        }

        /// <summary>
        /// Correction (Frequency) factor for all data.
        /// </summary>
        public IQ FrequencyCorrection
        {
            get { return this.CorrFreq; }
            set {this.CorrFreq = value.N; }
        }

        public float MaxToAverageRatio
        {
            get { return CorrAverage == 0 ? 0 : Math.Abs((CorrMaxValue * NumSymbols) / CorrAverage); }
        }

        public float MaxToEnergyRatio
        {
            get { return CorrEnergyAverage == 0 ? 0 : (CorrMaxValue * NumSymbols) / CorrEnergyAverage; }
        }

        public float MaxToTargetRatio
        {
            get { return CorrMaxEnergy == 0 ? 0 : (CorrMaxValue * CorrMaxValue) / (TargetEnergy * CorrMaxEnergy); }
        }

        /// <summary>
        /// Calculate and return corrected I/Q symbols (rotate constellation).
        /// </summary>
        /// <param name="startingIndex">Index (relative to the last symbol) where to start corrected data output.</param>
        /// <param name="outputArray">Array that receives corrected IQ values.</param>
        /// <param name="outputIndex">Starting index for the array.</param>
        /// <param name="NumberOfSymbols">Number of corrected symbols requested.</param>
        /// <returns>Number of corrected I/Q symbols returned.</returns>
        public int GetLastData(int startingIndex, IQ[] outputArray, int outputIndex, int numberOfSymbols)
        {
            int Start1, Start2;
            int End1, End2;

            if (startingIndex > this.DataLength){
                // No data available
                return 0;
            }
            Start1 = CurrentIndex - startingIndex;
            End1 = Start1 + numberOfSymbols;
            Start2 = DataLength;        // initially disable a second run
            End2 = End1;
            if (Start1 < 0)             // If pointer goes outside array bounds, then...
            {
                Start1 += DataLength;   // Bring it back...
                End1 += DataLength;
                if (End1 >= DataLength)
                {
                    End1 = DataLength;      // End first run at the end of the array
                    Start2 = 0;             // Start second run from the beginning of the array
                }
            }

            for (int i = Start1; i < End1; i++)
            {
                outputArray[outputIndex++] = Data[i] * this.CorrRotate;
            }
            // Do second calculation loop if wrap-around was detected
            for (int i = Start2; i < End2; i++)
            {
                outputArray[outputIndex++] = Data[i] * this.CorrRotate;
            }
            return numberOfSymbols;
        }

        /// <summary>
        /// Calculate and return corrected I/Q symbols (rotate constellation).
        /// </summary>
        /// <param name="startingIndex">Index (relative to the last symbol) where to start corrected data output.</param>
        /// <param name="outputArray">Array that receives corrected IQ values.</param>
        /// <param name="NumberOfSymbols">Number of corrected symbols requested.</param>
        /// <returns>Number of corrected I/Q symbols returned.</returns>
        public int GetLastData(int startingIndex, IQ[] outputArray, int NumberOfSymbols)
        {
            return GetLastData(startingIndex, outputArray, 0, NumberOfSymbols);
        }

        public bool IsSyncReady
        {
            get { return CorrelationMaxFound; }
        }

        int IndexFromMax
        {
            get
            {
                int ret = CurrentIndex - CorrMaxIndex;
                if (ret <= 0) ret += DataLength;
                return ret;
            }
        }

        /// <summary>
        /// This member calculates the criteria for the Correlation Maximum detection
        /// </summary>
        /// true - if correlatin maximum was detected</returns>
        public virtual bool IsMaxCorrelation
        {
            get
            {
                bool result = (this.NumSymbols >= this.TargetMargin);
                // First, we must pass the maximum for at least so many symbols
                result = result && (this.IndexFromMax >= this.TargetMargin);
                // Second, the Maximum should be at least as big as specified average
                result = result && ((this.CorrMaxValue * this.NumSymbols) >= Math.Abs(this.MaxToAverageThreshold * this.CorrAverage));
                // Third, the Maximum should be at least as big as specified energy
                result = result && ((this.CorrMaxValue * this.NumSymbols) >= Math.Abs(this.MaxToEnergyThreshold * this.CorrEnergyAverage));
                // Forth, check the target match index
                result = result && ((this.CorrMaxValue * this.CorrMaxValue) >= (this.TargetEnergy * this.CorrMaxEnergy * this.MaxToTargetThreshold));
                return result;
            }
        }

        /// <summary>
        /// Process I-Q sample in correlator and check if correlation maximum was detected
        /// </summary>
        /// <param name="data">IQ sample</param>
        /// <returns><typeparamref name="true"/> if we should continue to put more data.</returns>
        public int Process(IQ data)
        {
            NumSymbols++;
            // Save the data
            Data[CurrentIndex] = data;

            if (CorrelationMaxFound)
            {
                CurrentIndex++; if (CurrentIndex >= DataLength) CurrentIndex = 0;
                CorrelationPosition++;
                return CorrelationPosition;
            }

            if (CorrelationType == CORR_TYPE.NONE)
            {
                CurrentIndex++; if (CurrentIndex >= DataLength) CurrentIndex = 0;
                if (this.NumSymbols >= this.MinNumSymbols)
                {
                    CorrelationMaxFound = true;
                    CorrelationPosition = this.MinNumSymbols;
                    CalculateCorrections();
                }
                return CorrelationPosition;
            }

            float newVal;
            float newVal1 = 0;
            if (CorrelationType == CORR_TYPE.DELTASIN_DIFF)
                newVal = data.DeltaSin(PrevIQ);
            else if (CorrelationType == CORR_TYPE.DELTACOS_DIFF)
                newVal = data.DeltaCos(PrevIQ);
            else if (CorrelationType == CORR_TYPE.AMPLITUDE_DIFF)
                newVal = (data - PrevIQ).R2;
            else if (CorrelationType == CORR_TYPE.AMPLITUDE)
                newVal = data.R2;
            else if (CorrelationType == CORR_TYPE.I)
                newVal = data.I;
            else if (CorrelationType == CORR_TYPE.Q)
                newVal = data.Q;
            else if (CorrelationType == CORR_TYPE.PHASE_DIFF)
                newVal = data.Phase - PrevIQ.Phase;
            else if (CorrelationType == CORR_TYPE.DELTA_DIFF){
                newVal = data.DeltaSin(PrevIQ);
                newVal1 = data.DeltaCos(PrevIQ);
            }else if (CorrelationType == CORR_TYPE.IQ){
                newVal = data.I;
                newVal1 = data.Q;
            }else
                newVal = newVal1 = 0;

            IQDiff[CurrentIndex] = newVal;
            IQDiff1[CurrentIndex] = newVal1;
            PrevIQ = data;
            CurrentIndex++; if (CurrentIndex >= DataLength) CurrentIndex = 0;

            // Split the correlation calculation in two
            // to take into account the index wrap-around
            int Start1, Start2;
            int End1;
            Start1 = CurrentIndex - TargetLength;   // Start Correlation calculation
                                                    // N-symbols before
            End1 = CurrentIndex;                    // End it at the last sample
            Start2 = DataLength;        // initially disable a second run

            if (Start1 < 0)             // If pointer goes outside array bounds, then...
            {
                Start1 += DataLength;   // Bring it back...
                End1 = DataLength;      // End first run at the end of the array
                Start2 = 0;             // Start second run from the beginning of the array
            }

            float Symbol0 = IQDiff[Start1]; // We use it to get the energy of the first symbol
            float Symbol1 = IQDiff1[Start1]; // We use it to get the energy of the first symbol
            DataEnergy += (newVal * newVal) + (newVal1 * newVal1);
            DataEnergy -= PrevDataEnergy;
            PrevDataEnergy = Symbol0 * Symbol0 + Symbol1 * Symbol1;

            // If not enough symbols collected - do not do correlation yet
            if(this.NumSymbols < this.MinNumSymbols) return 0;
            
            // Now we can do correlation run
            Correlation = 0;
            int targetIdx = 0;              // Target array Index
            for (int i = Start1; i < End1; i++)
            {
                Correlation += TargetIQDiff[targetIdx++] * IQDiff[i];
            }
            // Do second calculation loop if wrap-around was detected
            for (int i = Start2; i < CurrentIndex; i++){
                Correlation += TargetIQDiff[targetIdx++] * IQDiff[i];
            }

            if ((CorrelationType == CORR_TYPE.DELTA_DIFF) || (CorrelationType == CORR_TYPE.IQ)){
                targetIdx = 0;          // Target array Index
                for (int i = Start1; i < End1; i++)
                {
                    Correlation += TargetIQDiff1[targetIdx++] * IQDiff1[i];
                }
                // Do second calculation loop if wrap-around was detected
                for (int i = Start2; i < CurrentIndex; i++)
                {
                    Correlation += TargetIQDiff1[targetIdx++] * IQDiff1[i];
                }
            }

            // Check if the newly calculated correlation is the best one...
            float AbsCorrelation = Math.Abs(Correlation);
            if (AbsCorrelation > CorrMaxValue)
            {
                PrevMaxValue = CorrMaxValue;
                CorrMaxValue = AbsCorrelation;
                CorrMaxEnergy = DataEnergy;
                CorrMaxIndex = Start1;
            }else{
                // Adjust the average by adding the new one
                CorrAverage += Correlation;
                CorrEnergyAverage += AbsCorrelation;
            }

            if (IsMaxCorrelation){
                CorrelationMaxFound = true;
                CorrelationPosition = IndexFromMax;
                CalculateCorrections();
            }
            return CorrelationPosition;
        }

        public void Process(CNTRL_MSG controlParam, IQ inData)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                if (IsSyncReady)
                {
                    DataOut.Process(inData);
                }
                else
                {
                    Process(inData);
                    if (IsSyncReady)
                    {
                        int Start1, Start2;
                        int End1, End2;

                        DataOut.Process(CNTRL_MSG.SYNC_DETECTED);

                        Start1 = CurrentIndex;
                        End1 = DataLength;
                        Start2 = 0;        // initially disable a second run
                        End2 = CurrentIndex;
                        for (int i = Start1; i < End1; i++)
                        {
                            DataOut.Process(Data[i] * this.CorrRotate);
                        }
                        // Do second calculation loop if wrap-around was detected
                        for (int i = Start2; i < End2; i++)
                        {
                            DataOut.Process(Data[i] * this.CorrRotate);
                        }
                    }
                }
            }
        }

        void CalculateCorrections()
        {
            int Start2;
            int End1, End2;
            End1 = End2 = CorrMaxIndex + TargetLength;
            Start2 = DataLength;        // initially disable a second run

            if (End1 >= DataLength)     // If pointer goes outside array bounds, then...
            {
                End2 -= DataLength;     // Bring it back...
                End1 = DataLength;      // End first run at the end of the array
                Start2 = 0;             // Start second run from the beginning of the array
            }

            float E = 0;
            IQ F = IQ.ZERO;
            int targetIdx = 0;
            IQ target, data;
            for (int i = CorrMaxIndex; i < End1; i++)
            {
                data = Data[i]; 
                target = Target[targetIdx++];
                F += target / data;
                E += data.R2;
            }
            // Do second calculation loop if wrap-around was detected
            for (int i = Start2; i < End2; i++)
            {
                data = Data[i];
                target = Target[targetIdx++];
                F += target / data;
                E += data.R2;
            }

            this.CorrRotate = F * (float)Math.Sqrt( this.TargetEnergy / ( E * F.R2));

            // Now figure out the frequency offset
            targetIdx = 0;
            IQ delta = IQ.ZERO;
            IQ prev = Target[0] / (Data[CorrMaxIndex] * CorrRotate);
            for (int i = CorrMaxIndex; i < End1; i++)
            {
                data = Target[targetIdx++] / (Data[i] * CorrRotate);
                delta += (data / prev);
                prev = data;
            }
            // Do second calculation loop if wrap-around was detected
            for (int i = Start2; i < End2; i++)
            {
                data = Target[targetIdx++] / (Data[i] * CorrRotate);
                delta += (data / prev);
                prev = data;
            }
            // Frequency offset will be in Radians/Symbol length (freqoff = SymbolFreq * (CorrFreq.degree/360))
            this.CorrFreq = delta / delta.R;
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<IQ>("DataIn", this.Process);
            DataOut = new OutputPin<IQ>("DataOut");
            base.SetIOParameters("IQ Correlator", new DataPin[] { DataIn, DataOut });
        }
    }

    class SymbolDetector
    {
        List<IQ []> TargetSymbolsIQ = new List<IQ[]>();

        public void Init()
        {
            TargetSymbolsIQ.Clear();
        }

        public void AddTarget(IQ[] symbolTarget)
        {
            IQ[] t = new IQ[symbolTarget.Length];
            symbolTarget.CopyTo(t, 0);
            TargetSymbolsIQ.Add(t);
        }

        public IQ[] this[int index]
        {
            set { TargetSymbolsIQ.Insert(index, value); }
            get { return TargetSymbolsIQ[index]; }
        }

        public int Process(IQ[] inputArray, int startingIndex)
        {
            float CorrMax = float.MinValue;
            int result = 0;
            int Index = 0;

            foreach (IQ[] CurrentSymbol in TargetSymbolsIQ)
            {
                float Corr = 0;
                for (int i = 0; i < CurrentSymbol.Length; i++)
                {
                    IQ t = inputArray[startingIndex + i];
                    IQ s = CurrentSymbol[i];
                    Corr += t.I * s.I + t.Q * s.Q;
                }
                Corr = Math.Abs(Corr);
                if (Corr >= CorrMax)
                {
                    result = Index;
                    CorrMax = Corr;
                }
                Index++;
            }
            return result;
        }
    }

    class BitCorrelator : DataProcessingModule
    {
        InputPin<byte> DataIn;
        OutputPin<byte> DataOut;

        int NumBits;
        int Target;
        int TargetMask = 0;
        bool MatchFound;
        int CurrentData;
        int Index = 0;

        public bool IsMatchFound
        {
            get { return MatchFound; }
        }

        public override void Init()
        {
            MatchFound = false;
            CurrentData = 0;
            Index = 0;
        }

        public void AddTarget(int targetData, int numBits)
        {
            NumBits = numBits;
            Target = 0;
            TargetMask = (1 << numBits) - 1;
            if (TargetMask == 0) TargetMask = -1;
            for (int i = 0; i < numBits; i++)
            {
                Target = (Target << 1) | (targetData & 0x01);
                targetData >>= 1;
            }
        }

        public int Process(int newData, int numBits)
        {
            if (MatchFound)
            {
                Index += numBits;
            }
            else
            {
                for (int i = 0; i < numBits; i++)
                {
                    CurrentData = (CurrentData << 1) | (newData & 0x01);
                    if (((CurrentData ^ Target) & TargetMask) == 0)
                    {
                        MatchFound = true;
                        Index = NumBits + numBits - (i + 1);
                        break;
                    }
                    newData >>= 1;
                }
            }
            return Index;
        }

        public void Process(CNTRL_MSG controlParam, byte bitData)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                DataOut.Process(bitData);
                if (MatchFound)
                {
                    Index++;
                }
                else
                {
                    CurrentData = (CurrentData << 1) | (bitData & 0x01);
                    if (((CurrentData ^ Target) & TargetMask) == 0)
                    {
                        MatchFound = true;
                        Index = NumBits;
                        DataOut.Process(CNTRL_MSG.EOM_DETECTED);
                    }
                }
            }
        }

        
        public int Process(byte [] newDataArray, int startIndex, int numBits)
        {
            if (MatchFound)
            {
                Index += numBits;
            }
            else
            {
                while ((numBits > 0) && !MatchFound)
                {
                    int newData = newDataArray[startIndex++];
                    CurrentData = (CurrentData << 1) | (newData & 0x01);
                    if (((CurrentData ^ Target) & TargetMask) == 0)
                    {
                        MatchFound = true;
                        Index = NumBits + numBits - 1;
                    }
                    numBits--;
                }
            }
            return Index;
        }

        public int TargetIndex { get { return this.Index; } }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<byte>("DataIn", this.Process);
            DataOut = new OutputPin<byte>("DataOut");
            base.SetIOParameters("BitPatternDetector", new DataPin[] { DataIn, DataOut });
        }
    }
}
