using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    class Samples
    {
        const int SAMPLES_BUFFSIZE = 100;
        const int BYTES_BUFFSIZE = SAMPLES_BUFFSIZE * 2;

        float ScalingFactor;
        string FileName;
        System.IO.FileStream File;
        float[] SampleBuff = new float[SAMPLES_BUFFSIZE];
        byte[] ByteBuff = new byte[BYTES_BUFFSIZE];

        public Samples(float samplesScalingFactor)
        {
            this.ScalingFactor = samplesScalingFactor;
        }

        public Samples(string outputFile, float samplesScalingFactor)
        {
            this.ScalingFactor = samplesScalingFactor;
            this.FileName = outputFile;
            File = System.IO.File.Open(FileName, System.IO.FileMode.Create);
        }

        public Samples(string inputFile)
        {
            this.ScalingFactor = 1;
            this.FileName = inputFile;
            File = System.IO.File.Open(FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read);
        }

        public static float ToFloat(int Data)
        {
            if (Data > 32767) Data = -32768 + (Data & 0x7FFF);   // Extend the sign
            return ((float)Data) / 32768.0F;
        }

        public static float ToSingle(byte Data)
        {
            return (Convert.ToSingle(Data) - 127.0F) / 256.0F;
        }

        public static int ToFloat(byte[] inputBuff, float[] resultBuff, int numBytes)
        {
            for (int i = 0; i < numBytes / 2; i++)
            {
                byte LoByte = inputBuff[2 * i];                     // Get LSB from the buffer
                byte HiByte = inputBuff[2 * i + 1];                 // Get MSB from the buffer
                int Sample = ((HiByte & 0x00FF) << 8) + LoByte;     // Create single 16-bit sample
                if (Sample > 32767) Sample = -32768 + (Sample & 0x7FFF);   // Extend the sign
                resultBuff[i] = ((float)Sample) / 32768.0F;
            }
            return numBytes / 2;
        }

        public static int ToFloat(byte[] inputBuff, int inputIndex, float[] resultBuff, int resultIndex, int numBytes)
        {
            int SamplesToConvert = numBytes / 2;
            for (int i = 0; i < SamplesToConvert; i++)
            {
                byte LoByte = inputBuff[inputIndex];                     // Get LSB from the buffer
                byte HiByte = inputBuff[inputIndex + 1];                 // Get MSB from the buffer
                int Sample = ((HiByte & 0x00FF) << 8) + LoByte;     // Create single 16-bit sample
                if (Sample > 32767) Sample = -32768 + (Sample & 0x7FFF);   // Extend the sign
                resultBuff[resultIndex] = ((float)Sample) / 32768.0F;
                inputIndex += 2;
                resultIndex++;
            }
            return numBytes / 2;
        }

        public int ToFloat(float[] resultBuffer)
        {
            return ToFloat(resultBuffer, 0, resultBuffer.Length);
        }

        public int ToFloat(float[] resultBuffer, int startingIndex, int numSamples)
        {
            int SamplesRead = 0;
            int SamplesConverted = 0;
            int NumBytesToRead = numSamples * 2;

            do
            {
                int BytesToRead = Math.Min(NumBytesToRead, BYTES_BUFFSIZE);
                int n = File.Read(ByteBuff, 0, BytesToRead);
                SamplesConverted = ToFloat(ByteBuff, 0, resultBuffer, startingIndex, n);
                startingIndex += SamplesConverted;
                SamplesRead += SamplesConverted;
                NumBytesToRead -= n;
            } while ((NumBytesToRead > 0) && (SamplesConverted > 0));
            return SamplesRead;
        }

        public int ToByte(float[] inputBuff)
        {
            int BytesWritten = 0;
            int NumSamples = inputBuff.Length;
            int SampleIdx = 0;
            while (NumSamples > 0)
            {
                int nConv = Math.Min(NumSamples, SAMPLES_BUFFSIZE);
                int n = this.ToByte(inputBuff, SampleIdx, ByteBuff, 0, nConv);
                File.Write(ByteBuff, 0, n);
                BytesWritten += n;
                SampleIdx += nConv;
                NumSamples -= nConv;
            }
            return BytesWritten;
        }

        public int ToByte(float[] inputBuff, int numSamples)
        {
            int BytesWritten = 0;
            int SampleIdx = 0;
            // Output prepared samples into the file
            while (numSamples > 0)
            {
                int nConv = Math.Min(numSamples, SAMPLES_BUFFSIZE);
                int n = this.ToByte(inputBuff, SampleIdx, ByteBuff, 0, nConv);
                File.Write(ByteBuff, 0, n);
                BytesWritten += n;
                SampleIdx += nConv;
                numSamples -= nConv;
            }
            return BytesWritten;
        }

        public int ToByte(float[] inputBuff, int inputIndex, byte[] resultBuff, int outputIndex, int numSamples)
        {
            // Output prepared samples into the file
            for (int i = 0; i < numSamples; i++)
            {
                float Samp = inputBuff[inputIndex++] * this.ScalingFactor;
                int Sample = (int)Math.Floor((Samp * 32767.0 + 0.5));
                resultBuff[outputIndex] = (byte)(Sample & 0x0000FF);
                resultBuff[outputIndex + 1] = (byte)((Sample >> 8) & 0x00FF);
                outputIndex += 2;
            }
            return numSamples * 2;
        }

        public void Close()
        {
            if (this.FileName != null)
            {
                File.Close();
                FileName = null;
            }
        }
    }
}
