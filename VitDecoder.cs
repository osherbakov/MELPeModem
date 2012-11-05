using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{

    class VitDecoder : DataProcessingModule
    {
        InputPin<byte> DataIn;
        OutputPin<byte> DataOut;

        const int BITSPERSYMBOL = 8;
        const int SYMBOLBITMASK = (1 << BITSPERSYMBOL) - 1;
        const int MAX_RATE = 32 / BITSPERSYMBOL;
        const int FULL_0 = 0x00;  // 100% sure that is 0
        const int FULL_1 = (1 << BITSPERSYMBOL) - 1;  // 100% sure that is 1
        const int ERASURE_0 = FULL_1 / 2; // can be 0 or 1, 50/50 chance
        const int ERASURE_1 = ERASURE_0 + 1; // can be 0 or 1, 50/50 chance

        int BACKTRACKDEPTH;
        int MAXWORDCOUNT;

        int Rate;       // Number of input bits will be (n * Rate)
        int K;          // The degree of the polynomial (how many bits affect result, including new current bit)
        int m;          // Number of memory bits (K - 1)
        int n;          // Number of possible values for input bits - in our case "0" and "1" ( 2^1 );
        int highbit;    // Position of the high bit in the memory

        int PunctureMask;   // Current puncture mask - bits are tested starting from LSB. "1" in bit position - output bit, 
                            // "0" in bit position - do not output 
        int PunctureSize;   // Total number of bits that are used in defining puncturing - how many bits are used
        int PuncturePass;   // Number of bits from "PunctureSize" to output. The puncture rate is = PuncturePass/PunctureSize.

        int PunctureCounter;   // Puncture counter

        int[] Polynomial;       // The polynomial that defines the output bits. Number of polynomials in array is "Rate"
        int NumberOfStates;     // Number of states (equal to 2^(memory length)
        int NumberOfOutputs;    // Number of all possible output values (equal to 2^Rate)
        int TrainingWindowSize; // The training window size
        int ReleaseWindowSize;  // The size of the release window (how many points are output in one backtrace)
        ConvEncoderType encoderType;// Zero-ending or tailbiting encoder

        int Time;               // Current running time (symbol Index)
        int OutputOffset;       // Offset to start outputting symbols;
        int currIdx, nextIdx;       // Flip-flop indices to update metrics
        int StateHistoryIdx;
        int TracebackStart;       // The starting index for the output data

        // Lookup tables to calculate...
        // First index is always state
        byte[,] output;              /* gives conv. encoder output[state, bit] */
        int[,] accum_prob_metric;   /* accumulated error metrics[state, current/next] */
        int[,] state_history;       /* state history table[state, current step]  */

        List<int> DepuncturedData;
        byte [] TempStack;
        Queue<byte> OutputData;

        int CurrentWord;          // Current assembled output word (made out of "Rate" nibbles
        int CurrentCounter;         // Current output counter

        public VitDecoder(ConvEncoderType encType, int convRate, int polyDegree, int[] polynomArray, int punctureMask, int punctureMaskSize, int rWindowSize)
        {
            if (convRate > MAX_RATE)
            {
                throw new ArgumentException( string.Format("The rate cannot be more than : {0}", MAX_RATE));
            }
            this.Rate = convRate;
            this.MAXWORDCOUNT = BITSPERSYMBOL * this.Rate;
            this.K = polyDegree;
            this.Polynomial = new int[Rate];
            Array.Copy(polynomArray, Polynomial, Rate);

            this.PunctureMask = punctureMask;
            this.PunctureSize = Math.Min(punctureMaskSize, 32);
            this.PuncturePass = Math.Min(this.PunctureSize, ConvEncoder.Bitcount(PunctureMask));
            /* ************************************************************************** */
            /* little degradation in performance achieved by limiting trellis depth
               to K * 5 ... K * 6 --interesting to experiment with smaller values and measure
               the resulting degradation. */
            // If we use puncturing, then the size of the backtrack depth should be doubled
            this.BACKTRACKDEPTH = 6 * (PuncturePass < PunctureSize ? 2 : 1);

            // In order to release RW bits we have to train for at least BT + RW
            this.TrainingWindowSize = K * BACKTRACKDEPTH + rWindowSize;
            this.ReleaseWindowSize = (rWindowSize == 0) ? TrainingWindowSize : rWindowSize;

            /* m (memory length) = K - 1 */
            this.m = K - 1;
            this.highbit = m - 1;           // the position of the high bit in the state
            this.NumberOfStates = 1 << m;    // Number of possible states 2^m
            this.NumberOfOutputs = (1 << Rate);
            this.encoderType = encType;

            /* n is 2^1 = 2 for the rate 1/Rate */
            this.n = 1 << 1;  // Number of possible inputs ( 0 or 1) for 1/Rate convolutional codec

            this.output = new byte[NumberOfStates, n];                           /* gives conv. encoder output */
            this.accum_prob_metric = new int[NumberOfStates, 2];                /* accumulated error metrics - current and next */
            this.state_history = new int[NumberOfStates, TrainingWindowSize];   /* state history table */

            // Use the Convolutional Encoder for pre-filling output table
            ConvEncoder encGen = new ConvEncoder(ConvEncoderType.Truncate, Rate, K, Polynomial, -1, 32);
            for (int state = 0; state < NumberOfStates; state++)
            {
                for (int bit = 0; bit < n; bit++)
                {
                    /* output , given current state and input */
                    output[state, bit] = encGen.Output(state, (byte) bit);
                } /* end of bit for loop */
            } /* end of state for loop */

            this.DepuncturedData = new List<int>();
            this.TempStack = new byte[TrainingWindowSize];
            this.OutputData = new Queue<byte>();

            Init();
        }



        void DepunctureInit()
        {
            DepuncturedData.Clear();
            PunctureCounter = 0;
            CurrentCounter = 0;
            CurrentWord = 0;
        }

        void DecoderInit(int startingTime, int startingOutputOffset)
        {
            Time = startingTime;
            OutputOffset = startingOutputOffset;
            // Where to start  traceback
            TracebackStart = startingTime + TrainingWindowSize;

            // Initialize indices
            currIdx = 0;
            nextIdx = 1;
            StateHistoryIdx = 0;
            for (int state = 0; state < NumberOfStates; state++)
            {
                /* initial accum_error_metric[x][0] = zero */
                accum_prob_metric[state, currIdx] = 0;
                accum_prob_metric[state, nextIdx] = 0;
            }
            // Assign initial high probability to the state 0
            //  if it is not tail-biting algorithm
            if ((startingTime == 0) && (encoderType == ConvEncoderType.ZeroState))
            {
                accum_prob_metric[0, currIdx] = FULL_1 * TrainingWindowSize;
            }
        }

        public override void  Init()
        {
            /* initialize data structures */
            DepunctureInit();
            DecoderInit(0, 0);
            OutputData.Clear();
        }


        // Support function - calculates the next state based on current state and incoming bit
        int NextState(int currentState, int dataBit)
        {
            return (currentState | (dataBit << m)) >> 1;
        }

        // Support function - calculates the input bit that brought us to that state
        byte InputDataBit(int nextState)
        {
            return (byte)((nextState >> highbit) & 0x0001);
        }

        public int Quantize(float[] inputArray, int startingIndex, int nSamples)
        {
            // We quantize and de-puncture the incoming sequence
            // The resulting array is specifically optimized to have int-per-word
            int outSymbol;

            bool activePuncture = (PunctureMask & (1 << PunctureCounter)) == 0;
            // for proper de-puncturing we need to go bit-by-bit
            while ((nSamples > 0) || activePuncture)
            {
                if (activePuncture)
                {   // This bit was not transmitted - replace with ERASURE 
                    outSymbol = ERASURE_0;
                }
                else
                {   // Make a hard decision for that bit
                    outSymbol = (inputArray[startingIndex++] < 0) ? FULL_0 : FULL_1;
                    nSamples--;
                }

                // Update puncture bit
                if (++PunctureCounter >= this.PunctureSize) PunctureCounter = 0;
                activePuncture = (PunctureMask & (1 << PunctureCounter)) == 0;
                // Update output word and if there are enough symbols - store it
                CurrentWord |= (outSymbol << CurrentCounter);
                CurrentCounter += BITSPERSYMBOL;    // We have 4-bits per symbol words
                if (CurrentCounter >= MAXWORDCOUNT)
                {
                    DepuncturedData.Add(CurrentWord);
                    CurrentWord = 0;
                    CurrentCounter = 0;
                }
            }
            return DepuncturedData.Count;
        }

        public int Quantize(byte[] inputArray, int startingIndex, int nBits)
        {
            // We quantize and de-puncture the incoming sequence
            // The resulting array is specifically optimized to have int-per-word
            int outSymbol;

            bool activePuncture = (PunctureMask & (1 << PunctureCounter)) == 0;
            // for proper de-puncturing we need to go bit-by-bit
            while ((nBits > 0) || activePuncture)
            {
                if (activePuncture)
                {   // This bit was not transmitted - replace with ERASURE 
                    outSymbol = ERASURE_0;
                }
                else
                {   // Make a hard decision for that bit
                    outSymbol = (inputArray[startingIndex++] == 0) ? FULL_0 : FULL_1;
                    nBits--;
                }

                // Update puncture bit
                if (++PunctureCounter >= this.PunctureSize) PunctureCounter = 0;
                activePuncture = (PunctureMask & (1 << PunctureCounter)) == 0;
                // Update output word and if there are enough symbols - store it
                CurrentWord |= (outSymbol << CurrentCounter);
                CurrentCounter += BITSPERSYMBOL;    // We have 4-bits per symbol words
                if (CurrentCounter >= MAXWORDCOUNT)
                {
                    DepuncturedData.Add(CurrentWord);
                    CurrentWord = 0;
                    CurrentCounter = 0;
                }
            }
            return DepuncturedData.Count;
        }

        public int Quantize(byte inputByte)
        {
            // We quantize and de-puncture the incoming sequence
            // The resulting array is specifically optimized to have int-per-word
            int outSymbol;
            int nBits = 1;

            bool activePuncture = (PunctureMask & (1 << PunctureCounter)) == 0;
            // for proper de-puncturing we need to go bit-by-bit
            while ((nBits > 0) || activePuncture)
            {
                if (activePuncture)
                {   // This bit was not transmitted - replace with ERASURE 
                    outSymbol = ERASURE_0;
                }
                else
                {   // Make a hard decision for that bit
                    outSymbol = (inputByte == 0) ? FULL_0 : FULL_1;
                    nBits--;
                }

                // Update puncture bit
                if (++PunctureCounter >= this.PunctureSize) PunctureCounter = 0;
                activePuncture = (PunctureMask & (1 << PunctureCounter)) == 0;
                // Update output word and if there are enough symbols - store it
                CurrentWord |= (outSymbol << CurrentCounter);
                CurrentCounter += BITSPERSYMBOL;    // We have 4-bits per symbol words
                if (CurrentCounter >= MAXWORDCOUNT)
                {
                    DepuncturedData.Add(CurrentWord);
                    CurrentWord = 0;
                    CurrentCounter = 0;
                }
            }
            return DepuncturedData.Count;
        }

        int SoftMetric(int inputWord, byte Guess)
        {
            int Value = 0;
            // Calculate SoftMetric - the bit values are in 4-byte nibbles
            // 0x00 - 0x07   -  Logical 0, 0x00 being the strongest
            // 0x08 - 0x0F   -  Logical 1, 0x0F being the strongest
            // 0x07, 0x08    -  Unknown or ERASURE
            for (int i = 0; i < this.Rate; i++)
            {
                int BitSoftValue1 = inputWord & SYMBOLBITMASK;
                int BitSoftValue0 = FULL_1 - BitSoftValue1;
                // The quantized value is the error distance from the "0" value
                // We can use maximum likelihood algorithm, i.e. we are minimizing the error distance.
                //  It real life, we deal with probabilities, so technically we have to maximize
                //  the probability of the correct decision (Maximum Likelihood - ML).

                Value += ((Guess & 0x01) == 0) ? BitSoftValue0 : BitSoftValue1;
                inputWord >>= BITSPERSYMBOL;
                Guess >>= 1;
            }
            return Value;
        }

        void ProcessForward(int inputWord)
        {
            int state;                           /* loop variables */
            int CurrPathMetric, NextPathMetric;

            /* repeat for each possible state */
            // Go vertically on trellis -  calculate branch metric
            // Here we unroll the loop - we deal with 1/Rate encoders, so the only possible input bits are "0" and "1".
            // So the first two operations just update the accum_prob_metric, while the second two do it conditionally
            for (state = 0; state < NumberOfStates; state++)
            {
                int NextState0 = NextState(state, 0);
                int NextState1 = NextState(state, 1);
                int Metric0, Metric1;
                int State0, State1;
                // Calculate and update metric for "0" and "1" transition from the state
                CurrPathMetric = accum_prob_metric[state, currIdx];

                Metric0 = CurrPathMetric + SoftMetric(inputWord, output[state, 0]);
                State0 = state;
                Metric1 = CurrPathMetric + SoftMetric(inputWord, output[state, 1]);
                State1 = state;

                state++;    // Now move to the next state in the butterfly
                CurrPathMetric = accum_prob_metric[state, currIdx];

                NextPathMetric = CurrPathMetric + SoftMetric(inputWord, output[state, 0]);
                /* now choose the surviving path--the one with the bigger accumlated metric... */
                if (Metric0 < NextPathMetric)
                {
                    Metric0 = NextPathMetric;
                    State0 = state;
                }
                NextPathMetric = CurrPathMetric + SoftMetric(inputWord, output[state, 1]);
                /* now choose the surviving path--the one with the bigger accumlated  metric... */
                if (Metric1 < NextPathMetric)
                {
                    Metric1 = NextPathMetric;
                    State1 = state;
                }

                /* save an accumulated metric value for the survivor state */
                /* update the state_history array with the state number of the survivor */
                accum_prob_metric[NextState0, nextIdx] = Metric0;
                state_history[NextState0, StateHistoryIdx] = State0;
                accum_prob_metric[NextState1, nextIdx] = Metric1;
                state_history[NextState1, StateHistoryIdx] = State1;
            } /* end of 'state' for-loop -- we have now updated the trellis */
            // Swap current and next indicies
            int tmp = currIdx;
            currIdx = nextIdx; nextIdx = tmp;
            StateHistoryIdx++; if (StateHistoryIdx >= TrainingWindowSize) StateHistoryIdx = 0;
        }

        void ProcessTraceback(int numOutputBits, bool shortTraceback, int numTracebackStates)
        {
            /* work backwards from the end of the trellis to the oldest state
               in the trellis to determine the optimal path. The purpose of this
               is to determine the most likely (ML) state sequence at the encoder
                based on what channel symbols we received. */
            int StateIdx = (shortTraceback && (encoderType == ConvEncoderType.ZeroState)) ? 0 : FindMaxState();
            int TraceBackIdx = StateHistoryIdx;    // Start with the last calculated state metric
            int EndOutput = OutputOffset + numOutputBits;
            for (int i = numTracebackStates - 1; i >= 0; i--)
            {
                TraceBackIdx--; if (TraceBackIdx < 0) TraceBackIdx += TrainingWindowSize;
                if ((i >= OutputOffset) && (i < EndOutput))
                {
                    TempStack[i - OutputOffset] = InputDataBit(StateIdx);
                }
                StateIdx = state_history[StateIdx, TraceBackIdx];
            }
            // Now move the data into the output queue
            for (int i = 0; i < numOutputBits; i++) OutputData.Enqueue(TempStack[i]);
        }

        int FindMaxState()
        {
            int StateIdx = 0;
            int x = 0;
            for (int state = 0; state < NumberOfStates; state++)
            {
                if (accum_prob_metric[state, currIdx] > x)
                {
                    x = accum_prob_metric[state, currIdx];
                    StateIdx = state;
                }
            }
            return StateIdx;
        }
        

        void Process(int numberOfBits)
        {
            /* ************************************************************************** */
            /* Start decoding of channel outputs with forward traversal of trellis! */
            bool ShortTraceback = false;
            int TracebackLength = TrainingWindowSize;
            int ReleaseSize;

            while (numberOfBits > 0)
            {
                ReleaseSize = Math.Min(ReleaseWindowSize, numberOfBits);
                if (TracebackStart >= DepuncturedData.Count)
                {
                    ShortTraceback = true;
                    TracebackLength = DepuncturedData.Count + TrainingWindowSize - TracebackStart;
                    TracebackStart = DepuncturedData.Count;
                    ReleaseSize = numberOfBits;
                }

                while (Time < TracebackStart)
                {
                    ProcessForward(DepuncturedData[Time]);
                    Time++;
                }
                /* now start the traceback, if we've filled the trellis */
                ProcessTraceback(ReleaseSize, ShortTraceback, TracebackLength);
                TracebackStart += ReleaseSize;
                numberOfBits -= ReleaseSize;
            }
        }

        public void Finish()
        {
            int StartingTime;

            // Calculate the number of output bits:
            int NumOutputBits = DepuncturedData.Count;
            if (encoderType == ConvEncoderType.ZeroState)
            {
                NumOutputBits -= this.m;
            }

            // For tailbiting codes add the additional data to train the decoder
            if (encoderType == ConvEncoderType.TailBiting_Head)
            {
                int DataToAdd = Math.Min(TrainingWindowSize, DepuncturedData.Count);
                for (int i = 0; i < DataToAdd; i++)
                {
                    DepuncturedData.Add(DepuncturedData[i]);
                }
                StartingTime = NumOutputBits - this.m;
                int Training = DataToAdd / 2;
                int HeadSize = this.m;
                // Get the first m bits from the middle of the sequence
                DecoderInit(StartingTime - Training, Training);
                Process(HeadSize);
            }
            else if (encoderType == ConvEncoderType.TailBiting_Tail)
            {
                int DataToAdd = Math.Min(TrainingWindowSize, DepuncturedData.Count);
                for (int i = 0; i < DataToAdd; i++)
                {
                    DepuncturedData.Add(DepuncturedData[i]);
                }
            }

            DecoderInit(0, 0);
            Process(NumOutputBits);
        }

        public void  Process(byte[] inputData, int startingIndex,  int numInputBits)
        {
            // First, quantize and de-puncture
            this.Quantize(inputData, startingIndex, numInputBits);
        } /* end of function Process */


        public void Process(CNTRL_MSG controlParam, byte incomingBit)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
            }
        }

        public int Count
        {
            get { return OutputData.Count; }
        }

        public byte GetData()
        {
            return OutputData.Dequeue();
        }

        public int GetData(byte[] outData)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outData, 0);
            OutputData.Clear();
            return ret;
        }

        public int GetData(byte[] outData, int startingIndex)
        {
            int ret = OutputData.Count;
            OutputData.CopyTo(outData, startingIndex);
            OutputData.Clear();
            return ret;
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<byte>("DataIn", this.Process);
            DataOut = new OutputPin<byte>("DataOut");
            base.SetIOParameters("Viterbi Soft Decoder", new DataPin[] { DataIn, DataOut });
        }
    }
}
