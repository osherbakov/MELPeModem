using System;
using System.Collections.Generic;
using System.Text;


// The following data types are used in the project
//  Sample - the raw sample (float) that goes in/out of the DAC/ADC at SAMPLE_RATE
//  IQ  - the IQ pair (float) that contain amplitude and phase information at SYMBOL_RATE
//  Symbol - the symbol to be encoded/decoded at SYMBOL_RATE
//  Value - any intermediate value (float)
//  Bits - Symbols packed into the byte array, usually at SYMBOL_RATE * BITS_PER_SYMBOL

namespace MELPeModem
{
    struct IQ
    {
        public IQ(float phase) {I = (float) Math.Cos(phase); Q = (float) Math.Sin(phase); }
        public IQ(float valueI, float valueQ) { I = valueI; Q = valueQ; }
        public float I, Q;

        public static IQ ZERO = new IQ(0, 0);
        public static IQ UNITY = new IQ(1, 0);
        public static IQ DEG45 = new IQ((float)Math.PI/4);

        public float R2
        {
            get { return I * I + Q * Q; }
        }
        public float R
        {
            get { return (float)Math.Sqrt(I * I + Q * Q); }
        }

        public IQ N2
        {
            get
            {
                float K = this.R2;
                return (K > 0) ? this / K : ZERO;
            }
        }
        
        public IQ N
        {
            get
            {
                float K = this.R;
                return (K > 0) ? this / K : ZERO;
            }
        }

        public IQ P
        {
            get
            {
                return new IQ(I*I, Q*Q) ;
            }
        }

        public IQ C
        {
            get { return new IQ(I, -Q); }
        }

        public float Phase
        {
            get { return (float)Math.Atan2(Q, I); }
        }
        public float Degrees
        {
            get { float ret = (float)(Math.Atan2(Q, I) * 180 / Math.PI); return ret; }
        }
        public float DeltaSin(IQ prevIQ) 
        {
            return Q * prevIQ.I - I * prevIQ.Q  ;
        }
        public float DeltaCos(IQ prevIQ) 
        {
            return I * prevIQ.I + Q * prevIQ.Q;
        }
        public float DeltaSinR(IQ prevIQ)
        {
            return (prevIQ.R > 0) ? (Q * prevIQ.I - I * prevIQ.Q) / prevIQ.R2 : 0;
        }
        public float DeltaCosR(IQ prevIQ)
        {
            return (prevIQ.R > 0) ? (Q * prevIQ.Q + I * prevIQ.I) / prevIQ.R2 : 0;
        }
        public IQ Delta(IQ prevIQ)
        {
            float rI = (Q * prevIQ.Q + I * prevIQ.I);
            float rQ = (Q * prevIQ.I - I * prevIQ.Q);
            return new IQ(rI, rQ);
        }

        public IQ DeltaR(IQ prevIQ)
        {
            float K = prevIQ.R2;
            if (K > 0)
            {
                float rI = (Q * prevIQ.Q + I * prevIQ.I) / K;
                float rQ = (Q * prevIQ.I - I * prevIQ.Q) / K;
                return new IQ(rI, rQ);
            }
            else
                return ZERO;
        }

        public override int GetHashCode()
        {
            return I.GetHashCode() ^ Q.GetHashCode();
        }
        public override bool Equals(object other) 
        {
            return (other is IQ) && Equals((IQ)other);
        }
        public static bool operator ==(IQ lhs, IQ rhs)
        {
            return lhs.Equals(rhs);
        }
        public static bool operator !=(IQ lhs, IQ rhs)
        {
            return !lhs.Equals(rhs);
        }
        private bool Equals(IQ other)
        {
            return (I == other.I) && (Q == other.Q);
        }


        public static IQ operator *(IQ a, IQ b)
        {
            float rI = a.I * b.I - a.Q * b.Q;
            float rQ = a.I * b.Q + a.Q * b.I;
            return new IQ(rI, rQ);
        }

        public static IQ operator *(IQ a, float n)
        {
            return new IQ(a.I * n, a.Q * n);
        }

        public static IQ operator *(float n, IQ a)
        {
            return new IQ(a.I * n, a.Q * n);
        }

        public static IQ operator /(IQ a, IQ b)
        {
            float rI = a.I * b.I + a.Q * b.Q;
            float rQ = a.Q * b.I - a.I * b.Q;
            return new IQ(rI, rQ);
        }

        public static IQ operator /(IQ a, float n)
        {
            return new IQ(a.I / n, a.Q / n);
        }

        public static IQ operator + (IQ a, IQ b)
        {
            return new IQ(a.I + b.I, a.Q + b.Q);
        }
        public static IQ operator -(IQ a, IQ b)
        {
            return new IQ(a.I - b.I, a.Q - b.Q);
        }
        public static IQ operator -(IQ a)
        {
            return new IQ(-a.I, - a.Q);
        }
    }
   
    struct Index
    {
        int Current;
        int MaxLen;

        public Index(int indexLen)
        {
            Current = 0; MaxLen = indexLen;  
        }
        public Index(int currIndex, int indexLen)
        {
            Current = currIndex % indexLen; MaxLen = indexLen; 
        }

        public void Init()
        {
            Current = 0;
        }

        public static implicit operator int(Index a)
        {
            return a.Current;
        }

        public static Index operator ++(Index a)
        {
            a.Current = (a.Current + 1) % a.MaxLen;
            return a;
        }

        public static Index operator --(Index a)
        {
            a.Current = (a.Current == 0) ? a.MaxLen - 1 : a.Current - 1;
            return a;
        }

        public static Index operator +(Index a, int b)
        {
            int val = (a.Current + b) % a.MaxLen;
            if (val < 0) val += a.MaxLen;
            return new Index(val, a.MaxLen);
        }

        public static Index operator -(Index a, int b)
        {
            int val = (a.Current - b) % a.MaxLen;
            if (val < 0) val += a.MaxLen;
            return new Index(val, a.MaxLen);
        }

        public static int operator -(Index a, Index b)
        {
            int ret = a.Current - b.Current;
            if (ret < 0) ret += a.MaxLen;
            return ret;
        }

        public int Value
        {
            get { return Current; }
            set { Current = value % MaxLen; if (Current < 0) Current += MaxLen; }
        }
    }


    struct BitGroup
    {
        enum Val
        {
            ZERO = -1,
            ONES = -2,
            ALT1 = -3,
            ALT0 = -4,
            COUNT_LSB = -5,
            COUNT_MSB = -6,
        }
        public int Position;
        public int Size;

        public BitGroup(int pos, int fieldSize)
        {
            this.Position = pos;
            this.Size = fieldSize;
        }

    }

    class DataSpreader<T> : DataProcessingModule where T:struct
    {
        InputPin<T> DataIn;
        OutputPin<T> DataOut;

        int NumBitsIn;
        int NumBitsOut;

        T[] DataArray;
        BitGroup[] BGArray;

        List<T> OutputData;

        int CurrentInIndex;
        int CurrentInCounter;

        int MaxBits;
        int MaxBitGroupIndex;

        public DataSpreader(int inputBlockSize, BitGroup[] bitgroupArray)
        {
            NumBitsIn = inputBlockSize;
            MaxBitGroupIndex = bitgroupArray.Length;
            BGArray = new BitGroup[MaxBitGroupIndex];
            bitgroupArray.CopyTo(BGArray, 0);

            MaxBits = 0;
            NumBitsOut = 0;
            foreach (BitGroup bg in bitgroupArray)
            {
                NumBitsOut += bg.Size;
                if (bg.Position >= MaxBits)
                {
                    MaxBits = bg.Position;
                }
            }
            MaxBits += inputBlockSize;  // Reserve this space for block 0

            DataArray = new T[MaxBits];
            OutputData = new List<T>(NumBitsOut);
            Init();
        }

        public override void Init()
        {
            Array.Clear(DataArray, 0, MaxBits);
            OutputData.Clear();
            CurrentInIndex = 0;
            CurrentInCounter = 0;
        }

        public int Process(T incomingBit)
        {
            DataArray[CurrentInIndex++] = incomingBit; if (CurrentInIndex >= MaxBits) CurrentInIndex = 0;
            CurrentInCounter++;
            if (CurrentInCounter >= NumBitsIn)
            {
                CurrentInCounter = 0;
                int CurrentFrame = CurrentInIndex - NumBitsIn; if (CurrentFrame < 0) CurrentFrame += MaxBits;
                // Start from the beginning of the input block

                int CurrentOutIndex;
                int CurrentOutCounter;
                BitGroup bg;

                int CurrentBitGroupIndex = 0;
                while (CurrentBitGroupIndex < MaxBitGroupIndex)
                {
                    bg = BGArray[CurrentBitGroupIndex++];
                    CurrentOutIndex = CurrentFrame - bg.Position; if (CurrentOutIndex < 0) CurrentOutIndex += MaxBits;
                    CurrentOutCounter = bg.Size;
                    while (CurrentOutCounter-- > 0)
                    {
                        OutputData.Add(DataArray[CurrentOutIndex++]); if (CurrentOutIndex >= MaxBits) CurrentOutIndex = 0;
                    }
                }
            }
            return this.OutputData.Count;
        }

        public void Process(CNTRL_MSG controlParam,  T incomingBit)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                DataArray[CurrentInIndex++] = incomingBit; if (CurrentInIndex >= MaxBits) CurrentInIndex = 0;
                CurrentInCounter++;
                if (CurrentInCounter >= NumBitsIn)
                {
                    CurrentInCounter = 0;
                    int CurrentFrame = CurrentInIndex - NumBitsIn; if (CurrentFrame < 0) CurrentFrame += MaxBits;
                    // Start from the beginning of the input block

                    int CurrentOutIndex;
                    int CurrentOutCounter;
                    BitGroup bg;

                    int CurrentBitGroupIndex = 0;
                    while (CurrentBitGroupIndex < MaxBitGroupIndex)
                    {
                        bg = BGArray[CurrentBitGroupIndex++];
                        CurrentOutIndex = CurrentFrame - bg.Position; if (CurrentOutIndex < 0) CurrentOutIndex += MaxBits;
                        CurrentOutCounter = bg.Size;
                        while (CurrentOutCounter-- > 0)
                        {
                            DataOut.Process(DataArray[CurrentOutIndex++]); if (CurrentOutIndex >= MaxBits) CurrentOutIndex = 0;
                        }
                    }
                }
            }
        }

        public int Count
        {
            get { return this.OutputData.Count;  }
        }

        public bool IsDataReady { get { return this.Count > 0; } }


        public int GetData(T[] outputArray, int startingIndex)
        {
            int ret = this.OutputData.Count;
            OutputData.CopyTo(outputArray, startingIndex);
            OutputData.Clear();
            return ret;
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<T>("DataIn", this.Process);
            DataOut = new OutputPin<T>("DataOut");
            base.SetIOParameters("DataSpreader", new DataPin[] { DataIn, DataOut });
        }

    }


    

    class DataCombiner : DataProcessingModule
    {
        InputPin<byte> DataIn;
        OutputPin<byte> DataOut;

        int NumBitsOut;
        int NumBitsIn;

        byte[] DataArray;
        BitGroup[] BGArray;

        List<byte> OutputData;

        int CurrentFrame;
        int CurrentBitGroupIndex;
        int CurrentBGCounter;
        int CurrentInIndex;
        int CurrentInCounter;

        int MaxBits;
        int MaxBitGroupIndex;

        public DataCombiner(int outputBlockSize, BitGroup[] bitgroupArray)
        {
            NumBitsOut = outputBlockSize;
            MaxBitGroupIndex = bitgroupArray.Length;
            BGArray = new BitGroup[MaxBitGroupIndex];
            bitgroupArray.CopyTo(BGArray, 0);

            // Scan bitgroup array and find the extent of the 
            //  combining
            MaxBits = 0;
            NumBitsIn = 0;
            foreach (BitGroup bg in bitgroupArray)
            {
                NumBitsIn += bg.Size;
                if (bg.Position >= MaxBits)
                {
                    MaxBits = bg.Position;
                }
            }
            MaxBits += outputBlockSize;  // Reserve this space for block 0

            DataArray = new byte[MaxBits];
            OutputData = new List<byte>(NumBitsIn);
            Init();
        }

        public override void Init()
        {
            Array.Clear(DataArray, 0, MaxBits);
            OutputData.Clear();
            CurrentBitGroupIndex = 0;
            CurrentInCounter = 0;
            CurrentFrame = 0;
            BitGroup bg = BGArray[CurrentBitGroupIndex++];
            CurrentInIndex = CurrentFrame - bg.Position; if (CurrentInIndex < 0) CurrentInIndex += MaxBits;
            CurrentBGCounter = bg.Size;
        }

        public void Process(CNTRL_MSG controlParam, byte incomingBit)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                DataArray[CurrentInIndex++] += (byte)((incomingBit == 0) ? -1 : 1); if (CurrentInIndex >= MaxBits) CurrentInIndex = 0;
                CurrentBGCounter--;
                if (CurrentBGCounter <= 0)
                {
                    BitGroup bg = BGArray[CurrentBitGroupIndex++]; if (CurrentBitGroupIndex >= MaxBitGroupIndex) CurrentBitGroupIndex = 0;
                    CurrentInIndex = CurrentFrame - bg.Position; if (CurrentInIndex < 0) CurrentInIndex += MaxBits;
                    CurrentBGCounter = bg.Size;
                }
                CurrentInCounter++;
                if (CurrentInCounter >= NumBitsIn)
                {
                    CurrentInCounter = 0;
                    // Output data
                    for (int i = 0; i < NumBitsOut; i++)
                    {
                        DataOut.Process( (byte) ((DataArray[CurrentFrame++] > 0) ? 1 : 0)); if (CurrentFrame >= MaxBits) CurrentFrame = 0;
                    }
                    Array.Clear(DataArray, CurrentFrame, NumBitsOut);
                }
            }
        }

        public int Process( byte incomingBit)
        {
            DataArray[CurrentInIndex++] += (byte)((incomingBit == 0) ? -1 : 1); if (CurrentInIndex >= MaxBits) CurrentInIndex = 0;
            CurrentBGCounter--;
            if (CurrentBGCounter <= 0)
            {
                BitGroup bg = BGArray[CurrentBitGroupIndex++]; if (CurrentBitGroupIndex >= MaxBitGroupIndex) CurrentBitGroupIndex = 0;
                CurrentInIndex = CurrentFrame - bg.Position; if (CurrentInIndex < 0) CurrentInIndex += MaxBits;
                CurrentBGCounter = bg.Size;
            }
            CurrentInCounter++;
            if (CurrentInCounter >= NumBitsIn)
            {
                CurrentInCounter = 0;
                // Output data
                for (int i = 0; i < NumBitsOut; i++)
                {
                    OutputData.Add( (byte) ((DataArray[CurrentFrame++] > 0) ? 1 : 0)); if (CurrentFrame >= MaxBits) CurrentFrame = 0;
                }
                Array.Clear(DataArray, CurrentFrame, NumBitsOut);
            }
            return this.OutputData.Count;
        }

        static void Combine(ref byte originalByte, byte newByte)
        {
            originalByte += (byte)((newByte == 0) ? -1 : 1);
        }

        static void Combine(ref IQ originalData, IQ newData)
        {
            originalData = originalData + newData;
        }

        public int Count
        {
            get { return OutputData.Count; }
        }

        public bool IsDataReady { get { return this.Count > 0; } }


        public int GetData(byte[] outputArray, int startingIndex)
        {
            int ret = this.OutputData.Count;
            OutputData.CopyTo(outputArray, startingIndex);
            OutputData.Clear();
            return ret;
        }

        public override void SetModuleParameters()
        {
            DataIn = new InputPin<byte>("DataIn", this.Process);
            DataOut = new OutputPin<byte>("DataOut");
            base.SetIOParameters("DataCombiner", new DataPin[] { DataIn, DataOut });
        }
    }

 }
