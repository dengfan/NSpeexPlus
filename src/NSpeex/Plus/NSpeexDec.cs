using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NSpeex.Plus
{
    public enum PrintLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

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
        /** Print level for messages */
        protected PrintLevel printlevel = PrintLevel.Info;

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

        public NSpeexDec(PrintLevel printlevel)
        {
            this.printlevel = printlevel;
        }

        /**
         * Decodes a Speex file to PCM.
         * @param srcPath
         * @param destPath
         * @exception IOException
         */
        public void Decode(string srcPath, Action<string> callback)
        {
            string destPath = srcPath + ".wav";

            byte[] header = new byte[2048];
            byte[] payload = new byte[65536];
            byte[] decdat = new byte[44100 * 2 * 2];
            //const int WAV_HEADERSIZE = 8;
            //const int WAVE_FORMAT_SPEEX = 0xa109;
            //const String RIFF = "RIFF";
            //const String WAVE = "WAVE";
            //const String FORMAT = "fmt ";
            //const String DATA = "data";
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
            while (reader.BaseStream.Length - reader.BaseStream.Position >= OGG_HEADERSIZE)
            {
                // read the OGG header
                reader.Read(header, 0, OGG_HEADERSIZE);
                origchksum = ReadInt(header, 22);
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
                        if (ReadSpeexHeader(payload, 0, bodybytes))
                        {
                            writer = new PcmWaveWriter(speexDecoder.getSampleRate(), speexDecoder.getChannels());
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
                    {
                        // Ogg Comment packet
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

            writer.Close();
            reader.Close();

            if (callback != null)
            {
                callback.Invoke(destPath);
            }
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
        private bool ReadSpeexHeader(byte[] packet,
                                        int offset,
                                        int bytes)
        {
            if (bytes != 80)
            {
                return false;
            }

            string oggId = Encoding.Default.GetString(packet.Skip(offset).Take(8).ToArray());
            if (!"Speex   ".Equals(oggId))
            {
                return false;
            }
            mode = packet[40 + offset] & 0xFF;
            sampleRate = ReadInt(packet, offset + 36);
            channels = ReadInt(packet, offset + 48);
            nframes = ReadInt(packet, offset + 64);
            return speexDecoder.init(mode, sampleRate, channels, enhanced);
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
