using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    class OFDMFFTDemodulator : DataProcessingModule
    {
        InputPin<float> DataIn;
        OutputPin<IQ> DataOut;

        Queue<IQ> OutputData;

        int NFREQ;
        float[] Frequencies;

        float SamplingFreq;
        float SymbolRate;
        int BlockSize;
        int FFTSize;
        int CurrFreqIndex = 0;

        int FFT_START_INDEX;

        float AccumEnergy;
        float FrameEnergy;

        FFT FFTDemodulator;

        Quad InputCorr;
        IQ[] OutputCorr;                      // Each frequency should have it's own correction

        IQ[] FFTInBuffer;
        IQ[] FFTOutBuffer;

        float FreqAdj;
        int SamplesCounter;

        public OFDMFFTDemodulator(float lowFreq, float highFreq, int numFreq, float processingRate, float symbolRate)
        {
            NFREQ = numFreq;
            SamplingFreq = processingRate;
            SymbolRate = symbolRate;
            BlockSize = (int)((SamplingFreq / SymbolRate) + 0.5f);

            // Calculate the size of the FFT Windows.
            // Guard Time will be BlockSize - FFTSize
            FFTSize = 1;
            while (FFTSize < BlockSize)
            {
                FFTSize <<= 1;
            }
            FFTSize >>= 1;

            if ((int)(BlockSize * symbolRate + 0.5f) != (int)processingRate)
            {
                throw new ApplicationException("The processingRate must be integer multiple of symbolRate");
            }

            Frequencies = new float[NFREQ];        // array of all frequencies
            OutputCorr = new IQ[NFREQ];

            FFTDemodulator = new FFT(FFTSize);
            FFTInBuffer = new IQ[FFTSize];
            FFTOutBuffer = new IQ[FFTSize];

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

            FFT_START_INDEX = (int)(Frequencies[0] / OneChannelBW + 0.5f);
            if( (int) ( (SamplingFreq / OneChannelBW) + 0.5f ) != FFTSize)
            {
                throw new ApplicationException("OFDM Frequencies are not orthogonal");
            }

            Init();
        }

        public override void Init()
        {
            for (int i = 0; i < NFREQ; i++)
            {
                OutputCorr[i] = IQ.UNITY;
            }
            InputCorr = new Quad();
            CurrFreqIndex = 0;
            OutputData = new Queue<IQ>();
            SamplesCounter = FFTSize - BlockSize;
            AccumEnergy = 0;
            FrameEnergy = 0;
            FreqAdj = 0;
        }

        public float FrequencyOffset
        {
            get { return FreqAdj; }
            set
            {
                FreqAdj = value;
                IQ FreqPerSampleCorr = new IQ((float)(value * 2.0 * Math.PI / SamplingFreq));
                InputCorr = new Quad(InputCorr.Value, FreqPerSampleCorr, 1);
            }
        }

        public int Index
        {
            get { return CurrFreqIndex; }
            set { CurrFreqIndex = value; }
        }

        public OFDMFFTDemodulator this[int freqIndex]
        {
            get { CurrFreqIndex = freqIndex; return this; }
        }

        public OFDMFFTDemodulator this[float freq]
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

            if(SamplesCounter >= 0)
            {
                AccumEnergy += incomingSample * incomingSample;
                FFTInBuffer[SamplesCounter] = new IQ(incomingSample, 0) / InputCorr.Value;
            }
            InputCorr.Next();
            SamplesCounter++;
            if(SamplesCounter >= FFTSize)
            {
                SamplesCounter =  FFTSize - BlockSize;      // Set it to negative number
                FFTDemodulator.ProcessFFT(FFTInBuffer, FFTOutBuffer);
                FrameEnergy = (2.0f * AccumEnergy) / FFTSize;
                AccumEnergy = 0;
                for (int i = 0; i < NFREQ; i++)
                {
                    Data = FFTOutBuffer[i + FFT_START_INDEX] * OutputCorr[i];
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
                if(SamplesCounter >= 0)
                {
                    AccumEnergy += incomingSample * incomingSample;
                    FFTInBuffer[SamplesCounter] = new IQ(incomingSample, 0) / InputCorr.Value;
                }
                InputCorr.Next();
                SamplesCounter++;
                if (SamplesCounter >= FFTSize)
                {
                    SamplesCounter =  FFTSize - BlockSize;
                    FFTDemodulator.ProcessFFT(FFTInBuffer, FFTOutBuffer);
                    FrameEnergy = (2.0f * AccumEnergy) / FFTSize;
                    AccumEnergy = 0;
                    for (int i = 0; i < NFREQ; i++)
                    {
                        Data = FFTOutBuffer[i + FFT_START_INDEX] * OutputCorr[i];
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
                this.OutputCorr[CurrFreqIndex] = value;
            }
            get { return this.OutputCorr[CurrFreqIndex]; }
        }

        public float SignalEnergy { get { return FrameEnergy; } }

        public float FrequencyEnergy { get { return FFTOutBuffer[CurrFreqIndex + FFT_START_INDEX].R2; } }

        public int Count { get { return OutputData.Count; } }

        public bool IsDataReady { get { return (OutputData.Count > 0); } }

        public int StartingOffset
        {
            set 
            {  
                if( value > SamplesCounter)
                {
                    SamplesCounter -= value;
                }else{
                    int DeltaTime = SamplesCounter - value;
                    if (DeltaTime >= 0)
                    {
                        Array.Copy(FFTInBuffer, value, FFTInBuffer, 0, SamplesCounter - value);
                        SamplesCounter -= value;
                    }
                    else
                    {
                        Array.Copy(FFTInBuffer, 0, FFTInBuffer, -DeltaTime, SamplesCounter);
                        Array.Copy(FFTInBuffer, FFTInBuffer.Length + DeltaTime, FFTInBuffer, 0, -DeltaTime);
                        SamplesCounter += DeltaTime;
                    }
                }
            }
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
            base.SetIOParameters("OFDMFFTDemodulator", new DataPin[] { DataIn, DataOut });
        }
    }


    class OFDMFFTModulator : DataProcessingModule
    {
        InputPin<IQ> DataIn;
        OutputPin<float> DataOut;

        int NFREQ;
        float[] Frequencies;
        float FreqAdj;
        Quad CorrFreq;
        bool[] GeneratorsDone;

        float SamplingFreq;
        float SymbolRate;
        int BlockSize;

        FFT FFTModulator;
        int FFTSize;
        int FFT_START_INDEX;
        int FFT_END_INDEX;

        IQ[] FFTInBuffer;
        IQ[] FFTOutBuffer;

        // Current frequency used
        int CurrFreqIndex = 0;

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
        public OFDMFFTModulator(float lowFreq, float highFreq, int numFreq, float processingRate, float symbolRate)
        {
            NFREQ = numFreq;
            SymbolRate = symbolRate;
            SamplingFreq = processingRate;
            BlockSize = (int)(processingRate / symbolRate);
            FFTSize = 1;
            while (FFTSize < BlockSize)
            {
                FFTSize <<= 1;
            }
            FFTSize >>= 1;

            if ((int)(BlockSize * symbolRate + 0.5) != (int)processingRate)
            {
                throw new ApplicationException("The processingRate must be integer multiple of symbolRate");
            }
            GeneratorsDone = new bool[NFREQ]; ;
            Frequencies = new float[NFREQ];        // array of all frequencies

            FFTModulator = new FFT(FFTSize);
            FFTInBuffer = new IQ[FFTSize];
            FFTOutBuffer = new IQ[FFTSize];

            OutputBuffer = new float[BlockSize];
            OutputData = new List<float>(BlockSize);

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

            FFT_START_INDEX = (int)(Frequencies[0] / OneChannelBW + 0.5f);
            FFT_END_INDEX = (int)(Frequencies[NFREQ - 1] / OneChannelBW + 0.5f);

            if ((int)((SamplingFreq / OneChannelBW) + 0.5f) != FFTSize)
            {
                throw new ApplicationException("OFDM Frequencies are not orthogonal");
            }
            Init();
        }

        public override void Init()
        {
            CurrFreqIndex = 0;
            FreqAdj = 0;
            CorrFreq = new Quad();
            OutputData.Clear();
            Array.Clear(GeneratorsDone, 0, NFREQ);
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
                CorrFreq = new Quad(IQ.UNITY, new IQ( (float)( value * 2.0 * Math.PI / SamplingFreq)), 1);
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
        public OFDMFFTModulator this[int freqIndex]
        {
            get { CurrFreqIndex = freqIndex; return this; }
        }

        /// <summary>
        /// The indexer for the modulator - allows selectively to work with individual frequencies in a set.
        /// </summary>
        /// <param name="freq">The frequency we will be currently working.</param>
        /// <returns></returns>
        public OFDMFFTModulator this[float freq]
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
            }
        }

        void CalculateOutput(float[] outBuffer, int startingIdx)
        {
            int StartData = BlockSize - FFTSize;
            FFTModulator.ProcessIFFT(FFTInBuffer, FFTOutBuffer);
            // Fill cyclic prefix
            for (int i = 0; i < StartData; i++)
            {
                outBuffer[startingIdx++] = (FFTOutBuffer[FFTSize - StartData + i] * CorrFreq.Next()).I;
            }
            for (int i = 0; i < FFTSize; i++)
            {
                outBuffer[startingIdx++] = (FFTOutBuffer[i] * CorrFreq.Next()).I;
            }
            Array.Clear(GeneratorsDone, 0, NFREQ);  // Mark all generators as non-processed
            Array.Clear(FFTInBuffer, 0, FFTSize);
        }

        void CalculateOutput()
        {
            int StartData = BlockSize - FFTSize;
            FFTModulator.ProcessIFFT(FFTInBuffer, FFTOutBuffer);
            // Fill cyclic prefix
            for (int i = 0; i < StartData; i++)
            {
                OutputData.Add((FFTOutBuffer[FFTSize - StartData + i] * CorrFreq.Next()).I);
            }
            for (int i = 0; i < FFTSize; i++)
            {
                OutputData.Add((FFTOutBuffer[i] * CorrFreq.Next()).I);
            }
            Array.Clear(GeneratorsDone, 0, NFREQ);  // Mark all generators as non-processed
            Array.Clear(FFTInBuffer, 0, FFTSize);
        }

        /// <summary>
        /// Process supplied I and Q components and apply them to the current frequency.
        /// </summary>
        /// <param name="data">IQ components of Quadrature signal.</param>
        /// <returns>true - if I and Q were placed and we still can continue. false - if all frequencies are done and we have to read the buffer.</returns>
        public int Process(IQ data)
        {
            // encode the I bit
            // encode the Q bit 
            FFTInBuffer[CurrFreqIndex + FFT_START_INDEX] = data;
            FFTInBuffer[FFTSize -  (CurrFreqIndex + FFT_START_INDEX)] = data.C;
            GeneratorsDone[CurrFreqIndex] = true;

            // Advance current frequency index and check for wrap-around
            CurrFreqIndex++; if (CurrFreqIndex >= NFREQ) CurrFreqIndex = 0;
            if (GeneratorsDone[CurrFreqIndex])
            {
                CalculateOutput();
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
            // encode the I bit
            // encode the Q bit 
            FFTInBuffer[CurrFreqIndex + FFT_START_INDEX] = data;
            FFTInBuffer[FFTSize - (CurrFreqIndex + FFT_START_INDEX)] = data.C;  // Conjugate
            GeneratorsDone[CurrFreqIndex] = true;

            // Advance current frequency index and check for wrap-around
            CurrFreqIndex++; if (CurrFreqIndex >= NFREQ) CurrFreqIndex = 0;
            if (GeneratorsDone[CurrFreqIndex])
            {
                CalculateOutput(outputArray, 0);
                return BlockSize;
            }
            return 0;
        }

        public int Process(IQ data, float[] outputArray, int outputIndex)
        {
            // encode the I bit
            // encode the Q bit 
            FFTInBuffer[CurrFreqIndex + FFT_START_INDEX] = data;
            FFTInBuffer[FFTSize - (CurrFreqIndex + FFT_START_INDEX)] = data.C;  // Conjugate
            GeneratorsDone[CurrFreqIndex] = true;

            // Advance current frequency index and check for wrap-around
            CurrFreqIndex++; if (CurrFreqIndex >= NFREQ) CurrFreqIndex = 0;
            if (GeneratorsDone[CurrFreqIndex])
            {
                CalculateOutput(outputArray, outputIndex);
                return BlockSize;
            }
            return 0;
        }

        public void Process(CNTRL_MSG controlParam, IQ data)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                // encode the I bit
                // encode the Q bit 
                FFTInBuffer[CurrFreqIndex + FFT_START_INDEX] = data;
                FFTInBuffer[FFTSize - (CurrFreqIndex + FFT_START_INDEX)] = data.C;  // Conjugate
                GeneratorsDone[CurrFreqIndex] = true;

                // Advance current frequency index and check for wrap-around
                CurrFreqIndex++; if (CurrFreqIndex >= NFREQ) CurrFreqIndex = 0;
                if (GeneratorsDone[CurrFreqIndex])
                {
                    CalculateOutput();
                    foreach (float samp in OutputBuffer) DataOut.Process(samp);
                }
            }
            else if (controlParam == CNTRL_MSG.FINISH)
            {
                CalculateOutput(OutputBuffer, 0);
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
            CalculateOutput(outputArray, 0);
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
            CalculateOutput(outputArray, outputIndex);
            // re-initialize index
            CurrFreqIndex = 0;
            return BlockSize;
        }

        /// <summary>
        /// In case of parallel tone modulator - fill remaining frequencies with 0 tails.
        /// </summary>
        public int Finish()
        {
            CalculateOutput();
            CurrFreqIndex = 0;
            return OutputData.Count;
        }

        public int Count { get { return OutputData.Count; } }
        public bool IsDataReady { get { return (OutputData.Count > 0); } }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<IQ>("DataIn", this.Process);
            DataOut = new OutputPin<float>("DataOut");
            base.SetIOParameters("OFDMFFTModulator", new DataPin[] { DataIn, DataOut });
        }
    }

}
