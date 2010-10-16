using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    [Flags]
    public enum EncodingType
    {
        NON_DIFF = 0x00,

        DIFF = 0x01,
        DIFF_ADD = 0x01,
        DIFF_SUB = 0x03,
        DIFF_XOR = 0x05,
        DIFF_XNOR = 0x07,
        DIFF_MASK = 0x07,

        SCRAMBLE = 0x010,
        SCRAMBLE_ADD = 0x010,
        SCRAMBLE_SUB = 0x030,
        SCRAMBLE_XOR = 0x050,
        SCRAMBLE_XNOR = 0x070,
        SCRAMBLE_MASK = 0x070,

        SAVE_IN1 = 0x0100,
        SAVE_IN2 = 0x0300,
        SAVE_OUT1 = 0x0500,
        SAVE_OUT2 = 0x0700,
        SAVE_MASK = 0x0700,

        DIFF_IQ = 0x8000
    }

    class IQEncoder
    {
        int BitsPerSymbol;
        int MaxSymbols;
        int SymbolMask;

        int[] BitsToSymbol;
        IQ[] SymbolToIQ;

        EncodingType EncType;

        int PrevSymbol;
        IQ PrevIQ;

        public IQEncoder(int bitsPerSymb, int[] tableBitsToSymb, IQ[] tableSymbToIQ, EncodingType mType)
        {
            BitsPerSymbol = bitsPerSymb;
            MaxSymbols = 1 << bitsPerSymb;
            SymbolMask = MaxSymbols - 1;

            BitsToSymbol = new int[tableBitsToSymb.Length];
            tableBitsToSymb.CopyTo(BitsToSymbol, 0);

            SymbolToIQ = new IQ[tableSymbToIQ.Length];
            tableSymbToIQ.CopyTo(SymbolToIQ, 0);

            EncType = mType;
            Init();
        }

        public IQEncoder(int bitsPerSymb, int[] tableBitsToSymb,
                            float[] tableSymbToI, float[] tableSymbToQ, EncodingType mType)
        {
            BitsPerSymbol = bitsPerSymb;
            MaxSymbols = 1 << bitsPerSymb;
            SymbolMask = MaxSymbols - 1;

            BitsToSymbol = new int[tableBitsToSymb.Length];
            tableBitsToSymb.CopyTo(BitsToSymbol, 0);

            SymbolToIQ = new IQ[tableSymbToI.Length];
            for (int i = 0; i < tableSymbToI.Length; i++)
            {
                SymbolToIQ[i] = new IQ(tableSymbToI[i], tableSymbToQ[i]);
            }
            EncType = mType;
            Init();
        }

        public void Init()
        {
            PrevSymbol = 0;
            PrevIQ = IQ.UNITY;
        }

        int ProcessIn(int inBits)
        {
            int newSymbol = BitsToSymbol[inBits & SymbolMask];
            int OutSymbol = newSymbol;
            if ((EncType & EncodingType.DIFF) != 0)
            {
                switch (EncType & EncodingType.DIFF_MASK)
                {
                    case EncodingType.DIFF_ADD:
                        OutSymbol += PrevSymbol;
                        break;
                    case EncodingType.DIFF_SUB:
                        OutSymbol -= PrevSymbol;
                        break;
                    case EncodingType.DIFF_XOR:
                        OutSymbol ^= PrevSymbol;
                        break;
                    case EncodingType.DIFF_XNOR:
                        OutSymbol ^= ~PrevSymbol;
                        break;
                }
                switch (EncType & EncodingType.SAVE_MASK)
                {
                    case EncodingType.SAVE_IN1:
                        PrevSymbol = inBits;
                        break;
                    case EncodingType.SAVE_IN2:
                        PrevSymbol = newSymbol;
                        break;
                    case EncodingType.SAVE_OUT1:
                        PrevSymbol = OutSymbol;
                        break;
                    case EncodingType.SAVE_OUT2:
                        PrevSymbol = OutSymbol;
                        break;
                }
                OutSymbol &= SymbolMask;
            }
            return OutSymbol;
        }

        int ProcessScramble(int inBits, int scrambleSymbol, int scrambleMask)
        {
            int newSymbol = inBits;
            if ((EncType & EncodingType.SCRAMBLE) != 0)
            {
                switch (EncType & EncodingType.SCRAMBLE_MASK)
                {
                    case EncodingType.SCRAMBLE_ADD:
                        newSymbol += scrambleSymbol;
                        break;
                    case EncodingType.SCRAMBLE_SUB:
                        newSymbol -= scrambleSymbol;
                        break;
                    case EncodingType.SCRAMBLE_XOR:
                        newSymbol ^= scrambleSymbol;
                        break;
                    case EncodingType.SCRAMBLE_XNOR:
                        newSymbol ^= ~scrambleSymbol;
                        break;
                }
                if ((EncType & EncodingType.SAVE_MASK) == EncodingType.SAVE_OUT2) PrevSymbol = newSymbol;
                newSymbol &= scrambleMask;
            }
            return newSymbol;
        }

        public void Process(int inputBits, out IQ data)
        {
            int newSymbol = ProcessIn(inputBits);
            data = SymbolToIQ[newSymbol];
            if ( (EncType & EncodingType.DIFF_IQ) != 0)
            {
                data = PrevIQ * data;
                PrevIQ = data;
            }
        }

        public void Process(int inputBits, int scrambleSymbol, int scrambleMask, out IQ data)
        {
            int newSymbol = ProcessIn(inputBits);
            newSymbol = ProcessScramble(newSymbol, scrambleSymbol, scrambleMask);
            data = SymbolToIQ[newSymbol];
            if ((EncType & EncodingType.DIFF_IQ) != 0)
            {
                data = PrevIQ * data;
                PrevIQ = data;
            }
        }


        public int PreviousSymbol
        {
            get { return PrevSymbol; }
            set { PrevSymbol = value; }
        }

        public IQ PreviousIQ
        {
            get { return PrevIQ; }
            set { PrevIQ = value; }
        }

    }

    class IQDecoder 
    {
        int BitsPerSymbol;
        int[] BitsToSymbol;
        IQ[] SymbolToIQ;
        int MaxSymbols;
        int SymbolMask;
        EncodingType EncType;

        int PrevSymbol;
        IQ PrevIQ;
        IQ TargetSymbol;

        IQ Rotate = IQ.UNITY;
        IQ FreqCorr = IQ.UNITY;
        IQ PrevRotate = IQ.UNITY;

        float Energy = 0;

        int SymbolsToProcess;
        int CurrentSymbolCnt;

        float Coeff1;       // (1 - 1/N);
        float Coeff2;       // 1/N;
        
        public IQDecoder(int bitsPerSymb, int[] tableBitsToSymb, IQ[] tableSymbToIQ, EncodingType mType)
        {
            BitsPerSymbol = bitsPerSymb;
            MaxSymbols = 1 << bitsPerSymb;
            SymbolMask = MaxSymbols - 1;

            BitsToSymbol = new int[tableBitsToSymb.Length];
            tableBitsToSymb.CopyTo(BitsToSymbol, 0);
            
            SymbolToIQ = new IQ[tableSymbToIQ.Length];
            tableSymbToIQ.CopyTo(SymbolToIQ, 0);

            EncType = mType;
            Init();
        }

        public IQDecoder(int bitsPerSymb, int[] tableBitsToSymb, float[] tableSymbToI, float[] tableSymbToQ, EncodingType mType)
        {
            BitsPerSymbol = bitsPerSymb;
            MaxSymbols = 1 << bitsPerSymb;
            SymbolMask = MaxSymbols - 1;

            BitsToSymbol = new int[tableBitsToSymb.Length];
            tableBitsToSymb.CopyTo(BitsToSymbol, 0);

            SymbolToIQ = new IQ[tableSymbToI.Length];
            for (int i = 0; i < tableSymbToI.Length; i++)
            {
                SymbolToIQ[i] = new IQ(tableSymbToI[i], tableSymbToQ[i]);
            }
            EncType = mType;
            Init();
        }

        public void Init()
        {
            PrevSymbol = 0;
            PrevIQ = IQ.UNITY;
            SymbolsToProcess = CurrentSymbolCnt = 0;

            Rotate = IQ.UNITY;
            FreqCorr = IQ.UNITY;
            PrevRotate = IQ.UNITY;
            Energy = 0;
        }

        public void StartCorrectionProcess(int numberOfSymbols)
        {
            SymbolsToProcess = numberOfSymbols;
            CurrentSymbolCnt = 0;

            Rotate = IQ.UNITY;
            FreqCorr = IQ.UNITY;
            PrevRotate = IQ.UNITY;
            Energy = 0;

            if (SymbolsToProcess < 0)    // This is a continious mode
            {
                Coeff2 = -1.0f / (float)SymbolsToProcess;
                Coeff1 = 1 - Coeff2;
            }
        }
        
        public bool IsCorrectionReady
        {
            get { return (CurrentSymbolCnt >= SymbolsToProcess); }
        }

        int ProcessIn(int inBits)
        {
            int newSymbol = BitsToSymbol[inBits];
            int OutSymbol = newSymbol;
            if ((EncType & EncodingType.DIFF) != 0)
            {
                switch (EncType & EncodingType.DIFF_MASK)
                {
                    case EncodingType.DIFF_ADD:
                        OutSymbol += PrevSymbol;
                        break;
                    case EncodingType.DIFF_SUB:
                        OutSymbol -= PrevSymbol;
                        break;
                    case EncodingType.DIFF_XOR:
                        OutSymbol ^= PrevSymbol;
                        break;
                    case EncodingType.DIFF_XNOR:
                        OutSymbol ^= ~PrevSymbol;
                        break;
                }
                OutSymbol &= SymbolMask;
            }
            return OutSymbol;
        }

        int ProcessScramble(int inBits, int scrambleSymbol, int scrambleMask)
        {
            int newSymbol = inBits;
            if ((EncType & EncodingType.SCRAMBLE) != 0)
            {
                switch (EncType & EncodingType.SCRAMBLE_MASK)
                {
                    case EncodingType.SCRAMBLE_ADD:
                        newSymbol += scrambleSymbol;
                        break;
                    case EncodingType.SCRAMBLE_SUB:
                        newSymbol -= scrambleSymbol;
                        break;
                    case EncodingType.SCRAMBLE_XOR:
                        newSymbol ^= scrambleSymbol;
                        break;
                    case EncodingType.SCRAMBLE_XNOR:
                        newSymbol ^= ~scrambleSymbol;
                        break;
                }
                newSymbol &= scrambleMask;
            }
            return newSymbol;
        }

        int ProcessDiff( int inBits)
        {
            int newSymbol = BitsToSymbol[inBits];
            int OutSymbol = newSymbol;
            if ((EncType & EncodingType.DIFF) != 0)
            {
                switch (EncType & EncodingType.DIFF_MASK)
                {
                    case EncodingType.DIFF_ADD:
                        OutSymbol += PrevSymbol;
                        break;
                    case EncodingType.DIFF_SUB:
                        OutSymbol -= PrevSymbol;
                        break;
                    case EncodingType.DIFF_XOR:
                        OutSymbol ^= PrevSymbol;
                        break;
                    case EncodingType.DIFF_XNOR:
                        OutSymbol ^= ~PrevSymbol;
                        break;
                }
                OutSymbol &= SymbolMask;

                switch (EncType & EncodingType.SAVE_MASK)
                {
                    case EncodingType.SAVE_IN1:
                        PrevSymbol = inBits;
                        break;
                    case EncodingType.SAVE_IN2:
                        PrevSymbol = newSymbol;
                        break;
                    case EncodingType.SAVE_OUT1:
                        PrevSymbol = OutSymbol;
                        break;
                    case EncodingType.SAVE_OUT2:
                        PrevSymbol = OutSymbol;
                        break;
                }
            }
            return OutSymbol;
        }

        int ProcessDiff(int inBits, int scrambleSymbol, int scrambleMask)
        {

            int newSymbol = ProcessDiff(inBits);
            if ((EncType & EncodingType.SCRAMBLE) != 0)
            {
                switch (EncType & EncodingType.SCRAMBLE_MASK)
                {
                    case EncodingType.SCRAMBLE_ADD:
                        newSymbol += scrambleSymbol;
                        break;
                    case EncodingType.SCRAMBLE_SUB:
                        newSymbol -= scrambleSymbol;
                        break;
                    case EncodingType.SCRAMBLE_XOR:
                        newSymbol ^= scrambleSymbol;
                        break;
                    case EncodingType.SCRAMBLE_XNOR:
                        newSymbol ^= ~scrambleSymbol;
                        break;
                }
                if ((EncType & EncodingType.SAVE_MASK) == EncodingType.SAVE_OUT2) PrevSymbol = newSymbol;
                newSymbol &= scrambleMask;
            }
            return newSymbol;
        }

        void CalculateCorrections(IQ inputData)
        {
            IQ AngleError = (TargetSymbol / inputData);
            if (SymbolsToProcess < 0)    // Continious mode
            {
                Energy = Energy * Coeff1 + inputData.R2 * Coeff2;
                Rotate = Rotate * Coeff1 + AngleError * Coeff2;
                FreqCorr = FreqCorr * Coeff1 + (AngleError / PrevRotate) * Coeff2;
            }else if (CurrentSymbolCnt < SymbolsToProcess)
            {
                Energy += inputData.R2;
                Rotate += AngleError;
                FreqCorr += AngleError / PrevRotate;
                CurrentSymbolCnt++;
            }
            PrevRotate = AngleError;
        }

        public void Process(IQ inputData, out int outputSymbol)
        {
            float MinDistance = float.MaxValue;
            int resultSymbol = 0;
            IQ Data;

            if ((EncType & EncodingType.DIFF_IQ) != 0)
            {
                Data = inputData / PrevIQ;
                PrevIQ = inputData;
            }
            else
            {
                Data = inputData;
            }
            // Simply go thru every point in constellation and find the best match
            for (int currSymbol = 0; currSymbol < MaxSymbols; currSymbol++)
            {
                int newSymbol = ProcessIn(currSymbol);
                IQ targetData = SymbolToIQ[newSymbol];
                float Distance = (targetData - Data).R2;
                if (Distance < MinDistance)
                {
                    MinDistance = Distance;
                    resultSymbol = currSymbol;
                    TargetSymbol = targetData;
                }
            }
            ProcessDiff(resultSymbol);
            CalculateCorrections(Data);
            outputSymbol = resultSymbol;
        }

        public void Process(IQ inputData, int scrambleSymbol, int scrambleMask, out int outputSymbol)
        {
            float MinDistance = float.MaxValue;
            int resultSymbol = 0;
            IQ Data;

            if ((EncType & EncodingType.DIFF_IQ) != 0)
            {
                Data = inputData / PrevIQ;
                PrevIQ = inputData;
            }
            else
            {
                Data = inputData;
            }
            // Simply go thru every point in constellation and find the best match
            for (int currSymbol = 0; currSymbol < MaxSymbols; currSymbol++)
            {
                int newSymbol = ProcessIn(currSymbol);
                newSymbol = ProcessScramble(newSymbol, scrambleSymbol, scrambleMask);
                IQ TargetData = SymbolToIQ[newSymbol];
                float Distance = (TargetData - Data).R2;
                if (Distance < MinDistance)
                {
                    MinDistance = Distance;
                    resultSymbol = currSymbol;
                    TargetSymbol = TargetData;
                }
            }
            ProcessDiff(resultSymbol, scrambleSymbol, scrambleMask);
            CalculateCorrections(Data);
            outputSymbol = resultSymbol;
        }

        /// <summary>
        /// Correction (Rotation) factor for the IQ data.
        /// </summary>
        public IQ RotateCorrection
        {
            get
            {
                return Energy == 0 ? IQ.UNITY : this.Rotate / Energy;
            }
        }

        /// <summary>
        /// Correction (Frequency) factor for the IQ data.
        /// </summary>
        public IQ FrequencyCorrection
        {
            get
            {
                return this.FreqCorr;
            }
        }

        public IQ Target
        {
            get { return TargetSymbol; }
        }

        public int PreviousSymbol
        {
            get { return PrevSymbol; }
            set { PrevSymbol = value; }
        }

        public IQ PreviousIQ
        {
            get { return PrevIQ; }
            set { PrevIQ = value; }
        }

    }

}
