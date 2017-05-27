using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NSpeex.Plus
{
    /**
     * Java Speex Command Line Encoder.
     * 
     * Currently this code has been updated to be compatible with release 1.0.3.
     * 
     * @author Marc Gimpel, Wimba S.A. (mgimpel@horizonwimba.com)
     * @version $Revision$
     */
    public class NSpeexEnc
    {
        /** Version of the Speex Encoder */
        public const String VERSION = "//github.com/dengfan/NSpeexPlus";

        /** Print level for messages : Print debug information */
        public static readonly int DEBUG = 0;
        /** Print level for messages : Print basic information */
        public static readonly int INFO = 1;
        /** Print level for messages : Print only warnings and errors */
        public static readonly int WARN = 2;
        /** Print level for messages : Print only errors */
        public static readonly int ERROR = 3;
        /** Print level for messages */
        protected int printlevel = DEBUG;

        /** File format for input or output audio file: Raw */
        public static readonly int FILE_FORMAT_RAW = 0;
        /** File format for input or output audio file: Ogg */
        public static readonly int FILE_FORMAT_OGG = 1;
        /** File format for input or output audio file: Wave */
        public static readonly int FILE_FORMAT_WAVE = 2;
        /** Defines File format for input audio file (Raw, Ogg or Wave). */
        protected int srcFormat = FILE_FORMAT_OGG;
        /** Defines File format for output audio file (Raw or Wave). */
        protected int destFormat = FILE_FORMAT_WAVE;

        /** Defines the encoder mode (0=NB, 1=WB and 2=UWB). */
        protected int mode = -1;
        /** Defines the encoder quality setting (integer from 0 to 10). */
        protected int quality = 8;
        /** Defines the encoders algorithmic complexity. */
        protected int complexity = 3;
        /** Defines the number of frames per speex packet. */
        protected int nframes = 1;
        /** Defines the desired bitrate for the encoded audio. */
        protected int bitrate = -1;
        /** Defines the sampling rate of the audio input. */
        protected int sampleRate = -1;
        /** Defines the number of channels of the audio input (1=mono, 2=stereo). */
        protected int channels = 1;
        /** Defines the encoder VBR quality setting (float from 0 to 10). */
        protected float vbr_quality = -1;
        /** Defines whether or not to use VBR (Variable Bit Rate). */
        protected bool vbr = false;
        /** Defines whether or not to use VAD (Voice Activity Detection). */
        protected bool vad = false;
        /** Defines whether or not to use DTX (Discontinuous Transmission). */
        protected bool dtx = false;

        public NSpeexEnc()
        {
        }

        public NSpeexEnc(int printlevel)
        {
            this.printlevel = printlevel;
        }

        /**
         * Encodes a PCM file to Speex. 
         * @param srcPath
         * @param destPath
         * @exception IOException
         */
        public void encode(string srcPath, string destPath)
        {
            if (srcPath.ToLower().EndsWith(".wav"))
            {
                srcFormat = FILE_FORMAT_WAVE;
            }
            else
            {
                srcFormat = FILE_FORMAT_RAW;
            }
            if (destPath.ToLower().EndsWith(".spx"))
            {
                destFormat = FILE_FORMAT_OGG;
            }
            else if (destPath.ToLower().EndsWith(".wav"))
            {
                destFormat = FILE_FORMAT_WAVE;
            }
            else
            {
                destFormat = FILE_FORMAT_RAW;
            }

            byte[] temp = new byte[2560]; // stereo UWB requires one to read 2560b
            const int HEADERSIZE = 8;
            const String RIFF = "RIFF";
            const String WAVE = "WAVE";
            const String FORMAT = "fmt ";
            const String DATA = "data";
            const int WAVE_FORMAT_PCM = 0x0001;

            // Open the input stream
            BinaryReader reader = new BinaryReader(new FileStream(srcPath, FileMode.Open));

            // Prepare input stream
            if (srcFormat == FILE_FORMAT_WAVE)
            {
                // read the WAVE header
                reader.Read(temp, 0, HEADERSIZE + 4);
                // make sure its a WAVE header
                string str1 = Encoding.Default.GetString(temp.Skip(0).Take(4).ToArray());
                string str2 = Encoding.Default.GetString(temp.Skip(8).Take(4).ToArray());
                if (!RIFF.Equals(str1) && !WAVE.Equals(str2))
                {
                    Console.WriteLine("Not a WAVE file");
                    return;
                }
                // Read other header chunks
                reader.Read(temp, 0, HEADERSIZE);
                String chunk = Encoding.Default.GetString(temp.Skip(0).Take(4).ToArray());
                int size = readInt(temp, 4);
                while (!chunk.Equals(DATA))
                {
                    reader.Read(temp, 0, size);
                    if (chunk.Equals(FORMAT))
                    {
                        /*
                        typedef struct waveformat_extended_tag {
                        WORD wFormatTag; // format type
                        WORD nChannels; // number of channels (i.e. mono, stereo...)
                        DWORD nSamplesPerSec; // sample rate
                        DWORD nAvgBytesPerSec; // for buffer estimation
                        WORD nBlockAlign; // block size of data
                        WORD wBitsPerSample; // Number of bits per sample of mono data
                        WORD cbSize; // The count in bytes of the extra size 
                        } WAVEFORMATEX;
                        */
                        if (readShort(temp, 0) != WAVE_FORMAT_PCM)
                        {
                            Console.WriteLine("Not a PCM file");
                            return;
                        }
                        channels = readShort(temp, 2);
                        sampleRate = readInt(temp, 4);
                        if (readShort(temp, 14) != 16)
                        {
                            Console.WriteLine("Not a 16 bit file " + readShort(temp, 18));
                            return;
                        }

                    }
                    reader.Read(temp, 0, HEADERSIZE);
                    chunk = Encoding.Default.GetString(temp.Skip(0).Take(4).ToArray());
                    size = readInt(temp, 4);
                }
                if (printlevel <= DEBUG) Console.WriteLine("Data size: " + size);
            }
            else
            {
                if (sampleRate < 0)
                {
                    switch (mode)
                    {
                        case 0:
                            sampleRate = 8000;
                            break;
                        case 1:
                            sampleRate = 16000;
                            break;
                        case 2:
                            sampleRate = 32000;
                            break;
                        default:
                            sampleRate = 8000;
                            break;
                    }
                }

            }

            // Set the mode if it has not yet been determined
            if (mode < 0)
            {
                if (sampleRate < 100) // Sample Rate has probably been given in kHz
                    sampleRate *= 1000;
                if (sampleRate < 12000)
                    mode = 0; // Narrowband
                else if (sampleRate < 24000)
                    mode = 1; // Wideband
                else
                    mode = 2; // Ultra-wideband
            }
            // Construct a new encoder
            SpeexEncoder speexEncoder = new SpeexEncoder();
            speexEncoder.init(mode, quality, sampleRate, channels);
            if (complexity > 0)
            {
                speexEncoder.getEncoder().Complexity = complexity;
            }
            if (bitrate > 0)
            {
                speexEncoder.getEncoder().BitRate = bitrate;
            }
            if (vbr)
            {
                speexEncoder.getEncoder().Vbr = vbr;
                if (vbr_quality > 0)
                {
                    speexEncoder.getEncoder().VbrQuality = vbr_quality;
                }
            }
            if (vad)
            {
                speexEncoder.getEncoder().Vad = vad;
            }
            if (dtx)
            {
                speexEncoder.getEncoder().Dtx = dtx;
            }

            // Display info
            if (printlevel <= DEBUG)
            {
                Console.WriteLine("");
                Console.WriteLine("Output File: " + destPath);
                Console.WriteLine("File format: Ogg Speex");
                Console.WriteLine("Encoder mode: " + (mode == 0 ? "Narrowband" : (mode == 1 ? "Wideband" : "UltraWideband")));
                Console.WriteLine("Quality: " + (vbr ? vbr_quality : quality));
                Console.WriteLine("Complexity: " + complexity);
                Console.WriteLine("Frames per packet: " + nframes);
                Console.WriteLine("Varible bitrate: " + vbr);
                Console.WriteLine("Voice activity detection: " + vad);
                Console.WriteLine("Discontinouous Transmission: " + dtx);
            }

            // Open the file writer
            AudioFileWriter writer;
            if (destFormat == FILE_FORMAT_OGG)
            {
                writer = new OggSpeexWriter(mode, sampleRate, channels, nframes, vbr);
            }
            else if (destFormat == FILE_FORMAT_WAVE)
            {
                nframes = PcmWaveWriter.WAVE_FRAME_SIZES[mode - 1, channels - 1, quality];
                writer = new PcmWaveWriter(mode, quality, sampleRate, channels, nframes, vbr);
            }
            else
            {
                //writer = new RawWriter();
                Console.WriteLine("暂不支持");
                return;
            }
            writer.Open(destPath);
            writer.WriteHeader("Encoded with: " + VERSION);
            int pcmPacketSize = 2 * channels * speexEncoder.getFrameSize();

            // read until we get to EOF
            while (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                reader.Read(temp, 0, nframes * pcmPacketSize);
                for (int i = 0; i < nframes; i++)
                    speexEncoder.processData(temp, i * pcmPacketSize, pcmPacketSize);
                int encsize = speexEncoder.getProcessedData(temp, 0);
                if (encsize > 0)
                {
                    writer.WritePacket(temp, 0, encsize);
                }
            }

            writer.Close();
            reader.Close();
        }

        /**
         * Converts Little Endian (Windows) bytes to an int (Java uses Big Endian).
         * @param data the data to read.
         * @param offset the offset from which to start reading.
         * @return the integer value of the reassembled bytes.
         */
        protected static int readInt(byte[] data, int offset)
        {
            sbyte[] buf = SpeexEncoder.ByteAryToSByteAry(data);
            return (buf[offset] & 0xff) |
                   ((buf[offset + 1] & 0xff) << 8) |
                   ((buf[offset + 2] & 0xff) << 16) |
                   (buf[offset + 3] << 24); // no 0xff on the last one to keep the sign
        }

        /**
         * Converts Little Endian (Windows) bytes to an short (Java uses Big Endian).
         * @param data the data to read.
         * @param offset the offset from which to start reading.
         * @return the integer value of the reassembled bytes.
         */
        protected static int readShort(byte[] data, int offset)
        {
            sbyte[] buf = SpeexEncoder.ByteAryToSByteAry(data);
            return (buf[offset] & 0xff) |
                   (buf[offset + 1] << 8); // no 0xff on the last one to keep the sign
        }
    }

}
