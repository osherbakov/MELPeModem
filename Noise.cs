using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    class Noise
    {
        // We use Crypto library noise generation
        System.Security.Cryptography.RNGCryptoServiceProvider NoiseGen;
        float Amplitude;
        byte[] RandomNoiseBytes;
        float[] Result;
        int BuffSize;

        public Noise(int size, float Amp)
        {
            BuffSize = size;
            Amplitude = Amp;
            NoiseGen = new System.Security.Cryptography.RNGCryptoServiceProvider();
            RandomNoiseBytes = new byte[size];
            Result = new float[size];
        }
        public float[] Add(float[] I, float[] Q)
        {
            NoiseGen.GetBytes(RandomNoiseBytes);
            for (int i = 0; i < BuffSize; i++)
            {
                Result[i] = I[i] + Q[i] + Amplitude * Samples.ToFloat(RandomNoiseBytes[i]);
            }
            return Result;
        }
        public void Add(float[] result, float[] I, float[] Q)
        {
            NoiseGen.GetBytes(RandomNoiseBytes);
            for (int i = 0; i < BuffSize; i++)
            {
                result[i] += I[i] + Q[i] + Amplitude * Samples.ToFloat(RandomNoiseBytes[i]);
            }
        }

        public void Add(float[] result)
        {
            NoiseGen.GetBytes(RandomNoiseBytes);
            for (int i = 0; i < BuffSize; i++)
            {
                result[i] += Amplitude * Samples.ToFloat(RandomNoiseBytes[i]);
            }
        }

    }
}
