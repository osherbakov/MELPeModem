using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    abstract class Interleaver :  DataProcessingModule
    {
        InputPin<byte> EncodeIn;
        OutputPin<byte> EncodeOut;

        InputPin<byte> DecodeIn;
        OutputPin<byte> DecodeOut;


        protected int SizeX, SizeY;

        int PutX, PutY;
        int GetX, GetY;
        protected int ColumnPutIncrement;
        protected int ColumnGetIncrement;
        int TotalLength;

        protected byte[,] Storage;
        Queue<byte> OutputData;
        protected int PutCounter = 0;
        protected int GetCounter = 0;
        protected Interleaver(int SizeX, int SizeY)
        {
            this.SizeX = SizeX;
            this.SizeY = SizeY;
            Storage = new byte[SizeX, SizeY];
            TotalLength = SizeX * SizeY;
            OutputData = new Queue<byte>(TotalLength);
            Clear();
        }
        protected Interleaver(int SizeX, int SizeY, int PutInc, int GetInc)
            : this(SizeX, SizeY)
        {
            this.ColumnPutIncrement = PutInc;
            this.ColumnGetIncrement = GetInc;
        }

        void Clear()
        {
            Array.Clear(Storage, 0, TotalLength);
            PutX = PutY = 0;
            GetX = GetY = 0;

            PutCounter = SizeX * SizeY;
            GetCounter = SizeX * SizeY;
        }

        public override void Init()
        {
            Clear();
        }

        protected virtual void NextPutPos(ref int Column, ref int Row)
        {
            Column += ColumnPutIncrement;
            if (Column >= SizeX)
            {
                Column -= SizeX;
            }
            else if (Column < 0)
            {
                Column += SizeX;
            }
        }

        protected virtual void NextGetPos(ref int Column, ref int Row)
        {
            Column += ColumnGetIncrement;
            if (Column >= SizeX)
            {
                Column -= SizeX;
            }
            else if (Column < 0)
            {
                Column += SizeX;
            }
        }

        byte this[int X, int Y]
        {
            get { return Storage[X, Y]; }
            set { Storage[X, Y] =  value; }
        }

        protected virtual void ProcessEncode()
        {
        }

        protected virtual void ProcessDecode()
        {
        }

        public void ProcessEncode(CNTRL_MSG controlParam,  byte dataByte)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                Storage[PutX, PutY] = dataByte;
                NextPutPos(ref PutX, ref PutY);
                PutCounter--;
                if (PutCounter == 0)
                {
                    ProcessEncode();
                    EncodeOut.Process(CNTRL_MSG.INTERLEAVER_FRAME);
                    while (GetCounter > 0)
                    {
                        EncodeOut.Process(Storage[GetX, GetY]);
                        NextGetPos(ref GetX, ref GetY);
                        GetCounter--;
                    }
                    this.Init();
                }
            }
        }

        public void ProcessDecode(CNTRL_MSG controlParam, byte dataByte)
        {
            if (controlParam == CNTRL_MSG.DATA_IN)
            {
                Storage[GetX, GetY] = dataByte;
                NextGetPos(ref GetX, ref GetY);
                GetCounter--;
                if (GetCounter == 0)
                {
                    ProcessDecode();
                    DecodeOut.Process(CNTRL_MSG.INTERLEAVER_FRAME);
                    while (PutCounter > 0)
                    {
                        DecodeOut.Process(Storage[PutX, PutY]);
                        NextPutPos(ref PutX, ref PutY);
                        PutCounter--;
                    }
                    this.Init();
                }
            }
        }

        public void ProcessEncode(byte dataByte)
        {
            Storage[PutX, PutY] = dataByte;
            NextPutPos(ref PutX, ref PutY);
            PutCounter--;
            if (PutCounter == 0)
            {
                ProcessEncode();
                while (GetCounter > 0)
                {
                    OutputData.Enqueue(Storage[GetX, GetY]);
                    NextGetPos(ref GetX, ref GetY);
                    GetCounter--;
                }
                this.Init();
            }
        }

        public void ProcessDecode(byte dataByte)
        {
            Storage[GetX, GetY] = dataByte;
            NextGetPos(ref GetX, ref GetY);
            GetCounter--;
            if (GetCounter == 0)
            {
                ProcessDecode();
                while (PutCounter > 0)
                {
                    OutputData.Enqueue(Storage[PutX, PutY]);
                    NextPutPos(ref PutX, ref PutY);
                    PutCounter--;
                }
                this.Init();
            }
        }
       
        public int EncodeBitsAvailable
        {
            get { return PutCounter; }
        }

        public int DecodeBitsAvailable
        {
            get { return GetCounter; }
        }

        public bool IsDataReady { get { return (OutputData.Count > 0);} }

        public int Length { get { return this.TotalLength; } }

        public int Count { get { return OutputData.Count; } }

        public byte GetData()
        {
            return OutputData.Dequeue();
        }

        public int GetData(byte[] outputArray)
        {
            int ret = this.OutputData.Count;
            OutputData.CopyTo(outputArray, 0);
            OutputData.Clear();
            return ret;
        }

        public int GetData(byte[] outputArray, int startingIndex)
        {
            int ret = this.OutputData.Count;
            OutputData.CopyTo(outputArray, startingIndex);
            OutputData.Clear();
            return ret;
        }

        public override void SetModuleParameters()
        {
            EncodeIn = new InputPin<byte>("EncIn", this.ProcessEncode);
            DecodeIn = new InputPin<byte>("DecIn", this.ProcessDecode);
            EncodeOut = new OutputPin<byte>("EncOut");
            DecodeOut = new OutputPin<byte>("DecOut");
            base.SetIOParameters("Interleaver EncoderDecoder", new DataPin[] { EncodeIn, DecodeIn, EncodeOut, DecodeOut });
        }
    }

    class Interleaver_188_110A : Interleaver
    {
        int PreviousColumn = 0;
        int RowPutIncrement = 9;
        int ColumnGetDecrement = 17;

        public Interleaver_188_110A(int XSize, int YSize)
            : base(XSize, YSize)
        {
            if (YSize < 40) // Special values for 75 bps
            {
                RowPutIncrement = 7;
                ColumnGetDecrement = 7;
            }
        }

        public override void Init()
        {
            PreviousColumn = 0;
            base.Init();
        }

        //Unknown data bits shall be loaded into the interleaver matrix starting at column zero as follows:
        //the first bit is loaded into row 0, the next bit is loaded into row 9, the third bit is loaded into row
        //18, and the fourth bit into row 27. Thus, the row location for the bits increases by 9 modulo 40.
        //This process continues until all 40 rows are loaded. The load then advances to column 1 and the
        //process is repeated until the matrix block is filled.
        protected override void NextPutPos(ref int Column, ref int Row)
        {
            Row += RowPutIncrement;
            if (Row >= SizeY)
                Row -= SizeY;
            if (Row == 0) Column += 1;
        }

        //The fetching sequence for all rates shall start with the first bit being taken from row zero, column
        //zero. The location of each successive fetched bit shall be determined by incrementing the row by
        //one and decrementing the column number by 17 (modulo number of columns in the interleaver
        //matrix). Thus, for 2400 bps with a long interleave setting, the second bit comes from row 1,
        //column 559, and the third bit from row 2, column 542. This interleaver fetch shall continue until
        //the row number reaches the maximum value. At this point, the row number shall be reset to zero,
        //the column number is reset to be one larger than the value it had when the row number was last
        //zero and the process continued until the entire matrix data block is unloaded.
        protected override void NextGetPos(ref int Column, ref int Row)
        {
            Row += 1; Column -= ColumnGetDecrement;
            if (Column < 0)
                Column += SizeX;
            if (Row >= SizeY)
            {
                Row = 0;
                Column = PreviousColumn + 1;
                PreviousColumn = Column;
            }
        }
    }

    class Interleaver_188_110A_4800 : Interleaver
    {
        public Interleaver_188_110A_4800()
            : base(1440, 1)
        {
            base.ColumnGetIncrement = 1;
            base.ColumnPutIncrement = 1;
        }
    }

    class Interleaver_188_110B_39 : Interleaver
    {
        int RSSymbolSize = 0;
        int Interleave = 0;
        int DataRows = 0; // How many Data symbols are needed to generate Parity symbols/rows 
        int ParityRows = 0; // How many RS parity symbols/rows will be added
        int PutDataX;
        int[] WorkArray;
        int[] ParityArray;
        int[] ErasuresArray;

        ReedSolomon RS;

        public Interleaver_188_110B_39(int InterleaveDegree, int SuperblocksNumber,
                int RSSymbolSize, int RSSymbolNumber, int RSDataNumber)
            : base(RSSymbolSize * InterleaveDegree * SuperblocksNumber, RSSymbolNumber)
        {
            this.RSSymbolSize = RSSymbolSize;
            Interleave = InterleaveDegree * RSSymbolSize;
            PutDataX = Math.Min(Interleave, 8);
            DataRows = RSDataNumber;
            ParityRows = RSSymbolNumber - DataRows;
            WorkArray = new int[DataRows + ParityRows];
            ParityArray = new int[ParityRows];
            ErasuresArray = new int[DataRows + ParityRows];
            RS = new ReedSolomon();
            RS.Init(RSSymbolSize, 0x13, 1, 1,
                ParityRows /* how many parity symbols */,
                    ((1 << RSSymbolSize) - 1) - (DataRows + ParityRows)  /* how many pads */ );
            Init();
        }

        public override void Init()
        {
            base.Init();
            base.PutCounter = SizeX * DataRows;     // Place 3 data symbols in case of RS(7, 3) or 
                                                    //   10 data symbols in case of RS(14, 10)
        }

        protected override void ProcessEncode()
        {
            int Col = 0;
            int Data = 0;
            while (Col < SizeX)
            {
                for (int row = 0; row < DataRows; row++)
                {
                    Data = 0;
                    for (int i = 0; i < RSSymbolSize; i++)
                        Data |= (Storage[Col + i, row] & 0x0001) << i;
                    WorkArray[row] = Data;
                }

                RS.Encode(WorkArray, ParityArray);
                for (int row = DataRows; row < SizeY; row++)
                {
                    Data = ParityArray[row - DataRows];
                    for (int i = 0; i < RSSymbolSize; i++)
                    {
                        Storage[Col + i, row] = (byte)(Data & 0x0001);
                        Data >>= 1;
                    }

                }
                Col += RSSymbolSize;
            }
        }

        protected override void ProcessDecode()
        {
            int Col = 0;
            int Data = 0;
            while (Col < SizeX)
            {
                for (int row = 0; row < SizeY; row++)
                {
                    Data = 0;
                    for (int i = 0; i < RSSymbolSize; i++)
                        Data |= (Storage[Col + i, row] & 0x0001) << i;
                    WorkArray[row] = Data;
                }

                RS.Decode(WorkArray, ErasuresArray, 0);  // Currently we do not support erasures - should add later
                for (int row = 0; row < DataRows; row++)
                {
                    Data = WorkArray[row];
                    for (int i = 0; i < RSSymbolSize; i++)
                    {
                        Storage[Col + i, row] = (byte)(Data & 0x0001);
                        Data >>= 1;
                    }
                }
                Col += RSSymbolSize;
            }
        }

        protected override void NextGetPos(ref int Column, ref int Row)
        {
            Column += 1;
            if ((Column % Interleave) == 0)
            {
                Column -= Interleave;
                Row += 1;
            }
            if (Row >= SizeY)
            {
                Row = 0;
                Column += Interleave;
            }
        }

        protected override void NextPutPos(ref int Column, ref int Row)
        {
            Column += 1;
            if ((Column % PutDataX) == 0)
            {
                Column -= PutDataX;
                Row += 1;
            }
            else if (Column >= SizeX)
            {
                Column -= RSSymbolSize;
                Row += 1;
            }
            if (Row >= DataRows)
            {
                Row = 0;
                Column += PutDataX;
            }
        }
    }

    class Interleaver_188_110B : Interleaver
    {
        public Interleaver_188_110B(int Size, int Increment)
            : base(Size, 1, Increment, 1)
        {
        }
    }
}
