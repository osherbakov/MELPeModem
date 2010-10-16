using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    class Constellation
    {
        public static int[] Bits_Simple_BPSK = { 0, 1 };
        public static IQ[] IQ_Simple_BPSK = new IQ[] {IQ.UNITY, -IQ.UNITY};

        public const float SQR2_2 = 0.70710678f;
        public static int[] BitsToPhase_BPSK = { 1, 0 };
        public static float[] ITable_BPSK = { SQR2_2, -SQR2_2 };
        public static float[] QTable_BPSK = { SQR2_2, -SQR2_2 };
        public static IQ[] IQTable_BPSK = new IQ[2];

        public static int[] BitsToPhase_QPSK = { 0, 1, 3, 2 };
        public static float[] ITable_QPSK = { 1.0f, 0.0f, -1.0f, 0.0f };
        public static float[] QTable_QPSK = { 0.0f, 1.0f, 0.0f, -1.0f };
        public static IQ[] IQTable_QPSK = new IQ[4];

        public static float[] ITable_QPSK45 = { SQR2_2, -SQR2_2, -SQR2_2, SQR2_2 };
        public static float[] QTable_QPSK45 = { SQR2_2, SQR2_2, -SQR2_2, -SQR2_2 };
        public static IQ[] IQTable_QPSK45 = new IQ[4];


        public static int[] BitsToPhase_8PSK = { 1, 0, 2, 3, 6, 7, 5, 4 };
        public static float[] ITable_8PSK = { 1.0f, SQR2_2, 0.0f, -SQR2_2, -1.0f, -SQR2_2, 0.0f, SQR2_2 };
        public static float[] QTable_8PSK = { 0.0f, SQR2_2, 1.0f, SQR2_2, 0.0f, -SQR2_2, -1.0f, -SQR2_2 };
        public static IQ[] IQTable_8PSK = new IQ[8];

        public static float[] ITable_16QAM = { 0.866025f, 0.5f, 1.0f, 0.258819f, -0.5f, 0.0f, -0.866025f, -0.258819f, 0.5f, 0.0f, 0.866025f, 0.258819f, -0.866025f, -0.5f, -1.0f, -0.258819f };
        public static float[] QTable_16QAM = { 0.5f, 0.866025f, 0.0f, 0.258819f, 0.866025f, 1.0f, 0.5f, 0.258819f, -0.866025f, -1.0f, -0.5f, -0.258819f, -0.5f, -0.866025f, 0.0f, -0.258819f };
        public static IQ[] IQTable_16QAM = new IQ[16];

        public static float[] ITable_32QAM = {
            0.866380f, 0.984849f, 0.499386f, 0.173415f, 0.520246f, 0.520246f, 0.173415f, 0.173415f,
            -0.866380f, -0.984849f, -0.499386f, -0.173415f, -0.520246f, -0.520246f, -0.173415f, -0.173415f,
             0.866380f, 0.984849f, 0.499386f, 0.173415f, 0.520246f, 0.520246f, 0.173415f, 0.173415f,
            -0.866380f, -0.984849f, -0.499386f, -0.173415f, -0.520246f, -0.520246f, -0.173415f, -0.173415f };
        public static float[] QTable_32QAM = {
            0.499386f, 0.173415f, 0.866380f, 0.984849f, 0.520246f, 0.173415f, 0.520246f, 0.173415f,
            0.499386f, 0.173415f, 0.866380f, 0.984849f, 0.520246f, 0.173415f, 0.520246f, 0.173415f,
            -0.499386f, -0.173415f, -0.866380f, -0.984849f, -0.520246f, -0.173415f, -0.520246f, -0.173415f,
            -0.499386f, -0.173415f, -0.866380f, -0.984849f,-0.520246f, -0.173415f, -0.520246f, -0.173415f };
        public static IQ[] IQTable_32QAM = new IQ[32];

        public static int[] Table_1_to_1 = new int[64];

        public static int[] BitsToPhase_39 = { 1, 2, 0, 3 };


        static Constellation()
        {
            for (int i = 0; i < 64; i++)
                Table_1_to_1[i] = i; 
            for (int i = 0; i < IQTable_BPSK.Length; i++)
                IQTable_BPSK[i] = new IQ(ITable_BPSK[i], QTable_BPSK[i]);
            for (int i = 0; i < IQTable_QPSK.Length; i++)
                IQTable_QPSK[i] = new IQ(ITable_QPSK[i], QTable_QPSK[i]);
            for (int i = 0; i < IQTable_QPSK45.Length; i++)
                IQTable_QPSK45[i] = new IQ(ITable_QPSK45[i], QTable_QPSK45[i]);
            for (int i = 0; i < IQTable_8PSK.Length; i++)
                IQTable_8PSK[i] = new IQ(ITable_8PSK[i], QTable_8PSK[i]);
            for (int i = 0; i < IQTable_16QAM.Length; i++)
                IQTable_16QAM[i] = new IQ(ITable_16QAM[i], QTable_16QAM[i]);
            for (int i = 0; i < IQTable_32QAM.Length; i++)
                IQTable_32QAM[i] = new IQ(ITable_32QAM[i], QTable_32QAM[i]);
        }
    }
}
