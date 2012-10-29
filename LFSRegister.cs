using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    public enum ConvEncoderType
    {
        Truncate,           // The encoder does not terminate the stream and does not force into a specific state
        ZeroState,          // The encoder/decoder that starts and ends with zero state
        TailBiting_Head,     // Tailbiting encoder/decoder - the start and end states are the same - load with the head.
        TailBiting_Tail,     // Tailbiting encoder/decoder - the start and end states are the same - load with the tail.
    }

    public struct LFSRState
    {
        public int CurrentState;
        public int CurrentCounter;
    }


    class LFSRegister
    {
        int CurrentState;
        int PolyHighBit;            // The Order of the polinomial (highest bit)  
        int Polynomial;             // The generator polynomial
        int OutputMask;             // The output bit(s) mask
        static int[] bitcounts =
	        {0, 1, 1, 2, 1, 2, 2, 3, 1, 2, 2, 3, 2, 3, 3, 4};

        protected int Bitcount(int u)
        {
	        int n = 0;
	        for(; u != 0; u >>= 4)
		        n += bitcounts[u & 0x0f];
	        return n;
        }

        public LFSRegister(int order, int genPoly)
        {
            this.PolyHighBit = order - 1;
            this.Polynomial = genPoly;
            this.OutputMask = genPoly;
        }

        public LFSRegister(int order, int genPoly, int outputMask)
        {
            this.PolyHighBit = order - 1;
            this.Polynomial = genPoly;
            this.OutputMask = outputMask;
        }

        protected void Init(int Seed)
        {
            CurrentState = Seed;
        }

        protected void ShiftFibOneLeft()
        {
            int NextBit = Bitcount( CurrentState & Polynomial) & 0x01;
            CurrentState = (CurrentState << 1) | NextBit;
        }

        protected void ShiftFibOneRight()
        {
            int NextBit = Bitcount(CurrentState & Polynomial) & 0x01;
            CurrentState = (CurrentState >> 1) | (NextBit << PolyHighBit) ;
        }

        protected void ShiftGalOneRight()
        {
            int LastBit = (CurrentState & 0x01);
            CurrentState = (CurrentState >> 1) | (LastBit << PolyHighBit);
            if( LastBit != 0)
                CurrentState ^= Polynomial;
        }

        protected void ShiftGalOneLeft()
        {
            int LastBit =  (CurrentState >> PolyHighBit) & 0x01;
            CurrentState = (CurrentState << 1) | LastBit;
            if (LastBit != 0)
                CurrentState ^= Polynomial;
        }
        
        protected int ShiftFibLeft(int nCount)
        {
            for (int i = 0; i < nCount; i++)
                ShiftFibOneLeft();
            return CurrentState;
        }
        protected int ShiftFibRight(int nCount)
        {
            for (int i = 0; i < nCount; i++)
                ShiftFibOneRight();
            return CurrentState;
        }

        protected  int ShiftGalRight(int nCount)
        {
            for (int i = 0; i < nCount; i++)
                ShiftGalOneRight();
            return CurrentState;
        }

        protected int ShiftGalLeft(int nCount)
        {
            for (int i = 0; i < nCount; i++)
                ShiftGalOneLeft();
            return CurrentState;
        }

        public virtual LFSRState State
        {
            get { LFSRState s; s.CurrentState = this.CurrentState; s.CurrentCounter = 0;  return s; }
            set { this.CurrentState = value.CurrentState; }
        }

        public int Value { get { return CurrentState; } }

        public int CurrentBit
        {
            get { return Bitcount(CurrentState & OutputMask); }
        }
    }

    class LFSR_188_110A : LFSRegister
    {
        int Counter;
        public LFSR_188_110A() : base(12, 0x0052)
        {
            this.Init();
        }

        public void Init()
        {
            base.Init(0x0BAD);
            base.ShiftGalLeft(8);
            this.Counter = 160;
        }

        public int DataNext() 
        {
            int result = base.Value & 0x0007;
            ShiftGalLeft(8);
            this.Counter--;
            if (this.Counter <= 0)
                Init();
            return result;
        }
        public override LFSRState State
        {
            get { LFSRState st = base.State; st.CurrentCounter = this.Counter; return st; }
            set { base.State = value; this.Counter = value.CurrentCounter; }
        }
    }

    class LFSR__188_110B_39 : LFSRegister
    {
        public LFSR__188_110B_39(int order, int Polynomial) : base(order, Polynomial, 0x0000001)
        {
        }
        public new void Init(int seed) { base.Init(seed); }
        public void Shift() { ShiftFibOneLeft(); }
        public void Shift(int nCount) { ShiftFibLeft(nCount); }
    }


    /// <summary>
    /// Class to implement convolutional encoder 1:R rate with K-constraint
    /// </summary>
    class ConvEncoder : DataProcessingModule
    {
        InputPin<byte> DataIn;
        OutputPin<byte> DataOut;
        byte[] FirstBits;

        Queue<byte> OutputData;

        int TailbitingCounter;

        int Rate;       // Number of output bits will be (n * Rate)
        int K;          // Number of bits (including input) that are used to generate output
        int m;          // number of memory locations
        int[] Polynomial;
        int PunctureMask;
        int PunctureSize;

        int CurrentState;
        int CurrentMaskCounter;
        ConvEncoderType EncoderType;

        static int[] bitcounts =
	        { 0, 1, 1, 2, 1, 2, 2, 3, 1, 2, 2, 3, 2, 3, 3, 4 };

        public static int Bitcount(int u)
        {
            int n = 0;
            uint uu = (uint)u;
            for (; uu != 0; uu >>= 4)
                n += bitcounts[uu & 0x0f];
            return n;
        }


        /// <summary>
        /// Constructor for Rate R convolutional encoder.
        /// </summary>
        /// <param name="convRate">The number of bits sent out for each input bit.</param>
        /// <param name="polyDegree">The number of bits (including current symbol) to use in calculations. Equal to Constraint + 1.</param>
        /// <param name="polynomArray">The array of size [Rate] that has polynomial coefficients.</param>
        /// <param name="punctureMask">The puncture mask. 1 in position means output bit. 0 means do not output.</param>
        /// <param name="punctureMaskSize">Total size of the puncture mask.</param>
        public ConvEncoder(ConvEncoderType encType, int convRate, int polyDegree, int[] polynomArray, int punctureMask, int punctureMaskSize)
        {
            this.Rate = convRate;
            this.K = polyDegree;
            this.m = K - 1;
            this.Polynomial = new int[Rate];
            Array.Copy(polynomArray, this.Polynomial, Rate);
            this.PunctureSize = punctureMaskSize;
            this.PunctureMask = punctureMask;
            this.EncoderType = encType;
            FirstBits = new byte[m];
            OutputData = new Queue<byte>();
            Init();
        }

        public void Clear()
        {
            this.Init();
        }

        public override void Init()
        {
            CurrentState = 0;
            CurrentMaskCounter = 0;
            TailbitingCounter = m;
            OutputData.Clear();
        }

        public int State
        {
            get { return CurrentState; }
            set { CurrentState = value; }
        }

        public int NextState(int currState, int InputBit)
        {
            InputBit &= 0x0001;
            return (currState >> 1) | (InputBit << (m - 1));
        }

        public byte Output(int currState, byte InputBit)
        {
            int OutByte = 0;
            InputBit &= 0x0001;
            int Data = currState | (InputBit << m);
            // Go thru every polynomial
            for (int i = 0; i < this.Rate; i++)
            {
                OutByte |= ((Bitcount(Data & Polynomial[i]) & 0x0001) << i);
            }
            return (byte) OutByte;
        }

        int  Process(byte[] outputArray, ref int outputIndex)
        {
            int Result = 0;
            // Go thru every polynomial and prepare "Rate" bits for each input bit 
            for (int i = 0; i < this.Rate; i++)
            {
                if ((PunctureMask & (1 << CurrentMaskCounter)) != 0)   // If bit is 1 in the Mask -> place result
                {
                    outputArray[outputIndex++] = (byte)(Bitcount(CurrentState & Polynomial[i]) & 0x0001);
                    Result++;
                }
                if (++CurrentMaskCounter >= PunctureSize) CurrentMaskCounter = 0;
            }
            // Update the State
            CurrentState >>= 1;
            return Result;
        }

        public void Process(byte inputByte)
        {
            CurrentState |= (inputByte & 0x0001) << m;
            // Go thru every polynomial and prepare "Rate" bits for each input bit 
            for (int i = 0; i < this.Rate; i++)
            {
                if ((PunctureMask & (1 << CurrentMaskCounter)) != 0)   // If bit is 1 in the Mask -> place result
                {
                    OutputData.Enqueue((byte)(Bitcount(CurrentState & Polynomial[i]) & 0x0001));
                }
                if (++CurrentMaskCounter >= PunctureSize) CurrentMaskCounter = 0;
            }
            // Update the State
            CurrentState >>= 1;
        }

        void Process()
        {
            // Go thru every polynomial and prepare "Rate" bits for each input bit 
            for (int i = 0; i < this.Rate; i++)
            {
                if ((PunctureMask & (1 << CurrentMaskCounter)) != 0)   // If bit is 1 in the Mask -> place result
                {
                    DataOut.Process((byte)(Bitcount(CurrentState & Polynomial[i]) & 0x0001));
                }
                if (++CurrentMaskCounter >= PunctureSize) CurrentMaskCounter = 0;
            }
            // Update the State
            CurrentState >>= 1;
        }



        /// <summary>
        /// Process supplied byte array and generate the output.
        /// </summary>
        /// <param name="inputData">Input byte array.</param>
        /// <param name="outputArray"> Array that receives the result of the convolutional encoding.</param>
        /// <param name="numInputBits">Number of input bits to process.</param>
        /// <returns>Number of bits placed in the output array.</returns>
        public int Process(byte[] inputData, int inputIndex, byte[] outputArray, int outputIndex, int numInputBits)
        {
            int Result = 0;

            // Initialize the state with first/last "m" bits of the sequence
            if ( TailbitingCounter > 0 )
            {
                int BitsCount;

                if (this.EncoderType == ConvEncoderType.TailBiting_Head)
                {
                    BitsCount = Math.Min(numInputBits, TailbitingCounter);
                    while (BitsCount > 0)
                    {
                        byte Data = inputData[inputIndex++];
                        CurrentState |= (Data & 0x0001) << (m - TailbitingCounter);
                        FirstBits[m - TailbitingCounter] = Data;
                        BitsCount--;
                        TailbitingCounter--;

                        numInputBits--;
                    }
                }
                else if (this.EncoderType == ConvEncoderType.TailBiting_Tail)
                {
                    int IdxByte = inputIndex + numInputBits - TailbitingCounter;
                    BitsCount = Math.Min(numInputBits, TailbitingCounter);
                    while (BitsCount > 0)
                    {
                        CurrentState |= (inputData[IdxByte++] & 0x0001) << (m - TailbitingCounter);
                        BitsCount--;
                        TailbitingCounter--;
                    }
                }
                else
                {
                    TailbitingCounter = 0;
                }
            }

            // Process all bits provided in the inputData array.
            while (numInputBits > 0)
            {
                // Add new bit to the register
                CurrentState |= (inputData[inputIndex++] & 0x0001) << m;
                Result += Process(outputArray, ref outputIndex);
                numInputBits--;
            }
            return Result;
        }


        public int Process(byte[] inputData, int inputIndex, int numInputBits)
        {
             // Initialize the state with first/last "m" bits of the sequence
            if (TailbitingCounter > 0)
            {
                int BitsCount;

                if (this.EncoderType == ConvEncoderType.TailBiting_Head)
                {
                    BitsCount = Math.Min(numInputBits, TailbitingCounter);
                    while (BitsCount > 0)
                    {
                        byte Data = inputData[inputIndex++];
                        CurrentState |= (Data & 0x0001) << (m - TailbitingCounter);
                        FirstBits[m - TailbitingCounter] = Data;
                        BitsCount--;
                        TailbitingCounter--;

                        numInputBits--;
                    }
                }
                else if (this.EncoderType == ConvEncoderType.TailBiting_Tail)
                {
                    int IdxByte = inputIndex + numInputBits - TailbitingCounter;
                    BitsCount = TailbitingCounter;
                    while (BitsCount > 0)
                    {
                        CurrentState |= (inputData[IdxByte++] & 0x0001) << (m - TailbitingCounter);
                        BitsCount--;
                        TailbitingCounter--;
                    }
                }
                else
                {
                    TailbitingCounter = 0;
                }
            }
            // Process all bits provided in the inputData array.
            while (numInputBits-- > 0)
            {
                Process(inputData[inputIndex++]);
            }
            return OutputData.Count;
        }


        public void Process(CNTRL_MSG controlParam, byte incomingBit)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                // Initialize the state with first "m - 1" bits of the sequence
                if (TailbitingCounter > 0)
                {
                    if (this.EncoderType == ConvEncoderType.TailBiting_Head)
                    {
                        CurrentState |= (incomingBit & 0x0001) << (m - TailbitingCounter);
                        FirstBits[m - TailbitingCounter] = incomingBit;
                        TailbitingCounter--;
                    }
                    else
                    {
                        TailbitingCounter = 0;
                    }
                }

                if (TailbitingCounter == 0)
                {
                    CurrentState |= (incomingBit & 0x0001) << m;
                    Process();
                }
            }
            else if (controlParam == CNTRL_MSG.INTERLEAVER_FRAME)
            {
                // If zero-terminating sequence - add "m" zero bits at the end
                if (this.EncoderType == ConvEncoderType.ZeroState)
                {
                    for (int BitsCount = 0; BitsCount < this.m; BitsCount++)
                    {
                        Process();
                    }
                }
                else if (this.EncoderType == ConvEncoderType.TailBiting_Head)
                {
                    // If tailbiting sequence - add "m" First bits at the end
                    for (int BitsCount = 0; BitsCount < this.m; BitsCount++)
                    {
                        CurrentState |= (FirstBits[BitsCount] & 0x0001) << m;
                        Process();
                    }
                }
            }
        }

        public int Finish(byte[] outputArray, int outputIndex)
        {
            int Result = 0;
            // If zero-terminating sequence - add "m" zero bits at the end
            if (this.EncoderType == ConvEncoderType.ZeroState)
            {
                for (int BitsCount = 0; BitsCount < this.m; BitsCount++)
                {
                    Result += Process(outputArray, ref outputIndex);
                }
            }
            else if (this.EncoderType == ConvEncoderType.TailBiting_Head)
            {
                // If tailbiting sequence - add "m" First bits at the end
                for (int BitsCount = 0; BitsCount < this.m; BitsCount++)
                {
                    CurrentState |= (FirstBits[BitsCount] & 0x0001) << m;
                    Result += Process(outputArray, ref outputIndex);
                }
            }
            return Result;
        }

        public int Finish()
        {
            // If zero-terminating sequence - add "m" zero bits at the end
            if (this.EncoderType == ConvEncoderType.ZeroState)
            {
                for (int BitsCount = 0; BitsCount < this.m; BitsCount++)
                {
                    Process(0);
                }
            }
            else if (this.EncoderType == ConvEncoderType.TailBiting_Head)
            {
                // If tailbiting sequence - add "m" First bits at the end
                for (int BitsCount = 0; BitsCount < this.m; BitsCount++)
                {
                    Process(FirstBits[BitsCount]);
                }
            }
            return OutputData.Count;
        }

        public bool IsDataReady { get { return (OutputData.Count > 0); } }

        public int Count
        {
            get { return OutputData.Count; }
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
            base.SetIOParameters("FEC Convolutional Encoder", new DataPin[] { DataIn, DataOut });
        }
    }

}
