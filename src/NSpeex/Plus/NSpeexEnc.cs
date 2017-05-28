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
        public const String Github = @"https://github.com/dengfan/NSpeexPlus";

        /** Print level for messages */
        protected PrintLevel printlevel = PrintLevel.Info;

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

        public NSpeexEnc(PrintLevel printlevel)
        {
            this.printlevel = printlevel;
        }

        /**
         * Encodes a PCM file to Speex. 
         * @param srcPath
         * @param destPath
         * @exception IOException
         */
        public void Encode(string srcPath, Action<string> callback)
        {
            string destPath = srcPath + ".spx";

            byte[] temp = new byte[2560]; // stereo UWB requires one to read 2560b
            const int HEADERSIZE = 8;
            const String RIFF = "RIFF";
            const String WAVE = "WAVE";
            const String FORMAT = "fmt ";
            const String DATA = "data";
            const int WAVE_FORMAT_PCM = 0x0001;

            // Open the input stream
            BinaryReader reader = new BinaryReader(new FileStream(srcPath, FileMode.Open));

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
            int size = ReadInt(temp, 4);
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
                    if (ReadShort(temp, 0) != WAVE_FORMAT_PCM)
                    {
                        Console.WriteLine("Not a PCM file");
                        return;
                    }
                    channels = ReadShort(temp, 2);
                    sampleRate = ReadInt(temp, 4);
                    if (ReadShort(temp, 14) != 16)
                    {
                        Console.WriteLine("Not a 16 bit file " + ReadShort(temp, 18));
                        return;
                    }

                }
                reader.Read(temp, 0, HEADERSIZE);
                chunk = Encoding.Default.GetString(temp.Skip(0).Take(4).ToArray());
                size = ReadInt(temp, 4);
            }
            if (printlevel <= PrintLevel.Debug) Console.WriteLine("Data size: " + size);

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
            if (printlevel <= PrintLevel.Debug)
            {
                Console.WriteLine("---------------------");
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
            AudioFileWriter writer = new OggSpeexWriter(mode, sampleRate, channels, nframes, vbr);
            writer.Open(destPath);
            writer.WriteHeader(Github);
            int pcmPacketSize = 2 * channels * speexEncoder.getFrameSize();

            int c = 0;
            // read until we get to EOF
            while (reader.BaseStream.Length - reader.BaseStream.Position >= pcmPacketSize)
            {
                reader.Read(temp, 0, nframes * pcmPacketSize);
                for (int i = 0; i < nframes; i++)
                    speexEncoder.processData(temp, i * pcmPacketSize, pcmPacketSize);
                int encsize = speexEncoder.getProcessedData(temp, 0);
                if (encsize > 0)
                {
                    writer.WritePacket(temp, 0, encsize);
                    c++;
                }
            }

            writer.Close();
            reader.Close();

            if (printlevel <= PrintLevel.Debug)
            {
                Console.WriteLine("----->" + c);
            }

            if (callback != null)
            {
                callback.Invoke(destPath);
            }
        }

        protected static int ReadInt(byte[] data, int offset)
        {
            var reader = new BinaryReader(new MemoryStream(data));
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            return reader.ReadInt32();
        }

        protected static int ReadShort(byte[] data, int offset)
        {
            var reader = new BinaryReader(new MemoryStream(data));
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            return reader.ReadInt16();
        }
    }

}
