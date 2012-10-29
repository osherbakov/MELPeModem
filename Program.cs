using System;
using System.Collections.Generic;
using System.Text;


namespace MELPeModem
{
    class Program
    {
        static float PROCESSINGFREQ = 24000;
        static float OUTSAMPLEFREQ = 8000;
        static float INSAMPLEFREQ = 8000;
        static float SYMBOLRATE = 2400;

        static int InputBlockSize = (int)(PROCESSINGFREQ / SYMBOLRATE);
        static int OutputBlockSize = (int)(PROCESSINGFREQ / OUTSAMPLEFREQ);

        static string testmsg = "**** Hello World from Sollecon, Inc.!!!!  **** ------ ";

        static void Main(string[] args)
        {
            Test t = new Test();
            t.Run();

            // Symbol encoder and modulator
            MILSTD188_110B mmodem = new MILSTD188_110B(MILSTD_188.Mode.D_4800N , INSAMPLEFREQ, PROCESSINGFREQ, OUTSAMPLEFREQ,
                            Filters.Fill(Filters.rrc_180, 1), Filters.Fill(Filters.interp_24000, 1), Filters.Fill(Filters.decim_24000, 1));

            StringBuilder test = new StringBuilder();
            for (int i = 0; i < 377; i++)
            {
                test.Append(testmsg);
            }

            // Pack all data as 8-bit entities
            SerialData TXSymbols = new SerialData(7, 1, 2, SerialData.Parity.N);
            foreach (char CharToEncode in test.ToString())
            {
                int ByteToEncode = ((int)Convert.ToByte(CharToEncode)) & 0x00FF;
                TXSymbols.PutSymbol(ByteToEncode);
            }
            byte[] TxData = new byte[TXSymbols.BitsCount];
            int TxBits = TXSymbols.GetData(TxData);


            string outputFile = @"C:\test.raw";
            string inputFile = @"C:\test.raw";

            // Send datastream thru the modem - the output of the modem will be arrray of int datasymbols
            Samples outputSamples = new Samples(outputFile, 0.1f);
            mmodem.Tx.Start();
            mmodem.Tx.Process(TxData, 0, TxBits);
            mmodem.Tx.Finish();

            float[] SamplesOut = new float[mmodem.Tx.Count];
            mmodem.Tx.GetData(SamplesOut, 0);
            outputSamples.ToByte(SamplesOut);
            outputSamples.Close();


            Samples InputSamples = new Samples(inputFile);
            float[] SamplesIn = new float[100];
            int n;
            do
            {
                n = InputSamples.ToFloat(SamplesIn);
                mmodem.Rx.Process(SamplesIn, 0, n);
            } while (n > 0);

            byte[] RxData = new byte[mmodem.Rx.Count];
            int RxBits = mmodem.Rx.GetData(RxData);

            SerialData RxSymbols = new SerialData(7, 1, 2, SerialData.Parity.N);
            RxSymbols.PutData(RxData, 0, RxBits);

            if (inputFile.CompareTo(outputFile) == 0)
            {
                int errors = 0;

                if (RxBits != TxBits)
                {
                    errors = 9999;
                }

                for (int idx = 0; idx < TxData.Length; idx++)
                {
                    byte sent = TxData[idx];
                    byte recd = RxData[idx];
                    if (sent != recd)
                    {
                        errors++;
                    }
                }
                
                for(int idx = 0; idx < TXSymbols.SymbolsCount; idx++)
                {
                    int sent = TXSymbols[idx];
                    int recd = RxSymbols[idx];
                    if (sent != recd)
                    {
                        errors++;
                    }
                }
            }

            InputSamples.Close();

        }
    }
}
