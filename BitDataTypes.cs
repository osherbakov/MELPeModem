using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
  
    /// <summary>
    /// The class that provides some useful functions to pack and unpack symbols into bit arrays
    /// </summary>
    class BitArray
    {
        int SymbolSize;
        int SymbolMask;
        List<byte> BitStorage;

        /// <summary>
        /// Constructor for the Non-Packed Data array.
        /// </summary>
        /// <param name="bitsPerSymbol">Specifies how many bits will be taken from the symbol.</param>
        public BitArray(int bitsPerSymbol)
        {
            this.SymbolSize = bitsPerSymbol;
            this.SymbolMask = (1 << bitsPerSymbol) - 1;
            this.BitStorage = new List<byte>();
            Init();
        }

        public void Init()
        {
            BitStorage.Clear();
        }

        public void Clear()
        {
            Init();
        }

        public void Add(int symbol) 
        { 
            PutSymbol(symbol); 
        }

        public void Add(int symbol, int numBits)
        {
            PutSymbol(symbol, numBits);
        }

        public void Add(int[] symbolArray)
        {
            PutSymbol(symbolArray);
        }

        public void Add(int[] symbolArray, int startingIndex, int symbolCount)
        {
           PutSymbol(symbolArray, startingIndex, symbolCount);
        }

        public void Add(byte []addBits) 
        {
            int NumBits = (addBits.Length / this.SymbolSize) * this.SymbolSize;
            for(int i = 0; i < NumBits; i++)
                BitStorage.Add(addBits[i]);
        }

        public void Add(byte[] addBits, int startingIndex, int numBits)
        {
            int NumBits = (numBits / this.SymbolSize) * this.SymbolSize;
            for (int i = 0; i < NumBits; i++)
                BitStorage.Add(addBits[startingIndex++]);
        }

        public void PutSymbol(int symbol)
        {
            symbol &= SymbolMask;

            for (int i = 0; i < SymbolSize; i++)
            {
                BitStorage.Add((byte)(symbol & 0x0001));
                symbol >>= 1;
            }
        }

        public void PutSymbol(int symbol, int numBits)
        {
            numBits = (numBits / SymbolSize) * SymbolSize;

            int Mask = (int)((1L << numBits) - 1);
            symbol &= Mask;
            for (int i = 0; i < numBits; i++)
            {
                BitStorage.Add((byte)(symbol & 0x0001));
                symbol >>= 1;
            }
        }

        public void PutSymbol(int[] symbolArray)
        {
            foreach (int symbol in symbolArray)
            {
                PutSymbol(symbol);
            }
        }

        public void PutSymbol(int[] symbolArray, int startingIndex, int symbolCount)
        {
            for (int i = 0; i < symbolCount; i++)
            {
                PutSymbol(symbolArray[startingIndex++]);
            }
        }

        public int GetSymbol(int symbolIndex)
        {
            int Symb = 0;
            int GetByteIdx = symbolIndex * SymbolSize;
            for(int i = 0; i < SymbolSize; i++)
            {
                Symb |= (BitStorage[GetByteIdx++] & 0x0001) << i;
            }
            return Symb & SymbolMask;
        }

        public int this[int symbolIndex]
        {
            get { return GetSymbol(symbolIndex); }
            set
            {
                int symbol = value & SymbolMask;
                int SetByteIdx = symbolIndex * SymbolSize;      // Position of the starting bit
                for (int i = 0; i < SymbolSize; i++)
                {
                    BitStorage[SetByteIdx++] = (byte)(symbol & 0x0001);
                    symbol >>= 1;
                }
            }
        }

        public int SymbolsCount
        {
            get { return BitStorage.Count / this.SymbolSize; }
        }

        public int BitsCount
        {
            get { return BitStorage.Count; }
        }

        public int GetData(byte[] outputArray)
        {
            int ret = BitStorage.Count;
            BitStorage.CopyTo(outputArray);
            BitStorage.Clear();
            return ret;
        }

        public int GetData(byte[] outputArray, int startingIndex)
        {
            int ret = BitStorage.Count;
            BitStorage.CopyTo(outputArray, startingIndex);
            BitStorage.Clear();
            return ret;
        }

        public int GetData(int[] outputSymbols)
        {
            int ret = SymbolsCount;
            for (int i = 0; i < SymbolsCount; i++)
            {
                outputSymbols[i] = GetSymbol(i);
            }
            BitStorage.Clear();
            return ret;
        }

        public int GetData(int[] outputSymbols, int startingIndex)
        {
            int ret = SymbolsCount;
            for (int i = 0; i < SymbolsCount; i++)
            {
                outputSymbols[startingIndex + i] = GetSymbol(i);
            }
            BitStorage.Clear();
            return ret;
        }
    }


    class SerialData
    {
        int StartBits;
        int DataBits;
        int StopBits;
        Parity ParityFlag;

        int SymbolSize;
        int SymbolMask;
        int ParityMask;
        int StopMask;
        State CurrentState;
        int CurrentData;
        int CurrentCount;
        List<byte> BitStorage;
        List<int> SymbolStorage;

        static int[] bitcounts =
	        { 0, 1, 1, 2, 1, 2, 2, 3, 1, 2, 2, 3, 2, 3, 3, 4 };

        int Bitcount(int u)
        {
            int n = 0;
            for (; u != 0; u >>= 4)
                n += bitcounts[u & 0x0f];
            return n;
        }

        public enum Parity
        {
            N = 0,
            E = 1,
            O = 2
        }

        public SerialData(int dataBits, int startBits, int stopBits, Parity parityBits)
        {
            StartBits = startBits;
            StopBits = stopBits;
            DataBits = dataBits;
            ParityFlag = parityBits;

            ParityMask = 1 << DataBits;
            SymbolMask = ParityMask - 1;
            DataBits += (ParityFlag == Parity.N) ? 0 : 1;
            StopMask = ((1 << StopBits) - 1) << DataBits;
            SymbolSize = (StartBits + DataBits + StopBits);
            BitStorage = new List<byte>();
            SymbolStorage = new List<int>();
            Init();
        }

        public void Init()
        {
            BitStorage.Clear();
            SymbolStorage.Clear();
            CurrentState = State.START_SEARCH;
            CurrentCount = 0;
            CurrentData = 0;
        }

        public void Clear()
        {
            Init();
        }

        public void PutSymbol(int symbol)
        {
            symbol &= SymbolMask;
            SymbolStorage.Add(symbol);

            // Add parity (if necessary), stop and start bits
            if (ParityFlag != Parity.N)
            {
                int n = Bitcount(symbol) & 0x01;
                if (((n == 0) && (ParityFlag == Parity.O)) ||
                    ((n != 0) && (ParityFlag == Parity.E)))
                {
                    symbol |= ParityMask;
                }
            }
            symbol |= StopMask;
            symbol <<= StartBits;

            for (int i = 0; i < SymbolSize; i++)
            {
                BitStorage.Add( (byte) (symbol & 0x0001));
                symbol >>= 1;
            }
        }

        public void PutSymbol(int[] symbolArray)
        {
            foreach (int symbol in symbolArray)
            {
                PutSymbol(symbol);
            }
        }

        public void PutSymbol(int[] symbolArray, int startIndex, int numSymbols)
        {
            for (int i = 0; i < numSymbols; i++)
            {
                PutSymbol(symbolArray[startIndex + i]);
            }
        }

        public int this[int symbolIndex]
        {
            get { return SymbolStorage[symbolIndex]; }
        }

        enum State
        {
            START_SEARCH,
            START,
            DATA,
            STOP
        }

        public int PutData(byte[] bitArray, int startingIndex, int numBits)
        {

            for (int i = 0; i < numBits; i++)
                BitStorage.Add(bitArray[startingIndex + i]);

            int CurrentBit;
            int BitIndex = startingIndex;
            while (BitIndex < (startingIndex + numBits))
            {
                CurrentBit = bitArray[BitIndex] & 0x01;
                switch (CurrentState)
                {
                    case State.START_SEARCH:
                        if (CurrentBit == 0x00)
                        {
                            CurrentCount = 0;
                            CurrentState = State.START;
                        }
                        else
                            BitIndex++;
                        break;
                    case State.START:
                        if (CurrentCount++ >= StartBits)
                        {
                            CurrentCount = 0;
                            CurrentState = State.DATA;
                        }
                        else if (CurrentBit != 0x00)
                        {
                            CurrentState = State.START_SEARCH;
                        }
                        else
                            BitIndex++;
                        break;
                    case State.DATA:
                        if (CurrentCount >= DataBits)
                        {
                            CurrentData &= SymbolMask;
                            CurrentState = State.STOP;
                        }
                        else
                        {
                            CurrentData |= (CurrentBit << CurrentCount);
                            CurrentCount++;
                            BitIndex++;
                        }
                        break;
                    case State.STOP:
                        if (CurrentBit == 0x00)
                        {
                            CurrentCount = 0;
                            CurrentState = State.START;
                        }
                        else
                        {
                            SymbolStorage.Add(CurrentData);
                            CurrentState = State.START_SEARCH;
                            BitIndex++;
                        }
                        CurrentData = 0;
                        break;
                }
            }
            return BitStorage.Count;
        }

        public int SymbolsCount
        {
            get { return SymbolStorage.Count; }
        }

        public int BitsCount
        {
            get { return BitStorage.Count; }
        }

        public int GetData(byte[] outputArray)
        {
            int ret = BitStorage.Count;
            BitStorage.CopyTo(outputArray);
            Init();
            return ret; ;
        }

        public int GetData(byte[] outputArray, int startingIndex)
        {
            int ret = BitStorage.Count;
            BitStorage.CopyTo(outputArray, startingIndex);
            Init();
            return ret; ;
        }

        public int GetData(int[] symbolArray)
        {
            int ret = SymbolStorage.Count;
            SymbolStorage.CopyTo(symbolArray);
            Init();
            return ret;
        }

        public int GetData(int[] symbolArray, int startingIndex)
        {
            int ret = SymbolStorage.Count;
            SymbolStorage.CopyTo(symbolArray, startingIndex);
            Init();
            return ret;
        }
    }
}
