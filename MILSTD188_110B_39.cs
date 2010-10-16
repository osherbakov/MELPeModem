using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    class MILSTD188_110B_39
    {
        public const int BITS_PER_SYMBOL = 2;
        public const float CARRIER_FREQ_LO = 675.0f;
        public const float CARRIER_FREQ_HI = 2812.5f;
        public const float DOPPLER_FREQ = 393.75f;
        public const int NUM_FREQ = 39;
        public const float SYMBOL_TIME = 0.0225f;      // Every symbol lasts 22.5ms
        public const float SYMBOLRATE = 1 / SYMBOL_TIME; // Symbol rate will be 44.44444444 Hz
        public const int RS_BITS = 4;
        public const float NORM_AMP = 1.0f / NUM_FREQ;

        public const int CARRIER_LO_FFT_IDX = 12;
        public const int CARRIER_HI_FFT_IDX = (CARRIER_LO_FFT_IDX + NUM_FREQ);


        // Preamble parameters - for the Parts 1, 2 and 3
        public const int NUMTONES1 = 4;
        public const int NUMTONES2 = 3;
        public const int NUMTONES3 = 39;

        public const int AMPLITUDE1 = 3;
        public const int AMPLITUDE2 = 4;
        public const int AMPLITUDE3 = 1;
        public const int AMPLITUDEDOPPLER = 2;

        public const int PREAMBLE_OVERSAMPLE = 6;
        public const int DOPPLERCORR_INTERVAL = 8;

        public const int FREQ_OFFSET = -12;

        public const float AMP_CORR1 = 1 ;/// ((NORM_AMP * AMPLITUDE1));
        public const float AMP_CORR2 = 1 ;/// ((NORM_AMP * AMPLITUDE2));
        public const float AMP_CORR3 = 1 ;/// ((NORM_AMP * AMPLITUDE3));

        static double[] InitialPhases = {
                0,     5.6,   19.7,   42.2, 73.1,  115.3,   165.9, 225.0, 295.3,  14.1,
                101.3, 199.7, 303.8,  59.1, 185.6, 317.8,   101.3, 253.1, 56.3,   225.0,
                45,    236.3, 73.1,   281.3, 137.8, 5.6,    239.1, 123.8, 19.7,   281.3,
                194.1, 115.3, 45,     345.9, 295.3, 253.1,  222.2, 199.7, 185.6
            };

        static MILSTD188_110B_39()
        {
            modemModes = new Modes(MILSTD188_110B_39.supportedModes);
        }

        static float[] Baudrates = { 2400, 1200, 600, 300, 1500, 75 };

        static ModeInfo[] supportedModes = {
            new ModeInfo(MILSTD_188.Mode.D_2400S, 0, 0, 0,  1, 144,  56,  0, 0,  1, 78, null),
            new ModeInfo(MILSTD_188.Mode.D_2400AS, 1, 0, 0,  9, 16,  56,  0, 0,  1, 78, null),
            new ModeInfo(MILSTD_188.Mode.D_2400MS, 2, 0, 0,  18, 12,  56,  0, 0,  1, 78, null),
            new ModeInfo(MILSTD_188.Mode.D_2400MAS, 3, 0, 0,  27, 9,  56,  0, 0,  1, 78, null),
            new ModeInfo(MILSTD_188.Mode.D_2400MAL, 4, 0, 0, 36, 7, 56,  0, 0, 1, 78, null),
            new ModeInfo(MILSTD_188.Mode.D_2400ML, 5, 0, 0, 72, 3, 56,  0, 0, 1, 78, null),
            new ModeInfo(MILSTD_188.Mode.D_2400AL, 6, 0, 0, 144, 1, 56,  0, 0, 1, 78, null),
            new ModeInfo(MILSTD_188.Mode.D_2400L, 7, 0, 0, 288, 1, 56,  0, 0, 1, 78, null),

            new ModeInfo(MILSTD_188.Mode.D_1200S, 8, 1, 0,  1, 567,  28,  0, 0,  1, 64, null),
            new ModeInfo(MILSTD_188.Mode.D_1200AS, 9, 1, 0,  63, 14,  28,  0, 0,  1, 64, null),
            new ModeInfo(MILSTD_188.Mode.D_1200AL, 10, 1, 0, 189, 6, 28,  0, 0, 1, 64, null),
            new ModeInfo(MILSTD_188.Mode.D_1200L, 11, 1, 0, 567, 1, 28,  0, 0, 1, 64, null),

            new ModeInfo(MILSTD_188.Mode.D_600S, 12, 2,  0,  1, 567,  28,  0, 0,  2, 32, null),
            new ModeInfo(MILSTD_188.Mode.D_600AS, 13, 2,  0,  33, 30,  28,  0, 0,  2, 32, null),
            new ModeInfo(MILSTD_188.Mode.D_600AL, 14, 2,  0, 99, 10, 28,  0, 0, 2, 32, null),
            new ModeInfo(MILSTD_188.Mode.D_600L, 15, 2,  0, 297, 2, 28,  0, 0, 2, 32, null),

            new ModeInfo(MILSTD_188.Mode.D_300S, 16, 3,  0,  1, 567,  28,  0, 0,  4, 16, null),
            new ModeInfo(MILSTD_188.Mode.D_300AS, 17, 3,  0,  17, 54,  28,  0, 0,  4, 16, null),
            new ModeInfo(MILSTD_188.Mode.D_300AL, 18, 3,  0, 47, 18, 28,  0, 0, 4, 16, null),
            new ModeInfo(MILSTD_188.Mode.D_300L, 19, 3,  0, 153, 4, 28,  0, 0, 4, 16, null),

            new ModeInfo(MILSTD_188.Mode.D_150S, 20, 4,  0,  1, 576,  28,  0, 0,  8, 8, null),
            new ModeInfo(MILSTD_188.Mode.D_150AS, 21, 4,  0,  9, 100,  28,  0, 0,  8, 8, null),
            new ModeInfo(MILSTD_188.Mode.D_150AL, 22, 4,  0, 25, 36, 28,  0, 0, 8, 8, null),
            new ModeInfo(MILSTD_188.Mode.D_150L, 23, 4,  0, 81, 8, 28,  0, 0, 8, 8, null),

            new ModeInfo(MILSTD_188.Mode.D_75S, 24, 5,   0,  1, 567,   28,  0,  0,  16, 4, null),
            new ModeInfo(MILSTD_188.Mode.D_75AS, 25, 5,  0,  4, 234,   28,  0,  0,  16, 4, null),
            new ModeInfo(MILSTD_188.Mode.D_75AL, 26, 5,  0, 12, 75,  28, 0,  0, 16, 4, null),
            new ModeInfo(MILSTD_188.Mode.D_75L, 27, 5,   0, 36, 16,  28, 0,  0, 16, 4, null),
        };

        static BitGroup[] D2400_old = { new BitGroup(0 * 78, 78) };
        static BitGroup[] D1200_old = { new BitGroup(0 * 64, 64), new BitGroup(0 * 63, 14) };
        static BitGroup[] D600_old = { new BitGroup(0 * 32, 32), new BitGroup(0 * 32, 32), new BitGroup(0, 14) };
        static BitGroup[] D300_old = { new BitGroup(0 * 16, 16), new BitGroup(0 * 16, 16), new BitGroup(0 * 16, 16), new BitGroup(0 * 16, 16), 
                                    new BitGroup(0 * 0,  14) };
        static BitGroup[] D150_old = { new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), 
                                    new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), 
                                    new BitGroup(0 * 0, 8), new BitGroup(0 * 8, 6) };

        static BitGroup[] D75_old = {  new BitGroup(0 * 0, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 2) };

        static BitGroup[] D2400_new = { new BitGroup(0 * 78, 78) };
        static BitGroup[] D1200_new = { new BitGroup(0 * 64, 64), new BitGroup(0 * 63, 14) };
        static BitGroup[] D600_new = { new BitGroup(0 * 32, 32), new BitGroup(8 * 32, 32), new BitGroup(0, 14) };
        static BitGroup[] D300_new = { new BitGroup(0 * 16, 16), new BitGroup(4 * 16, 16), new BitGroup(8 * 16, 16), new BitGroup(12 * 16, 16), 
                                    new BitGroup(0 * 0, 14) };
        static BitGroup[] D150_new = { new BitGroup(0 * 8, 8), new BitGroup(2 * 8, 8), new BitGroup(4 * 8, 8), new BitGroup(6 * 8, 8), 
                                    new BitGroup(8 * 8, 8), new BitGroup(10 * 8, 8), new BitGroup(12 * 8, 8), new BitGroup(14 * 8, 8), 
                                    new BitGroup(0 * 0, 8), new BitGroup(2 * 8, 6) };

        static BitGroup[] D75_new = {  new BitGroup(0 * 0, 4), new BitGroup(1 * 4, 4), new BitGroup(2 * 4, 4), new BitGroup(3 * 4, 4), 
                                    new BitGroup(4 * 4, 4), new BitGroup(5 * 4, 4), new BitGroup(6 * 4, 4), new BitGroup(7 * 4, 4), 
                                    new BitGroup(8 * 4, 4), new BitGroup(9 * 4, 4), new BitGroup(10 * 4, 4), new BitGroup(11 * 4, 4), 
                                    new BitGroup(12 * 4, 4), new BitGroup(13 * 4, 4), new BitGroup(14 * 4, 4), new BitGroup(15 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(1 * 4, 4), new BitGroup(2 * 4, 4), new BitGroup(3 * 4, 2) };

        static BitGroup[][] Diversity_Old = { D2400_old, D1200_old, D600_old, D300_old, D150_old, D75_old };
        static BitGroup[][] Diversity_New = { D2400_new, D1200_new, D600_new, D300_new, D150_new, D75_new };

        static public Modes modemModes;

        public TxModem Tx;
        public RxModem Rx;

        public MILSTD188_110B_39(MILSTD_188.Mode modemMode, float processingFreq, float outputFreq, float[] symbolFilter, float[] outputFilter)
        {
            Tx = new TxModem(modemMode, processingFreq, outputFreq, symbolFilter, outputFilter);
            Rx = new RxModem(outputFreq);
        }

        public class RxModem
        {
            public Preamble1Detector pd1;
            public Preamble2Detector pd2;
            public Preamble3Detector pd3;

            public static float MaxMinRatio(float[] inputArray)
            {
                Array.Sort<float>(inputArray);
                return (inputArray[0] != 0) ? Math.Abs(inputArray[inputArray.Length - 1] / inputArray[0]) : 0;
            }

            public RxModem(float samplingFreq)
            {
                pd1 = new Preamble1Detector(samplingFreq);
                pd2 = new Preamble2Detector(samplingFreq);
                pd3 = new Preamble3Detector(samplingFreq);
            }

            public class Preamble1Detector
            {
                enum STATES
                {
                    INIT,
                    FIND_START1,
                    START1_DETECTED,
                    FIND_END1,
                    END1_DETECTED
                }

                float SamplingFrequency;

                STATES State;
                
                IQDetector [] ToneDetectors1;   // Part 1 of the preamble
                IQDetector[] ToneDetectors2;    // Part 2 of the preamble
                int SamplesCounter;             // General purpose counter

                int Counter;
                float EnergyMargin = 3;         // 9db of error margin

                public Preamble1Detector(float samplingFreq)
                {
                    SamplingFrequency = samplingFreq;
                    ToneDetectors1 = new IQDetector[NUMTONES1];
                    ToneDetectors2 = new IQDetector[NUMTONES2]; 
                    Init();
                }


                public void Init()
                {
                    State = STATES.INIT;
                    ToneDetectors1[0] = new IQDetector(787.5f, SamplingFrequency, SYMBOLRATE * PREAMBLE_OVERSAMPLE, (0 * (2 * Math.PI / 360)), 0, -10, false);
                    ToneDetectors1[1] = new IQDetector(1462.5f, SamplingFrequency, SYMBOLRATE * PREAMBLE_OVERSAMPLE, (103.7 * (2 * Math.PI / 360)), 0, -10, false);
                    ToneDetectors1[2] = new IQDetector(2137.5f, SamplingFrequency, SYMBOLRATE * PREAMBLE_OVERSAMPLE, (103.7 * (2 * Math.PI / 360)), 0, -10, false);
                    ToneDetectors1[3] = new IQDetector(2812.5f, SamplingFrequency, SYMBOLRATE * PREAMBLE_OVERSAMPLE, (0 * (2 * Math.PI / 360)), 0, -10, false);

                    ToneDetectors2[0] = new IQDetector(1125.0f, SamplingFrequency, SYMBOLRATE * PREAMBLE_OVERSAMPLE, (0 * (2 * Math.PI / 360)), 0, -10, false);
                    ToneDetectors2[1] = new IQDetector(1800.0f, SamplingFrequency, SYMBOLRATE * PREAMBLE_OVERSAMPLE, (90.0 * (2 * Math.PI / 360)), 0, -10, false);
                    ToneDetectors2[2] = new IQDetector(2475.0f, SamplingFrequency, SYMBOLRATE * PREAMBLE_OVERSAMPLE, (0 * (2 * Math.PI / 360)), 0, -10, false);
                    Counter = 0;
                }

                public int SamplesProcessed { get { return Counter; } }

                public bool IsSyncFound
                {
                    get { return State == STATES.END1_DETECTED; }
                }

                public float FrequencyOffset
                {
                    get 
                    { 
                        float FreqOffset = (ToneDetectors1[0].FrequencyOffset + ToneDetectors1[1].FrequencyOffset + ToneDetectors1[2].FrequencyOffset + ToneDetectors1[3].FrequencyOffset) / 4.0f;
                        return FreqOffset;
                    }
                }

                public int Process(float inSample)
                {
                    float[] FreqEnergy1 = new float[NUMTONES1];
                    float TotalFreqEnergy1 = 0;
                    float TotalSignalEnergy = 0;

                    float[] FreqEnergy2 = new float[NUMTONES2];
                    float TotalFreqEnergy2 = 0;

                    bool DataReady = true;
                    bool ConditionsMet;

                    Counter++;

                    for (int i = 0; i < NUMTONES1; i++)
                    {
                        ToneDetectors1[i].Process(inSample);
                        DataReady = DataReady && ToneDetectors1[i].IsDataReady;
                    }
                    for (int i = 0; i < NUMTONES2; i++)
                    {
                        ToneDetectors2[i].Process(inSample);
                        DataReady = DataReady && ToneDetectors2[i].IsDataReady;
                    }

                    if (!DataReady)
                        return 1;

                    for (int i = 0; i < NUMTONES1; i++)
                    {
                        ToneDetectors1[i].GetData();
                        FreqEnergy1[i] = ToneDetectors1[i].FrequencyEnergy;
                        TotalFreqEnergy1 += FreqEnergy1[i];
                    }
                    TotalSignalEnergy = ToneDetectors1[0].SignalEnergy;
                    for (int i = 0; i < NUMTONES2; i++)
                    {
                        ToneDetectors2[i].GetData();
                        FreqEnergy2[i] = ToneDetectors2[i].FrequencyEnergy;
                        TotalFreqEnergy2 += FreqEnergy2[i];
                    }

                    switch (State)
                    {
                        case STATES.INIT:
                            State = STATES.FIND_START1;
                            break;

                        case STATES.FIND_START1:
                            // Process incoming signal, looking for all frequencies
                            // In this phase look to find NUMTONES1 frequencies
                            // 1. Each frequency amplitude/energy should be equal (within a margin)
                            // 2. Each frequency phase should be correct
                            // 3. Total energy should be above a certain margin
                            // Now check for the conditions:
                            // 1. Energy should be within the limit
                            ConditionsMet = true; //  ConditionsMet = MaxMinRatio(FreqEnergy1) < EnergyMargin;
                            // 2. Energy of signal should be good too
                            ConditionsMet = ConditionsMet && ((TotalFreqEnergy1 * AMP_CORR1 * EnergyMargin ) > TotalSignalEnergy);
                            if (ConditionsMet)
                            {
                                State = STATES.START1_DETECTED;
                                SamplesCounter = 4 *  PREAMBLE_OVERSAMPLE ;     // Should be stable for at least 4 symbols
                            }
                            break;

                        case STATES.START1_DETECTED:
                            // The start condition was detected
                            // In this state we are looking for the frequency to be stable
                            //  for at least 2 symbol periods.
                            // At the same time looking for Preamble 2 tones.
                            // The transition will happen when Preamble1 tones go below Preamble 2 tones
                            SamplesCounter--;
                            ConditionsMet = true; //  MaxMinRatio(FreqEnergy1) < EnergyMargin;
                            // 2. Energy of signal should be good too
                            ConditionsMet = ConditionsMet && ((TotalFreqEnergy1 * AMP_CORR1 * EnergyMargin) > TotalSignalEnergy);
                            if(!ConditionsMet)
                            {
                                State = STATES.FIND_START1;
                                // 3. This should be for at least 3 symbols
                            }
                            else if (SamplesCounter <= 0)
                            {
                                State = STATES.FIND_END1;
                            }
                            break;

                        case STATES.FIND_END1:
                            // In this state we are looking for energy of Tones2 to become bigger than Tones 1
                            ConditionsMet = true; //  ConditionsMet = MaxMinRatio(FreqEnergy2) < EnergyMargin;
//                            ConditionsMet = ConditionsMet && ((TotalFreqEnergy2 * AMP_CORR2 * EnergyMargin) > TotalSignalEnergy2);
                            ConditionsMet = ConditionsMet && ((TotalFreqEnergy1 * EnergyMargin) < TotalSignalEnergy);
                            if (ConditionsMet)
                            {
                                State = STATES.END1_DETECTED;
                            }
                            break;
                        default:
                            break;
                    }
                    return 1;
                }

            }

            public class Preamble2Detector
            {
                enum STATES
                {
                    INIT = 0,
                    START2_DETECTED,
                    END2_DETECTED,
                    ERROR_DETECTED
                }
                float SamplingFrequency;
                float FreqOff;
                IQDetector[] ToneDetectors;

                STATES State;
                int Counter;     // General purpose counter

                public Preamble2Detector(float samplingFreq)
                {
                    SamplingFrequency = samplingFreq;
                    ToneDetectors = new IQDetector[NUMTONES2];
                    ToneDetectors[0] = new IQDetector(1125.0f, SamplingFrequency, SYMBOLRATE, (float)(0 * (2 * Math.PI / 360)), 0, -10, true); 
                    ToneDetectors[1] = new IQDetector(1800.0f, SamplingFrequency, SYMBOLRATE, (float)(90 * (2 * Math.PI / 360)), 0, -10, true);
                    ToneDetectors[2] = new IQDetector(2475.0F, SamplingFrequency, SYMBOLRATE, (float)(0 * (2 * Math.PI / 360)), 0, -10, true);
                    ToneDetectors[0].StartingOffset = (int) (SamplingFrequency / (SYMBOLRATE * PREAMBLE_OVERSAMPLE));
                    ToneDetectors[1].StartingOffset = (int)(SamplingFrequency / (SYMBOLRATE * PREAMBLE_OVERSAMPLE));
                    ToneDetectors[2].StartingOffset = (int)(SamplingFrequency / (SYMBOLRATE * PREAMBLE_OVERSAMPLE));
                    Init(0);
                }

                public void Init(float frequencyOffset)
                {
                    FreqOff = frequencyOffset;
                    // Here is the math:
                    // for (1/symbolrate) time interval the accumulated phase error was E.  E = 2*pi*F * (1/sr) ==> F = (E * sr ) / (2 * pi)
                    foreach (IQDetector td in ToneDetectors) td.FrequencyOffset = frequencyOffset;
                    State = STATES.START2_DETECTED;
                    Counter = 0;
                }

                public bool IsSyncFound
                {
                    get { return State == STATES.END2_DETECTED; }
                }

                public bool IsErrorFound
                {
                    get { return State == STATES.ERROR_DETECTED; }
                }

                public float FrequencyOffset
                {
                    get 
                    {
                        float FreqOffset = (ToneDetectors[0].FrequencyOffset + ToneDetectors[1].FrequencyOffset + ToneDetectors[2].FrequencyOffset) / 3.0f; 
                        return FreqOffset;
                    }
                }

                public int Process(float inSample)
                {
                    bool FailTest;
                    bool DataReady = true;
                    for (int i = 0; i < NUMTONES2; i++)
                    {
                        ToneDetectors[i].Process(inSample);
                        DataReady = DataReady && ToneDetectors[i].IsDataReady;
                    }
                    if (!DataReady)
                    {
                        return 1;
                    }

                    switch (State)
                    {
                        case STATES.START2_DETECTED:
                            // Process incoming signal, looking for all frequencies
                            FailTest = false;
                            // All 3 detectors have data - process
                            // The symbols received should be alternating 0 and 1
                            foreach (IQDetector id in ToneDetectors)
                            {
                                if (id.GetData() != (Counter & 0x0001))
                                {
//                                    FailTest = true;
                                }
                            }
                            Counter++;
                            if ((Counter >= 8) && (FailTest == false))
                            {
                                State = STATES.END2_DETECTED;
                            }
                            if (FailTest)
                            {
                                State = STATES.ERROR_DETECTED;
                            }
                            break;

                        default:
                            break;
                    }
                    return 1;
                }
            }

            public class Preamble3Detector
            {

                enum STATES
                {
                    INIT = 0,
                    FIRST_REF_SYMBOL,
                    REGULAR_DATA,
                    FREQ_CORRECTION
                }

                STATES State;

                float SamplingFrequency;
                IQDecoder[] OFDMDecoders;
                IQDetector Doppler;
                DataCombiner Despreader;

                int FrameCounter;

                float FreqOff;

                Queue<byte> OutputData;
                Queue<IQ> RawData;

                int FrameSize;
                OFDMFFTDemodulator FFTDemodulator;

                public Preamble3Detector(float samplingFreq)
                {
                    SamplingFrequency = samplingFreq;
                    Doppler = new IQDetector(DOPPLER_FREQ, SamplingFrequency, SYMBOLRATE, 0.0, 1, DOPPLERCORR_INTERVAL, false);
                    OFDMDecoders = new IQDecoder[NUMTONES3];
                    Despreader = new DataCombiner(78, D2400_new);
                    OutputData = new Queue<byte>();
                    RawData = new Queue<IQ>();
                    FrameSize = (int) ((SamplingFrequency / SYMBOLRATE) + 0.5);

                    FFTDemodulator = new OFDMFFTDemodulator(CARRIER_FREQ_LO, CARRIER_FREQ_HI, NUM_FREQ, samplingFreq, SYMBOLRATE);

                    for (int i = 0; i < NUMTONES3; i++)
                    {
                        OFDMDecoders[i] = new IQDecoder(BITS_PER_SYMBOL, Constellation.BitsToPhase_39, Constellation.IQTable_QPSK45,
                                        EncodingType.DIFF_IQ);
                    }
                    Init(0);   
                }

                public void Init(float frequencyOffset)
                {
                    FreqOff = frequencyOffset;
                    FFTDemodulator.FrequencyOffset = FreqOff;
                    Doppler.FrequencyOffset = FreqOff;

                    State = STATES.FIRST_REF_SYMBOL;
                    FrameCounter = 0;
                }

                public int Process(float inSample)
                {
                    FFTDemodulator.Process(inSample);
                    Doppler.Process(inSample);

                    if (FFTDemodulator.IsDataReady)
                    {
                        IQ Data;
                        int DataBits;
                        switch (State)
                        {
                            case STATES.FIRST_REF_SYMBOL:
                                IQ RotCorr;
                                for (int i = 0; i < NUMTONES3; i++)
                                {
                                    Data = FFTDemodulator.GetData();
                                    RotCorr = (IQ.UNITY / Data) / Data.R2;
                                    FFTDemodulator[i].RotateCorrection = RotCorr;
                                    OFDMDecoders[i].Init();
                                    OFDMDecoders[i].PreviousIQ = Data * RotCorr;
                                    OFDMDecoders[i].StartCorrectionProcess(DOPPLERCORR_INTERVAL);
                                }
                                Doppler.StartCorrectionProcess(DOPPLERCORR_INTERVAL);
                                State = STATES.REGULAR_DATA;
                                break;

                            case STATES.REGULAR_DATA:
                                for (int i = 0; i < NUMTONES3; i++)
                                {
                                    Data = FFTDemodulator.GetData();
                                    RawData.Enqueue(Data);
                                    OFDMDecoders[i].Process(Data, out DataBits);
                                    for (int j = 0; j < BITS_PER_SYMBOL; j++)
                                    {
                                        OutputData.Enqueue((byte)(DataBits & 0x0001));
                                        DataBits >>= 1;
                                    }
                                }
                                
                                if (Doppler.IsCorrectionReady)
                                {
                                    IQ DecodeCorr = IQ.UNITY;
                                    for (int i = 0; i < NUMTONES3; i++)
                                    {
//                                        DecodeCorr += OFDMDecoders[i].FrequencyCorrection;
                                        OFDMDecoders[i].StartCorrectionProcess(DOPPLERCORR_INTERVAL);
                                    }
                                    float FreqCorr = Doppler.FrequencyOffset; //  +DecodeCorr;
                                    FFTDemodulator.FrequencyOffset = FreqCorr;
                                    Doppler.FrequencyOffset = FreqCorr;
                                    Doppler.StartCorrectionProcess(DOPPLERCORR_INTERVAL);

                                }
                                break;

                            case STATES.FREQ_CORRECTION:
                                break;
                            
                            default:
                                break;

                        }
                        if (FFTDemodulator.Count > 0)
                        {
                            throw new ApplicationException("Extra symbols detected");
                        }
                        FrameCounter++;
                    }
                    return 0;
                }

                public int Count
                {
                    get { return OutputData.Count; }
                }

                public int RawCount
                {
                    get { return RawData.Count; }
                }

                public byte GetData() { return OutputData.Dequeue(); }

                public void GetData(byte []outputArray) 
                {
                    OutputData.CopyTo(outputArray, 0); 
                }

                public IQ GetRawData() { return RawData.Dequeue(); }

                public void GetRawData(IQ[] outputArray)
                {
                    RawData.CopyTo(outputArray, 0);
                }
            }
        }

        public class TxModem
        {
            MILSTD_188.Mode ModemMode;
            bool LongPreamble = false;
            bool UseDopplerTone = true;

            int D1, D2;         // Values to inform the receiving modem aboud Rate and Interleave factor

            int DataSymbols;    // Number of data symbols to be sent before sync
            int SyncSymbols;    // Number of sync symbols to be sent

            // Interleaver parameters
            int InterleavingDegree;
            int InterleaverNumBlocks;

            int BlockLength;
            int RepeatDataBits;
            int BitsPerSymbol;
            int BitsPerCodeword;

            float DataRate;                 // Data transmission rate - 75-2400 bits/sec
            BitGroup[] DiversityArray;      // Bits diversity array

            int PreambleSize;
            int InputBlockSize;

            Interleaver DataInterleaver;

            LFSR__188_110B_39 SyncLFSR;
            int LSFRSeed;

            IQEncoder[] Encoder;            // Each channel has a separate encoder
            OFDMFFTModulator Modulator;
            IQModulator PreambleModulator;

            FIR OutputFilter;
            Generator DopplerTone;

            DataSpreader<byte> DiversitySpreader;

            List<float> OutputData = new List<float>();

            int[] BitsToSymbols;

            int FlushModulatorLength;

            int BitCounter = 0;
            int OutputSymbol = 0;

            int DataSymbolCounter = 0;

            public TxModem(MILSTD_188.Mode modemMode, float processingFreq, float outputFreq, float[] symbolFilter, float[] outputFilter)
            {
                this.ModemMode = modemMode;
                // Set up all the required parameters
                MILSTD188_110B_39.modemModes[ModemMode].GetModeInfo(out ModemMode, out D1, out D2, out PreambleSize, out InterleavingDegree,
                            out InterleaverNumBlocks, out BitsPerCodeword, out SyncSymbols, out BlockLength,
                                out RepeatDataBits, out BitsPerSymbol, out BitsToSymbols);

                this.DataRate = Baudrates[D2];
                this.DiversityArray = Diversity_New[D2];

                this.InputBlockSize = (int) (processingFreq / SYMBOLRATE);

                // For 2400 baud we use RS(14, 10), for all others - RS(7,3)
                int SymbTotal = (DataRate == 2400) ? 14 : 7;
                int SymbData = (DataRate == 2400) ? 10 : 3;
                DataSymbols = BitsPerCodeword * InterleavingDegree * InterleaverNumBlocks;
                // DataSymbols = TotalBits * RS_BITS * InterleavingDegree * InterleaverNumBlocks;  // The same
                // Here is the magic formula that keeps the incoming and outgoing bitrate:
                //   @ 2400      K = (16 * Int * NumBlocks ) / 9
                //   @ any other K = (4 * Int * NumBlocks ) / 9
                SyncSymbols = (((DataRate == 2400) ? 16 : 4) * InterleavingDegree * InterleaverNumBlocks) / 9;
                SyncLFSR = new LFSR__188_110B_39(9, 0x0116);
                // Calculate the seed value - the scrambler repeats every 2^9 - 1 = 511 symbols
                // Start with the desired end value and count the (511 - necessary number of symbols)
                int NumShift = 0x1FF - SyncSymbols; if (NumShift < 0) NumShift += 0x1FF;
                SyncLFSR.Init(0x1FF);
                SyncLFSR.Shift(NumShift + 1);
                LSFRSeed = SyncLFSR.Value & 0x1FF;  

                this.DataInterleaver = new Interleaver_188_110B_39(InterleavingDegree, InterleaverNumBlocks, RS_BITS, SymbTotal, SymbData);
                this.DataInterleaver.Init();

                this.DiversitySpreader = new DataSpreader<byte>(BitsPerSymbol, this.DiversityArray);
                this.DiversitySpreader.Init();

                this.Encoder = new IQEncoder[NUM_FREQ];
                for(int i = 0; i < NUM_FREQ; i++)
                    Encoder[i] = new IQEncoder(BITS_PER_SYMBOL, Constellation.BitsToPhase_39, Constellation.IQTable_QPSK45, 
                                        EncodingType.DIFF_IQ);

                this.Modulator = new OFDMFFTModulator(CARRIER_FREQ_LO, CARRIER_FREQ_HI, NUM_FREQ, processingFreq, SYMBOLRATE);
                this.PreambleModulator = new IQModulator(CARRIER_FREQ_LO, CARRIER_FREQ_HI, NUM_FREQ, processingFreq, SYMBOLRATE, null);
                this.DopplerTone = new Generator();
                this.DopplerTone.Init(DOPPLER_FREQ + FREQ_OFFSET, processingFreq, 0);
                this.OutputFilter = new FIR(outputFilter, (int)(processingFreq / outputFreq));

                this.FlushModulatorLength = 20;
                if (symbolFilter != null) FlushModulatorLength += (symbolFilter.Length * 2);
                if (outputFilter != null) FlushModulatorLength += (outputFilter.Length * 2);
            }

            void SendIQ(IQ iqData)
            {
                if (Modulator.Process(iqData * 1 * NORM_AMP) > 0)
                {
                    float[] OutData = new float[Modulator.Count];
                    Modulator.GetData(OutData, 0);
                    if (UseDopplerTone && iqData != IQ.ZERO) 
                    {
                        this.DopplerTone.GenerateAdd(2 * NORM_AMP, OutData, InputBlockSize);
                    }
                    int nSamp = OutputFilter.Decimate(OutData, 0, OutData, 0, InputBlockSize);
                    for (int i = 0; i < nSamp; i++) OutputData.Add(OutData[i]);
                }
            }

            void SendBit(int bitSymb)
            {
                IQ IQData;
                DiversitySpreader.Process((byte) bitSymb);
                if (DiversitySpreader.IsDataReady)
                {
                    byte[] OutData = new byte[DiversitySpreader.Count];
                    DiversitySpreader.GetData(OutData, 0);
                    foreach (byte OutBit in OutData)
                    {
                        // Assemble Symbol from individual bits
                        OutputSymbol |= (OutBit & 0x0001) << BitCounter;
                        BitCounter++;
                        if (BitCounter >= BITS_PER_SYMBOL)
                        {
                            Encoder[Modulator.Index].Process(OutputSymbol, out IQData);
                            SendIQ(IQData);
                            BitCounter = 0;
                            OutputSymbol = 0;
                        }
                    }
                }
            }

            int SendBlockSync()
            {
                SyncLFSR.Init(LSFRSeed);
                for (int i = 0; i < SyncSymbols; i++)
                {
                    SyncLFSR.Shift();
                    SendBit(SyncLFSR.CurrentBit);
                }
                return SyncSymbols;
            }

            public int SendSyncPreamble(bool extendedPreamble, bool useDopplerTone)
            {
                int Part1Length = extendedPreamble ? 58: 14;
                int Part2Length = extendedPreamble ? 27: 8;
                int Part3Length = extendedPreamble ? 12: 1;

                int nSamp;
                IQ Symb;

                int DataSize = Math.Max(Part1Length, Part2Length); DataSize = Math.Max(DataSize, Part3Length);
                float[] ModDataBuff = new float[InputBlockSize * DataSize];

                float[] PhasesInRads = new float[NUM_FREQ];
                for (int i = 0; i < NUM_FREQ; i++) PhasesInRads[i] = (float)(InitialPhases[i] * (2 * Math.PI / 360));
                
                //--------------------------------------------------
                // Part one of the preamble
                //--------------------------------------------------
                PreambleModulator.Init();
                PreambleModulator.FrequencyOffset = FREQ_OFFSET;

                Array.Clear(ModDataBuff, 0, ModDataBuff.Length);

                Symb = IQ.UNITY * AMPLITUDE1 * NORM_AMP;  
                for (int i = 0; i < Part1Length; i++)
                {
                    PreambleModulator[787.5f].Process(Symb, ModDataBuff, i * InputBlockSize);
                    PreambleModulator[1462.5f].Process(Symb, ModDataBuff, i * InputBlockSize);
                    PreambleModulator[2137.5f].Process(Symb, ModDataBuff, i * InputBlockSize);
                    PreambleModulator[2812.5f].Process(Symb, ModDataBuff, i * InputBlockSize);
                    PreambleModulator.Finish(ModDataBuff, i * InputBlockSize);
                }
                nSamp = OutputFilter.Decimate(ModDataBuff, 0, ModDataBuff, 0, Part1Length * InputBlockSize);
                for (int i = 0; i < nSamp; i++) OutputData.Add(ModDataBuff[i]);

                //--------------------------------------------------
                // Part two of the preamble
                //--------------------------------------------------
                PreambleModulator.Init();
                PreambleModulator.FrequencyOffset = FREQ_OFFSET;

                Array.Clear(ModDataBuff, 0, ModDataBuff.Length);
                for (int i = 0; i < Part2Length; i++)
                {
                    float Sign = (i % 2) == 0 ? 1 : -1;
                    Symb = IQ.UNITY * Sign * AMPLITUDE2 * NORM_AMP;
                    PreambleModulator[1125.0f].Process(Symb * new IQ((float)(0 * (2 * Math.PI / 360))), ModDataBuff, i * InputBlockSize);
                    PreambleModulator[1800.0f].Process(Symb * new IQ((float)(90 * (2 * Math.PI / 360))), ModDataBuff, i * InputBlockSize);
                    PreambleModulator[2475.0f].Process(Symb * new IQ((float)(0 * (2 * Math.PI / 360))), ModDataBuff, i * InputBlockSize);
                    PreambleModulator.Finish(ModDataBuff, i * InputBlockSize);
                }
                nSamp = OutputFilter.Decimate(ModDataBuff, 0, ModDataBuff, 0, Part2Length * InputBlockSize);
                for (int i = 0; i < nSamp; i++) OutputData.Add(ModDataBuff[i]);

                //--------------------------------------------------
                // Part three of the preamble
                //--------------------------------------------------
                Array.Clear(ModDataBuff, 0, ModDataBuff.Length);
                Symb = IQ.UNITY * AMPLITUDE3 * NORM_AMP;
                for (int i = 0; i < Part3Length; i++)
                {
                    PreambleModulator.FrequencyOffset = FREQ_OFFSET;

                    Array.Clear(ModDataBuff, 0, ModDataBuff.Length);
                    this.Modulator.Index = 0;
                    for (int k = 0; k < NUM_FREQ; k++)
                    {
                        IQ NewSymb = Symb * new IQ(PhasesInRads[k]);
                        this.Modulator[k].Process(NewSymb, ModDataBuff, i * InputBlockSize);
                        this.Encoder[k].PreviousIQ = NewSymb.N;
                    }
//                    this.Modulator.Finish(ModDataBuff, i * InputBlockSize);
                }
                if (useDopplerTone) this.DopplerTone.GenerateAdd(AMPLITUDEDOPPLER * NORM_AMP, ModDataBuff, Part3Length * InputBlockSize);
                nSamp = OutputFilter.Decimate(ModDataBuff, 0, ModDataBuff, 0, Part3Length * InputBlockSize);
                for (int i = 0; i < nSamp; i++) OutputData.Add(ModDataBuff[i]);

                this.Modulator.Index = 0;
                return OutputData.Count;
            }

            public bool Start()
            {
                SendSyncPreamble(LongPreamble, UseDopplerTone);
                SendBlockSync();
                return true;
            }

            public bool Process(byte dataBit)
            {
                // Fill the interleaver and start sending the data

                DataInterleaver.ProcessEncode(dataBit);
                if (DataInterleaver.IsDataReady)
                {
                    byte[] OutData = new byte[DataInterleaver.Count];
                    DataInterleaver.GetData(OutData, 0);
                    foreach (byte DataBit in OutData)
                    {
                        SendBit(DataBit);
                    }
                }
                DataSymbolCounter++;
                if (DataSymbolCounter >= DataSymbols)
                {
                    SendBlockSync();
                    DataSymbolCounter = 0;
                }
                return true;
            }

            public bool Process(byte[] dataByteArray, int startIndex, int numDataBits)
            {
                // Fill the interleaver and start sending the data
                while (numDataBits-- > 0)
                {
                    Process(dataByteArray[startIndex++]);
                }
                return true;
            }

            public bool ProcessRaw(IQ[] dataArray, int startIndex, int numDataBits)
            {
                while (numDataBits-- > 0)
                {
                    SendIQ(dataArray[startIndex++]);
                }
                return true;
            }


            public bool ProcessRaw(byte[] dataByteArray, int startIndex, int numDataBits)
            {
                IQ IQData;
                // Fill the interleaver and start sending the data
                while (numDataBits-- > 0)
                {
                   // Assemble Symbol from individual bits
                    OutputSymbol |= (dataByteArray[startIndex++] & 0x0001) << BitCounter;
                    BitCounter++;
                    if (BitCounter >= BITS_PER_SYMBOL)
                    {
                        Encoder[Modulator.Index].Process(OutputSymbol, out IQData);
                        SendIQ(IQData);
                        BitCounter = 0;
                        OutputSymbol = 0;
                    }
                }
                return true;
            }

            public bool Finish()
            {
                int InterleaverFlushBits = 100;
                // and send it to the Interleaver
                for(int i = 0; i < InterleaverFlushBits; i++)
                    Process(0);

                FlushInterleaver();
                Modulator.Finish();

                for (int j = 0; j < FlushModulatorLength * NUM_FREQ; j++)
                {
                    SendIQ(IQ.ZERO);
                }
                return true;
            }

            void FlushInterleaver()
            {
                // Make sure that interleaver is completely full - fill it with zero bytes.
                while (!DataInterleaver.IsDataReady)
                    DataInterleaver.ProcessEncode(0);

                byte[] OutData = new byte[DataInterleaver.Count];
                DataInterleaver.GetData(OutData, 0);
                foreach (byte symb in OutData)
                {
                    SendBit(symb);
                }
            }

            public int GetData(float[] sampleArray)
            {
                int ret = OutputData.Count;
                OutputData.CopyTo(sampleArray);
                OutputData.Clear();
                return ret;
            }

            public int GetData(float[] sampleArray, int startingIndex)
            {
                int ret = OutputData.Count;
                OutputData.CopyTo(sampleArray, startingIndex);
                OutputData.Clear();
                return ret;
            }

            public int Count { get { return OutputData.Count; } }
            public int SampleCount { get { return OutputData.Count; } }
            public bool IsDataReady { get { return OutputData.Count > 0; } }
        }


    }
}
