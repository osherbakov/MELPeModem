using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    class IQDemodulator : DataProcessingModule
    {
        InputPin<float> DataIn;
        OutputPin<IQ> DataOut;

        Queue<IQ> OutputData;

        int NFREQ;
        float[] Frequencies;
        float[] InitialPhases;
        Quad[] Correction;

        float FreqAdj = 0;
        IQ []CorrRotate;
        IQ []CorrFreq;

        float SamplingFreq;
        int BlockSize;
        int Half;
        int CurrFreqIndex = 0;

        // Carrier generators
        Generator[] gI;
        Generator[] gQ;

        float[] fE;
        float sE;
        float Coeff1, Coeff2;

        bool InSyncProcess;
        int CorrSymbolIndex;
        SymbolSync CorrSymbol;

        // Demodulation filters for I and Q 
        FIR[] fI;
        FIR[] fQ;

        float[] TempModBufferI;
        float[] TempModBufferQ;

        public IQDemodulator(float lowFreq, float highFreq, int numFreq, float processingRate, float symbolRate, float[] symbolShaperArray)
        {
            NFREQ = numFreq;
            SamplingFreq = processingRate;
            BlockSize = (int)(processingRate / symbolRate);
            Half = BlockSize / 2;
            if ((int)(BlockSize * symbolRate + 0.5f) != (int)processingRate)
            {
                throw new ArgumentException("The processingRate must be integer multiple of symbolRate");
            }

            gI = new Generator[NFREQ];
            gQ = new Generator[NFREQ];
            fI = new FIR[NFREQ];
            fQ = new FIR[NFREQ];
            fE = new float[NFREQ];
            Frequencies = new float[NFREQ];        // array of all frequencies
            InitialPhases = new float[NFREQ];
            CorrRotate = new IQ[NFREQ];
            CorrFreq = new IQ[NFREQ];
            Correction = new Quad[NFREQ];

            TempModBufferI = new float[BlockSize];
            TempModBufferQ = new float[BlockSize];

            // Evenly distribute all frequencies between Hi and Lo
            float OneChannelBW;
            if (NFREQ <= 1)
            {
                OneChannelBW = (highFreq + lowFreq) / 2;
                lowFreq = OneChannelBW;
            }
            else
            {
                OneChannelBW = (highFreq - lowFreq) / (NFREQ - 1);
            }

            for (int i = 0; i < NFREQ; i++)
            {
                float Freq = lowFreq + i * OneChannelBW;
                Frequencies[i] = Freq;
                InitialPhases[i] = 0;
                gI[i] = new Generator();
                gQ[i] = new Generator();

                fI[i] = new FIR(symbolShaperArray, BlockSize);
                fQ[i] = new FIR(symbolShaperArray, BlockSize);
            }
            Init();
        }

        public IQDemodulator(float[] freqArray, int numFreq, float processingRate, float symbolRate, float[] symbolShaperArray)
        {
            NFREQ = numFreq;
            SamplingFreq = processingRate;
            BlockSize = (int)(processingRate / symbolRate);
            if ((int)(BlockSize * symbolRate + 0.5f) != (int)processingRate)
            {
                throw new ArgumentException("The processingRate must be integer multiple of symbolRate");
            }
            gI = new Generator[NFREQ];
            gQ = new Generator[NFREQ];
            fI = new FIR[NFREQ];
            fQ = new FIR[NFREQ];
            fE = new float[NFREQ];
            Frequencies = new float[NFREQ];        // array of all frequencies
            InitialPhases = new float[NFREQ];
            CorrRotate = new IQ[NFREQ];
            CorrFreq = new IQ[NFREQ];
            Correction = new Quad[NFREQ];

            TempModBufferI = new float[BlockSize];
            TempModBufferQ = new float[BlockSize];

            freqArray.CopyTo(Frequencies, 0);
            for (int i = 0; i < NFREQ; i++)
            {
                InitialPhases[i] = 0;
                gI[i] = new Generator();
                gQ[i] = new Generator();

                fI[i] = new FIR(symbolShaperArray, BlockSize);
                fQ[i] = new FIR(symbolShaperArray, BlockSize);
            }
            Init();
        }

        public override void Init()
        {
            for (int i = 0; i < NFREQ; i++)
            {
                gI[i].Init(Frequencies[i], SamplingFreq, InitialPhases[i] + (float)Math.PI / 2);
                gQ[i].Init(Frequencies[i], SamplingFreq, InitialPhases[i] );

                fI[i].Init();
                fQ[i].Init();
                CorrRotate[i] = IQ.UNITY;
                CorrFreq[i] = IQ.UNITY;
                Correction[i] = new Quad(CorrRotate[i], CorrFreq[i]);
                fE[i] = 0;
            }
            sE = 0;
            FreqAdj = 0;
            CurrFreqIndex = 0;
            InSyncProcess = false;
            CorrSymbolIndex = 0;
            CorrSymbol = new SymbolSync(BlockSize);
            OutputData = new Queue<IQ>();
            Coeff2 = 1.0f / BlockSize; Coeff1 = 1.0f - Coeff2;

        }

        public void Init(float[] Phases)
        {
            Phases.CopyTo(InitialPhases, 0);
            Init();
        }

        public float FrequencyOffset
        {
            get { return FreqAdj; }
            set
            {
                FreqAdj = value;
                for (int i = 0; i < NFREQ; i++)
                {
                    gI[i].Frequency = Frequencies[i] + FreqAdj;
                    gQ[i].Frequency = Frequencies[i] + FreqAdj;
                }
            }
        }

        public int Index
        {
            get { return CurrFreqIndex; }
            set { CurrFreqIndex = value; }
        }

        public IQDemodulator this[int freqIndex]
        {
            get { CurrFreqIndex = freqIndex; return this; }
        }

        public IQDemodulator this[float freq]
        {
            get
            {
                CurrFreqIndex = Array.IndexOf<float>(Frequencies, freq);
                return this;
            }
        }

        public float Frequency
        {
            get { return Frequencies[CurrFreqIndex]; }
            set
            {
                Frequencies[CurrFreqIndex] = value;
                gI[CurrFreqIndex].Frequency = value;
                gQ[CurrFreqIndex].Frequency = value;
            }
        }

        public float Phase
        {
            get { return gI[CurrFreqIndex].Phase; }
            set
            {
                gI[CurrFreqIndex].Phase = value + (float)Math.PI / 2;
                gQ[CurrFreqIndex].Phase = value;
            }
        }

        public void StartCorrectionProcess(SYNC_TYPE sType, int numSymbols)
        {
            if (numSymbols != 0)
            {
                CorrSymbolIndex = CurrFreqIndex;
                InSyncProcess = true;
                CorrSymbol.StartCorrectionProcess(sType, numSymbols, fQ[CurrFreqIndex].DecimateIndex);
            }
        }
        public bool IsSyncReady
        {
            get { return !InSyncProcess; }
        }

        public int Process(float[] incomingData, int sampleIndex, int numSamples)
        {
            while (numSamples-- > 0)
            {
                Process(incomingData[sampleIndex++]);
            }
            return OutputData.Count;
        }

        public int Process(float incomingSample)
        {
            float I, Q;
            IQ Data;

            sE = Coeff1 * sE + Coeff2 * incomingSample * incomingSample;
            for (int idx = 0; idx < NFREQ; idx++)
            {
                gI[idx].Process(incomingSample, out I);
                gQ[idx].Process(incomingSample, out Q);

                if (InSyncProcess && (CorrSymbolIndex == idx))
                {
                    fI[idx].Process(I, out I);
                    fQ[idx].Process(Q, out Q);
                    if (CorrSymbol.Process(new IQ(I, Q)) > 0)
                    {
                        InSyncProcess = false;
                        int NewIdx = CorrSymbol.SymbolCorrection;
                        for (int i = 0; i < NFREQ; i++)
                        {
                            fI[i].DecimateIndex = NewIdx;
                            fQ[i].DecimateIndex = NewIdx;
                        }
                        // Get all the data from the symbol corrector
                        while (CorrSymbol.Count > 0)
                        {
                            Data = CorrSymbol.GetData() * Correction[idx].Next();
                            OutputData.Enqueue(Data);
                            fE[idx] = Coeff1 * fE[idx] + Coeff2 * Data.R2;
                        }
                    }
                }
                else
                {
                    fI[idx].Decimate(I, out I);
                    if (fQ[idx].Decimate(Q, out Q) > 0)
                    {
                        Data = new IQ(I, Q) * Correction[idx].Next();
                        OutputData.Enqueue(Data);
                        fE[idx] = Coeff1 * fE[idx] + Coeff2 * Data.R2;
                    }
                }
            }
            return OutputData.Count;
        }

        public void Process(CNTRL_MSG controlParam, float incomingSample)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                bool SamplesReady = false;
                float I, Q;
                IQ Data;

                sE = Coeff1 * sE + Coeff2 * incomingSample * incomingSample;
                for (int idx = 0; idx < NFREQ; idx++)
                {
                    gI[idx].Process(incomingSample, out I);
                    gQ[idx].Process(incomingSample, out Q);

                    if (InSyncProcess && (CorrSymbolIndex == idx))
                    {
                        // If we are in Sync mode for that channel - just pass samples
                        fI[idx].Process(I, out I);
                        fQ[idx].Process(Q, out Q);
                        if (CorrSymbol.Process(new IQ(I, Q)) > 0)
                        {
                            InSyncProcess = false;
                            // Sync mode just ended, and the data is ready
                            int NewIdx = CorrSymbol.SymbolCorrection;
                            for (int i = 0; i < NFREQ; i++)
                            {
                                // Adjust all FIRs/Decimators
                                fI[i].DecimateIndex = NewIdx;
                                fQ[i].DecimateIndex = NewIdx;
                            }
                            // Get all the data from the symbol corrector
                            while (CorrSymbol.Count > 0)
                            {
                                Data = CorrSymbol.GetData() * Correction[idx].Next();
                                DataOut.Process(Data);
                                fE[idx] = Coeff1 * fE[idx] + Coeff2 * Data.R2;
                            }
                        }
                    }
                    else
                    {
                        fI[idx].Decimate(I, out I);
                        SamplesReady = fQ[idx].Decimate(Q, out Q) > 0;
                        if (SamplesReady)
                        {
                            Data = new IQ(I, Q) * Correction[idx].Next();
                            DataOut.Process(Data);
                            fE[idx] = Coeff1 * fE[idx] + Coeff2 * Data.R2;
                        }
                    }
                }
                // All symbols go sequentially in the data queues, and NEW_SYMBOL messages are used
                //  to act as delimiters
                if (SamplesReady && (NFREQ > 1))
                {
                    DataOut.Process(CNTRL_MSG.NEW_SYMBOL);
                }
            }
        }

        /// <summary>
        /// Correction (Rotation) factor for the IQ data.
        /// </summary>
        public IQ RotateCorrection
        {
            set
            {
                this.CorrRotate[CurrFreqIndex] = value; Correction[CurrFreqIndex] = new Quad(CorrRotate[CurrFreqIndex], CorrFreq[CurrFreqIndex], 1);
            }
            get { return this.Correction[CurrFreqIndex].Value; }
        }

        /// <summary>
        /// Correction (Frequency) factor for the IQ data.
        /// </summary>
        public IQ FrequencyCorrection
        {
            set
            {
                this.CorrFreq[CurrFreqIndex] = value.N; Correction[CurrFreqIndex] = new Quad(CorrRotate[CurrFreqIndex], CorrFreq[CurrFreqIndex], 1);
            }
            get { return this.CorrFreq[CurrFreqIndex]; }
        }

        public int StartingOffset
        {
            get { return fI[CurrFreqIndex].DecimateIndex; }
            set
            {
                for (int i = 0; i < NFREQ; i++)
                {
                    fI[i].DecimateIndex = value;
                    fQ[i].DecimateIndex = value;
                }
            }
        }


        public float SignalEnergy { get { return sE / BlockSize; } }

        public float FrequencyEnergy { get { return fE[CurrFreqIndex]; } }

        public int Count { get { return OutputData.Count; } }

        public bool IsDataReady { get { return (OutputData.Count > 0); } }

        public IQ GetData()
        {
            return OutputData.Dequeue();
        }

        public int GetData(IQ[] outData)
        {
            int ret = OutputData.Count;
            for (int i = 0; OutputData.Count > 0; i++)
            {
                outData[i] = OutputData.Dequeue();
            }
            return ret;
        }

        public int GetData(IQ[] outData, int startingIndex)
        {
            int ret = OutputData.Count;
            while (OutputData.Count > 0)
            {
                outData[startingIndex++] = OutputData.Dequeue();
            }
            return ret;
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<float>("DataIn", this.Process);
            DataOut = new OutputPin<IQ>("DataOut");
            base.SetIOParameters("SamplesToIQDemodulator", new DataPin[] { DataIn, DataOut });
        }

    }

    class IQModulator : DataProcessingModule
    {
        InputPin<IQ> DataIn;
        OutputPin<float> DataOut;

        int NFREQ;
        float[] Frequencies;
        float FreqAdj;
        float[] InitialPhases;

        float SamplingFreq;
        int BlockSize;

        bool[] GeneratorsDone;

        // Carrier generators
        Generator[] gI;
        Generator[] gQ;
        // Modulation Envelopes for I and Q 
        FIR[] fI;
        FIR[] fQ;

        // Current frequency used
        int CurrFreqIndex = 0;

        float[] TempModBuffer;
        float[] OutputBuffer;
        List<float> OutputData;

        /// <summary>
        /// Constructor for the Modulator class.
        /// </summary>
        /// <param name="lowFreq">Lowest frequency (inclusive) in the set.</param>
        /// <param name="highFreq">Highest frequency (inclusive) in the set.</param>
        /// <param name="numFreq">Number of frequencies(from lowFreq to highFreq), or 1 if single frequency is used.</param>
        /// <param name="samplingRate">The rate at which the carrier(s) are sampled.</param>
        /// <param name="symbolRate">The desired symbol rate - one symbol will have samplingRate/symbolRate samples generated.</param>
        /// <param name="symbolShaperArray">Array that specifies the carrier modulation envelope.</param>
        public IQModulator(float lowFreq, float highFreq, int numFreq, float processingRate, float symbolRate, float[] symbolShaperArray)
        {
            NFREQ = numFreq;
            SamplingFreq = processingRate;
            BlockSize = (int)(processingRate / symbolRate);
            if ((int)(BlockSize * symbolRate + 0.5) != (int)processingRate)
            {
                throw new ApplicationException("The processingRate must be integer multiple of symbolRate");
            }
            gI = new Generator[NFREQ];
            gQ = new Generator[NFREQ];
            fI = new FIR[NFREQ];
            fQ = new FIR[NFREQ];
            Frequencies = new float[NFREQ];        // array of all frequencies
            GeneratorsDone = new bool[NFREQ];
            InitialPhases = new float[NFREQ];

            TempModBuffer = new float[BlockSize];
            OutputBuffer = new float[BlockSize];
            OutputData = new List<float>(BlockSize);

            if (symbolShaperArray == null)
            {
                symbolShaperArray = new float[BlockSize];
                for (int i = 0; i < BlockSize; i++)
                    symbolShaperArray[i] = 1.0f;
            }

            // Evenly distribute all frequencies between Hi and Lo
            float OneChannelBW;
            if (NFREQ <= 1)
            {
                OneChannelBW = (highFreq + lowFreq) / 2;
                lowFreq = OneChannelBW;
            }
            else
            {
                OneChannelBW = (highFreq - lowFreq) / (NFREQ - 1);
            }
            for (int i = 0; i < NFREQ; i++)
            {
                float Freq = lowFreq + i * OneChannelBW;
                Frequencies[i] = Freq;
                gI[i] = new Generator();
                gQ[i] = new Generator();
                InitialPhases[i] = 0;

                fI[i] = new FIR(symbolShaperArray, BlockSize);
                fQ[i] = new FIR(symbolShaperArray, BlockSize);
            }
            Init();
        }

        /// <summary>
        /// Constructor for the Modulator class.
        /// </summary>
        /// <param name="freqArray">Array of frequencies.</param>
        /// <param name="numFreq">Number of frequencies(from lowFreq to highFreq), or 1 if single frequency is used.</param>
        /// <param name="samplingRate">The rate at which the carrier(s) are sampled.</param>
        /// <param name="symbolRate">The desired symbol rate - one symbol will have samplingRate/symbolRate samples generated.</param>
        /// <param name="symbolShaperArray">Array that specifies the carrier modulation envelope.</param>
        public IQModulator(float[] freqArray, int numFreq, float processingRate, float symbolRate, float[] symbolShaperArray)
        {
            NFREQ = numFreq;
            SamplingFreq = processingRate;
            BlockSize = (int)(processingRate / symbolRate);
            if ((int)(BlockSize * symbolRate + 0.5f) != (int)processingRate)
            {
                throw new ApplicationException("The processingRate must be integer multiple of symbolRate");
            }
            gI = new Generator[NFREQ];
            gQ = new Generator[NFREQ];
            fI = new FIR[NFREQ];
            fQ = new FIR[NFREQ];
            Frequencies = new float[NFREQ];        // array of all frequencies
            GeneratorsDone = new bool[NFREQ];
            InitialPhases = new float[NFREQ];

            OutputBuffer = new float[BlockSize];
            TempModBuffer = new float[BlockSize];

            if (symbolShaperArray == null)
            {
                symbolShaperArray = new float[BlockSize];
                for (int i = 0; i < BlockSize; i++)
                    symbolShaperArray[i] = 1.0f;
            }

            freqArray.CopyTo(Frequencies, 0);
            for (int i = 0; i < NFREQ; i++)
            {
                gI[i] = new Generator();
                gQ[i] = new Generator();
                InitialPhases[i] = 0;

                fI[i] = new FIR(symbolShaperArray, BlockSize);
                fQ[i] = new FIR(symbolShaperArray, BlockSize);
            }
            Init();
        }

        public override void Init()
        {
            for (int i = 0; i < NFREQ; i++)
            {
                gI[i].Init(Frequencies[i], SamplingFreq, InitialPhases[i] + (float)Math.PI / 2);
                gQ[i].Init(Frequencies[i], SamplingFreq, InitialPhases[i]);

                fI[i].Init();
                fQ[i].Init();

                GeneratorsDone[i] = true;
            }
            CurrFreqIndex = 0;
            FreqAdj = 0;
            OutputData.Clear();
        }


        public void Init(float[] Phases)
        {
            Phases.CopyTo(InitialPhases, 0);
            Init();
        }

        /// <summary>
        /// Get/set current working Index.
        /// </summary>
        public int Index
        {
            get { return CurrFreqIndex; }
            set { CurrFreqIndex = value; }
        }

        public float FrequencyOffset
        {
            get { return FreqAdj; }
            set
            {
                FreqAdj = value;
                for (int i = 0; i < NFREQ; i++)
                {
                    gI[i].Frequency = Frequencies[i] + FreqAdj;
                    gQ[i].Frequency = Frequencies[i] + FreqAdj;
                }
            }
        }


        public int GetData(float[] outData)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outData);
            OutputData.Clear();
            return ret;
        }

        public int GetData(float[] outData, int startingIndex)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outData, startingIndex);
            OutputData.Clear();
            return ret;
        }

        /// <summary>
        /// The indexer for the modulator - allows selectively to work with individual frequencies in a set.
        /// </summary>
        /// <param name="freqIndex">Index for the frequency we will be currently working.</param>
        /// <returns></returns>
        public IQModulator this[int freqIndex]
        {
            get { CurrFreqIndex = freqIndex; return this; }
        }

        /// <summary>
        /// The indexer for the modulator - allows selectively to work with individual frequencies in a set.
        /// </summary>
        /// <param name="freq">The frequency we will be currently working.</param>
        /// <returns></returns>
        public IQModulator this[float freq]
        {
            get
            {
                CurrFreqIndex = Array.IndexOf<float>(Frequencies, freq);
                return this;
            }
        }

        public float Frequency
        {
            get { return Frequencies[CurrFreqIndex]; }
            set
            {
                Frequencies[CurrFreqIndex] = value;
                gI[CurrFreqIndex].Frequency = value;
                gQ[CurrFreqIndex].Frequency = value;
            }
        }

        public float Phase
        {
            get { return gI[CurrFreqIndex].Phase; }
            set
            {
                gI[CurrFreqIndex].Phase = value + (float)Math.PI / 2;
                gQ[CurrFreqIndex].Phase = value;
            }
        }

        /// <summary>
        /// Check if this is a first call to the function. If it is - clear the buffer.
        /// </summary>
        void PrepareOutputBuffer(float[] outBuffer, int startingIdx)
        {
            if (GeneratorsDone[CurrFreqIndex])
            {
                for (int i = 0; i < NFREQ; i++)
                {
                    // If some of the generators were not used - advance them so all of them are in phase
                    if (!GeneratorsDone[i])
                    {
                        fI[i].InterpolateVoid();
                        gI[i].GenerateVoid(BlockSize);
                        fQ[i].InterpolateVoid();
                        gQ[i].GenerateVoid(BlockSize);
                    }
                }
                Array.Clear(GeneratorsDone, 0, NFREQ);  // Mark all generators as non-processed
                Array.Clear(outBuffer, startingIdx, BlockSize);
            }
        }

        /// <summary>
        /// Process supplied I and Q components and apply them to the current frequency.
        /// </summary>
        /// <param name="data">IQ components of Quadrature signal.</param>
        /// <returns>true - if I and Q were placed and we still can continue. false - if all frequencies are done and we have to read the buffer.</returns>
        public int Process(IQ data)
        {
            PrepareOutputBuffer(OutputBuffer, 0);
            // encode the I bit
            fI[CurrFreqIndex].Interpolate(data.I, TempModBuffer);
            gI[CurrFreqIndex].GenerateAdd(TempModBuffer, OutputBuffer); // Modulate a carrier with symbol

            // encode the Q bit 
            fQ[CurrFreqIndex].Interpolate(data.Q, TempModBuffer);
            gQ[CurrFreqIndex].GenerateAdd(TempModBuffer, OutputBuffer); // Modulate a carrier with symbol
            GeneratorsDone[CurrFreqIndex] = true;

            // Advance current frequency index and check for wrap-around
            CurrFreqIndex++; if (CurrFreqIndex >= NFREQ) CurrFreqIndex = 0;
            if (GeneratorsDone[CurrFreqIndex])
            {
                OutputData.AddRange(OutputBuffer);
            }
            return OutputData.Count;
        }

        /// <summary>
        /// Process supplied I and Q components and apply them to the current frequency.
        /// </summary>
        /// <param name="data">IQ components of Qudrature signal.</param>
        /// <param name="outputArray">The array that will receive the result.</param>
        /// <returns>true - if I and Q were placed and we still can continue. false - if all frequencies are done and we have to read the buffer.</returns>
        public int Process(IQ data, float[] outputArray)
        {
            PrepareOutputBuffer(outputArray, 0);
            // encode the I bit
            fI[CurrFreqIndex].Interpolate(data.I, TempModBuffer);
            gI[CurrFreqIndex].GenerateAdd(TempModBuffer, outputArray); // Modulate a carrier with symbol

            // encode the Q bit 
            fQ[CurrFreqIndex].Interpolate(data.Q, TempModBuffer);
            gQ[CurrFreqIndex].GenerateAdd(TempModBuffer, outputArray); // Modulate a carrier with symbol
            GeneratorsDone[CurrFreqIndex] = true;

            // Advance current frequency index and check for wrap-around
            CurrFreqIndex++; if (CurrFreqIndex >= NFREQ) CurrFreqIndex = 0;
            return GeneratorsDone[CurrFreqIndex] ? BlockSize : 0;
        }

        public int Process(IQ data, float[] outputArray, int outputIndex)
        {
            PrepareOutputBuffer(outputArray, outputIndex);
            // encode the I bit
            fI[CurrFreqIndex].Interpolate(data.I, TempModBuffer);
            gI[CurrFreqIndex].GenerateAdd(TempModBuffer, outputArray, outputIndex); // Modulate a carrier with symbol

            // encode the Q bit 
            fQ[CurrFreqIndex].Interpolate(data.Q, TempModBuffer);
            gQ[CurrFreqIndex].GenerateAdd(TempModBuffer, outputArray, outputIndex); // Modulate a carrier with symbol
            GeneratorsDone[CurrFreqIndex] = true;

            // Advance current frequency index and check for wrap-around
            CurrFreqIndex++; if (CurrFreqIndex >= NFREQ) CurrFreqIndex = 0;
            return GeneratorsDone[CurrFreqIndex] ? BlockSize : 0;
        }

        public void Process(CNTRL_MSG controlParam, IQ data)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                PrepareOutputBuffer(OutputBuffer, 0);
                // encode the I bit
                fI[CurrFreqIndex].Interpolate(data.I, TempModBuffer);
                gI[CurrFreqIndex].GenerateAdd(TempModBuffer, OutputBuffer); // Modulate a carrier with symbol

                // encode the Q bit 
                fQ[CurrFreqIndex].Interpolate(data.Q, TempModBuffer);
                gQ[CurrFreqIndex].GenerateAdd(TempModBuffer, OutputBuffer); // Modulate a carrier with symbol
                GeneratorsDone[CurrFreqIndex] = true;

                // Advance current frequency index and check for wrap-around
                CurrFreqIndex++; if (CurrFreqIndex >= NFREQ) CurrFreqIndex = 0;
                if (GeneratorsDone[CurrFreqIndex])
                {
                    foreach (float samp in OutputBuffer) DataOut.Process(samp);
                }
            }
            else if (controlParam == CNTRL_MSG.FINISH)
            {
                for (int i = 0; i < NFREQ; i++)
                {
                    if (!GeneratorsDone[i])
                    {
                        // encode the I bit
                        fI[i].Interpolate(0, TempModBuffer);
                        gI[i].GenerateAdd(TempModBuffer, OutputBuffer); // Modulate a carrier with symbol

                        // encode the Q bit 
                        fQ[i].Interpolate(0, TempModBuffer);
                        gQ[i].GenerateAdd(TempModBuffer, OutputBuffer); // Modulate a carrier with symbol
                        GeneratorsDone[i] = true;
                    }
                }
                foreach (float samp in OutputBuffer) DataOut.Process(samp);
                // re-initialize index
                CurrFreqIndex = 0;
            }
        }


        /// <summary>
        /// In case of parallel tone modulator - fill remaining frequencies with 0 tails.
        /// </summary>
        /// <param name="outputArray">Resulting output buffer.</param>
        public int Finish(float[] outputArray)
        {
            PrepareOutputBuffer(outputArray, 0);
            for (int i = 0; i < NFREQ; i++)
            {
                if (!GeneratorsDone[i])
                {
                    // encode the I bit
                    fI[i].Interpolate(0, TempModBuffer);
                    gI[i].GenerateAdd(TempModBuffer, outputArray); // Modulate a carrier with symbol

                    // encode the Q bit 
                    fQ[i].Interpolate(0, TempModBuffer);
                    gQ[i].GenerateAdd(TempModBuffer, outputArray); // Modulate a carrier with symbol
                    GeneratorsDone[i] = true;
                }
            }
            // re-initialize index
            CurrFreqIndex = 0;
            return BlockSize;
        }

        /// <summary>
        /// In case of parallel tone modulator - fill remaining frequencies with 0 tails.
        /// </summary>
        /// <param name="outputArray">Resulting output buffer.</param>
        public int Finish(float[] outputArray, int outputIndex)
        {
            PrepareOutputBuffer(outputArray, outputIndex);
            for (int i = 0; i < NFREQ; i++)
            {
                if (!GeneratorsDone[i])
                {
                    // encode the I bit
                    fI[i].Interpolate(0, TempModBuffer);
                    gI[i].GenerateAdd(TempModBuffer, outputArray, outputIndex); // Modulate a carrier with symbol

                    // encode the Q bit 
                    fQ[i].Interpolate(0, TempModBuffer);
                    gQ[i].GenerateAdd(TempModBuffer, outputArray, outputIndex); // Modulate a carrier with symbol
                    GeneratorsDone[i] = true;
                }
            }
            // re-initialize index
            CurrFreqIndex = 0;
            return BlockSize;
        }

        /// <summary>
        /// In case of parallel tone modulator - fill remaining frequencies with 0 tails.
        /// </summary>
        public int Finish()
        {
            PrepareOutputBuffer(OutputBuffer, 0);

            for (int i = 0; i < NFREQ; i++)
            {
                if (!GeneratorsDone[i])
                {
                    // encode the I bit
                    fI[i].Interpolate(0, TempModBuffer);
                    gI[i].GenerateAdd(TempModBuffer, OutputBuffer); // Modulate a carrier with symbol

                    // encode the Q bit 
                    fQ[i].Interpolate(0, TempModBuffer);
                    gQ[i].GenerateAdd(TempModBuffer, OutputBuffer); // Modulate a carrier with symbol
                    GeneratorsDone[i] = true;
                }
            }
            OutputData.AddRange(OutputBuffer);
            // re-initialize index
            CurrFreqIndex = 0;
            return OutputData.Count;
        }

        public int Count { get { return OutputData.Count; } }
        public bool IsDataReady { get { return (OutputData.Count > 0); } }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<IQ>("DataIn", this.Process);
            DataOut = new OutputPin<float>("DataOut");
            base.SetIOParameters("IQtoSamplesModulator", new DataPin[] { DataIn, DataOut });
        }
    }
}
