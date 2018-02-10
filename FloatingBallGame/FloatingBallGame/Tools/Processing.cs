using System;
using System.Linq;
using FloatingBallGame.ViewModels;
using NAudio.Wave;

namespace FloatingBallGame.Tools
{
    public class Processing
    {
        public static float[] ProcessFloats(byte[] buffer, WaveFormat format)
        {
            // We know that this is single channel audio

            int bytesPerSample = format.BitsPerSample / 8;

            int sampleCount = buffer.Length / bytesPerSample;
            float[] sampleBuffer = new float[sampleCount];

            int offset = 0;
            int count = 0;
            while (count < sampleCount)
            {
                if (format.BitsPerSample == 16)
                {
                    sampleBuffer[count] = BitConverter.ToInt16(buffer, offset) / 32768f;
                    offset += 2;
                }
                else if (format.BitsPerSample == 24)
                {
                    sampleBuffer[count] = (((sbyte)buffer[offset + 2] << 16) | (buffer[offset + 1] << 8) | buffer[offset]) / 8388608f;
                    offset += 3;
                }
                else if (format.BitsPerSample == 32 && format.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    sampleBuffer[count] = BitConverter.ToSingle(buffer, offset);
                    offset += 4;
                }
                else if (format.BitsPerSample == 32)
                {
                    sampleBuffer[count] = BitConverter.ToInt32(buffer, offset) / (Int32.MaxValue + 1f);
                    offset += 4;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported bit depth");
                }
                count++;
            }
            return sampleBuffer;
        }

        public static double RmsValue(byte[] buffer, WaveFormat format)
        {
            var samples = Processing.ProcessFloats(buffer, format);
            double sumOfSquares = 0;
            foreach (var sample in samples)
            {
                sumOfSquares += sample * sample;
            }
            return Math.Sqrt(sumOfSquares);
        }
    }
}