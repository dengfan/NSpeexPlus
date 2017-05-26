using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NSpeex.Plus
{
    /**
     * Main Speex Decoder class.
     * This class decodes the given Speex packets into PCM 16bit samples.
     * 
     * <p>Here's an example that decodes and recovers one Speex packet.
     * <pre>
     * SpeexDecoder speexDecoder = new SpeexDecoder();
     * speexDecoder.processData(data, packetOffset, packetSize);
     * byte[] decoded = new byte[speexDecoder.getProcessedBataByteSize()];
     * speexDecoder.getProcessedData(decoded, 0);
     * </pre>
     * 
     * @author Jim Lawrence, helloNetwork.com
     * @author Marc Gimpel, Wimba S.A. (mgimpel@horizonwimba.com)
     * @version $Revision$
     */
    public class SpeexDecoder
    {
        private int sampleRate;
        private int channels;
        private float[] decodedData;
        private short[] outputData;
        private int outputSize;
        private Bits bits;
        private IDecoder decoder;
        private int frameSize;

        /**
         * Constructor
         */
        public SpeexDecoder()
        {
            bits = new Bits();
            sampleRate = 0;
            channels = 0;
        }

        /**
         * Initialise the Speex Decoder.
         * @param mode       the mode of the decoder (0=NB, 1=WB, 2=UWB).
         * @param sampleRate the number of samples per second.
         * @param channels   the number of audio channels (1=mono, 2=stereo, ...).
         * @param enhanced   whether to enable perceptual enhancement or not.
         * @return true if initialisation successful.
         */
        public bool init(int mode,
                            int sampleRate,
                            int channels,
                            bool enhanced)
        {
            switch (mode)
            {
                case 0:
                    decoder = new NbDecoder();
                    sampleRate = 8000;
                    break;
                case 1:
                    decoder = new SbDecoder(false);
                    sampleRate = 16000;
                    break;
                case 2:
                    decoder = new SbDecoder(true);
                    sampleRate = 32000;
                    break;
                default:
                    decoder = new NbDecoder();
                    sampleRate = 8000;
                    return false;
            }

            /* initialize the speex decoder */
            decoder.PerceptualEnhancement = enhanced;
            /* set decoder format and properties */
            this.frameSize = decoder.FrameSize;
            this.sampleRate = sampleRate;
            this.channels = channels;
            int secondSize = sampleRate * channels;
            decodedData = new float[secondSize * 2];
            outputData = new short[secondSize * 2];
            outputSize = 0;
            return true;
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
         * Pull the decoded data out into a byte array at the given offset
         * and returns the number of bytes processed and just read.
         * @param data
         * @param offset
         * @return the number of bytes processed and just read.
         */
        public int getProcessedData(byte[] data, int offset)
        {
            if (outputSize <= 0)
            {
                return outputSize;
            }
            for (int i = 0; i < outputSize; i++)
            {
                int dx = offset + (i << 1);
                data[dx] = (byte)(outputData[i] & 0xff);
                data[dx + 1] = (byte)((outputData[i] >> 8) & 0xff);
            }
            int size = outputSize * 2;
            outputSize = 0;
            return size;
        }

        /**
         * Pull the decoded data out into a short array at the given offset
         * and returns tne number of shorts processed and just read
         * @param data
         * @param offset
         * @return the number of samples processed and just read.
         */
        public int getProcessedData(short[] data, int offset)
        {
            if (outputSize <= 0)
            {
                return outputSize;
            }
            Array.Copy(outputData, 0, data, offset, outputSize);
            int size = outputSize;
            outputSize = 0;
            return size;
        }

        /**
         * Returns the number of bytes processed and ready to be read.
         * @return the number of bytes processed and ready to be read.
         */
        public int getProcessedDataByteSize()
        {
            return (outputSize * 2);
        }

        /**
         * This is where the actual decoding takes place
         * @param data - the Speex data (frame) to decode.
         * If it is null, the packet is supposed lost.
         * @param offset - the offset from which to start reading the data.
         * @param len - the length of data to read (Speex frame size).
         * @throws StreamCorruptedException If the input stream is invalid.
         */
        public void processData(byte[] data,
                                int offset,
                                int len)
        {
            if (data == null)
            {
                processData(true);
            }
            else
            {
                /* read packet bytes into bitstream */
                bits.ReadFrom(data, offset, len);
                processData(false);
            }
        }

        /**
         * This is where the actual decoding takes place.
         * @param lost - true if the Speex packet has been lost.
         * @throws StreamCorruptedException If the input stream is invalid.
         */
        public void processData(bool lost)
        {
            int i;
            /* decode the bitstream */
            if (lost)
                decoder.Decode(null, decodedData);
            else
                decoder.Decode(bits, decodedData);
            if (channels == 2)
                decoder.DecodeStereo(decodedData, frameSize);

            /* PCM saturation */
            for (i = 0; i < frameSize * channels; i++)
            {
                if (decodedData[i] > 32767.0f)
                    decodedData[i] = 32767.0f;
                else if (decodedData[i] < -32768.0f)
                    decodedData[i] = -32768.0f;
            }

            /* convert to short and save to buffer */
            for (i = 0; i < frameSize * channels; i++, outputSize++)
            {
                outputData[outputSize] = (decodedData[i] > 0) ?
                                         (short)(decodedData[i] + 0.5f) :
                                         (short)(decodedData[i] - 0.5f);
            }
        }
    }

}
