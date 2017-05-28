using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NSpeex.Plus
{
    /**
     * Java Speex Command Line Decoder.
     * 
     * Decodes SPX files created by Speex's speexenc utility to WAV entirely in pure java.
     * Currently this code has been updated to be compatible with release 1.0.3.
     *
     * NOTE!!! A number of advanced options are NOT supported. 
     * 
     * --  DTX implemented but untested.
     * --  Packet loss support implemented but untested.
     * --  SPX files with more than one comment. 
     * --  Can't force decoder to run at another rate, mode, or channel count. 
     * 
     * @author Jim Lawrence, helloNetwork.com
     * @author Marc Gimpel, Wimba S.A. (mgimpel@horizonwimba.com)
     * @version $Revision$
     */
    public class NSpeexDec
    {
        /** Print level for messages : Print debug information */
        public static readonly int DEBUG = 0;
        /** Print level for messages : Print basic information */
        public static readonly int INFO = 1;
        /** Print level for messages : Print only warnings and errors */
        public static readonly int WARN = 2;
        /** Print level for messages : Print only errors */
        public static readonly int ERROR = 3;
        /** Print level for messages */
        protected int printlevel = INFO;

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

        /** Random number generator for packet loss simulation. */
        protected static Random random = new Random();
        /** Speex Decoder */
        protected SpeexDecoder speexDecoder;

        /** Defines whether or not the perceptual enhancement is used. */
        protected bool enhanced = true;
        /** If input is raw, defines the decoder mode (0=NB, 1=WB and 2-UWB). */
        private int mode = 0;
        /** If input is raw, defines the quality setting used by the encoder. */
        private int quality = 8;
        /** If input is raw, defines the number of frmaes per packet. */
        private int nframes = 1;
        /** If input is raw, defines the sample rate of the audio. */
        private int sampleRate = -1;
        /** */
        private float vbr_quality = -1;
        /** */
        private bool vbr = false;
        /** If input is raw, defines th number of channels (1=mono, 2=stereo). */
        private int channels = 1;
        /** The percentage of packets to lose in the packet loss simulation. */
        private int loss = 0;

        public NSpeexDec()
        {
        }

        public NSpeexDec(int printlevel)
        {
            this.printlevel = printlevel;
        }

        /**
         * Decodes a Speex file to PCM.
         * @param srcPath
         * @param destPath
         * @exception IOException
         */
        public void decode(string srcPath, string destPath)
        {
            if (srcPath.ToLower().EndsWith(".spx"))
            {
                srcFormat = FILE_FORMAT_OGG;
            }
            else if (srcPath.ToLower().EndsWith(".wav"))
            {
                srcFormat = FILE_FORMAT_WAVE;
            }
            else
            {
                srcFormat = FILE_FORMAT_RAW;
            }
            if (destPath.ToLower().EndsWith(".wav"))
            {
                destFormat = FILE_FORMAT_WAVE;
            }
            else
            {
                destFormat = FILE_FORMAT_RAW;
            }

            byte[] header = new byte[2048];
            byte[] payload = new byte[65536];
            byte[] decdat = new byte[44100 * 2 * 2];
            const int WAV_HEADERSIZE = 8;
            const int WAVE_FORMAT_SPEEX = 0xa109;
            const String RIFF = "RIFF";
            const String WAVE = "WAVE";
            const String FORMAT = "fmt ";
            const String DATA = "data";
            const int OGG_HEADERSIZE = 27;
            const int OGG_SEGOFFSET = 26;
            const String OGGID = "OggS";
            int segments = 0;
            int curseg = 0;
            int bodybytes = 0;
            int decsize = 0;
            int packetNo = 0;

            // construct a new decoder
            speexDecoder = new SpeexDecoder();
            // open the input stream
            BinaryReader reader = new BinaryReader(new FileStream(srcPath, FileMode.Open));

            AudioFileWriter writer = null;
            int origchksum;
            int chksum;

            // read until we get to EOF
            while (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                if (srcFormat == FILE_FORMAT_OGG)
                {
                    // read the OGG header
                    reader.Read(header, 0, OGG_HEADERSIZE);
                    origchksum = readInt(header, 22);
                    header[22] = 0;
                    header[23] = 0;
                    header[24] = 0;
                    header[25] = 0;
                    chksum = OggCrc.checksum(0, header, 0, OGG_HEADERSIZE);

                    // make sure its a OGG header
                    string oggId = Encoding.Default.GetString(header.Skip(0).Take(4).ToArray());
                    if (!OGGID.Equals(oggId))
                    {
                        Console.WriteLine("missing ogg id!");
                        return;
                    }

                    /* how many segments are there? */
                    segments = header[OGG_SEGOFFSET] & 0xFF;
                    reader.Read(header, OGG_HEADERSIZE, segments);
                    chksum = OggCrc.checksum(chksum, header, OGG_HEADERSIZE, segments);

                    /* decode each segment, writing output to wav */
                    for (curseg = 0; curseg < segments; curseg++)
                    {
                        /* get the number of bytes in the segment */
                        bodybytes = header[OGG_HEADERSIZE + curseg] & 0xFF;
                        if (bodybytes == 255)
                        {
                            Console.WriteLine("sorry, don't handle 255 sizes!");
                            return;
                        }
                        reader.Read(payload, 0, bodybytes);
                        chksum = OggCrc.checksum(chksum, payload, 0, bodybytes);

                        /* decode the segment */
                        /* if first packet, read the Speex header */
                        if (packetNo == 0)
                        {
                            if (readSpeexHeader(payload, 0, bodybytes))
                            {

                                /* once Speex header read, initialize the wave writer with output format */
                                if (destFormat == FILE_FORMAT_WAVE)
                                {
                                    writer = new PcmWaveWriter(speexDecoder.getSampleRate(), speexDecoder.getChannels());
                                }
                                else
                                {
                                    //writer = new RawWriter();
                                    Console.WriteLine("暂不支持");
                                    return;
                                }
                                writer.Open(destPath);
                                writer.WriteHeader(null);
                                packetNo++;
                            }
                            else
                            {
                                packetNo = 0;
                            }
                        }
                        else if (packetNo == 1)
                        { // Ogg Comment packet
                            packetNo++;
                        }
                        else
                        {
                            if (loss > 0 && random.Next(100) < loss)
                            {
                                speexDecoder.processData(null, 0, bodybytes);
                                for (int i = 1; i < nframes; i++)
                                {
                                    speexDecoder.processData(true);
                                }
                            }
                            else
                            {
                                speexDecoder.processData(payload, 0, bodybytes);
                                for (int i = 1; i < nframes; i++)
                                {
                                    speexDecoder.processData(false);
                                }
                            }
                            /* get the amount of decoded data */
                            if ((decsize = speexDecoder.getProcessedData(decdat, 0)) > 0)
                            {
                                writer.WritePacket(decdat, 0, decsize);
                            }
                            packetNo++;
                        }
                    }
                    if (chksum != origchksum)
                        throw new IOException("Ogg CheckSums do not match");
                }
                else
                { // Wave or Raw Speex
                  /* if first packet, initialise everything */
                    if (packetNo == 0)
                    {
                        if (srcFormat == FILE_FORMAT_WAVE)
                        {
                            // read the WAVE header
                            reader.Read(header, 0, WAV_HEADERSIZE + 4);
                            // make sure its a WAVE header
                            string str1 = Encoding.Default.GetString(header.Skip(0).Take(4).ToArray());
                            string str2 = Encoding.Default.GetString(header.Skip(8).Take(4).ToArray());
                            if (!RIFF.Equals(str1) && !WAVE.Equals(str2))
                            {
                                Console.WriteLine("Not a WAVE file");
                                return;
                            }
                            // Read other header chunks
                            reader.Read(header, 0, WAV_HEADERSIZE);
                            String chunk = Encoding.Default.GetString(header.Skip(0).Take(4).ToArray());
                            int size = readInt(header, 4);
                            while (!chunk.Equals(DATA))
                            {
                                reader.Read(header, 0, size);
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
                                    if (readShort(header, 0) != WAVE_FORMAT_SPEEX)
                                    {
                                        Console.WriteLine("Not a Wave Speex file");
                                        return;
                                    }
                                    channels = readShort(header, 2);
                                    sampleRate = readInt(header, 4);
                                    bodybytes = readShort(header, 12);
                                    /*
                                    The extra data in the wave format are
                                    18 : ACM major version number
                                    19 : ACM minor version number
                                    20-100 : Speex header
                                    100-... : Comment ?
                                    */
                                    if (readShort(header, 16) < 82)
                                    {
                                        Console.WriteLine("Possibly corrupt Speex Wave file.");
                                        return;
                                    }
                                    readSpeexHeader(header, 20, 80);
                                }
                                reader.Read(header, 0, WAV_HEADERSIZE);
                                chunk = Encoding.Default.GetString(header.Skip(0).Take(4).ToArray());
                                size = readInt(header, 4);
                            }
                            if (printlevel <= DEBUG) Console.WriteLine("Data size: " + size);
                        }
                        else
                        {

                            /* initialize the Speex decoder */
                            speexDecoder.init(mode, sampleRate, channels, enhanced);
                            if (!vbr)
                            {
                                switch (mode)
                                {
                                    case 0:
                                        bodybytes = NbEncoder.NB_FRAME_SIZE[NbEncoder.NB_QUALITY_MAP[quality]];
                                        break;
                                    //Wideband
                                    case 1:
                                        bodybytes = SbEncoder.NB_FRAME_SIZE[SbEncoder.NB_QUALITY_MAP[quality]];
                                        bodybytes += SbEncoder.SB_FRAME_SIZE[SbEncoder.WB_QUALITY_MAP[quality]];
                                        break;
                                    case 2:
                                        bodybytes = SbEncoder.NB_FRAME_SIZE[SbEncoder.NB_QUALITY_MAP[quality]];
                                        bodybytes += SbEncoder.SB_FRAME_SIZE[SbEncoder.WB_QUALITY_MAP[quality]];
                                        bodybytes += SbEncoder.SB_FRAME_SIZE[SbEncoder.UWB_QUALITY_MAP[quality]];
                                        break;
                                    //*/
                                    default:
                                        throw new IOException("Illegal mode encoundered.");
                                }
                                bodybytes = (bodybytes + 7) >> 3;
                            }
                            else
                            {
                                // We have read the stream to find out more
                                bodybytes = 0;
                            }
                        }
                        /* initialize the wave writer with output format */
                        if (destFormat == FILE_FORMAT_WAVE)
                        {
                            writer = new PcmWaveWriter(sampleRate, channels);
                        }
                        else
                        {
                            //writer = new RawWriter();
                            Console.WriteLine("暂不支持");
                            return;
                        }
                        writer.Open(destPath);
                        writer.WriteHeader(null);
                        packetNo++;
                    }
                    else
                    {
                        reader.Read(payload, 0, bodybytes);
                        if (loss > 0 && random.Next(100) < loss)
                        {
                            speexDecoder.processData(null, 0, bodybytes);
                            for (int i = 1; i < nframes; i++)
                            {
                                speexDecoder.processData(true);
                            }
                        }
                        else
                        {
                            speexDecoder.processData(payload, 0, bodybytes);
                            for (int i = 1; i < nframes; i++)
                            {
                                speexDecoder.processData(false);
                            }
                        }
                        /* get the amount of decoded data */
                        if ((decsize = speexDecoder.getProcessedData(decdat, 0)) > 0)
                        {
                            writer.WritePacket(decdat, 0, decsize);
                        }
                        packetNo++;
                    }
                }
            }

            /* close the output file */
            reader.Close();
            writer.Close();
        }

        /**
         * Reads the header packet.
         * <pre>
         *  0 -  7: speex_string: "Speex   "
         *  8 - 27: speex_version: "speex-1.0"
         * 28 - 31: speex_version_id: 1
         * 32 - 35: header_size: 80
         * 36 - 39: rate
         * 40 - 43: mode: 0=narrowband, 1=wb, 2=uwb
         * 44 - 47: mode_bitstream_version: 4
         * 48 - 51: nb_channels
         * 52 - 55: bitrate: -1
         * 56 - 59: frame_size: 160
         * 60 - 63: vbr
         * 64 - 67: frames_per_packet
         * 68 - 71: extra_headers: 0
         * 72 - 75: reserved1
         * 76 - 79: reserved2
         * </pre>
         * @param packet
         * @param offset
         * @param bytes
         * @return
         */
        private bool readSpeexHeader(byte[] packet,
                                        int offset,
                                        int bytes)
        {
            if (bytes != 80)
            {
                Console.WriteLine("Oooops");
                return false;
            }
            string oggId = Encoding.Default.GetString(packet.Skip(offset).Take(8).ToArray());
            if (!"Speex   ".Equals(oggId))
            {
                return false;
            }
            mode = packet[40 + offset] & 0xFF;
            sampleRate = readInt(packet, offset + 36);
            channels = readInt(packet, offset + 48);
            nframes = readInt(packet, offset + 64);
            return speexDecoder.init(mode, sampleRate, channels, enhanced);
        }

        /**
         * Converts Little Endian (Windows) bytes to an int (Java uses Big Endian).
         * @param data the data to read.
         * @param offset the offset from which to start reading.
         * @return the integer value of the reassembled bytes.
         */
        protected static int readInt(byte[] data, int offset)
        {
            return (data[offset] & 0xff) |
                   ((data[offset + 1] & 0xff) << 8) |
                   ((data[offset + 2] & 0xff) << 16) |
                   (data[offset + 3] << 24); // no 0xff on the last one to keep the sign
        }

        /**
         * Converts Little Endian (Windows) bytes to an short (Java uses Big Endian).
         * @param data the data to read.
         * @param offset the offset from which to start reading.
         * @return the integer value of the reassembled bytes.
         */
        protected static int readShort(byte[] data, int offset)
        {
            return (data[offset] & 0xff) |
                   (data[offset + 1] << 8); // no 0xff on the last one to keep the sign
        }
    }

}
