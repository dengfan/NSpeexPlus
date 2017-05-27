using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NSpeex.Plus
{
    /**
     * Main Speex Encoder class.
     * This class encodes the given PCM 16bit samples into Speex packets.
     *
     * @author Marc Gimpel, Wimba S.A. (mgimpel@horizonwimba.com)
     * @version $Revision$
     */
    public class SpeexEncoder
    {
        private IEncoder encoder;
        private Bits bits;
        private float[] rawData;
        private int sampleRate;
        private int channels;
        private int frameSize;

        /**
         * Constructor
         */
        public SpeexEncoder()
        {
            bits = new Bits();
        }

        /**
         * Initialisation
         * @param mode       the mode of the encoder (0=NB, 1=WB, 2=UWB).
         * @param quality    the quality setting of the encoder (between 0 and 10).
         * @param sampleRate the number of samples per second.
         * @param channels   the number of audio channels (1=mono, 2=stereo, ...).
         * @return true if initialisation successful.
         */
        public bool init(int mode,
                            int quality,
                            int sampleRate,
                            int channels)
        {
            switch (mode)
            {
                case 0:
                    encoder = new NbEncoder();
                    sampleRate = 8000;
                    break;
                case 1:
                    encoder = new SbEncoder(false);
                    sampleRate = 16000;
                    break;
                case 2:
                    encoder = new SbEncoder(true);
                    sampleRate = 32000;
                    break;
                default:
                    encoder = new NbEncoder();
                    sampleRate = 8000;
                    return false;
            }

            /* initialize the speex decoder */
            encoder.Quality = quality;

            /* set decoder format and properties */
            this.frameSize = encoder.FrameSize;
            this.sampleRate = sampleRate;
            this.channels = channels;
            rawData = new float[channels * frameSize];

            return true;
        }

        /**
         * Returns the Encoder being used (Narrowband, Wideband or Ultrawideband).
         * @return the Encoder being used (Narrowband, Wideband or Ultrawideband).
         */
        public IEncoder getEncoder()
        {
            return encoder;
        }

        /**
         * Returns the sample rate.
         * @return the sample rate.
         */
        public int getSampleRate()
        {
            return sampleRate;
        }

        /**
         * Returns the number of channels.
         * @return the number of channels.
         */
        public int getChannels()
        {
            return channels;
        }

        /**
         * Returns the size of a frame.
         * @return the size of a frame.
         */
        public int getFrameSize()
        {
            return frameSize;
        }

        /**
         * Pull the decoded data out into a byte array at the given offset
         * and returns the number of bytes of encoded data just read.
         * @param data
         * @param offset
         * @return the number of bytes of encoded data just read.
         */
        public int getProcessedData(byte[] data, int offset)
        {
            int size = bits.BufferSize;
            Array.Copy(bits.Buffer, 0, data, offset, size);
            bits.Reset();
            return size;
        }

        /**
         * Returns the number of bytes of encoded data ready to be read.
         * @return the number of bytes of encoded data ready to be read.
         */
        public int getProcessedDataByteSize()
        {
            return bits.BufferSize;
        }

        /**
         * This is where the actual encoding takes place
         * @param data
         * @param offset
         * @param len
         * @return true if successful.
         */
        public bool processData(byte[] data,
                                   int offset,
                                   int len)
        {
            // converty raw bytes into float samples
            mapPcm16bitLittleEndian2Float(data, offset, rawData, 0, len / 2);
            // encode the bitstream
            return processData(rawData, len / 2);
        }

        /**
         * Encode an array of shorts.
         * @param data
         * @param offset
         * @param numShorts
         * @return true if successful.
         */
        public bool processData(short[] data,
                                   int offset,
                                   int numShorts)
        {
            int numSamplesRequired = channels * frameSize;
            if (numShorts != numSamplesRequired)
            {
                throw new Exception("SpeexEncoder requires " + numSamplesRequired + " samples to process a Frame, not " + numShorts);
            }
            // convert shorts into float samples,
            for (int i = 0; i < numShorts; i++)
            {
                rawData[i] = (float)data[offset + i];
            }
            // encode the bitstream
            return processData(rawData, numShorts);
        }

        /**
         * Encode an array of floats.
         * @param data
         * @param numSamples
         * @return true if successful.
         */
        public bool processData(float[] data, int numSamples)
        {
            int numSamplesRequired = channels * frameSize;
            if (numSamples != numSamplesRequired)
            {
                throw new Exception("SpeexEncoder requires " + numSamplesRequired + " samples to process a Frame, not " + numSamples);
            }
            // encode the bitstream
            if (channels == 2)
            {
                Stereo.Encode(bits, data, frameSize);
            }
            encoder.Encode(bits, data);
            return true;
        }

        /**
         * Converts a 16 bit linear PCM stream (in the form of a byte array)
         * into a floating point PCM stream (in the form of an float array).
         * Here are some important details about the encoding:
         * <ul>
         * <li> Java uses big endian for shorts and ints, and Windows uses little Endian.
         *      Therefore, shorts and ints must be read as sequences of bytes and
         *      combined with shifting operations.
         * </ul>
         * @param pcm16bitBytes - byte array of linear 16-bit PCM formated audio.
         * @param offsetInput
         * @param samples - float array to receive the 16-bit linear audio samples.
         * @param offsetOutput
         * @param length
         */
        public static void mapPcm16bitLittleEndian2Float(byte[] pcm16bitBytes,
                                                         int offsetInput,
                                                         float[] samples,
                                                         int offsetOutput,
                                                         int length)
        {
            sbyte[] data = ByteAryToSByteAry(pcm16bitBytes);
            if (data.Length - offsetInput < 2 * length)
            {
                throw new Exception("Insufficient Samples to convert to floats");
            }
            if (samples.Length - offsetOutput < length)
            {
                throw new Exception("Insufficient float buffer to convert the samples");
            }
            for (int i = 0; i < length; i++)
            {
                samples[offsetOutput + i] = (float)((data[offsetInput + 2 * i] & 0xff) | (data[offsetInput + 2 * i + 1] << 8)); // no & 0xff at the end to keep the sign
            }
        }

        public static sbyte[] ByteAryToSByteAry(byte[] myByte)
        {
            sbyte[] mySByte = new sbyte[myByte.Length];

            for (int i = 0; i < myByte.Length; i++)
            {
                if (myByte[i] > 127)
                    mySByte[i] = (sbyte)(myByte[i] - 256);
                else
                    mySByte[i] = (sbyte)myByte[i];
            }

            return mySByte;
        }
    }

}
