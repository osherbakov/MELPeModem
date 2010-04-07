using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{

    class Quad
    {
        double I0, I1, Q0, Q1, Coeff;
        double DeltaPhase;

        public Quad()
        {
            I0 = 1;
            Q0 = 0;
            I1 = 1;
            Q1 = 0;
            Coeff = 0;
            DeltaPhase = 0;
        }
        public Quad(IQ initialVector, IQ delta, int offset)
        {
            double initialPhase = initialVector.Phase;
            double initialR = initialVector.R;
            DeltaPhase = delta.Phase;
            Coeff = 2 * Math.Sin(DeltaPhase);

            double newPhase = initialPhase + DeltaPhase * offset;
            I0 = initialR * Math.Cos(newPhase);
            Q0 = initialR * Math.Sin(newPhase);
            I1 = initialR * Math.Cos(newPhase + DeltaPhase);
            Q1 = initialR * Math.Sin(newPhase + DeltaPhase);
        }

        public Quad(IQ initialVector, IQ delta)
        {
            double initialPhase = initialVector.Phase;
            double initialR = initialVector.R;
            DeltaPhase = delta.Phase;
            Coeff = 2 * Math.Sin(DeltaPhase);

            I0 = initialVector.I;
            Q0 = initialVector.Q;
            I1 = initialR * Math.Cos(initialPhase + DeltaPhase);
            Q1 = initialR * Math.Sin(initialPhase + DeltaPhase);
       }

       public Quad(float outputFreq, float sampleFreq)
       {
           DeltaPhase = 2 * Math.PI * outputFreq / sampleFreq;
           Coeff = 2 * Math.Sin(DeltaPhase);

           double initialPhase = 0 ;
           I0 = 1;
           Q0 = 0;
           I1 = Math.Cos(initialPhase + DeltaPhase);
           Q1 = Math.Sin(initialPhase + DeltaPhase);
       }

        public Quad(float outputFreq, float sampleFreq, double initialPhase)
        {
            DeltaPhase = 2 * Math.PI * outputFreq / sampleFreq;
            Coeff = 2 * Math.Sin(DeltaPhase);

            I0 = Math.Cos(initialPhase);
            Q0 = Math.Sin(initialPhase);
            I1 = Math.Cos(initialPhase + DeltaPhase);
            Q1 = Math.Sin(initialPhase + DeltaPhase);
        }

        public Quad(float outputFreq, float sampleFreq, Quad initialQuad)
        {
            DeltaPhase = 2 * Math.PI * outputFreq / sampleFreq;
            Coeff = 2 * Math.Sin(DeltaPhase);

            float initialPhase = initialQuad.Value.Phase;
            I0 = initialQuad.Value.I;
            Q0 = initialQuad.Value.Q;
            I1 = Math.Cos(initialPhase + DeltaPhase);
            Q1 = Math.Sin(initialPhase + DeltaPhase);
        }
       
        public static Quad operator ++(Quad a)
        {            
            double Q2 = a.Q0 + a.Coeff * a.I1;
            double I2 = a.I0 - a.Coeff * a.Q1;
            a.I0 = a.I1; a.I1 = I2;
            a.Q0 = a.Q1; a.Q1 = Q2;
            return a;
        }
        public IQ Next()
        {
            IQ result = new IQ((float)I0, (float)Q0);
            double Q2 = Q0 + Coeff * I1;
            double I2 = I0 - Coeff * Q1;
            I0 = I1; I1 = I2;
            Q0 = Q1; Q1 = Q2;
            return result;
        }

        public int Process(float data, out IQ outIQ)
        {
            outIQ.I = (float)I0 * data; 
            outIQ.Q = (float)Q0 * data; 
            double Q2 = Q0 + Coeff * I1;
            double I2 = I0 - Coeff * Q1;
            I0 = I1; I1 = I2;
            Q0 = Q1; Q1 = Q2;
            return 1;
        }
        public static implicit operator IQ(Quad a)
        {
            return new IQ((float)a.I0, (float)a.Q0);
        }

        public static IQ operator +(IQ a, Quad b)
        {
            return a + b.Value;
        }

        public static IQ operator +(Quad b, IQ a )
        {
            return a + b.Value;
        }

        public IQ Value
        {
            get { return new IQ((float)I0, (float)Q0); }
            set {
                double initialPhase = value.Phase;
                value = value/value.R;
                I0 = value.I;
                Q0 = value.Q;
                I1 = Math.Cos(initialPhase + DeltaPhase);
                Q1 = Math.Sin(initialPhase + DeltaPhase);
            }
        }

        public IQ NextValue
        {
            get { return new IQ((float)I1, (float)Q1); }
        }
    }


    class Generator
    {
        double OutputFrequency;
        double SamplingFrequency;
        double DeltaPhase;

        double S0, S1, Coeff;

        public Generator()
        {
        }

        public Generator(float FreqDest, float FreqSample) : this(FreqDest, FreqSample, 0)
        {
        }

        public Generator(float FreqDest, float FreqSample, float PhaseDest)
        {
            this.SamplingFrequency = FreqSample;
            this.OutputFrequency = FreqDest;
            this.DeltaPhase = (2 * Math.PI * FreqDest) / SamplingFrequency;
            this.Coeff = 2 * Math.Cos(DeltaPhase);
            this.Phase = PhaseDest;
        }

        /// <summary>
        /// Initializes the generator.
        /// </summary>
        /// <param name="FreqDest">Desired frequency in Hertz</param>
        /// <param name="FreqSample">Sampling frequency in Hertz</param>
        /// <param name="PhaseDest">Starting phase in radians</param>
        public void Init(float FreqDest, float FreqSample, float PhaseDest)
        {
            this.SamplingFrequency = FreqSample;
            this.OutputFrequency = FreqDest;
            this.DeltaPhase = (2 * Math.PI * FreqDest) / SamplingFrequency;
            this.Coeff = 2 * Math.Cos(DeltaPhase);
            this.Phase = PhaseDest;
        }

        public float Frequency
        {
            get { return (float)this.OutputFrequency; }
            set
            {
                float CurrentPhase = Phase;
                this.OutputFrequency = value;
                this.DeltaPhase = (2 * Math.PI * value) / SamplingFrequency;
                this.Coeff = 2 * Math.Cos(DeltaPhase);
                this.Phase = CurrentPhase;
            }
        }
        public float Phase
        {
            get 
            {
                S0 = Math.Max(Math.Min(S0, 1.0), -1.0);
                double ph0 = Math.Asin(S0);
                // Now we have to resolve a phase ambiguity
                // Use the formula : cos(w) = (S1 - Cos(D)S0)/sin(D)
                // cos(D) = Coeff/2, sin(D) is always positive
                double CosW = S1 - Coeff * S0 / 2.0;
                if (CosW < 0)
                    ph0 = Math.PI - ph0;
                if (ph0 < 0)
                    ph0 += (2 * Math.PI);
                return (float) ph0;
            }
            set
            {
                S0 = Math.Sin(value);
                S1 = Math.Sin(value + DeltaPhase);
            }
        }

        public void SetFrequency(float newFrequency)
        {
            Frequency = newFrequency;
        }

        public void SetPhase(float newPhase)
        {
            Phase = newPhase;
        }

        public int GenerateVoid(int NumSamples)
        {
            double OldValue;
            for (int i = 0; i < NumSamples; i++)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
            }
            return NumSamples;
        }

        public int Generate(float Value, float[] outputBuffer, int NumSamples)
        {
            double OldValue;
            for (int i = 0; i < NumSamples; i++)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
                outputBuffer[i] = (float)OldValue * Value;
            }
            return NumSamples;
        }

        public int Generate(float[] outputBuffer, int NumSamples)
        {
            double OldValue;
            for (int i = 0; i < NumSamples; i++)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
                outputBuffer[i] = (float)OldValue ;
            }
            return NumSamples;
        }

        public int Generate(float[] outputBuffer)
        {
            double OldValue;
            for (int i = 0; i < outputBuffer.Length; i++)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
                outputBuffer[i] = (float)OldValue ;
            }
            return outputBuffer.Length;
        }


        public int Generate(float[] modulation, float[] outputBuffer)
        {
            double OldValue;
            int i = 0;
            foreach (float modval in modulation)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
                outputBuffer[i++] = (float)OldValue * modval;
            }
            return modulation.Length;
        }

        public int Generate(float[] modulation, float[] outputBuffer, int startIndex)
        {
            double OldValue;
            int i = 0;
            foreach (float modval in modulation)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
                outputBuffer[startIndex + i++] = (float)OldValue * modval;
            }
            return modulation.Length;
        }

        public int Process(float Value, out float outResult)
        {
            double OldValue;
            OldValue = S0;
            S0 = S1;
            S1 = S1 * Coeff - OldValue;
            outResult = (float)OldValue * Value;
            return 1;
        }

        public int Process(float[] inputSignal, int inputIndex, float[] outputBuffer, int numSamples)
        {
            double OldValue;
            for( int i = 0; i < numSamples; i++)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
                outputBuffer[i] = (float)OldValue * inputSignal[inputIndex + i];
            }
            return numSamples;
        }

        /// <summary>
        /// Adds (mixes) the new samples into provided array
        /// </summary>
        /// <param name="Buffer">The buffer where samples will be added</param>
        /// <param name="NumSamples">Number of sampless to add</param>
        /// <returns>Resulting array</returns>
        public int GenerateAdd(float Value, float[] outputBuffer, int NumSamples)
        {
            double OldValue;
            for (int i = 0; i < NumSamples; i++)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
                outputBuffer[i] += (float)OldValue * Value;
            }
            return NumSamples;
        }

        public int GenerateAdd(float[] modulation, float[] outputBuffer)
        {
            double OldValue;
            int i = 0;
            foreach (float modval in modulation)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
                outputBuffer[i++] += (float)OldValue * modval;
            }
            return modulation.Length;
        }

        public int GenerateAdd(float[] modulation, float[] outputBuffer, int startIndex)
        {
            double OldValue;
            int i = 0;
            foreach (float modval in modulation)
            {
                OldValue = S0;
                S0 = S1;
                S1 = S1 * Coeff - OldValue;
                outputBuffer[startIndex + i++] += (float)OldValue * modval;
            }
            return modulation.Length;
        }
    }
}
