using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{

    /*============================================================================

        fourierd.c  -  Don Cross <dcross@intersrv.com>

        http://www.intersrv.com/~dcross/fft.html

        Contains definitions for doing Fourier transforms
        and inverse Fourier transforms.

        This module performs operations on arrays of 'double'.

        Revision history:

    1998 September 19 [Don Cross]
        Updated coding standards.
        Improved efficiency of trig calculations.

    ============================================================================*/
    class FFT
    {
        int NumSamples;
        int NumBitsNeeded;
        double[] st1;
        double[] st2;
        double[] ct1;
        double[] ct2;

        float[] fst1;
        float[] fst2;
        float[] fct1;
        float[] fct2;

        IQ[] T1;
        IQ[] T2;

        int[] ReverseBits_Table;

        public FFT(int numberOfSamples)
        {
            NumSamples = numberOfSamples;
            NumBitsNeeded = NumberOfBitsNeeded(NumSamples);

            st1 = new double[NumBitsNeeded+1];
            st2 = new double[NumBitsNeeded+1];
            ct1 = new double[NumBitsNeeded+1];
            ct2 = new double[NumBitsNeeded+1];

            fst1 = new float[NumBitsNeeded + 1];
            fst2 = new float[NumBitsNeeded + 1];
            fct1 = new float[NumBitsNeeded + 1];
            fct2 = new float[NumBitsNeeded + 1];

            T1 = new IQ[NumBitsNeeded + 1];
            T2 = new IQ[NumBitsNeeded + 1];

            ReverseBits_Table = new int[NumSamples];

            int TwiddlesIndex = 0;
            for (int BlockSize = 2; BlockSize <= 2 * NumSamples; BlockSize <<= 1)
            {
                Twiddles(BlockSize, out st2[TwiddlesIndex], out st1[TwiddlesIndex], out ct2[TwiddlesIndex], out ct1[TwiddlesIndex]);
                fst2[TwiddlesIndex] = (float)st2[TwiddlesIndex];
                fst1[TwiddlesIndex] = (float)st1[TwiddlesIndex];
                fct2[TwiddlesIndex] = (float)ct2[TwiddlesIndex];
                fct1[TwiddlesIndex] = (float)ct1[TwiddlesIndex];
                T1[TwiddlesIndex] = new IQ((float)ct1[TwiddlesIndex], (float)st1[TwiddlesIndex]);
                T2[TwiddlesIndex] = new IQ((float)ct2[TwiddlesIndex], (float)st2[TwiddlesIndex]);

                TwiddlesIndex++;
            }

            for (int i = 0; i < NumSamples; i++)
            {
                ReverseBits_Table[i] = ReverseBits(i, NumBitsNeeded);
            }
        }

        void Twiddles(int BlockSize, out double sm2, out double sm1, out double cm2, out double cm1)
        {
            double angle_numerator = 2.0 * Math.PI;
            double delta_angle = (BlockSize == 0) ? 0 : angle_numerator / (double)BlockSize;
            sm2 = Math.Sin(-2 * delta_angle);
            sm1 = Math.Sin(-delta_angle);
            cm2 = Math.Cos(-2 * delta_angle);
            cm1 = Math.Cos(-delta_angle);
        }

        public void ProcessIFFT(
            double[] RealIn,
            double[] ImagIn,
            double[] RealOut,
            double[] ImagOut)
        {
            ProcessFFT(ImagIn, RealIn, ImagOut, RealOut);
            /*
            **   Need to normalize if inverse transform...
            */
            double denom = (double)NumSamples;

            for (int i = 0; i < NumSamples; i++)
            {
                RealOut[i] /= denom;
                ImagOut[i] /= denom;
            }
        }

        public void ProcessIFFT(
            IQ[] dataIn,
            IQ[] dataOut)
        {
            int i, j, k, n;
            int BlockSize, BlockEnd;

            IQ T;   // Temporary vriable for swapping

            /*
            **   Do simultaneous data copy and bit-reversal ordering into outputs...
             *    For IFFT swap Real and Img parts
            */
            for (i = 0; i < NumSamples; i++)
            {
                j = ReverseBits_Table[i];
                dataOut[j].I = dataIn[i].Q;
                dataOut[j].Q = dataIn[i].I;
            }

            /*
            **   Do the FFT itself...
            */

            BlockEnd = 1;
            int TwiddlesIndex = 0;
            for (BlockSize = 2; BlockSize <= NumSamples; BlockSize <<= 1)
            {
                IQ M1 = T1[TwiddlesIndex];
                IQ M2 = T2[TwiddlesIndex];

                float w = 2 * fct1[TwiddlesIndex];

                IQ A0, A1, A2;

                for (i = 0; i < NumSamples; i += BlockSize)
                {
                    A2 = M2;
                    A1 = M1;
                    for (j = i, n = 0, k = i + BlockEnd; n < BlockEnd; j++, n++, k++)
                    {
                        A0 = (w * A1) - A2;
                        A2 = A1;
                        A1 = A0;

                        T = A0 * dataOut[k];
                        dataOut[k] = dataOut[j] - T;
                        dataOut[j] += T;
                    }
                }
                TwiddlesIndex++;
                BlockEnd = BlockSize;
            }
            /*
            **   Need to normalize if inverse transform...
            */
            float norm = 1.0f / 2.0f;    // (float)Math.Sqrt(1.0 / 2);
            float t;
            for (i = 0; i < NumSamples; i++)
            {
                // Swap Real and Img parts
                t = dataOut[i].Q;
                dataOut[i].Q = dataOut[i].I * norm;
                dataOut[i].I = t * norm;
            }
        }

        public void ProcessFFT(
            double[] RealIn,
            double[] ImagIn,
            double[] RealOut,
            double[] ImagOut)
        {
            int i, j, k, n;
            int BlockSize, BlockEnd;

            double tr, ti;     /* temp real, temp imaginary */
            /*
            **   Do simultaneous data copy and bit-reversal ordering into outputs...
            */

            for (i = 0; i < NumSamples; i++)
            {
                j = ReverseBits_Table[i];
                RealOut[j] = RealIn[i];
                ImagOut[j] = (ImagIn == null) ? 0.0 : ImagIn[i];
            }

            /*
            **   Do the FFT itself...
            */

            BlockEnd = 1;
            int TwiddlesIndex = 0;
            for (BlockSize = 2; BlockSize <= NumSamples; BlockSize <<= 1)
            {
                double sm1, sm2, cm1, cm2;
                sm2 = st2[TwiddlesIndex];
                sm1 = st1[TwiddlesIndex];
                cm2 = ct2[TwiddlesIndex];
                cm1 = ct1[TwiddlesIndex];

                double w = 2 * cm1;
                double ar0, ar1, ar2, ai0, ai1, ai2;

                for (i = 0; i < NumSamples; i += BlockSize)
                {
                    ar2 = cm2;
                    ar1 = cm1;

                    ai2 = sm2;
                    ai1 = sm1;

                    for (j = i, n = 0; n < BlockEnd; j++, n++)
                    {
                        ar0 = w * ar1 - ar2;
                        ar2 = ar1;
                        ar1 = ar0;

                        ai0 = w * ai1 - ai2;
                        ai2 = ai1;
                        ai1 = ai0;

                        k = j + BlockEnd;
                        tr = ar0 * RealOut[k] - ai0 * ImagOut[k];
                        ti = ar0 * ImagOut[k] + ai0 * RealOut[k];

                        RealOut[k] = RealOut[j] - tr;
                        ImagOut[k] = ImagOut[j] - ti;

                        RealOut[j] += tr;
                        ImagOut[j] += ti;
                    }
                }
                TwiddlesIndex++;
                BlockEnd = BlockSize;
            }
        }

        public void ProcessFFT(
            IQ[] dataIn,
            IQ[] dataOut
            )
        {
            int i, j, k, n;
            int BlockSize, BlockEnd;

            IQ T;   // Temporary vriable for swapping

            /*
            **   Do simultaneous data copy and bit-reversal ordering into outputs...
            */
            float norm = 2.0f / NumSamples;
            for (i = 0; i < NumSamples; i++)
            {
                j = ReverseBits_Table[i];
                dataOut[j] = dataIn[i] * norm;
            }

            /*
            **   Do the FFT itself...
            */

            BlockEnd = 1;
            int TwiddlesIndex = 0;
            for (BlockSize = 2; BlockSize <= NumSamples; BlockSize <<= 1)
            {
                IQ M1 = T1[TwiddlesIndex];
                IQ M2 = T2[TwiddlesIndex];

                float w = 2 * fct1[TwiddlesIndex];

                IQ A0, A1, A2;

                for (i = 0; i < NumSamples; i += BlockSize)
                {
                    A2 = M2;
                    A1 = M1;
                    for (j = i, n = 0, k = i + BlockEnd; n < BlockEnd; j++, n++, k++)
                    {
                        A0 = (w * A1) - A2;
                        A2 = A1;
                        A1 = A0;

                        T = A0 * dataOut[k];
                        dataOut[k] = dataOut[j] - T;
                        dataOut[j] += T;
                    }
                }
                TwiddlesIndex++;
                BlockEnd = BlockSize;
            }
        }


        public void ProcessFFT(
            IQ[] dataIn,
            int startingIndex,
            IQ[] dataOut
            )
        {
            int i, j, k, n;
            int BlockSize, BlockEnd;

            IQ T;   // Temporary vriable for swapping

            /*
            **   Do simultaneous data copy and bit-reversal ordering into outputs...
            */
            float norm = 2.0f / NumSamples;
            for (i = 0; i < NumSamples; i++)
            {
                j = ReverseBits_Table[i];
                dataOut[j] = dataIn[startingIndex++] * norm;
            }

            /*
            **   Do the FFT itself...
            */

            BlockEnd = 1;
            int TwiddlesIndex = 0;
            for (BlockSize = 2; BlockSize <= NumSamples; BlockSize <<= 1)
            {
                IQ M1 = T1[TwiddlesIndex];
                IQ M2 = T2[TwiddlesIndex];

                float w = 2 * fct1[TwiddlesIndex];

                IQ A0, A1, A2;

                for (i = 0; i < NumSamples; i += BlockSize)
                {
                    A2 = M2;
                    A1 = M1;
                    for (j = i, n = 0, k = i + BlockEnd; n < BlockEnd; j++, n++, k++)
                    {
                        A0 = (w * A1) - A2;
                        A2 = A1;
                        A1 = A0;

                        T = A0 * dataOut[k];
                        dataOut[k] = dataOut[j] - T;
                        dataOut[j] += T;
                    }
                }
                TwiddlesIndex++;
                BlockEnd = BlockSize;
            }
        }

        public static int NumberOfBitsNeeded(int PowerOfTwo)
        {
            int i;
            for (i = 0; ; i++)
            {
                if ((PowerOfTwo & (1 << i)) != 0 )
                    return i;
            }
        }

        static int ReverseBits(int index, int NumBits)
        {
            int i, rev;

            for (i = rev = 0; i < NumBits; i++)
            {
                rev = (rev << 1) | (index & 1);
                index >>= 1;
            }

            return rev;
        }

        public static double Index_to_frequency(int NumSamples, int Index)
        {
            if (Index >= NumSamples)
                return 0.0;
            else if (Index <= NumSamples / 2)
                return (double)Index / (double)NumSamples;

            return -(double)(NumSamples - Index) / (double)NumSamples;
        }

        public double Index_to_frequency(int Index)
        {
            if (Index >= NumSamples)
                return 0.0;
            else if (Index <= NumSamples / 2)
                return (double)Index / (double)NumSamples;

            return -(double)(NumSamples - Index) / (double)NumSamples;
        }
    }
}
