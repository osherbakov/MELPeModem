using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    class FIR
    {
        // Array of the coefficients
        float[] Coeffs;             //  for regular processing 
        float[] InterpCoeffs;       //  .. and interpolation

        float[] Samples;            // Array that keeps all samples for 1 coeff run
        int CurrIdx;


        int InterpDecimFactor;
        int MaxCoverage;
        int CoeffsLength;           // Number of coefficients
        int LastSample;             //  that number - 1

        int SamplesProcessed;       // Processed samples counter

        public FIR(float []coeffs, int InterpDecim)
        {
            if (coeffs == null)
            {
                coeffs = new float[1];
                coeffs[0] = 1.0f;
            }
            this.CoeffsLength = coeffs.Length;
            this.LastSample = coeffs.Length - 1;
            this.Coeffs = new float[CoeffsLength];
            coeffs.CopyTo(this.Coeffs, 0);

            this.Samples = new float[CoeffsLength];
            this.CurrIdx = 0;
            this.InterpDecimFactor = InterpDecim;
            this.SamplesProcessed = 0;

            this.MaxCoverage = (LastSample / InterpDecim) + 1;
            this.InterpCoeffs  = new float[InterpDecim * MaxCoverage];

            
            // Fill out the Polyphase coefficients array
            int idx = 0;
            int st = 0;
            foreach (float val in Coeffs)
            {
                InterpCoeffs[idx] = val;
                idx += MaxCoverage;
                if (idx >= InterpCoeffs.Length)
                {
                    idx = ++st;
                }
            }
        }

        public void Clear()
        {
            Array.Clear(this.Samples,0,Samples.Length);
//            this.SamplesProcessed = InterpDecimFactor - 1;
            this.SamplesProcessed = 0;
            this.CurrIdx = 0;
        }

        public void Init()
        {
            this.Clear();
        }

        public int ProcessVoid()
        {
            Samples[CurrIdx++] = 0;           // Put zero sample
            if (CurrIdx >= CoeffsLength) CurrIdx = 0;
            SamplesProcessed++; if (SamplesProcessed >= InterpDecimFactor) SamplesProcessed = 0;
            return 1;
        }

        public int Process(float Sample, out float outData)
        {
            int j, sIdx;
            Samples[CurrIdx] = Sample;                   // Insert a new sample
            float Accum = 0.0f;
            for (j = 0, sIdx = CurrIdx; sIdx >= 0; )
            {
                Accum += Coeffs[j++] * Samples[sIdx--];
            }
            for (sIdx = LastSample; j < CoeffsLength; )
            {
                Accum += Coeffs[j++] * Samples[sIdx--];
            }
            CurrIdx++; if(CurrIdx >= CoeffsLength) CurrIdx = 0;
            outData = Accum;
            SamplesProcessed++; if (SamplesProcessed >= InterpDecimFactor) SamplesProcessed = 0;
            return 1;
        }

        public int Process(float[] inOutData, int numSamples)
        {
            return Process(inOutData, 0, inOutData, 0, numSamples);
        }

        public int Process(float[] inData, int inputIndex, float[] outData, int outputIndex, int numSamples)
        {
            int j, sIdx;
            for (int i = 0; i < numSamples; i++)
            {
                Samples[CurrIdx] = inData[inputIndex++];                   
                float Accum = 0.0f;
                for (j = 0, sIdx = CurrIdx; sIdx >= 0 ; )
                {
                    Accum += Coeffs[j++] * Samples[sIdx--];
                }
                for (sIdx = LastSample; j < CoeffsLength; )
                {
                    Accum += Coeffs[j++] * Samples[sIdx--];
                }
                CurrIdx++; if (CurrIdx >= CoeffsLength) CurrIdx = 0;
                outData[outputIndex++] = Accum;
            }
            SamplesProcessed += numSamples; SamplesProcessed %= InterpDecimFactor;
            return numSamples;
        }

        public int Process(float[] inOutData)
        {
            return Process(inOutData, inOutData.Length);
        }

        public int InterpolateVoid()
        {
            Samples[CurrIdx++] = 0;           // Put zero sample
            if (CurrIdx >= CoeffsLength) CurrIdx = 0;
            return this.InterpDecimFactor;
        }

        public int Interpolate(float Sample, float[] outputDataArray)
        {
            return Interpolate(Sample, outputDataArray, 0);
        }

        public int Interpolate(float Sample, float[] outputDataArray, int outputIndex)
        {
            Samples[CurrIdx] = Sample;           // Save sample

            int sIdx, j;
            int coeffIdx = 0;
            int end1 = Math.Min(CurrIdx, (MaxCoverage - 1));
            for (int i = 0; i < InterpDecimFactor; i++)  
            {
                float Accum = 0;
                for (j = 0, sIdx = CurrIdx; j <= end1; j++)
                {
                    Accum += Samples[sIdx--] * InterpCoeffs[coeffIdx++];
                }
                for (sIdx = LastSample; j < MaxCoverage; j++)
                {
                    Accum += Samples[sIdx--] * InterpCoeffs[coeffIdx++];
                }
                outputDataArray[outputIndex++] = Accum;
            }
            CurrIdx++; if (CurrIdx >= CoeffsLength) CurrIdx = 0;
            return InterpDecimFactor;
        }

        public int Interpolate(float[] inputSamples, int inputIndex, float[] outputDataArray, int outputIndex, int numSamples)
        {
            
            for(int l = 0; l < numSamples; l++)
            {
                Samples[CurrIdx] = inputSamples[inputIndex++];           // Save sample

                int sIdx, j;
                int coeffIdx = 0;
                int end1 = Math.Min(CurrIdx, (MaxCoverage - 1));
                for (int i = 0; i < InterpDecimFactor; i++)
                {
                    float Accum = 0;
                    for (j = 0, sIdx = CurrIdx; j <= end1; j++)
                    {
                        Accum += Samples[sIdx--] * InterpCoeffs[coeffIdx++];
                    }
                    for (sIdx = LastSample; j < MaxCoverage; j++)
                    {
                        Accum += Samples[sIdx--] * InterpCoeffs[coeffIdx++];
                    }
                    outputDataArray[outputIndex++] = Accum;
                }
                CurrIdx++; if (CurrIdx >= CoeffsLength) CurrIdx = 0;
            }
            return InterpDecimFactor * numSamples;
        }

        public int DecimateVoid()
        {
            Samples[CurrIdx++] = 0;           // Save sample
            if (CurrIdx >= CoeffsLength) CurrIdx = 0;
            SamplesProcessed++;
            if (SamplesProcessed >= InterpDecimFactor)
            {
                SamplesProcessed = 0;
                return 1;
            }
            else
                return 0;
        }

        public int Decimate(float[] inputData, float[] Result)
        {
            return Decimate(inputData, 0, Result, 0, inputData.Length);
        }

        public int Decimate(float[] inputData, int inputIndex, float[] Result, int resultIndex, int numSamples)
        {
            int OutNumber = 0;
            float Sample;
            while (numSamples-- > 0)
            {
                if (Decimate(inputData[inputIndex++], out Sample) > 0)
                {
                    Result[resultIndex++] = Sample;
                    OutNumber++;
                }
            }
            return OutNumber;
        }


        public int Decimate(float inputData, out float outputData)
        {
            int result = 0;
            outputData = 0;
            Samples[CurrIdx] = inputData;           // Save sample
            SamplesProcessed++;

            if (SamplesProcessed >= InterpDecimFactor) // Wrap-around happened
            {
                float Accum = 0;
                int sIdx, j;
                SamplesProcessed = 0;
                for (j = 0, sIdx = CurrIdx; sIdx >= 0; )
                {
                    Accum += Samples[sIdx--] * Coeffs[j++];
                }
                for (sIdx = LastSample; j < CoeffsLength; )
                {
                    Accum += Samples[sIdx--] * Coeffs[j++];
                }
                result = 1;
                outputData = Accum;
            }
            CurrIdx++; if (CurrIdx >= CoeffsLength) CurrIdx = 0;
            return result;
        }

        public int DecimateIndex
        {
            get { return SamplesProcessed; }
            set { SamplesProcessed = value; }
        }
   }

    class Filters
    {
        public static float[] Fill(double[] Data, double scale)
        {
            double Acc = 0;
            foreach (double el in Data) Acc += el;
            float[] result = new float[Data.Length];
            for (int i = 0; i < Data.Length; i++)
            {
                result[i] = (float)(Data[i] * scale /Acc);
            }
            return result;
        }

        public static double[] d24000nyq = 
            {
                  0,-0.0002460464844501,-0.0003389612251459,-0.0005232304757652,
  -0.0007208718543232,-0.0008998162561367,-0.001018130138995,-0.001028260316817,
  -0.0008834009614893,-0.0005465979855785,                 0,0.0007467196362043,
   0.001647922578822, 0.002620397964117, 0.003544214609695, 0.004271454569226,
   0.004640115999623,  0.00449418889927, 0.003707149110627, 0.002207158596563,
                   0,-0.002813505376165,-0.006028162788183,-0.009336949609433,
   -0.01234574993128, -0.01460137008467, -0.01563048977349, -0.01498680271893,
   -0.01230136795214,-0.007331197511731,                 0, 0.009574274310366,
    0.02106910049207,  0.03396723284599,  0.04758667364166,  0.06112925399886,
    0.07374351712027,  0.08459614206785,  0.09294464308768,  0.09820397812826,
                 0.1,  0.09820397812826,  0.09294464308768,  0.08459614206785,
    0.07374351712027,  0.06112925399886,  0.04758667364166,  0.03396723284599,
    0.02106910049207, 0.009574274310366,                 0,-0.007331197511731,
   -0.01230136795214, -0.01498680271893, -0.01563048977349, -0.01460137008467,
   -0.01234574993128,-0.009336949609433,-0.006028162788183,-0.002813505376165,
                   0, 0.002207158596563, 0.003707149110627,  0.00449418889927,
   0.004640115999623, 0.004271454569226, 0.003544214609695, 0.002620397964117,
   0.001647922578822,0.0007467196362043,                 0,-0.0005465979855785,
  -0.0008834009614893,-0.001028260316817,-0.001018130138995,-0.0008998162561367,
  -0.0007208718543232,-0.0005232304757652,-0.0003389612251459,-0.0002460464844501,
                   0
            };

        public static double[] d24000 = 
    {
        0.000910872857072,-0.001202659076358,-0.002791233485382,  -0.0035100999215,
        -0.003196424246184,-0.001913849425103,4.99621784361e-005, 0.002235349013293,
        0.004104216867903, 0.005157620081481, 0.005033107235571, 0.003718569838614,
        0.001357291621331,-0.001631610399827,-0.004610205092979,-0.006854650310232,
        -0.007715584463565,-0.006779799140911,-0.003999848980079,0.0002376470165934,
        0.005124446713864, 0.009525508907763,  0.01194825625385,  0.01171048460352,
        0.008366062779345, 0.002118420161942,-0.006095774147265, -0.01465198880508,
        -0.02141856303315, -0.02405462042134, -0.02038409526382, -0.01323748756763,
        0.01731812178228,   0.0603678282897,   0.1135096093325,   0.1726923899862,
        0.2326380688347,   0.2874736953089,   0.3314851687667,   0.3598811442081,
        0.3694510480363,   0.3598811442081,   0.3314851687667,   0.2874736953089,
        0.2326380688347,   0.1726923899862,   0.1135096093325,   0.0603678282897,
        0.01731812178228, -0.01323748756763, -0.02038409526382, -0.02405462042134,
        -0.02141856303315, -0.01465198880508,-0.006095774147265, 0.002118420161942,
        0.008366062779345,  0.01171048460352,  0.01194825625385, 0.009525508907763,
        0.005124446713864,0.0002376470165934,-0.003999848980079,-0.006779799140911,
        -0.007715584463565,-0.006854650310232,-0.004610205092979,-0.001631610399827,
        0.001357291621331, 0.003718569838614, 0.005033107235571, 0.005157620081481,
        0.004104216867903, 0.002235349013293,4.99621784361e-005,-0.001913849425103,
        -0.003196424246184,  -0.0035100999215,-0.002791233485382,-0.001202659076358,
        0.000910872857072
    };

        public static double[] decim_24000 = 
            {
                -0.01482539121217,-0.0009860977987585,-0.000977118659425,-0.0004750713465453,
                0.0005582048233637, 0.001548915679221,  0.00168871319009, 0.000669807392963,
                -0.0008232938227548,-0.001454630592092,-0.0002113463429767, 0.002647363254865,
                0.005553496299106, 0.006615409691426, 0.005057819399812, 0.001963123795704,
                -0.0002786798830031,0.0003826786289548, 0.004113233032908, 0.008840505614248,
                0.01153746922628,  0.01027839048715, 0.005722734179002,0.0008481049734146,
                -0.000933377670617, 0.001857471682897, 0.007605507655441,  0.01245337921672,
                0.01278760607689, 0.007694442932831, -0.00020466480526,-0.006277875288379,
                -0.006825492936844,-0.001647662429132, 0.005563385743909,  0.00934602156541,
                0.005966370039817,-0.003964781615916, -0.01547308438864, -0.02208655489303,
                -0.01999934424418,  -0.0107553044011,-0.0009794164290164, 0.001546156859457,
                -0.007161420205422, -0.02411897403544, -0.04036142684946,  -0.0459979358783,
                -0.03659608423673, -0.01705517530272,-3.172823800825e-005,0.0008830111379149,
                -0.0200891695259, -0.05537578120971, -0.08549170967555,  -0.0877652580078,
                -0.04831961616316,   0.0288681675016,   0.1211667093568,    0.195873950449,
                0.2245222325314,    0.195873950449,   0.1211667093568,   0.0288681675016,
                -0.04831961616316,  -0.0877652580078, -0.08549170967555, -0.05537578120971,
                -0.0200891695259,0.0008830111379149,-3.172823800825e-005, -0.01705517530272,
                -0.03659608423673,  -0.0459979358783, -0.04036142684946, -0.02411897403544,
                -0.007161420205422, 0.001546156859457,-0.0009794164290164,  -0.0107553044011,
                -0.01999934424418, -0.02208655489303, -0.01547308438864,-0.003964781615916,
                0.005966370039817,  0.00934602156541, 0.005563385743909,-0.001647662429132,
                -0.006825492936844,-0.006277875288379, -0.00020466480526, 0.007694442932831,
                0.01278760607689,  0.01245337921672, 0.007605507655441, 0.001857471682897,
                -0.000933377670617,0.0008481049734146, 0.005722734179002,  0.01027839048715,
                0.01153746922628, 0.008840505614248, 0.004113233032908,0.0003826786289548,
                -0.0002786798830031, 0.001963123795704, 0.005057819399812, 0.006615409691426,
                0.005553496299106, 0.002647363254865,-0.0002113463429767,-0.001454630592092,
                -0.0008232938227548, 0.000669807392963,  0.00168871319009, 0.001548915679221,
                0.0005582048233637,-0.0004750713465453,-0.000977118659425,-0.0009860977987585,
                -0.01482539121217
            };


        public static double[] rrc_05_24000 = 
            {
  0.0004451886520053,0.0004042191852315,0.0002674727068399,5.756567993179e-005,
  -0.0001849378497112,-0.0004092346891623,-0.0005648131453822,-0.0006126925416244,
  -0.0005349511529163,-0.0003403189203405,-6.430502751188e-005,0.0002365929161655,
   0.000495795656809,0.0006506845894532,0.0006569574194782,0.0005001757311984,
  0.0002017321931508,-0.0001825605430358,-0.000571615173291,-0.0008746639552588,
  -0.001010507575187,-0.0009265784007801,-0.000613655162573,-0.0001125978562075,
  0.0004891777252042, 0.001071805138282, 0.001505751553351, 0.001679337935001,
   0.001525624514216, 0.001042830577909, 0.000303152272556,-0.0005533353296819,
  -0.001340309982572,-0.001859868234964, -0.00194476173959,-0.001500527193595,
  -0.0005388788035434,0.0008056525910254, 0.002281624415831, 0.003551346769221,
   0.004244131815784, 0.004025481153629, 0.002671818654866, 0.000138102165167,
  -0.003394615682795,-0.007502635967976, -0.01154097789764, -0.01470396220394,
   -0.01611999560954, -0.01496951289634, -0.01061032953946,-0.002692141246963,
   0.008757875030779,   0.0232933278703,  0.04006012210124,  0.05786324696326,
    0.07528286479934,  0.09082627414475,   0.1030966113163,   0.1109561185655,
     0.1136619772368,   0.1109561185655,   0.1030966113163,  0.09082627414475,
    0.07528286479934,  0.05786324696326,  0.04006012210124,   0.0232933278703,
   0.008757875030779,-0.002692141246963, -0.01061032953946, -0.01496951289634,
   -0.01611999560954, -0.01470396220394, -0.01154097789764,-0.007502635967976,
  -0.003394615682795, 0.000138102165167, 0.002671818654866, 0.004025481153629,
   0.004244131815784, 0.003551346769221, 0.002281624415831,0.0008056525910254,
  -0.0005388788035434,-0.001500527193595, -0.00194476173959,-0.001859868234964,
  -0.001340309982572,-0.0005533353296819, 0.000303152272556, 0.001042830577909,
   0.001525624514216, 0.001679337935001, 0.001505751553351, 0.001071805138282,
  0.0004891777252042,-0.0001125978562075,-0.000613655162573,-0.0009265784007801,
  -0.001010507575187,-0.0008746639552588,-0.000571615173291,-0.0001825605430358,
  0.0002017321931508,0.0005001757311984,0.0006569574194782,0.0006506845894532,
   0.000495795656809,0.0002365929161655,-6.430502751188e-005,-0.0003403189203405,
  -0.0005349511529163,-0.0006126925416244,-0.0005648131453822,-0.0004092346891623,
  -0.0001849378497112,5.756567993179e-005,0.0002674727068399,0.0004042191852315,
  0.0004451886520053
            };

        public static double[] rrc = 
        {

  -4.972795476272e-035,-1.364747269443e-005,-5.465102970855e-005,-0.0001179451532648,
  -0.0001918787049844,-0.0002598315486745,-0.0003031717593917,-0.0003050916986321,
  -0.0002546082816139,-0.0001498775867873,2.59878122168e-019,0.0001753047700814,
  0.0003484113327348,0.0004886808378384,0.0005688272809992,0.0005716294070839,
  0.0004956011470986, 0.000358215443425,0.0001955371257754,5.765010298693e-005,
  8.95103185729e-035,7.160588667802e-005,0.0003018818783901,0.0006883988357217,
   0.001188171980865, 0.001714888221252, 0.002143857441413, 0.002325431375193,
   0.002106304875169,  0.00135668412975,-1.29939061084e-018,-0.001959107693716,
   -0.00441321021464,  -0.0071413107311,-0.009812270597235, -0.01200421754876,
   -0.01323963064392, -0.01303353113385, -0.01095007904342,-0.006661207354217,
  3.061616997868e-018, 0.008998473092538,  0.02007514491294,  0.03274811604219,
    0.04633870725372,  0.06002108774381,  0.07289115300803,   0.0840477339891,
    0.09267741450744,  0.09813348538525,               0.1,  0.09813348538525,
    0.09267741450744,   0.0840477339891,  0.07289115300803,  0.06002108774381,
    0.04633870725372,  0.03274811604219,  0.02007514491294, 0.008998473092538,
  3.061616997868e-018,-0.006661207354217, -0.01095007904342, -0.01303353113385,
   -0.01323963064392, -0.01200421754876,-0.009812270597235,  -0.0071413107311,
   -0.00441321021464,-0.001959107693716,-1.29939061084e-018,  0.00135668412975,
   0.002106304875169, 0.002325431375193, 0.002143857441413, 0.001714888221252,
   0.001188171980865,0.0006883988357217,0.0003018818783901,7.160588667802e-005,
  8.95103185729e-035,5.765010298693e-005,0.0001955371257754, 0.000358215443425,
  0.0004956011470986,0.0005716294070839,0.0005688272809992,0.0004886808378384,
  0.0003484113327348,0.0001753047700814,2.59878122168e-019,-0.0001498775867873,
  -0.0002546082816139,-0.0003050916986321,-0.0003031717593917,-0.0002598315486745,
  -0.0001918787049844,-0.0001179451532648,-5.465102970855e-005,-1.364747269443e-005,
  -4.972795476272e-035
        };

        public static double[] rrc_180 = 
        {
  -1.094977248654e-005,8.032374026925e-005,0.0001556662131955, 0.000197568387139,
  0.0001951965191072, 0.000147110509176,6.202985856947e-005,-4.261366420897e-005,
  -0.0001441227128519,-0.0002193160406314,-0.0002496548126932,-0.0002255989591495,
  -0.0001491933699557,-3.422331667266e-005,9.621999115969e-005,0.0002143610276565,
  0.0002936438851384,0.0003146839455096,0.0002699564567597,0.0001661182944401,
  2.331940558123e-005,-0.0001285132078122,-0.0002557713608766,-0.0003284336652296,
  -0.0003269703808889,-0.0002473396472959,-0.0001028569691097,7.766690889759e-005,
  0.0002553421969212,0.0003892014514433,0.0004451886520053,0.0004042191852315,
  0.0002674727068399,5.756567993179e-005,-0.0001849378497112,-0.0004092346891623,
  -0.0005648131453822,-0.0006126925416244,-0.0005349511529163,-0.0003403189203405,
  -6.430502751188e-005,0.0002365929161655, 0.000495795656809,0.0006506845894532,
  0.0006569574194782,0.0005001757311984,0.0002017321931508,-0.0001825605430358,
  -0.000571615173291,-0.0008746639552588,-0.001010507575187,-0.0009265784007801,
  -0.000613655162573,-0.0001125978562075,0.0004891777252042, 0.001071805138282,
   0.001505751553351, 0.001679337935001, 0.001525624514216, 0.001042830577909,
   0.000303152272556,-0.0005533353296819,-0.001340309982572,-0.001859868234964,
   -0.00194476173959,-0.001500527193595,-0.0005388788035434,0.0008056525910254,
   0.002281624415831, 0.003551346769221, 0.004244131815784, 0.004025481153629,
   0.002671818654866, 0.000138102165167,-0.003394615682795,-0.007502635967976,
   -0.01154097789764, -0.01470396220394, -0.01611999560954, -0.01496951289634,
   -0.01061032953946,-0.002692141246963, 0.008757875030779,   0.0232933278703,
    0.04006012210124,  0.05786324696326,  0.07528286479934,  0.09082627414475,
     0.1030966113163,   0.1109561185655,   0.1136619772368,   0.1109561185655,
     0.1030966113163,  0.09082627414475,  0.07528286479934,  0.05786324696326,
    0.04006012210124,   0.0232933278703, 0.008757875030779,-0.002692141246963,
   -0.01061032953946, -0.01496951289634, -0.01611999560954, -0.01470396220394,
   -0.01154097789764,-0.007502635967976,-0.003394615682795, 0.000138102165167,
   0.002671818654866, 0.004025481153629, 0.004244131815784, 0.003551346769221,
   0.002281624415831,0.0008056525910254,-0.0005388788035434,-0.001500527193595,
   -0.00194476173959,-0.001859868234964,-0.001340309982572,-0.0005533353296819,
   0.000303152272556, 0.001042830577909, 0.001525624514216, 0.001679337935001,
   0.001505751553351, 0.001071805138282,0.0004891777252042,-0.0001125978562075,
  -0.000613655162573,-0.0009265784007801,-0.001010507575187,-0.0008746639552588,
  -0.000571615173291,-0.0001825605430358,0.0002017321931508,0.0005001757311984,
  0.0006569574194782,0.0006506845894532, 0.000495795656809,0.0002365929161655,
  -6.430502751188e-005,-0.0003403189203405,-0.0005349511529163,-0.0006126925416244,
  -0.0005648131453822,-0.0004092346891623,-0.0001849378497112,5.756567993179e-005,
  0.0002674727068399,0.0004042191852315,0.0004451886520053,0.0003892014514433,
  0.0002553421969212,7.766690889759e-005,-0.0001028569691097,-0.0002473396472959,
  -0.0003269703808889,-0.0003284336652296,-0.0002557713608766,-0.0001285132078122,
  2.331940558123e-005,0.0001661182944401,0.0002699564567597,0.0003146839455096,
  0.0002936438851384,0.0002143610276565,9.621999115969e-005,-3.422331667266e-005,
  -0.0001491933699557,-0.0002255989591495,-0.0002496548126932,-0.0002193160406314,
  -0.0001441227128519,-4.261366420897e-005,6.202985856947e-005, 0.000147110509176,
  0.0001951965191072, 0.000197568387139,0.0001556662131955,8.032374026925e-005,
  -1.094977248654e-005
        };

        public static double[] interp_24000 = 
            {
  0.0003024034947894,-0.0002028361833849,-0.0003758279156737,-0.0003786884620388,
  -8.369691588726e-005,0.0003457421018625,0.0005379979943931,0.0002370740390836,
  -0.0003974618658848,-0.0008284506251053,-0.0005735458892037,0.0003011232543834,
   0.001104556152436,  0.00104343335504,-2.887776951664e-005,-0.001318347688077,
  -0.001643700213071,-0.0004861898610156, 0.001371463088544, 0.002319949254368,
   0.001283890728493,-0.001153401486208,-0.002978937243208,-0.002372894899251,
  0.0005472609914908, 0.003483880751285, 0.003714031035639,0.0005520501505903,
   -0.00366022806047,-0.005209721994014,-0.002221189212989, 0.003303666036785,
   0.006696317913743, 0.004490291364634,-0.002194333791262,-0.007942529929099,
  -0.007329007417154,0.0001072832604312, 0.008647548681653,  0.01064043957486,
   0.003187398463581,-0.008436126007072, -0.01426099134937,-0.007945724576866,
   0.006826108569295,   0.0179719262669,  0.01453601988876, -0.00312425690069,
   -0.02151717831981, -0.02369123820627,-0.003919584779841,  0.02463013429385,
    0.03734987504778,  0.01741554691684, -0.02706170859548, -0.06250946166187,
   -0.04958691167931,  0.02860924990091,   0.1496317965726,   0.2597931620277,
     0.3041930247899,   0.2597931620277,   0.1496317965726,  0.02860924990091,
   -0.04958691167931, -0.06250946166187, -0.02706170859548,  0.01741554691684,
    0.03734987504778,  0.02463013429385,-0.003919584779841, -0.02369123820627,
   -0.02151717831981, -0.00312425690069,  0.01453601988876,   0.0179719262669,
   0.006826108569295,-0.007945724576866, -0.01426099134937,-0.008436126007072,
   0.003187398463581,  0.01064043957486, 0.008647548681653,0.0001072832604312,
  -0.007329007417154,-0.007942529929099,-0.002194333791262, 0.004490291364634,
   0.006696317913743, 0.003303666036785,-0.002221189212989,-0.005209721994014,
   -0.00366022806047,0.0005520501505903, 0.003714031035639, 0.003483880751285,
  0.0005472609914908,-0.002372894899251,-0.002978937243208,-0.001153401486208,
   0.001283890728493, 0.002319949254368, 0.001371463088544,-0.0004861898610156,
  -0.001643700213071,-0.001318347688077,-2.887776951664e-005,  0.00104343335504,
   0.001104556152436,0.0003011232543834,-0.0005735458892037,-0.0008284506251053,
  -0.0003974618658848,0.0002370740390836,0.0005379979943931,0.0003457421018625,
  -8.369691588726e-005,-0.0003786884620388,-0.0003758279156737,-0.0002028361833849,
  0.0003024034947894

            
            };

        public static float[] delay_0_125 =
            {
        0,	0.01727343f,	-0.08156896f,	0.9788275f,	0.1048744f,	-0.01957655f,	0
            };
        public static float[] delay_0_250 =
            { 
        -0.004f,	0.03464584f,	-0.15465f,	1.02897f,	0.25721f,	-0.04401f,	0.00467f
            };
        public static float [] delay_0_333 = 
            {
        -0.005f,	0.042858f,	-0.18749f,	0.99999f,	0.37478f,	-0.06f,	0.0063f
            };

        public static float[] RaisedCosine(int symbolsize, int symbolsnum, float b)
        {
            int N = symbolsnum;
            int len = symbolsize * N;
            int halflen = len / 2;
            float[] result = new float[len];
            for (int i = 0; i < len; i++)
            {
                double t = (i - halflen) / (double)(symbolsize);
                double sinc = (t == 0) ? 1 : Math.Sin(Math.PI * t) / (Math.PI * t);
                double den = (1 - (2 * b * t) * (2 * b * t));
                if (den == 0) den = 1;
                result[i] = (float)((Math.Cos(Math.PI * b * t) / den) * sinc);
            }
            return result;
        }

        public static float[] SQRTRaisedCosine(int symbolsize, int symbolsnum, float b)
        {
            int N = symbolsnum;
            int len = symbolsize * N;
            int halflen = len / 2;
            float[] result = new float[len];
            for (int i = 0; i < len; i++)
            {
                double t = (i - halflen) / (double)(symbolsize);
                double sinf = (t == 0) ? 1 : Math.Sin((1 - b) * Math.PI * t) / (4 * t);
                double cosf = 2 * b * Math.Cos((1 + b) * Math.PI * t);
                double den = Math.PI * (1 - (4 * b * t) * (4 * b * t));
                if (den == 0) den = 1;
                result[i] = (float)((cosf + sinf) / den);
            }
            return result;
        }

        public static double[] FIR100Hz =
        {
    0.01021280233,  0.03114883602,  0.02503223345,  0.04098343104,  0.04895215109,
    0.06004384533,  0.06887367368,  0.07663602382,  0.08200938255,  0.08485817909,
    0.08485817909,  0.08200938255,  0.07663602382,  0.06887367368,  0.06004384533,
    0.04895215109,  0.04098343104,  0.02503223345,  0.03114883602,  0.01021280233
        };

    }

    class IIRFilter100Hz
    {
        IQ IC00, IC01;
        IQ IC10, IC11;

        public void Init()
        {
            IC00 = IQ.ZERO; IC01 = IQ.ZERO;
            IC10 = IQ.ZERO; IC11 = IQ.ZERO;
        }

        // requires output array y be already created by the calling function
        public void Process(IQ x, out IQ y)
        {
            IQ t;
            IQ xi;

            xi = x;
            // Stage 0
            t = (0.007434780f * xi) - (-1.9125397f * IC00) - (0.9230745f * IC01);
            xi = (0.003538524f * t) + (0.007077f * IC00) + (0.0035385242f * IC01);
            IC01 = IC00;
            IC00 = t;
            // Stage 1
            t = xi - (-1.81387480f * IC10) - (0.823866132f * IC11);
            xi = (0.25f * t) + (0.5f * IC10) + (0.25f * IC11);
            IC11 = IC10;
            IC10 = t;
            y = xi;
        }
    }

    class FIRIQ
    {
                                    // Array of the coefficients
        float[] Coeffs;             //  for regular processing 
        IQ[] Samples;               // Array that keeps all samples for 1 coeff run
        int CurrIdx;

        int CoeffsLength;           // Number of coefficients
        int LastSample;             //  that number - 1

        public FIRIQ(float[] coeffs)
        {
            if (coeffs == null)
            {
                coeffs = new float[1];
                coeffs[0] = 1.0f;
            }
            this.CoeffsLength = coeffs.Length;
            this.LastSample = coeffs.Length - 1;

            this.Coeffs = new float[CoeffsLength];
            coeffs.CopyTo(this.Coeffs, 0);
            this.Samples = new IQ[CoeffsLength];
        }

        public void Clear()
        {
            Array.Clear(this.Samples, 0, Samples.Length);
            CurrIdx = 0;
        }

        public void Init()
        {
            this.Clear();
        }

        public int ProcessVoid()
        {
            Array.Copy(Samples, 1, Samples, 0, LastSample);
            Samples[LastSample] = IQ.ZERO;           // Put the latest sample last
            return 1;
        }

        public int Process(IQ Sample, out IQ outData)
        {
            Array.Copy(Samples, 1, Samples, 0, LastSample); // Shift all samples by 1
            Samples[LastSample] = Sample;                   // Insert a new sample
            IQ Accum = IQ.ZERO;
            for (int j = 0, sIdx = LastSample; j < CoeffsLength; j++, sIdx--)
            {
                Accum += Coeffs[j] * Samples[sIdx];
            }
            outData = Accum;
            return 1;
        }

        public int Process(ref IQ inOutSample)
        {
            int j, sIdx;
            Samples[CurrIdx] = inOutSample;                   // Insert a new sample
            IQ Accum = IQ.ZERO;
            for (j = 0, sIdx = CurrIdx; sIdx >= 0; )
            {
                Accum += Coeffs[j++] * Samples[sIdx--];
            }
            for (sIdx = LastSample; j < CoeffsLength; )
            {
                Accum += Coeffs[j++] * Samples[sIdx--];
            }
            CurrIdx++; if (CurrIdx >= CoeffsLength) CurrIdx = 0;
            inOutSample = Accum;
            return 1;
        }

    }
}
