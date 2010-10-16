using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    class OFDMDemodulator : DataProcessingModule
    {
        InputPin<float> DataIn;
        OutputPin<IQ> DataOut;

        Queue<IQ> OutputData;

        int NFREQ;
        float[] Frequencies;
        float[] InitialPhases;

        float FreqAdj = 0;

        float SamplingFreq;
        int BlockSize;
        int ActiveSize;
        int CurrFreqIndex = 0;

        
        Quad[] IQGens;                    // Carrier generators
        IntegrateAndDump[] IQDemod;       // Simple Integrate and dump demodulator

        IQ[] CorrRotate;

        public OFDMDemodulator(float lowFreq, float highFreq, int numFreq, float processingRate, float symbolRate, float activePart)
        {
            NFREQ = numFreq;
            SamplingFreq = processingRate;
            BlockSize = (int)((processingRate / symbolRate) + 0.5f);
            ActiveSize = (int)(BlockSize * activePart + 0.5f);
            if ((int)(BlockSize * symbolRate + 0.5f) != (int)processingRate)
            {
                throw new ApplicationException("The processingRate must be integer multiple of symbolRate");
            }
            IQGens = new Quad[NFREQ];
            IQDemod = new IntegrateAndDump[NFREQ];
            Frequencies = new float[NFREQ];        // array of all frequencies
            InitialPhases = new float[NFREQ];
            
            CorrRotate = new IQ[NFREQ];

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
            }
            Array.Clear(InitialPhases, 0, NFREQ);
            Init();
        }

        public OFDMDemodulator(float[] freqArray, int numFreq, float processingRate, float symbolRate, float activePart)
        {
            NFREQ = numFreq;
            SamplingFreq = processingRate;
            BlockSize = (int)( (processingRate / symbolRate) + 0.5f);
            ActiveSize = (int)(BlockSize * activePart + 0.5f);
            if ((int)(BlockSize * symbolRate + 0.5f) != (int)processingRate)
            {
                throw new ApplicationException("The processingRate must be integer multiple of symbolRate");
            }
            IQGens = new Quad[NFREQ];
            IQDemod = new IntegrateAndDump[NFREQ];
            Frequencies = new float[NFREQ];        // array of all frequencies
            InitialPhases = new float[NFREQ];

            CorrRotate = new IQ[NFREQ];

            freqArray.CopyTo(Frequencies, 0);
            Array.Clear(InitialPhases, 0, NFREQ);
            Init();
        }

        public override void Init()
        {
            for (int i = 0; i < NFREQ; i++)
            {
                IQGens[i] = new Quad(Frequencies[i], SamplingFreq, InitialPhases[i]);
                IQDemod[i] = new IntegrateAndDump(SYNC_TYPE.GARDNER_DD | SYNC_TYPE.MUELLER_NDA | SYNC_TYPE.QAMLD_NDA | SYNC_TYPE.ZERODET_NDA, BlockSize, ActiveSize);
                CorrRotate[i] = IQ.UNITY;
            }
            FreqAdj = 0;
            CurrFreqIndex = 0;
            OutputData = new Queue<IQ>();
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
                    IQGens[i] = new Quad(Frequencies[i] + FreqAdj, SamplingFreq, IQGens[i]);
                }
            }
        }

        public int Index
        {
            get { return CurrFreqIndex; }
            set { CurrFreqIndex = value; }
        }

        public OFDMDemodulator this[int freqIndex]
        {
            get { CurrFreqIndex = freqIndex; return this; }
        }

        public OFDMDemodulator this[float freq]
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
                IQGens[CurrFreqIndex] = new Quad(value, SamplingFreq, IQGens[CurrFreqIndex]);
            }
        }

        public float Phase
        {
            get { return IQGens[CurrFreqIndex].Value.Phase; }
            set
            {
                IQGens[CurrFreqIndex] = new Quad(Frequencies[CurrFreqIndex], SamplingFreq, value);
            }
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
            IQ Data;

            bool DataReady = true;
            for (int idx = 0; idx < NFREQ; idx++)
            {
                IQGens[idx].Process(incomingSample, out Data);
                IQDemod[idx].Process(Data);
                DataReady = DataReady && (IQDemod[idx].Count > 0);
            }
            if (DataReady)
            {
                for (int idx = 0; idx < NFREQ; idx++)
                {
                    Data = IQDemod[idx].GetData() * CorrRotate[idx];
                    OutputData.Enqueue(Data);
                }
            }
            return OutputData.Count;
        }

        public void Process(CNTRL_MSG controlParam, float incomingSample)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                IQ Data;
                bool DataReady = true;
                for (int idx = 0; idx < NFREQ; idx++)
                {
                    IQGens[idx].Process(incomingSample, out Data);
                    IQDemod[idx].Process(Data);
                    DataReady = DataReady && (IQDemod[idx].Count > 0);
                }
                if (DataReady)
                {
                    DataOut.Process(CNTRL_MSG.NEW_SYMBOL);
                    for (int idx = 0; idx < NFREQ; idx++)
                    {
                        Data = IQDemod[idx].GetData() * CorrRotate[idx];
                        DataOut.Process(Data);
                    }
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
                this.CorrRotate[CurrFreqIndex] = value;
            }
            get { return this.CorrRotate[CurrFreqIndex]; }
        }

        public float SignalEnergy { get { return IQDemod[CurrFreqIndex].SignalEnergy; } }

        public float FrequencyEnergy { get { return IQDemod[CurrFreqIndex].FrequencyEnergy; } }

        public int Count { get { return OutputData.Count; } }

        public bool IsDataReady { get { return (OutputData.Count > 0); } }

        public int StartingOffset
        {
            get { return IQDemod[CurrFreqIndex].StartingOffset; }
            set { IQDemod[CurrFreqIndex].StartingOffset = value; }
        }

        public IQ GetData()
        {
            return OutputData.Dequeue();
        }

        public int GetData(IQ[] outData)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outData, 0);
            OutputData.Clear();
            return ret;
        }

        public int GetData(IQ[] outData, int startingIndex)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outData, startingIndex);
            OutputData.Clear();
            return ret;
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<float>("DataIn", this.Process);
            DataOut = new OutputPin<IQ>("DataOut");
            base.SetIOParameters("OFDMDemodulator", new DataPin[] { DataIn, DataOut });
        }

    }

    class IQDetector : DataProcessingModule
    {
        InputPin<float> DataIn;
        OutputPin<int> DataOut;

        Queue<int> OutputData;

        double InitialPhase;
        int InitialValue;
        int SymbolsToDetectCorrection;
        bool DoTimingCorrection;
        bool FirstSymbol;

        float FreqAdj = 0;
        IQ CorrRotate = IQ.UNITY;

        float CarrierFrequency;
        float SymbolRate;
        float SamplingFreq;
        int BlockSize;

        Quad IQGenerator;                       // Carrier generator
        IntegrateAndDump IQDemodulator;         // Simple Integrate and dump demodulator
        IQDecoder IQDecoder;                    // Simple BPSK decoder

        public IQDetector(float carrierFreq, float processingRate, float symbolRate, double initPhase, int initValue, int symbolsToUseInCorrection, bool useTimingCorrection)
        {
            CarrierFrequency = carrierFreq;
            SamplingFreq = processingRate;
            SymbolRate = symbolRate;

            BlockSize = (int)(processingRate / symbolRate + 0.5f);
            if ((int)(BlockSize * symbolRate + 0.5f) != (int)processingRate)
            {
                throw new ApplicationException("The processingRate must be integer multiple of symbolRate");
            }
            InitialPhase = initPhase;
            InitialValue = initValue;
            SymbolsToDetectCorrection = symbolsToUseInCorrection;
            DoTimingCorrection = useTimingCorrection;
            Init();
        }

        public IQDetector(float carrierFreq, float processingRate, float symbolRate)
            : this(carrierFreq, processingRate, symbolRate, 0, 0, -10, true)
        {
        }

        public override void Init()
        {
            IQGenerator = new Quad(CarrierFrequency, SamplingFreq, InitialPhase);
            SYNC_TYPE st = DoTimingCorrection ? SYNC_TYPE.GARDNER_DD | SYNC_TYPE.MUELLER_NDA | SYNC_TYPE.QAMLD_NDA | SYNC_TYPE.ZERODET_NDA : SYNC_TYPE.NONE;
            IQDemodulator = new IntegrateAndDump(st, BlockSize, BlockSize);
            IQDecoder = new IQDecoder(1, Constellation.Bits_Simple_BPSK, Constellation.IQ_Simple_BPSK, EncodingType.NON_DIFF);

            FreqAdj = 0;
            CorrRotate = IQ.UNITY;
            OutputData = new Queue<int>();
            FirstSymbol = true;
        }

        public void Init(float initPhase)
        {
            InitialPhase = initPhase;
            Init();
        }

        public float FrequencyOffset
        {
            get { return FreqAdj + (float)((IQDecoder.RotateCorrection.Phase * SymbolRate) / (2.0 * Math.PI));  }
            set
            {
                FreqAdj = value;
                IQGenerator = new Quad(CarrierFrequency + FreqAdj, SamplingFreq, IQGenerator);
            }
        }

        public float Frequency
        {
            get { return CarrierFrequency; }
            set
            {
                CarrierFrequency = value;
                IQGenerator = new Quad(CarrierFrequency + FreqAdj, SamplingFreq, IQGenerator);
            }
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
            IQ Data;
            IQ Target;
            IQ Diff;
            int BitData;
            IQGenerator.Process(incomingSample, out Data);
            IQDemodulator.Process(Data);
            while (IQDemodulator.Count > 0)
            {
                Data = IQDemodulator.GetData();
                if (Data != IQ.ZERO)
                {
                    if (FirstSymbol)
                    {
                        FirstSymbol = false;
                        Target = Constellation.IQ_Simple_BPSK[Constellation.Bits_Simple_BPSK[InitialValue]];
                        Diff = Target / Data;
                        this.CorrRotate = Diff / Data.R2;
                        IQDecoder.StartCorrectionProcess(SymbolsToDetectCorrection);
                        IQDecoder.PreviousIQ = Target;
                    }
                    IQDecoder.Process(Data * CorrRotate, out BitData);
                    OutputData.Enqueue(BitData);
                    // Re-adjust constellation
                    Diff = IQDecoder.Target / Data;
                    this.CorrRotate = Diff / Data.R2;
                }
            }
            return OutputData.Count;
        }

        public void Process(CNTRL_MSG controlParam, float incomingSample)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                IQ Data;
                IQ Target;
                IQ Diff;
                int BitData;
                IQGenerator.Process(incomingSample, out Data);
                IQDemodulator.Process(Data);
                while (IQDemodulator.Count > 0)
                {
                    Data = IQDemodulator.GetData();
                    if (Data != IQ.ZERO)
                    {
                        if (FirstSymbol)
                        {
                            FirstSymbol = false;
                            Target = Constellation.IQ_Simple_BPSK[Constellation.Bits_Simple_BPSK[InitialValue]];
                            Diff = Target / Data;
                            this.CorrRotate = Diff / Data.R2;
                            IQDecoder.StartCorrectionProcess(SymbolsToDetectCorrection);
                            IQDecoder.PreviousIQ = Target;
                        }
                        IQDecoder.Process(Data * CorrRotate, out BitData);
                        DataOut.Process(BitData);
                        // Re-adjust constellation
                        Diff = IQDecoder.Target / Data;
                        this.CorrRotate = Diff / Data.R2;
                    }
                }
            }
        }

        public void StartCorrectionProcess(int symbolsToUseInCorrection)
        {
            SymbolsToDetectCorrection = symbolsToUseInCorrection;
            IQDecoder.StartCorrectionProcess(SymbolsToDetectCorrection);
        }


        public float SignalEnergy { get { return IQDemodulator.SignalEnergy; } }

        public float FrequencyEnergy { get { return IQDemodulator.FrequencyEnergy; } }

        public int Count { get { return OutputData.Count; } }

        public int StartingOffset
        {
            get { return IQDemodulator.StartingOffset; }
            set { IQDemodulator.StartingOffset = value; }
        }

        public bool IsCorrectionReady
        {
            get { return IQDecoder.IsCorrectionReady; }
        }

        public bool IsDataReady { get { return (OutputData.Count > 0); } }

        public int GetData()
        {
            return OutputData.Dequeue();
        }

        public int GetData(int[] outData)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outData, 0);
            OutputData.Clear();
            return ret;
        }

        public int GetData(int[] outData, int startingIndex)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outData, startingIndex);
            OutputData.Clear();
            return ret;
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<float>("DataIn", this.Process);
            DataOut = new OutputPin<int>("DataOut");
            base.SetIOParameters("Frequency Detector", new DataPin[] { DataIn, DataOut });
        }
    }
}
