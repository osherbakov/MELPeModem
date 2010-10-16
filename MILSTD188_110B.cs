using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    class MILSTD188_110B
    {

        static int[] MGDTable8 = { 0, 7, 3, 4, 1, 6, 2, 5 };
        static int[] MGDTable4 = { 0, 6, 2, 4 };
        static int[] MGDTable2 = { 0, 4 };
        static int[] MGDTable75N = { 0, 3, 1, 2 };

        static ModeInfo[] supportedModes = {
            new ModeInfo(MILSTD_188.Mode.D_4800N, 7, 6, 3,  0,  0,   32,  16, 1440,  1, 3, MGDTable8),

            new ModeInfo(MILSTD_188.Mode.V_2400S, 7, 7, 3,  40, 72,  32,  16, 1440,  1, 3, MGDTable8),

            new ModeInfo(MILSTD_188.Mode.D_2400S, 6, 4, 3,  40, 72,  32,  16, 1440,  1, 3, MGDTable8),
            new ModeInfo(MILSTD_188.Mode.D_2400L, 4, 4, 24, 40, 576, 32,  16, 11520, 1, 3, MGDTable8),
            new ModeInfo(MILSTD_188.Mode.D_1200S, 6, 5, 3,  40, 36,  20,  20, 1440,  1, 2, MGDTable4),
            new ModeInfo(MILSTD_188.Mode.D_1200L, 4, 5, 24, 40, 288, 20,  20, 11520, 1, 2, MGDTable4),
            new ModeInfo(MILSTD_188.Mode.D_600S, 6, 6,  3,  40, 18,  20,  20, 1440,  1, 1, MGDTable2),
            new ModeInfo(MILSTD_188.Mode.D_600L, 4, 6,  24, 40, 144, 20,  20, 11520, 1, 1, MGDTable2),
            new ModeInfo(MILSTD_188.Mode.D_300S, 6, 7,  3,  40, 18,  20,  20, 1440,  2, 1, MGDTable2),
            new ModeInfo(MILSTD_188.Mode.D_300L, 4, 7,  24, 40, 144, 20,  20, 11520, 2, 1, MGDTable2),
            new ModeInfo(MILSTD_188.Mode.D_150S, 7, 4,  3,  40, 18,  20,  20, 1440,  4, 1, MGDTable2),
            new ModeInfo(MILSTD_188.Mode.D_150L, 5, 4,  24, 40, 144, 20,  20, 11520, 4, 1, MGDTable2),
            new ModeInfo(MILSTD_188.Mode.D_75S, 7, 5,   3,  10, 9,   45,  0,  1440,  1, 2, MGDTable75N),
            new ModeInfo(MILSTD_188.Mode.D_75L, 5, 5,   24, 20, 36,  360, 0,  11520, 1, 2, MGDTable75N),
        };

        public static Modes modemModes;

        static MILSTD188_110B()
        {
            modemModes = new Modes(MILSTD188_110B.supportedModes);
        }


        static int[] ChanSymb0 = { 0, 0, 0, 0, 0, 0, 0, 0 };
        static int[] ChanSymb1 = { 0, 4, 0, 4, 0, 4, 0, 4 };
        static int[] ChanSymb2 = { 0, 0, 4, 4, 0, 0, 4, 4 };
        static int[] ChanSymb3 = { 0, 4, 4, 0, 0, 4, 4, 0 };
        static int[] ChanSymb4 = { 0, 0, 0, 0, 4, 4, 4, 4 };
        static int[] ChanSymb5 = { 0, 4, 0, 4, 4, 0, 4, 0 };
        static int[] ChanSymb6 = { 0, 0, 4, 4, 4, 4, 0, 0 };
        static int[] ChanSymb7 = { 0, 4, 4, 0, 4, 0, 0, 4 };
        static int[][] ChanSymbToTribit = { ChanSymb0, ChanSymb1, ChanSymb2, ChanSymb3, ChanSymb4, ChanSymb5, ChanSymb6, ChanSymb7 };

        static int[] SyncPreamble =  { 0, 1, 3, 0, 1, 3, 1, 2, 0, };        // D1 D2 C1 C2 C3  0
        static int FECEncoderRate = 2;
        static int FECEncoderConstraint = 7;
        static int[] FECEncoderPoly = { 0x5b, 0x79, 0x5b, 0x79, 0x5b, 0x79, 0x5b, 0x79 };


        public const float SYMBOLRATE = 2400;
        public const float CARRIER_FREQ = 1800;
        public const int BITS_PER_SYMBOL = 3;
        public const int NUM_FREQ = 1;
        public const int SCRAMBLE_MASK = (1 << MILSTD188_110B.BITS_PER_SYMBOL) - 1;

        public const int InterleaverFlushBits = 144;

        public TxModem Tx;
        public RxModem Rx;

        public MILSTD188_110B(MILSTD_188.Mode modemMode, float inputFreq, float processingFreq, float outputFreq, float[] symbolFilter, float[] inputFilter, float[] outputFilter)
        {
            Rx = new RxModem(inputFreq, processingFreq, inputFilter, symbolFilter);
            Tx = new TxModem(modemMode, processingFreq, outputFreq, symbolFilter, outputFilter);
        }

        public class RxModem
        {
            RxStateFunc PrevFunction;
            RxStateFunc NextFunction;

            float EnergyThreshold = 0.000001f;
            float SignalThreshold = 0.0001f;

            float CorrAverageThreshold = 60f;
            float CorrEnergyThreshold = 30f;
            float CorrTargetThreshold = 0.8f;

            const int INTERP_BUFFER_SIZE = 20;
            const int SYMBOL_SYNC_SIZE = 100;

            int SymbolsCounter;     // Generic counter
            int PreambuleCounter;   // We check the preambule proper countdown
            int ProbeCounter;       // Counts Probe (KnownData) symbols

            IQ[] PreambleBlock = new IQ[32 * 15];   // Preamble comes in  32*15 chunks
            int PreambleIdx;

            ModeInfo CurrentMode;

            int[] DataBlock;
            int TotalPatternsInBlock;
            IQ[] ProbeTarget;
            int[] Probe;

            float InputFrequency;
            float ProcessingFrequency;
            int DECFACTOR;
            int INTFACTOR;
            FIR InputFilter;

            float[] InputFilterCoeffs;
            float[] SymbolFilterCoeffs;

            // Encoder to generate Probe patterns that we use to sync to
            IQEncoder ProbeEncoder;
            //  Demodulator
            IQDemodulator Demodulator;
            // Correlator to catch the symbol pattern and sync on it
            Correlator SyncCorr;
            // Correlator to lock into Probe patterns and get Frequency and Phase params
            Correlator ProbeCorr;
            // Detector to extract info from preamble (sent at 75 bps)
            SymbolDetector SymbDetector;
            // EOM Correlator
            BitCorrelator EOMDetector;

            // Decoder
            IQDecoder Decoder;
            // Deta scrambler
            LFSR_188_110A DataScrambler;

            // Data interleaver
            Interleaver Interleaver;
            VitDecoder FECDecoder;
            byte[] FECBuffer;

            float[] InterpBuffer;
            int InterpBlockSize;

            Queue<byte> OutputData = new Queue<byte>();

            // Preamble pattern that will be correlated with
            static IQ[] PreamblePattern = new IQ[SyncPreamble.Length * 32];

            public RxModem(float samplingFreq, float processingFreq, float[] interpFilter, float[] symbFilter)
            {
                InputFrequency = samplingFreq;
                ProcessingFrequency = processingFreq;
                InputFilterCoeffs = interpFilter;
                SymbolFilterCoeffs = symbFilter;
                DECFACTOR = (int)(processingFreq / SYMBOLRATE);
                INTFACTOR = (int)(processingFreq / samplingFreq);

                InputFilter = new FIR(InputFilterCoeffs, INTFACTOR);
                InterpBlockSize = DECFACTOR * INTERP_BUFFER_SIZE;
                InterpBuffer = new float[InterpBlockSize * INTFACTOR];
                Init();
            }

            void FillSyncPatterns()
            {
                IQEncoder Enc = new IQEncoder(BITS_PER_SYMBOL, Constellation.Table_1_to_1, Constellation.ITable_8PSK, Constellation.QTable_8PSK, EncodingType.SCRAMBLE_ADD);
                SyncScrambler scrambler = new SyncScrambler();

                int SyncSymbolCounter = 0;
                foreach (int BitChanSymb in SyncPreamble)
                {
                    int[] sequence = ChanSymbToTribit[BitChanSymb];
                    scrambler.Init();
                    // repeat sequence 4 times
                    for (int i = 0; i < 4; i++)
                    {
                        foreach (int tribit in sequence)
                        {
                            Enc.Process(tribit, scrambler.DataNext(), SCRAMBLE_MASK, out PreamblePattern[SyncSymbolCounter]);
                            SyncSymbolCounter++;
                        }
                    }
                }

                IQ[] symb75 = new IQ[32];
                foreach (int[] sequence in ChanSymbToTribit)
                {
                    SyncSymbolCounter = 0;
                    scrambler.Init();
                    for (int i = 0; i < 4; i++)
                    {
                        foreach (int tribit in sequence)
                        {
                            Enc.Process(tribit, scrambler.DataNext(), SCRAMBLE_MASK, out symb75[SyncSymbolCounter]);
                            SyncSymbolCounter++;
                        }
                    }
                    SymbDetector.AddTarget(symb75);
                }
            }

            public void Init()
            {
                SyncCorr = new Correlator(CORR_TYPE.DELTA_DIFF, 15 * 32, 9 * 32, 6 * 32, CorrTargetThreshold, CorrAverageThreshold, CorrEnergyThreshold);
                Demodulator = new IQDemodulator(CARRIER_FREQ - 15, CARRIER_FREQ - 15, NUM_FREQ, ProcessingFrequency, SYMBOLRATE, SymbolFilterCoeffs);
                ProbeEncoder = new IQEncoder(BITS_PER_SYMBOL, Constellation.Table_1_to_1, Constellation.ITable_8PSK, Constellation.QTable_8PSK, EncodingType.SCRAMBLE_ADD);
                SymbDetector = new SymbolDetector();
                EOMDetector = new BitCorrelator();
                FillSyncPatterns();
                SyncCorr.AddTarget(PreamblePattern);
                int FlipEOM = MILSTD_188.MSBFirst(MILSTD_188.EOM);
                EOMDetector.AddTarget(FlipEOM, 32);
                OutputData.Clear();
                InputFilter.Clear();

                PrevFunction = null;
                NextFunction = Idle;
            }


            void Process(int numSamples)
            {
                int Index = 0;
                int nProcessed;
                while (numSamples > 0)
                {
                    // If state function is called the very first time, then
                    // there will be a special call to indicate that. numSamples == 0 to tell that 
                    // we are enering the state, so some state initialization can be done
                    while(PrevFunction != NextFunction)
                    {
                            PrevFunction = NextFunction;
                            NextFunction(InterpBuffer, Index, 0);
                    }
                    nProcessed = NextFunction(InterpBuffer, Index, numSamples);
                    Index += nProcessed;
                    numSamples -= nProcessed;
                }
            }

            public bool Process(float[] incomingSamples, int startingIndex, int numSamples)
            {
                while (numSamples > 0)
                {
                    int nProc = Math.Min(numSamples, InterpBlockSize);
                    int NumInterp = InputFilter.Interpolate(incomingSamples, startingIndex, InterpBuffer, 0, nProc);
                    Process(NumInterp);
                    startingIndex += nProc;
                    numSamples -= nProc;
                }
                return true;
            }

            // Initial state - looking for any reasonable amount of energy in signal
            int Idle(float[] incomingSamples, int startingIndex, int numSamples)
            {
                int i = 0;
                for (i = 0; i < numSamples; i++)
                {
                    float Sample = incomingSamples[startingIndex + i];
                    if ((Sample * Sample) > EnergyThreshold)
                    {
                        NextFunction = LookForCarrierEnergy;
                        numSamples = 0;     // This is how to terminate a loop!!!
                    }
                }
                return i;
            }

            // Looking for any energy in carrier frequency
            int LookForCarrierEnergy(float[] incomingSamples, int startingIndex, int numSamples)
            {
                if (numSamples == 0)
                {
                    Demodulator.Init();
                }
                int i = 0;
                for (i = 0; i < numSamples; i++)
                {
                    float Sample = incomingSamples[startingIndex + i];
                    Demodulator.Process(Sample);
                    if (Demodulator.FrequencyEnergy > Demodulator.SignalEnergy * SignalThreshold)
                    {
                        NextFunction = LookForSymbolSync;
                        numSamples = 0;     // This is how to terminate a loop!!!
                    }
                }
                return i;
            }

            // Looking for symbol sync
            int LookForSymbolSync(float[] incomingSamples, int startingIndex, int numSamples)
            {
                if (numSamples == 0)
                {
                    Demodulator.Init();
                    Demodulator.StartCorrectionProcess(SYNC_TYPE.GARDNER_DD | SYNC_TYPE.GARDNER_NDA | SYNC_TYPE.QAMLD_NDA |
                                            SYNC_TYPE.DIFF_NDA | SYNC_TYPE.ZERODET_NDA | SYNC_TYPE.PEAK_NDA | SYNC_TYPE.CORR_NDA,
                                                SYMBOL_SYNC_SIZE);
                }
                int i, nProc;

                for (i = 0; i < numSamples; i += nProc)
                {
                    nProc = Math.Min(numSamples - i, DECFACTOR);
                    Demodulator.Process(incomingSamples, startingIndex + i, nProc);
                    if (Demodulator.IsSyncReady)
                    {
                        NextFunction = LookForSyncPreamble;
                        numSamples = 0;     // This is how to terminate a loop!!!
                    }
                }
                return i;
            }

            int LookForSyncPreamble(float[] incomingSamples, int startingIndex, int numSamples)
            {
                if (numSamples == 0)
                {
                    SymbolsCounter = 0;
                }
                int i = 0;
                while (i < numSamples)
                {
                    // Feed the data into demodulator by DFAC chunks
                    int nProc = Math.Min(numSamples - i, DECFACTOR);
                    int nSymb = Demodulator.Process(incomingSamples, startingIndex + i, nProc);
                    while(nSymb-- > 0)
                    {
                        // If we went thru 3 preamble segments and did not catch it - 
                        //  something is wrong -  start from the beginning
                        if (SymbolsCounter++ > (3 * 480))
                        {
                            NextFunction = Idle;
                            numSamples = 0;     // This is how to terminate a loop!!!
                            break;
                        }
                        if (SyncCorr.Process(Demodulator.GetData()) > 0)
                        {
                            NextFunction = SyncDetected;
                            numSamples = 0;     // This is how to terminate a loop!!!
                            break;
                        }
                    }
                    i += nProc;
                }
                return i;
            }

            void ProcessPreamble(out int d1, out int d2, out int counter, out int z)
            {
                d1 = SymbDetector.Process(PreambleBlock, 9 * 32);
                d2 = SymbDetector.Process(PreambleBlock, 10 * 32);
                int C1 = SymbDetector.Process(PreambleBlock, 11 * 32);
                int C2 = SymbDetector.Process(PreambleBlock, 12 * 32);
                int C3 = SymbDetector.Process(PreambleBlock, 13 * 32);
                z = SymbDetector.Process(PreambleBlock, 14 * 32);
                counter = ((C1 & 0x3) << 4) | ((C2 & 0x3) << 2) | ((C3 & 0x3) << 0);
            }

            int SyncDetected(float[] incomingSamples, int startingIndex, int numSamples)
            {
                if (numSamples == 0)
                {
                    // Adjust demodulator
                    Demodulator.RotateCorrection = SyncCorr.RotateCorrection;

                    // Frequency Correction on receiving the first Sync block
                    // Only apply correction if the accumulated error will be more that 45 degrees
                    if (Math.Abs(SyncCorr.FrequencyCorrection.Degrees * SyncCorr.CorrelationMaxLength) > 45)
                        Demodulator.FrequencyCorrection = SyncCorr.FrequencyCorrection;
                    // Get the last 15*32 symbols
                    SyncCorr.GetLastData(SyncCorr.CorrelationMaxIndex, PreambleBlock, 15 * 32);
                    int D1, D2, Z, Cnt;
                    ProcessPreamble(out D1, out D2, out Cnt, out Z);
                    // Check for the reasonable values that we extracted from the preamble
                    // Get the mode from D1 and D2, verify counter
                    ModeInfo mi = MILSTD188_110B.modemModes[D1, D2];
                    if (Z == 0 && mi != null && Cnt < mi.PreambleSize)
                    {
                        this.CurrentMode = mi;
                        PreambuleCounter = Cnt;
                        if (PreambuleCounter == 0)
                        {
                            InitReceiveData();
                            NextFunction = ReceiveData;
                            numSamples = 0;
                        }
                        else
                        {
                            PreambleIdx = 0;
                            SyncCorr.StartCorrectionProcess();
                        }
                    }
                    else
                    {
                        NextFunction = LookForSymbolSync;
                        numSamples = 0;
                    }
                }

                int i, nProc;
                for (i = 0; i < numSamples; i += nProc)
                {
                    // Decode the preamble chunks
                    nProc = Math.Min(numSamples - i, DECFACTOR);
                    int nSym = Demodulator.Process(incomingSamples, startingIndex + i, nProc);
                    while (nSym-- > 0)
                    {
                        IQ IQData = Demodulator.GetData();
                        PreambleBlock[PreambleIdx++] = IQData;
                        SyncCorr.Process(IQData);
                        if (PreambleIdx >= PreambleBlock.Length)
                        {
                            // Do demodulator correction
                            Demodulator.RotateCorrection *= SyncCorr.RotateCorrection;
                            Demodulator.FrequencyCorrection *= (IQ.UNITY + 0.05f * SyncCorr.FrequencyCorrection) / 1.05f;
                            int D1, D2, Z, Cnt;
                            ProcessPreamble(out D1, out D2, out Cnt, out Z);
                            // Check the values that we extracted from the preambule
                            // Get the mode from D1 and D2, verify counter
                            if (Z == 0 && D1 == CurrentMode.D1 && D2 == CurrentMode.D2 && Cnt == (PreambuleCounter - 1))
                            {
                                PreambuleCounter = Cnt;
                                if (PreambuleCounter == 0)
                                {
                                    InitReceiveData();
                                    NextFunction = ReceiveData;
                                    numSamples = 0;
                                    break;
                                }
                                else
                                {
                                    PreambleIdx = 0;
                                    SyncCorr.StartCorrectionProcess();
                                }
                            }
                            else
                            {
                                NextFunction = LookForSymbolSync;
                                numSamples = 0;
                                break;
                            }
                        }
                    }
                }
                return i;
            }

            void InitReceiveData()
            {
                if (CurrentMode.Mode == MILSTD_188.Mode.D_4800N)
                    this.Interleaver = new Interleaver_188_110A_4800();
                else
                    this.Interleaver = new Interleaver_188_110A(CurrentMode.InterleaverColumns, CurrentMode.InterleaverRows);
                this.TotalPatternsInBlock = CurrentMode.BlockLength / (CurrentMode.ProbeDataSymbols + CurrentMode.UnknownDataSymbols);
                this.ProbeCounter = 0;
                this.ProbeTarget = new IQ[CurrentMode.ProbeDataSymbols];
                this.Probe = new int[CurrentMode.ProbeDataSymbols];

                this.DataBlock = new int[CurrentMode.UnknownDataSymbols];
                this.FECBuffer = new byte[Interleaver.Length];
                this.OutputData.Clear();

                this.DataScrambler = new LFSR_188_110A();
                if (CurrentMode.Mode == MILSTD_188.Mode.D_4800N)
                    this.FECDecoder = null;
                else
                    this.FECDecoder = new VitDecoder(ConvEncoderType.Truncate, FECEncoderRate * CurrentMode.RepeatDataBits, FECEncoderConstraint, FECEncoderPoly, -1, 8, 0);
                this.ProbeCorr = new Correlator(CORR_TYPE.NONE, CurrentMode.ProbeDataSymbols, CurrentMode.ProbeDataSymbols, 0, 0, 0, 0);
                this.Decoder = new IQDecoder(CurrentMode.BitsPerSymbol, CurrentMode.BitsToSymbolTable, Constellation.IQTable_8PSK, EncodingType.SCRAMBLE_ADD);
                this.Decoder.StartCorrectionProcess(CurrentMode.UnknownDataSymbols + CurrentMode.ProbeDataSymbols);
            }

            void ProcessData()
            {
                foreach (int Data in DataBlock)
                {
                    // Send every bit of the symbol individually into the interleaver
                    for(int BitShift = 0; BitShift < CurrentMode.BitsPerSymbol; BitShift++)
                    {
                        int DataBit = (Data >> BitShift) & 0x0001;
                        Interleaver.ProcessDecode((byte)DataBit);
                        if (Interleaver.IsDataReady)
                        {
                            // Interleaver is full - get the data from it into the buffer
                            int numOutputBits = Interleaver.Count;
                            Interleaver.GetData(FECBuffer);
                            // Process thru Viterby FEC decoder
                            int nBits;
                            if (FECDecoder == null)
                                nBits = numOutputBits;
                            else
                            {
                                FECDecoder.Process(FECBuffer, 0, numOutputBits);
                                FECDecoder.Finish();
                                nBits = FECDecoder.GetData(FECBuffer);
                                FECDecoder.Init();
                            }
                            // Add new bytes into the output array
                            // If EOM was found - only copy those bytes up to EOM
                            // ATTENTION!!!!!!
                            //  It will only work when the EOM symbol lies exactly within the interleaver block
                            // If it spans across the interleaver boundaries - then it will not work!!!!!!!
                            EOMDetector.Process(FECBuffer, 0, nBits);
                            if (EOMDetector.IsMatchFound)
                            {
                                nBits -= EOMDetector.TargetIndex;
                            }
                            for (int i = 0; i < nBits; i++) OutputData.Enqueue(FECBuffer[i]);
                            Interleaver.Init();
                        }
                    }
                }
            }

            int ReceiveData(float[] incomingSamples, int startingIndex, int numSamples)
            {
                if (numSamples == 0)
                {
                    this.SymbolsCounter = 0;
                }

                int i, nProc;
                for (i = 0; i < numSamples; i += nProc)
                {
                    nProc = Math.Min(numSamples - i, DECFACTOR);
                    int nSym = Demodulator.Process(incomingSamples, startingIndex + i, nProc);
                    while (nSym-- > 0)
                    {
                        Decoder.Process(Demodulator.GetData(), DataScrambler.DataNext(), SCRAMBLE_MASK, out DataBlock[SymbolsCounter]);
                        SymbolsCounter++;
                        // Is probe sequence coming ?
                        if (SymbolsCounter >= CurrentMode.UnknownDataSymbols)
                        {
                            NextFunction = ReceiveProbe;
                            numSamples = 0;
                            break;
                        }
                    }
                }
                return i;
            }

            int ReceiveProbe(float[] incomingSamples, int startingIndex, int numSamples)
            {
                if (numSamples == 0)
                {
                    LFSRState State = DataScrambler.State;  // Save current DataScrambler state
                    Array.Clear(Probe, 0, CurrentMode.ProbeDataSymbols);
                    // The last 2 probes for the interleaver are special - 
                    //  they are not 0, but D1 and D2 values instead
                    if (ProbeCounter == (TotalPatternsInBlock - 2))
                    {
                        ChanSymbToTribit[CurrentMode.D1].CopyTo(Probe, 0);// Send 8 tribits
                        ChanSymbToTribit[CurrentMode.D1].CopyTo(Probe, 8);// Send 8 tribits
                    }
                    else if (ProbeCounter == (TotalPatternsInBlock - 1))
                    {
                        ChanSymbToTribit[CurrentMode.D2].CopyTo(Probe, 0);// Send 8 tribits
                        ChanSymbToTribit[CurrentMode.D2].CopyTo(Probe, 8);// Send 8 tribits
                    }

                    for (int k = 0; k < CurrentMode.ProbeDataSymbols; k++)
                    {
                        ProbeEncoder.Process(Probe[k], DataScrambler.DataNext(), SCRAMBLE_MASK, out ProbeTarget[k]);
                    }
                    ProbeCorr.AddTarget(ProbeTarget);
                    DataScrambler.State = State;    // Restore scrambler state

                    ProbeCounter++; if (ProbeCounter >= TotalPatternsInBlock) ProbeCounter = 0;

                    Demodulator.RotateCorrection *= Decoder.RotateCorrection;
                    Demodulator.FrequencyCorrection *= (IQ.UNITY + 0.05f * Decoder.FrequencyCorrection) / 1.05f;
                    ProbeCorr.StartCorrectionProcess();
//                    Demodulator.StartCorrectionProcess(SYNC_TYPE.GARDNER_DD | SYNC_TYPE.GARDNER_NDA | SYNC_TYPE.QAMLD_NDA |
//                                            SYNC_TYPE.DIFF_NDA | SYNC_TYPE.ZERODET_NDA | SYNC_TYPE.PEAK_NDA | SYNC_TYPE.CORR_NDA,
//                                                CurrentMode.UnknownDataSymbols + CurrentMode.ProbeDataSymbols);
                    Decoder.StartCorrectionProcess(CurrentMode.UnknownDataSymbols + CurrentMode.ProbeDataSymbols);
                    this.SymbolsCounter = 0;
                }

                int i, nProc;
                int Data;
                IQ Symb;
                for (i = 0; i < numSamples; i += nProc)
                {
                    nProc = Math.Min(numSamples - i, DECFACTOR);
                    int nSymb = Demodulator.Process(incomingSamples, startingIndex + i, nProc);
                    while (nSymb-- > 0)
                    {
                        Symb = Demodulator.GetData();
                        ProbeCorr.Process(Symb);
                        Decoder.Process(Symb, DataScrambler.DataNext(), SCRAMBLE_MASK, out Data);
                        SymbolsCounter++;
                        // Is data sequence coming ?
                        if (SymbolsCounter >= CurrentMode.ProbeDataSymbols)
                        {
                            Demodulator.RotateCorrection *= ProbeCorr.RotateCorrection;
                            Demodulator.FrequencyCorrection *= (IQ.UNITY + 0.05f * ProbeCorr.FrequencyCorrection) / 1.05f;
                            ProcessData();  // Process received data
                            NextFunction = ReceiveData;
                            numSamples = 0;
                            break;
                        }
                    }
                }
                return i;
            }

            public int GetData(byte[] outputArray)
            {
                int ret = OutputData.Count;
                OutputData.CopyTo(outputArray, 0);
                OutputData.Clear();
                return ret;
            }

            public int GetData(byte[] outputArray, int startingIndex)
            {
                int ret = OutputData.Count;
                OutputData.CopyTo(outputArray, startingIndex);
                OutputData.Clear();
                return ret;
            }

            public int Count { get { return OutputData.Count; } }
            public bool IsDataReady { get { return (OutputData.Count > 0); } }
            public bool IsEOMDetected { get { return EOMDetector.IsMatchFound; } }
        }

        /// <summary>
        /// Scrambler for the Sync preamble - has a repeating pattern of 32 symbols that are added to the preamble data
        /// </summary>
        class SyncScrambler
        {
            int SyncSymbolCounter = 0;
            static int[] SyncScramble = { 7, 4, 3, 0, 5, 1, 5, 0, 2, 2, 1, 1, 5, 7, 4, 3, 
                                      5, 0, 2, 6, 2, 1, 6, 2, 0, 0, 5, 0, 5, 2, 6, 6};
            public void Init() { SyncSymbolCounter = 0; }

            public int DataNext() { return SyncScramble[SyncSymbolCounter++ & 0x1F]; }
        }

        public class TxModem
        {
            MILSTD_188.Mode ModemMode;

            // Number of Data (unknown) and Probe symbols in one packet
            int UnknownDataSymbols;
            int ProbeDataSymbols;

            // Interleaver parameters
            int InterleaverRows;
            int InterleaverColumns;
            int BitsPerSymbol;

            int[] MGDTable;
            int D1;
            int D2;

            int BlockLength;
            int PreambleSize;
            int RepeatDataBits;
            int TotalPatternsInBlock;

            Interleaver DataInterleaver;
            LFSR_188_110A DataScrambler;
            SyncScrambler PreambleScrambler;
            ConvEncoder FECEncoder;
            int FECCounter;
            int FECLength;
            byte[] FECBuffer;

            IQEncoder Encoder;
            IQModulator Modulator;
            FIR OutputFilter;

            List<float> OutputData = new List<float>();

            float[] OutputBuff;

            int FlushModulatorLength;

            int SyncSymbolCounter = 0;
            int DataSymbolCounter = 0;
            int ProbeSymbolCounter = 0;

            public TxModem(MILSTD_188.Mode modemMode, float processingFreq, float outputFreq, float[] symbolFilter, float[] outputFilter)
            {
                this.ModemMode = modemMode;
                // Set up all the required parameters
                MILSTD188_110B.modemModes[ModemMode].GetModeInfo(out ModemMode, out D1, out D2, out PreambleSize, out InterleaverRows,
                            out InterleaverColumns, out UnknownDataSymbols, out ProbeDataSymbols, out BlockLength,
                                out RepeatDataBits, out BitsPerSymbol, out MGDTable);

                if (modemMode == MILSTD_188.Mode.D_4800N)
                    this.DataInterleaver = new Interleaver_188_110A_4800();
                else
                    this.DataInterleaver = new Interleaver_188_110A(InterleaverColumns, InterleaverRows);
                this.DataInterleaver.Init();
                this.DataScrambler = new LFSR_188_110A();
                this.PreambleScrambler = new SyncScrambler();
                if (modemMode == MILSTD_188.Mode.D_4800N)
                    this.FECEncoder = null;
                else
                    this.FECEncoder = new ConvEncoder(ConvEncoderType.Truncate, FECEncoderRate * RepeatDataBits, FECEncoderConstraint, FECEncoderPoly, -1, 8);

                this.Encoder = new IQEncoder(BITS_PER_SYMBOL, Constellation.Table_1_to_1, Constellation.IQTable_8PSK, EncodingType.SCRAMBLE_ADD);
                this.Modulator = new IQModulator(CARRIER_FREQ, CARRIER_FREQ, NUM_FREQ, processingFreq, SYMBOLRATE, symbolFilter);
                this.OutputBuff = new float[(int)(processingFreq / SYMBOLRATE)];
                this.OutputFilter = new FIR(outputFilter, (int)(processingFreq / outputFreq));

                this.FlushModulatorLength = 0;
                if (symbolFilter != null) FlushModulatorLength += (symbolFilter.Length * 2);
                if (outputFilter != null) FlushModulatorLength += (outputFilter.Length * 2);

                this.TotalPatternsInBlock = BlockLength / (ProbeDataSymbols + UnknownDataSymbols);

                // FEC Size and Counter - reinitialize FEC on interleaver boundaries
                FECBuffer = new byte[this.DataInterleaver.Length];
                FECLength = this.DataInterleaver.Length / (FECEncoderRate * RepeatDataBits);
                FECCounter = 0;     // Re-initialize FEC coder on interleaver boundaries.
            }

            void SendBuffer()
            {
                int nSamples = OutputFilter.Decimate(OutputBuff, OutputBuff);
                for (int i = 0; i < nSamples; i++)
                    OutputData.Add(OutputBuff[i]);
            }

            void SendIQ(IQ iqData)
            {
                if (Modulator.Process(iqData, OutputBuff) > 0)
                {
                    SendBuffer();
                }
            }

            void SendSymbol(int Symb)
            {
                IQ iqData;
                Encoder.Process(Symb, out iqData);
                SendIQ(iqData);
            }

            void SendSymbol(int Symb, int scrambleSymb)
            {
                IQ iqData;
                Encoder.Process(Symb, scrambleSymb, SCRAMBLE_MASK, out iqData);
                SendIQ(iqData);
            }

            int SendSyncString(int[] syncChanSymbArray)
            {
                int NumSent = 0;
                foreach (int BitChanSymb in syncChanSymbArray)
                {
                    int[] sequence = ChanSymbToTribit[BitChanSymb];
                    // repeat sequence 4 times
                    for (int j = 0; j < 4; j++)
                    {
                        foreach (int tribit in sequence)
                        {
                            SendSymbol(tribit, PreambleScrambler.DataNext());
                            NumSent++;
                        }
                    }
                }
                return NumSent;
            }

            int SendDataString(int[] dataSymbArray)
            {
                int NumSent = 0;
                foreach (int tribit in dataSymbArray)
                {
                    SendSymbol(tribit, DataScrambler.DataNext());
                    NumSent++;
                }
                return NumSent;
            }

            int SendData(int dataSymb)
            {
                SendSymbol(dataSymb, DataScrambler.DataNext());
                return 1;
            }

            int SendData75N(int dataSymb)
            {
                int NumSent = 0;
                int[] sequence = ChanSymbToTribit[dataSymb];
                NumSent += SendDataString(sequence);   // Send 8 tribits
                NumSent += SendDataString(sequence);   // Send 8 tribits
                NumSent += SendDataString(sequence);   // Send 8 tribits
                NumSent += SendDataString(sequence);   // Send 8 tribits - total 32
                return NumSent;
            }

            int SendProbe()
            {
                //5.3.2.3.7.1.2 Known data.
                //During the periods where known (channel probe) symbols are to be transmitted, the channel
                //symbol formation output shall be set to 0 (000) except for the two known symbol patterns
                //preceding the transmission of each new interleaved block.. The block length shall be 1440 tribit
                //channel symbols for short interleave setting and 11520 tribit channels symbols for the long
                //interleave setting. When the two known symbol patterns preceding the transmission of each new
                //interleaver block are transmitted, the 16 tribit symbols of these two known symbol patterns shall
                //be set to Dl and D2, respectively, as defined in table XV of 5.3.2.3.7.2.1 and table XVII of
                //5.3.2.3.7.2.2. The two known symbol patterns are repeated twice rather than four times as they
                //are in table XVII to produce a pattern of 16 tribit numbers. In cases where the duration of the
                //known symbol pattern is 20 tribit symbols, the unused last four tribit symbols shall be set to 0
                //(000)
                int NumSent = 0;
                if (ProbeSymbolCounter == (TotalPatternsInBlock - 2))
                {
                    int[] sequence = ChanSymbToTribit[D1];
                    NumSent += SendDataString(sequence);   // Send 8 tribits
                    NumSent += SendDataString(sequence);   // Send 8 tribits
                }
                else if (ProbeSymbolCounter == (TotalPatternsInBlock - 1))
                {
                    int[] sequence = ChanSymbToTribit[D2];
                    NumSent += SendDataString(sequence);   // Send 8 tribits
                    NumSent += SendDataString(sequence);   // Send 8 tribits
                }
                for (int i = NumSent; i < ProbeDataSymbols; i++)    // Fill whatever left with zeroes
                    NumSent += SendData(0x00);
                ProbeSymbolCounter++;
                if (ProbeSymbolCounter >= TotalPatternsInBlock)
                    ProbeSymbolCounter = 0;
                return NumSent;
            }

            int SendSyncPreamble()
            {
                int NumSent = 0;
                int[] DCBits = new int[6];
                DCBits[0] = D1;
                DCBits[1] = D2;
                DCBits[5] = 0;
                PreambleScrambler.Init();
                for (SyncSymbolCounter = PreambleSize - 1; SyncSymbolCounter >= 0; SyncSymbolCounter--)
                {
                    NumSent += SendSyncString(SyncPreamble);
                    // This is how the counter is formed - bits 5:4, 3:2 and 1:0 are mapped into tribits
                    DCBits[2] = ((SyncSymbolCounter >> 4) & 0x3) | 0x4;
                    DCBits[3] = ((SyncSymbolCounter >> 2) & 0x3) | 0x4;
                    DCBits[4] = ((SyncSymbolCounter >> 0) & 0x3) | 0x4;
                    NumSent += SendSyncString(DCBits);
                }
                return NumSent;
            }

            int ProcessFullInterleaver()
            {
                int NumSent = 0;
                // Interleaver is full
                // Now start taking bits from the interleaver
                int OutByte = 0;
                DataSymbolCounter = 0;
                while (DataInterleaver.Count > 0)
                {
                    // Generate Symbol from individual bits
                    OutByte = 0;
                    for(int i = 0; i < BitsPerSymbol; i++)
                        OutByte |= (DataInterleaver.GetData() & 0x0001) << i;

                    // Convert Symbol to tribit using Modified Gray Dibit table
                    int tribit = MGDTable[OutByte];
                    if (ModemMode == MILSTD_188.Mode.D_75L || ModemMode == MILSTD_188.Mode.D_75S)
                    {
                        // in case of 75 bps there will be no probe symbols, but the "exceptional" set
                        DataSymbolCounter++;
                        if (DataSymbolCounter >= UnknownDataSymbols)
                        {
                            tribit += 0x04;
                            DataSymbolCounter = 0;
                        }
                        NumSent += SendData75N(tribit);
                    }
                    else
                    {
                        NumSent += SendData(tribit);

                        // After "UnknownDataSymbols" data symbols we should send "known" data (probes)
                        DataSymbolCounter++;
                        if (DataSymbolCounter >= UnknownDataSymbols)
                        {
                            NumSent += SendProbe();
                            DataSymbolCounter = 0;
                        }
                    }
                }
                return NumSent;
            }

            public bool Start()
            {
                DataInterleaver.Init();
                if (FECEncoder != null) FECEncoder.Init();
                FECCounter = 0;
                DataScrambler.Init();
                PreambleScrambler.Init();
                SendSyncPreamble();
                return true;
            }

            public bool Process(byte[] dataByteArray, int startIndex, int numDataBits)
            {
                //  If no interleaver - then just copy databits  
                if (FECEncoder == null)
                {
                    for (int i = 0; i < numDataBits; i++)
                    {
                        DataInterleaver.ProcessEncode(dataByteArray[startIndex++]);
                        if (DataInterleaver.IsDataReady) ProcessFullInterleaver();
                    }
                }
                else
                {
                    for (int i = 0; i < numDataBits; i++)
                    {
                        FECEncoder.Process(dataByteArray[startIndex++]);
                        FECCounter++;
                        if (FECCounter >= FECLength)
                        {
                            FECEncoder.Finish();
                            int nBits = FECEncoder.GetData(FECBuffer);
                            for (int BitIdx = 0; BitIdx < nBits; BitIdx++)
                            {
                                DataInterleaver.ProcessEncode(FECBuffer[BitIdx]);
                                if (DataInterleaver.IsDataReady) ProcessFullInterleaver();
                            }
                            FECCounter = 0;
                            FECEncoder.Init();
                        }
                    }
                }
                return true;
            }

            public bool Finish()
            {
                int FlushBuffSize = InterleaverFlushBits + 4 * 8;
                byte[] FlushInData = new byte[FlushBuffSize];


                BitArray EOMBits = new BitArray(8);
                // Place EOM bits thru FEC
                int FlipEOM = MILSTD_188.MSBFirst(MILSTD_188.EOM);
                EOMBits.Add(FlipEOM, 32);
                EOMBits.GetData(FlushInData);
                Process(FlushInData, 0, FlushBuffSize);

                if (FECEncoder != null)
                {
                    FECEncoder.Finish();
                    int nBits = FECEncoder.GetData(FECBuffer);
                    for (int BitIdx = 0; BitIdx < nBits; BitIdx++)
                    {
                        DataInterleaver.ProcessEncode(FECBuffer[BitIdx]);
                        if (DataInterleaver.IsDataReady) ProcessFullInterleaver();
                    }
                }

                // Make sure that interleaver is completely full - fill it with zero bytes.
                while (!DataInterleaver.IsDataReady)
                {
                    DataInterleaver.ProcessEncode(0);
                }
                ProcessFullInterleaver();

                Modulator.Finish(OutputBuff);
                SendBuffer();
                for (int j = 0; j < FlushModulatorLength * NUM_FREQ; j++)
                {
                    SendIQ(IQ.ZERO);
                }
                return true;
            }

            public int GetData(float[] sampleArray, int startingIndex)
            {
                int ret = OutputData.Count;
                OutputData.CopyTo(sampleArray, startingIndex);
                OutputData.Clear();
                return ret;
            }

            public int Count { get { return OutputData.Count; } }
            public bool IsDataReady { get { return OutputData.Count > 0; } }
        }

    }

}
