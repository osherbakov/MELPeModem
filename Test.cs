using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    public class Test
    {
        public Test()
        {

        }

        public float[] incomingData = new float[3000];
        public int[] datastream = { 
//      0, 0, 4, 4, 0, 0, 4, 4,
//      0, 0, 4, 4, 0, 0, 4, 4,
//      0, 0, 4, 4, 0, 0, 4, 4,
//      0, 0, 4, 4, 0, 0, 4, 4,
//      0, 0, 4, 4, 0, 0, 4, 4,
        0, 4, 0, 4, 0, 4, 0, 4,
        0, 4, 0, 4, 0, 4, 0, 4,
//        0, 4, 0, 4, 0, 4, 0, 4,
//        0, 4, 0, 4, 0, 4, 0, 4,
//        0, 4, 0, 4, 0, 4, 0, 4,
        1, 2, 3, 4, 5, 6, 7, 0, 
        1, 2, 3, 4, 5, 6, 7, 0, 
        1, 2, 3, 4, 5, 6, 7, 0, 
//        1, 2, 3, 4, 5, 6, 7, 0, 
//        1, 2, 3, 4, 5, 6, 7, 0, 
            // +0
           7, 4, 3, 0, 5, 1, 5, 0,  
           2, 2, 1, 1, 5, 7, 4 ,3,
           5, 0, 2, 6, 2, 1, 6, 2,
           0, 0, 5, 0, 5, 2, 6, 6, 
            // +1
           7, 0, 3, 4, 5, 5, 5, 4,  
           2, 6, 1, 5, 5, 3, 4 ,7,
           5, 4, 2, 2, 2, 5, 6, 6,
           0, 4, 5, 4, 5, 6, 6, 2, 
            // +3
           7, 0, 7, 0, 5, 5, 1, 0,  
           2, 6, 5, 1, 5, 3, 0 ,3,
           5, 4, 6, 6, 2, 5, 2, 2,
           0, 4, 1, 0, 5, 6, 2, 6, 
            // +0
           7, 4, 3, 0, 5, 1, 5, 0,  
           2, 2, 1, 1, 5, 7, 4 ,3,
           5, 0, 2, 6, 2, 1, 6, 2,
           0, 0, 5, 0, 5, 2, 6, 6, 

                    0,4,0,4,1,2,3,4,4,  
                    7,4,5,4,1,0,3,4,4,  
                    0,4,0,4,1,2,3,4,4,  
                                0,1,3,0,1,3,1,2,0,    7,7,4,4,6,0,  
                                0,1,3,0,1,3,1,2,0,    7,7,4,4,5,0,  
                                0,1,3,0,1,3,1,2,0,    7,7,4,4,4,0,  
                    0, 0, 0, 0, 0, 0, 2, 4, 5, 6, 1, 7, 3, 2, 3, 2, 3, 5, 6, 1, 5, 1, 2, 1, 5, 7, 
//        0, 4, 0, 4, 0, 4, 0, 4,
//        0, 4, 0, 4, 0, 4, 0, 4,
//        0, 4, 0, 4, 0, 4, 0, 4,
//        0, 4, 0, 4, 0, 4, 0, 4,
//        0, 4, 0, 4, 0, 4, 0, 4,
            };

        public void Run()
        {
            #region "Test of Frequency Detector"
            {
                IQModulator iqm = new IQModulator(1200 + 13, 1200 + 13, 1, 7200, 36, null);
                iqm.Init();
                for (int i = 0; i < 243; i++) 
                    iqm.Process(IQ.UNITY);
                iqm.Finish();

                float[] fa = new float[iqm.Count];
                iqm.GetData(fa);

                IQDetector iqd = new IQDetector(1200, 7200, 36, 0, 1, -10, false);
                iqd.Init();

                iqd.Process(fa, 0, fa.Length);
            }
            #endregion

            #region Test of the FFT Modulator and Demodulator
            {
                float BlockSize;
                BlockSize = ((7200.0f / MILSTD188_110B_39.SYMBOLRATE) + 0.5f);
                float[] Filter = new float[(int)BlockSize];
                for (int i = 0; i < Filter.Length; i++)
                {
                    Filter[i] = 2.0f / Filter.Length;
                }
                OFDMFFTModulator fftm = new OFDMFFTModulator(MILSTD188_110B_39.CARRIER_FREQ_LO, MILSTD188_110B_39.CARRIER_FREQ_HI, 39, 7200, MILSTD188_110B_39.SYMBOLRATE);
                OFDMFFTDemodulator fftd = new OFDMFFTDemodulator(MILSTD188_110B_39.CARRIER_FREQ_LO, MILSTD188_110B_39.CARRIER_FREQ_HI, 39, 7200, MILSTD188_110B_39.SYMBOLRATE);
                IQModulator regularm = new IQModulator(MILSTD188_110B_39.CARRIER_FREQ_LO, MILSTD188_110B_39.CARRIER_FREQ_HI, 39, 7200, MILSTD188_110B_39.SYMBOLRATE, null);
                IQDemodulator regulard = new IQDemodulator(MILSTD188_110B_39.CARRIER_FREQ_LO, MILSTD188_110B_39.CARRIER_FREQ_HI, 39, 7200, MILSTD188_110B_39.SYMBOLRATE, Filter);
                OFDMDemodulator ofdmd = new OFDMDemodulator(MILSTD188_110B_39.CARRIER_FREQ_LO, MILSTD188_110B_39.CARRIER_FREQ_HI, 39, 7200, MILSTD188_110B_39.SYMBOLRATE, 64f / 81f);
                IQDetector regdet = new IQDetector(MILSTD188_110B_39.CARRIER_FREQ_LO, 7200, MILSTD188_110B_39.SYMBOLRATE);


                fftm.Init();
                fftd.Init();
                ofdmd.Init();
                regulard.Init();

                fftm[0].Process(new IQ(1, 0));
                fftm[1].Process(new IQ(0.7071f, 0.7071f));
                fftm[2].Process(new IQ(0, -1));
                fftm[3].Process(new IQ(-0.7071f, -0.7071f));
                fftm[4].Process(new IQ(0, 1));

                fftm[12].Process(new IQ(0, -1));
                fftm[22].Process(new IQ(-1, 0));

                fftm[0].Process(new IQ(-1, 0));
                fftm[1].Process(new IQ(-0.7071f, -0.7071f));
                fftm[2].Process(new IQ(0, 1));
                fftm[3].Process(new IQ(0.7071f, -0.7071f));
                fftm[4].Process(new IQ(0, -1));

                fftm[12].Process(new IQ(-1, 0));
                fftm[22].Process(new IQ(0, -1));

                fftm[0].Process(new IQ(0, -1));
                fftm[1].Process(new IQ(-0.7071f, 0.7071f));
                fftm[2].Process(new IQ(-0.7071f, -0.7071f));
                fftm[3].Process(new IQ(0, 1));
                fftm[4].Process(new IQ(0.7071f, 0.7071f));

                fftm[12].Process(new IQ(0.7071f, -0.7071f));
                fftm[22].Process(new IQ(0, -1));

                fftm[0].Process(new IQ(1, 0));
                fftm[1].Process(new IQ(0.7071f, 0.7071f));
                fftm[2].Process(new IQ(0, -1));
                fftm[3].Process(new IQ(-0.7071f, -0.7071f));
                fftm[4].Process(new IQ(0, 1));

                fftm[12].Process(new IQ(0, -1));
                fftm[22].Process(new IQ(-1, 0));

                fftm.Finish();

                regularm[0].Process(new IQ(1, 0));
                regularm[1].Process(new IQ(0.7071f, 0.7071f));
                regularm[2].Process(new IQ(0, -1));
                regularm[3].Process(new IQ(-0.7071f, -0.7071f));
                regularm[4].Process(new IQ(0, 1));

                regularm[12].Process(new IQ(0, -1));
                regularm[22].Process(new IQ(-1, 0));
                regularm.Finish();

                float[] ffr = new float[fftm.Count];

                OFDMSync os = new OFDMSync((int)BlockSize);
                fftm.GetData(ffr);
                fftd.Process(ffr, 0, ffr.Length);
                regulard.Process(ffr, 0, ffr.Length);
                ofdmd.Process(ffr, 0, ffr.Length);
                regdet.Process(ffr, 0, ffr.Length);

                IQ[] ffriq = new IQ[ffr.Length];
                for(int i = 0; i < ffriq.Length; i++)
                {
                    ffriq[i] = new IQ(ffr[i], 0);
                }

                int SymbOffset = 18;
                os.Process(ffriq, SymbOffset, ffriq.Length - SymbOffset);


                regularm.GetData(ffr);
                fftd.Init();
                ofdmd.Init();
                regulard.Init();

                fftd.Process(ffr, 0, ffr.Length);
                regulard.Process(ffr, 0, ffr.Length);
                ofdmd.Process(ffr, 0, ffr.Length);

            }
            #endregion

            #region Test of Generator and Quad

            double delta = (2 * Math.PI * 500) / 8000;
            double initialphase = 0.123456;

            //            Generator tgSin = new Generator();
            //            Generator tgCos = new Generator();
            //            tgSin.Init(500, 8000, (float)(0 + initialphase));
            //            tgCos.Init(500, 8000, (float)(Math.PI / 2 + initialphase));


            Quad tq = new Quad(500, 8000, initialphase);

            float maxError = 0;
            float tgdataSin;
            float tgdataCos;
            IQ tqData;
            for (int i = 0; i < 100000; i++)
            {

                float idealdataSin = (float)Math.Sin(initialphase + i * delta);
                float idealdataCos = (float)Math.Cos(initialphase + i * delta);

                //                tqData = tq.Value;
                tq.Process(1, out tqData);
                tgdataSin = tqData.I;
                tgdataCos = tqData.Q;
                //                tgSin.Process(1, out tgdataSin);
                //                tgCos.Process(1, out tgdataCos);

                float error = Math.Abs(tgdataCos - idealdataCos);
                error += Math.Abs(tgdataSin - idealdataSin);
                if (error > maxError)
                    maxError = error;
            }
            #endregion

            #region Test of Quad Modulators/Demodulators
            {
                IQModulator tm = new IQModulator(1000, 1000, 1, 8000, 250, null);
                IQEncoder te = new IQEncoder(2, Constellation.BitsToPhase_39, Constellation.IQTable_QPSK45, EncodingType.DIFF_IQ);

                OFDMDemodulator tt = new OFDMDemodulator(1000, 1000, 1, 8000, 250, 1);
                IQDecoder td = new IQDecoder(2, Constellation.BitsToPhase_39, Constellation.IQTable_QPSK45, EncodingType.DIFF_IQ);

                int[] EncData = new int[] { 0, 1, 2, 3, 0, 0, 1, 1, 2, 2, 3, 3, 2, 1, 0 };
                IQ IQData;
                // Send reference signal
                tm.Process(IQ.UNITY);

                foreach (int symbol in EncData)
                {
                    te.Process(symbol, out IQData);
                    tm.Process(IQData);
                }

                Samples outputTestSamples = new Samples(@"C:\TestMod.raw", 1.0f);
                float[] TestSamplesOut = new float[tm.Count];
                tm.GetData(TestSamplesOut);
                outputTestSamples.ToByte(TestSamplesOut);
                outputTestSamples.Close();

                tt.Process(TestSamplesOut, 0, TestSamplesOut.Length);
                List<int> TestOut = new List<int>();
                int Symb;
                while (tt.Count > 0)
                {
                    td.Process(tt.GetData(), out Symb);
                    TestOut.Add(Symb);
                }
                TestOut.Clear();
            }
            #endregion

            #region Test SoftConvolutional Encoder
            {
                int[] Poly =  { 0x7, 0x5, 0x3, 0x6 };
                int[] PolyMIL = { 0x5B, 0x79 };

                int[] input = { 0xF7, 0x45, 0x12, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x00, 0x11, 0xFF, 0xA5 };
                BitArray sba = new BitArray(8);
                sba.Add(input);

                ConvEncoder sce = new ConvEncoder(ConvEncoderType.TailBiting_Head, 2, 7, PolyMIL, 0x7, 4);

                byte[] ddd = new byte[sba.BitsCount];
                sba.GetData(ddd);
                sce.Process(ddd, 0, ddd.Length);
                sce.Finish();
                ddd = new byte[sce.Count];
                sce.GetData(ddd);
                // ddd has all databits

                VitDecoder svd = new VitDecoder(ConvEncoderType.TailBiting_Head, 2, 7, PolyMIL, 0x7, 4, 3);
                svd.Process(ddd, 0, ddd.Length);
                svd.Finish();

                ddd = new byte[svd.Count];
                svd.GetData(ddd);
                BitArray res = new BitArray(8);
                res.Add(ddd);

                int[] ttt = new int[res.SymbolsCount];
                res.GetData(ttt);

            }
            #endregion


            // Test of the 39-tone codec
            MILSTD188_110B_39 m = new MILSTD188_110B_39(MILSTD_188.Mode.D_2400S, 7200, 7200, null, null);

            BitArray ba = new BitArray(12);
            for (int i = 0; i < 800; i++)
                ba.Add(i);
            byte[] da = new byte[ba.BitsCount];
            ba.GetData(da);

            m.Tx.Start();
            m.Tx.Process(da, 0, da.Length);
            m.Tx.Finish();

            Samples outputSamples = new Samples(@"C:\test39.raw", 1.5f);
            float[] SamplesOut = new float[m.Tx.SampleCount];
            m.Tx.GetData(SamplesOut, 0);
            outputSamples.ToByte(SamplesOut);
            outputSamples.Close();

            string FileName = @"C:\test39";

            Samples InputSamples = new Samples(FileName + ".raw");
            float[] SamplesIn = new float[100];
            List<float> Test39Samples = new List<float>();
            int n;
            do
            {
                n = InputSamples.ToFloat(SamplesIn);
                for (int i = 0; i < n; i++)
                    Test39Samples.Add(SamplesIn[i]);
            } while (n > 0);

            InputSamples.Close();

            m.Rx.pd1.Init();
            m.Rx.pd2.Init(0);
            //            foreach (float sample in SamplesOut)
            //            {
            //                m.Rx.pd3.Process(sample);
            //            }

            foreach (float sample in Test39Samples)
            {
                if (!m.Rx.pd1.IsSyncFound)
                {
                    m.Rx.pd1.Process(sample);
                    if (m.Rx.pd1.IsSyncFound)
                    {
                        m.Rx.pd2.Init(m.Rx.pd1.FrequencyOffset);
                    }
                }
                else
                {
                    if (!m.Rx.pd2.IsSyncFound)
                    {
                        m.Rx.pd2.Process(sample);
                        if (m.Rx.pd2.IsErrorFound)
                        {
                            m.Rx.pd2.Init(0);
                            m.Rx.pd1.Init();
                        }
                        if (m.Rx.pd2.IsSyncFound)
                        {
                            m.Rx.pd3.Init(m.Rx.pd2.FrequencyOffset);
                        }
                    }
                    else
                    {
                        m.Rx.pd3.Process(sample);
                    }
                }
            }

            List<byte> RegenData = new List<byte>();

            System.IO.FileStream File = System.IO.File.Open(FileName + ".asc", System.IO.FileMode.Create);
            while (m.Rx.pd3.Count > 0)
            {
                byte dbyte = m.Rx.pd3.GetData();
                RegenData.Add(dbyte);
                File.WriteByte((byte)(dbyte + 0x30));
            }
            File.Close();

            da = new byte[RegenData.Count];
            IQ[] iqda = new IQ[m.Rx.pd3.RawCount];
            RegenData.CopyTo(da);
            m.Rx.pd3.GetRawData(iqda);

            m = new MILSTD188_110B_39(MILSTD_188.Mode.D_2400S, 7200, 7200, null, null);
            m.Tx.Start();

            m.Tx.ProcessRaw(da, 0, da.Length);
            //            m.Tx.ProcessRaw(iqda, 0, iqda.Length);
            m.Tx.Finish();

            outputSamples = new Samples(FileName + "-regen.raw", 1.0f);
            SamplesOut = new float[m.Tx.SampleCount];
            m.Tx.GetData(SamplesOut, 0);
            outputSamples.ToByte(SamplesOut);
            outputSamples.Close();




            #region  Test of Complex data
            {

                IQ a = new IQ(1, 2);
                IQ b = new IQ(3, 4);
                IQ c = new IQ(5, 6);

                IQ f = a + b;
                f = a * c;
                f = f - a;
                f = a / b;
                f -= b;
            }
            #endregion


            #region Test of Serial Class

            SerialData tx = new SerialData(7, 1, 1, SerialData.Parity.N);

            SerialData rx = new SerialData(7, 1, 1, SerialData.Parity.N);

            for (int i = 0; i < 256; i++)
            {
                tx.PutSymbol(i);
            }

            byte[] rdd = new byte[10000];
            int nbits = tx.GetData(rdd);

            rx.PutData(rdd, 0, nbits);

            int[] rsymb = new int[256];
            rx.GetData(rsymb);

            #endregion


            #region Test Convolutional Encoder
            {
                int[] Poly =  { 0x7, 0x5, 0x3, 0x6 };
                int[] PolyMIL = { 0x5B, 0x79 };
//                OldConvEncoder ce = new OldConvEncoder(2, 7, PolyMIL, 0x7, 4, ConvEncoderType.TailBiting_Head);
                byte[] input = { 0xF7, 0x45, 0x12, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x00, 0x11, 0xFF, 0xA5 };
                byte[] output = new byte[input.Length * 4];
                byte[] res = new byte[input.Length];

                int iBits = input.Length * 8;
//                int nBits = ce.Process(input, 0, output, 0, iBits);

                // Test Convolutional decoder
                output[0] ^= 0x08;
                output[2] ^= 0x04;
                output[5] ^= 0x01;
                output[7] ^= 0x20;
                output[10] ^= 0x40;
                output[13] ^= 0x10;
                output[15] ^= 0x80;

//                OldVitDecoder vd = new OldVitDecoder(2, 7, PolyMIL, 0x7, 4, 6, ConvEncoderType.TailBiting_Head);

                int[] sdd = new int[200];
//                int qBits = vd.Quantize(output, sdd, nBits);

//                int rBits = vd.Process(output, res, nBits);
            }
            #endregion


            #region Test of the IQEncoder and IQDecoder


            int[] symb = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 0, 3, 5, 7, 4, 3, 2, 1 };

            IQEncoder e = new IQEncoder(2, Constellation.Table_1_to_1, Constellation.ITable_QPSK, Constellation.QTable_QPSK, EncodingType.NON_DIFF);
            IQDecoder d = new IQDecoder(2, Constellation.Table_1_to_1, Constellation.ITable_QPSK, Constellation.QTable_QPSK, EncodingType.NON_DIFF);

            IQ iqs;
            int r;
            for (int i = 0; i < symb.Length; i++)
            {
                e.Process(symb[i], out iqs);
                d.Process(iqs, out r);
            }

            #endregion

            float[] fCoeff = Filters.Fill(Filters.rrc_180, 2);

            int[] datadecoded = new int[300];
            int[] syncseq = { 
                                0,1,3,0,1,3,1,2,0,    7,7,4,4,6,0,  
                                0,1,3,0,1,3,1,2,0,    7,7,4,4,5,0,  
            };

            IQ[] syncIQ = new IQ[syncseq.Length];

            const float SamplingFreq = 24000;
            const float SymbolFreq = 2400;
            const float CarrierFreq = 1800;
            const float FreqOffset = 0;
            const float PhaseOffset = 0;
            const int GrpDelay = 0;



            int DFac = (int)(SamplingFreq / SymbolFreq);

            // Let's start with the encoder and decoder
            e = new IQEncoder(3, Constellation.Table_1_to_1, Constellation.IQTable_8PSK, EncodingType.NON_DIFF);
            d = new IQDecoder(3, Constellation.Table_1_to_1, Constellation.IQTable_8PSK, EncodingType.NON_DIFF);

            // Create Modulator
            IQModulator mod = new IQModulator(CarrierFreq + FreqOffset, CarrierFreq + FreqOffset, 1, SamplingFreq, SymbolFreq, fCoeff);
            // Add offset
            mod.Phase = PhaseOffset;

            // Create Demodulator
            IQDemodulator dem = new IQDemodulator(CarrierFreq, CarrierFreq, 1, SamplingFreq, SymbolFreq, fCoeff);

            // Now correlator to catch the symbol pattern and sync on it
            Correlator corr = new Correlator(CORR_TYPE.DELTA_DIFF, 70, 30, 15, 0.5f, 10.0f, 5.0f);

            e.Init();
            // Fill the sync pattern
            for (int i = 0; i < syncseq.Length; i++)
            {
                e.Process(syncseq[i], out syncIQ[i]);
            }
            corr.AddTarget(syncIQ);

            IQ sym;
            //Encode and Modulate - result goes into incomingData[] array
            e.Init();
            for (int i = 0; i < datastream.Length; i++)
            {
                e.Process(datastream[i], out sym);
                mod.Process(sym, incomingData, i * DFac + GrpDelay);
            }
            // Output 20 more ZERO symbols to flush modulator
            sym = IQ.ZERO;
            for (int i = datastream.Length; i < datastream.Length + 20; i++)
            {
                mod.Process(sym, incomingData, i * DFac + GrpDelay);
            }

            //------------------  Now start processing the data on the receiver side

            IQ[] demodArray = new IQ[300];

            // start feeding the data into the demodulator - turn symbolsync search first
            int startIdx = 0;
            int outIdx = 0;
            // feed the data until we get a symbol sync
            dem.Init();
            dem.StartCorrectionProcess(SYNC_TYPE.GARDNER_DD | SYNC_TYPE.GARDNER_NDA | SYNC_TYPE.QAMLD_NDA |
                                    SYNC_TYPE.DIFF_NDA | SYNC_TYPE.ZERODET_NDA | SYNC_TYPE.PEAK_NDA | SYNC_TYPE.CORR_NDA, 100);
            while (!dem.IsSyncReady)
            {
                dem.Process(incomingData, startIdx, DFac);
                startIdx += DFac;
            }

            // Feed demodulated data into correlator and search for sync pattern  
            corr.Init();
            corr.StartCorrectionProcess();
            for (int i = 0; !corr.IsSyncReady; i++, startIdx += DFac)
            {
                int nSymb = dem.Process(incomingData, startIdx, DFac);
                while (nSymb-- > 0)
                {
                    IQ iqData = dem.GetData();
                    demodArray[outIdx++] = iqData;
                    corr.Process(iqData);
                }
            }

            dem.RotateCorrection = corr.RotateCorrection;
            dem.FrequencyCorrection = corr.FrequencyCorrection;

            corr.GetLastData(corr.CorrelationMaxIndex, demodArray, corr.CorrelationMaxIndex);

            outIdx = corr.CorrelationMaxIndex;
            while (startIdx < (incomingData.Length - DFac))
            {
                int nSym = dem.Process(incomingData, startIdx, DFac);
                while (nSym-- > 0)
                {
                    demodArray[outIdx++] = dem.GetData();
                }
                startIdx += DFac;
            }
            d.Init();
            d.StartCorrectionProcess(300);
            int iSymb;
            for (int i = 0; i < demodArray.Length; i++)
            {
                d.Process(demodArray[i], out iSymb);
                datadecoded[i] = iSymb;
            }




            demodArray.Initialize();

            #region Test of Integrate and Dump decoder
            {
                int[] teststring = { 0, 4, 0, 4, 0, 4, 0, 4, 4 };
                int[] datadecoded1 = new int[300];

                const float SYMBOL_TIME = 0.0225f;      // Every symbol lasts for 22.5ms
                const float SYMBOLRATE = 1 / SYMBOL_TIME; // Symbol rate will be 44.44444444 Hz

                const float SamplingFreq1 = 8000;
                const float SymbolFreq1 = SYMBOLRATE;
                const float CarrierFreq1 = 1800;
                const float FreqOffset1 = 0;
                const float PhaseOffset1 = 0;
                const int GrpDelay1 = 0;

                int DFac1 = (int)(SamplingFreq1 / SymbolFreq1);

                // Let's start with the encoder and decoder
                e = new IQEncoder(3, Constellation.Table_1_to_1, Constellation.IQTable_8PSK, EncodingType.NON_DIFF);
                d = new IQDecoder(3, Constellation.Table_1_to_1, Constellation.IQTable_8PSK, EncodingType.NON_DIFF);

                // Create Modulator
                IQModulator mod1 = new IQModulator(CarrierFreq1 + FreqOffset1, CarrierFreq1 + FreqOffset1, 1, SamplingFreq1, SymbolFreq1, null);
                // Add offset
                mod1.Phase = PhaseOffset1;

                // Create Demodulator
                Quad dem1 = new Quad(CarrierFreq1, SamplingFreq1);

                e.Init();

                IQ sym1;
                //Encode and Modulate - result goes into incomingData[] array
                e.Init();
                Array.Clear(incomingData, 0, incomingData.Length);
                for (int i = 0; i < teststring.Length; i++)
                {
                    e.Process(teststring[i], out sym1);
                    mod1.Process(sym1, incomingData, i * DFac1 + GrpDelay1);
                }

                // Output 20 more ZERO symbols to flush modulator
                //------------------  Now start processing the data on the receiver side

                IQ[] demodArray1 = new IQ[300];
                IntegrateAndDump IandD = new IntegrateAndDump(DFac1);
                // IandD.Offset = DFac1 / 2;

                // start feeding the data into the demodulator
                int[] decodedData = new int[64];
                int outIdx1 = 0;
                foreach (float sample in incomingData)
                {
                    dem1.Process(sample, out sym1);
                    IandD.Process(sym1);
                    while (IandD.Count > 0)
                    {
                        sym1 = IandD.GetData();
                        demodArray1[outIdx1] = sym1;
                        d.Process(sym1, out decodedData[outIdx1++]);
                    }
                }
            }


            #endregion


            #region Decimator Testing

            /*    Testing of the Decimator  */
            int K = 50;
            int SAMP = 200;
            int DEC = 10;

            float[] Arr = new float[K];
            float[] Samp = new float[SAMP];

            for (int i = 0; i < Arr.Length; i++)
            {
                Arr[i] = 1;
            }
            for (int i = 0; i < Samp.Length; i++)
            {
                Samp[i] = i / 100.0f;
            }

            FIR fff = new FIR(Arr, DEC);

            float[] rrr = new float[Samp.Length / DEC];

            float[] mmm = { 1, 2, 3 };

            float[] mmmm = { 1, 2, 3, 4, 5, 6, 7 };
            fff.Decimate(mmm, rrr);
            fff.Decimate(mmm, rrr);
            fff.Decimate(mmmm, rrr);
            fff.Decimate(Samp, rrr);
            fff.Decimate(mmmm, rrr);
            fff.Decimate(mmm, rrr);
            fff.Decimate(mmmm, rrr);
            fff.Decimate(mmm, rrr);
            fff.Decimate(mmmm, rrr);
            fff.Decimate(mmmm, rrr);
            fff.Decimate(mmmm, rrr);

            #endregion



            #region // Test EOM detector

            BitCorrelator bc = new BitCorrelator();

            int FlipEOM = MILSTD_188.MSBFirst(MILSTD_188.EOM);
            bc.AddTarget(FlipEOM, 32);

            BitArray testBA = new BitArray(8);
            testBA.Add(0x12);
            testBA.Add(0x15);
            testBA.Add((FlipEOM >> 0) & 0x00FF);
            testBA.Add((FlipEOM >> 8) & 0x00FF);
            testBA.Add((FlipEOM >> 16) & 0x00FF);
            testBA.Add((FlipEOM >> 24) & 0x00FF);
            testBA.Add(0xFF);
            testBA.Add(0x05);

            byte[] testArray = new byte[testBA.BitsCount];
            testBA.GetData(testArray);

            bc.Process(testArray, 0, testArray.Length);
            #endregion
            //  etab [] Tab = new etab[]
            //{
            //  {4, 0x13,    1,   1, 4, 8, 10 },    // RS(7,3) on GF(15)
            //  {4, 0x13,    1,   1, 4, 1, 10 },    // RS(14,10) on GF(15)
            //  {4, 0x13,    1,   1, 4, 9, 10 },    // RS(6,2) on GF(15)
            //  {4, 0x13,    1,   1, 4, 10, 10 },   // RS(5,1) on GF(15)
            //  {2, 0x7,     1,   1, 1, 0, 10 },
            //  {3, 0xb,     1,   1, 2, 0, 10 },
            //  {4, 0x13,    1,   1, 4, 0, 10 },
            //  {5, 0x25,    1,   1, 6, 0, 10 },
            //  {6, 0x43,    1,   1, 8, 0, 10 },
            //  {7, 0x89,    1,   1, 10, 0, 10 },
            //  {8, 0x11d,   1,   1, 32, 0, 10 },
            //  {8, 0x187,   112,11, 32, 0, 10 }, /* Duplicates CCSDS codec */
            //  {9, 0x211,   1,   1, 32, 0, 10 },
            //  {10,0x409,   1,   1, 32, 0, 10 },
            //  {11,0x805,   1,   1, 32, 0, 10 },
            //  {12,0x1053,  1,   1, 32, 0, 5 },
            //  {13,0x201b,  1,   1, 32, 0, 2 },
            //  {14,0x4443,  1,   1, 32, 0, 1 },
            //  {15,0x8003,  1,   1, 32, 0, 1 },
            //  {16,0x1100b, 1,   1, 32, 0, 1 },
            //  {0, 0, 0, 0, 0},
            //};

            //-----------------------------------------------------------------------------------------------------------------------------------

            #region Test of the bit rearranger

            BitGroup[] D2400_old = { new BitGroup(0 * 78, 64) };
            BitGroup[] D1200_old = { new BitGroup(0 * 64, 64), new BitGroup(0 * 63, 14) };
            BitGroup[] D600_old = { new BitGroup(0 * 32, 32), new BitGroup(0 * 32, 32), new BitGroup(0, 14) };
            BitGroup[] D300_old = { new BitGroup(0 * 16, 16), new BitGroup(0 * 16, 16), new BitGroup(0 * 16, 16), new BitGroup(0 * 16, 16), 
                                    new BitGroup(0 * 0,  14) };
            BitGroup[] D150_old = { new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), 
                                    new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), new BitGroup(0 * 8, 8), 
                                    new BitGroup(0 * 0, 8), new BitGroup(0 * 8, 6) };

            BitGroup[] D75_old = {  new BitGroup(0 * 0, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 4), new BitGroup(0 * 4, 2) };

            BitGroup[] D2400_new = { new BitGroup(0 * 78, 64) };
            BitGroup[] D1200_new = { new BitGroup(0 * 64, 64), new BitGroup(0 * 63, 14) };
            BitGroup[] D600_new = { new BitGroup(0 * 32, 32), new BitGroup(8 * 32, 32), new BitGroup(0, 14) };
            BitGroup[] D300_new = { new BitGroup(0 * 16, 16), new BitGroup(4 * 16, 16), new BitGroup(8 * 16, 16), new BitGroup(12 * 16, 16), 
                                    new BitGroup(0 * 0, 14) };
            BitGroup[] D150_new = { new BitGroup(0 * 8, 8), new BitGroup(2 * 8, 8), new BitGroup(4 * 8, 8), new BitGroup(6 * 8, 8), 
                                    new BitGroup(8 * 8, 8), new BitGroup(10 * 8, 8), new BitGroup(12 * 8, 8), new BitGroup(14 * 8, 8), 
                                    new BitGroup(0 * 0, 8), new BitGroup(2 * 8, 6) };

            BitGroup[] D75_new = {  new BitGroup(0 * 0, 4), new BitGroup(1 * 4, 4), new BitGroup(2 * 4, 4), new BitGroup(3 * 4, 4), 
                                    new BitGroup(4 * 4, 4), new BitGroup(5 * 4, 4), new BitGroup(6 * 4, 4), new BitGroup(7 * 4, 4), 
                                    new BitGroup(8 * 4, 4), new BitGroup(9 * 4, 4), new BitGroup(10 * 4, 4), new BitGroup(11 * 4, 4), 
                                    new BitGroup(12 * 4, 4), new BitGroup(13 * 4, 4), new BitGroup(14 * 4, 4), new BitGroup(15 * 4, 4), 
                                    new BitGroup(0 * 4, 4), new BitGroup(1 * 4, 4), new BitGroup(2 * 4, 4), new BitGroup(3 * 4, 2) };

            DataSpreader<byte> dsp = new DataSpreader<byte>(4, D75_new);
            DataCombiner dcb = new DataCombiner(4, D75_new);
            dsp.Init();
            dcb.Init();


            for (int idx = 0; idx < 100; idx++)
                dsp.Process((byte)idx);

            byte[] od = new byte[dsp.Count];
            dsp.GetData(od, 0);
            foreach (byte b in od)
                dcb.Process(b);


            #endregion

            //-----------------------------------------------------------------------------------------------------------------------------------

            #region Test of the SoftInterleaver
            //            Interleaver_188_110B_39 il = new Interleaver_188_110B_39(18, 12, 4, 7, 3);
            Interleaver_188_110B_39 il = new Interleaver_188_110B_39(18, 12, 4, 7, 3);
            il.Init();

            int ii = 0;
            il.Init();
            while (!il.IsDataReady)
            {
                int Data = ii++;
                for (int i = 0; (i < 4); i++)
                {
                    il.ProcessEncode((byte)(Data & 0x0001));
                    Data >>= 1;
                }
            }
            byte[] OutData = new byte[il.Count];
            il.GetData(OutData, 0);

            Queue<byte> chann = new Queue<byte>();
            foreach (byte b in OutData) chann.Enqueue(b);

            il.Init();
            while (chann.Count > 0) il.ProcessDecode(chann.Dequeue());

            while (!il.IsDataReady) il.ProcessDecode(0);
            OutData = new byte[il.Count];
            il.GetData(OutData, 0);
            #endregion

            ReedSolomon rs = new ReedSolomon();
            rs.Init(4, 0x13, 1, 1, 4, 8);

            int[] data = new int[7];
            data[0] = 0x08;
            data[1] = 0x03;
            data[2] = 0x04;
            int[] parity, eras;
            eras = new int[15];
            parity = new int[15];

            rs.Encode(data, parity);
            Array.Copy(parity, 0, data, 3, 4);

            data[0] = 0x00;
            data[1] = 0x00;
            data[2] = 0x00;
            //          data[3] = 0x00;
            eras[0] = 0;
            eras[1] = 1;
            eras[2] = 2;
            eras[3] = 3;
            int nerr = rs.Decode(data, eras, 3);

            LFSR__188_110B_39 lsr = new LFSR__188_110B_39(9, 0x0116);

            lsr.Init(0x01);
            int ff = 0;
            while ((lsr.Value & 0x1FF) != 0x1FF)
            {
                ff++;
                lsr.Shift();
            }
            int rr = (int)lsr.CurrentBit;

            int[] requiredoffsets = { 
                252, 256, 260, 264, 267, 268, 272, 280, 288, 
                300, 308, 340, 356, 376, 384, 385, 392, 396, 
                400, 408, 416, 420, 432, 440, 444, 448, 464, 480, 484, 497, 
                504, 512, 520, 528 };

            int[] calculatedseeds = new int[requiredoffsets.Length];
            int[] mem = new int[511];
            int el = 0;
            int q = 0;

            el = 0;
            lsr.Init(0x1FF);
            do
            {
                lsr.Shift();
                mem[el++] = lsr.Value & 0x1FF;
            } while ((lsr.Value & 0x1FF) != 0x1FF);

            foreach (int req in requiredoffsets)
            {
                el = 510 - req;
                if (el < 0)
                    el += 511;
                calculatedseeds[q++] = mem[el] & 0x1FF;
            }

            q = 0;
            foreach (int load in calculatedseeds)
            {
                lsr.Init(load);
                lsr.Shift(requiredoffsets[q++]);
                el = (int)lsr.Value;
                rr = (int)lsr.CurrentBit;
            }

            int[] AllValues = new int[512];
            lsr.Init(0x1FF);
            ii = 0;
            for (ii = 0; ii < 0x200; ii++)
            {
                AllValues[ii] = lsr.Value & 0x1FF;
                lsr.Shift();
            }

            // Now calculate the seed value differently
            int[] newseeds = new int[requiredoffsets.Length];
            ii = 0;
            foreach (int req in requiredoffsets)
            {
                int ShiftVal = 0x200 - req - 1; if (ShiftVal < 0) ShiftVal += 0x200 - 1;
                lsr.Init(0x1FF);
                lsr.Shift(ShiftVal);
                newseeds[ii++] = lsr.Value & 0x1FF;
            }

        }
    }
}
