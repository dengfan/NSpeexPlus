using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NSpeex.Plus
{
    /**
     * Abstract Class that defines an Audio File Writer.
     * 
     * @author Marc Gimpel, Wimba S.A. (mgimpel@horizonwimba.com)
     * @version $Revision$
     */
    public abstract class AudioFileWriter
    {
        /**
         * Closes the output file.
         * @exception IOException if there was an exception closing the Audio Writer.
         */
        public abstract void Close();

        /**
         * Open the output file. 
         * @param file - file to open.
         * @exception IOException if there was an exception opening the Audio Writer.
         */
        public abstract void Open(Stream file);

        /**
         * Open the output file. 
         * @param filename - file to open.
         * @exception IOException if there was an exception opening the Audio Writer.
         */
        public abstract void Open(String filename);

        /**
         * Writes the header pages that start the Ogg Speex file. 
         * Prepares file for data to be written.
         * @param comment description to be included in the header.
         * @exception IOException
         */
        public abstract void WriteHeader(String comment);

        /**
         * Writes a packet of audio. 
         * @param data audio data
         * @param offset the offset from which to start reading the data.
         * @param len the length of data to read.
         * @exception IOException
         */
        public abstract void WritePacket(byte[] data, int offset, int len);

        /// <summary>
		/// Writes an Ogg Page Header to the given byte array.
		/// </summary>
		///
		/// <param name="buf">the buffer to write to.</param>
		/// <param name="offset">the from which to start writing.</param>
		/// <param name="headerType">the header type flag (0=normal, 2=bos: beginning of stream,</param>
		/// <param name="granulepos">the absolute granule position.</param>
		/// <param name="streamSerialNumber"></param>
		/// <param name="pageCount"></param>
		/// <param name="packetCount"></param>
		/// <param name="packetSizes"></param>
		/// <returns>the amount of data written to the buffer.</returns>
		public static int WriteOggPageHeader(BinaryWriter buf,
                int headerType, long granulepos, int streamSerialNumber,
                int pageCount, int packetCount, byte[] packetSizes)
        {
            buf.Write(System.Text.Encoding.UTF8.GetBytes("OggS")); // 0 - 3: capture_pattern
            buf.Write(Byte.MinValue); // 4: stream_structure_version
            buf.Write((byte)headerType); // 5: header_type_flag
            buf.Write(granulepos); // 6 - 13: absolute granule
                                   // position
            buf.Write(streamSerialNumber); // 14 - 17: stream
                                           // serial number
            buf.Write(pageCount); // 18 - 21: page sequence no
            buf.Write(0); // 22 - 25: page checksum
            buf.Write((byte)packetCount); // 26: page_segments
            buf.Write(packetSizes, 0, packetCount);
            return packetCount + 27;
        }

        /// <summary>
		/// Builds and returns an Ogg Page Header.
		/// </summary>
		///
		/// <param name="headerType">the header type flag (0=normal, 2=bos: beginning of stream,</param>
		/// <param name="granulepos">the absolute granule position.</param>
		/// <param name="streamSerialNumber"></param>
		/// <param name="pageCount"></param>
		/// <param name="packetCount"></param>
		/// <param name="packetSizes"></param>
		/// <returns>an Ogg Page Header.</returns>
		public static byte[] BuildOggPageHeader(int headerType, long granulepos,
                int streamSerialNumber, int pageCount, int packetCount,
                byte[] packetSizes)
        {
            byte[] data = new byte[packetCount + 27];
            WriteOggPageHeader(new BinaryWriter(new MemoryStream(data)), headerType, granulepos, streamSerialNumber,
                    pageCount, packetCount, packetSizes);
            return data;
        }

        /// <summary>
		/// Writes a Speex Header to the given byte array.
		/// </summary>
		/// <param name="buf">the buffer to write to.</param>
		/// <param name="offset">the from which to start writing.</param>
		/// <returns>the amount of data written to the buffer.</returns>
		public static int WriteSpeexHeader(
            BinaryWriter buf,
            int sampleRate,
            int mode,
            int channels,
            bool vbr,
            int nframes)
        {
            buf.Write(System.Text.Encoding.UTF8.GetBytes("Speex   ")); // 0 - 7: speex_string
            buf.Write(System.Text.Encoding.UTF8.GetBytes("speex-1.0")); // 8 - 27: speex_version
            for (int i = 0; i < 11; i++)
                buf.Write(Byte.MinValue); // (fill in up to 20 bytes)
            buf.Write(1); // 28 - 31: speex_version_id
            buf.Write(80); // 32 - 35: header_size
            buf.Write(sampleRate); // 36 - 39: rate
            buf.Write(mode); // 40 - 43: mode (0=NB, 1=WB, 2=UWB)
            buf.Write(4); // 44 - 47: mode_bitstream_version
            buf.Write(channels); // 48 - 51: nb_channels
            buf.Write(-1); // 52 - 55: bitrate
            buf.Write(160 << mode); // 56 - 59: frame_size
                                    // (NB=160, WB=320, UWB=640)
            buf.Write((vbr) ? 1 : 0); // 60 - 63: vbr
            buf.Write(nframes); // 64 - 67: frames_per_packet
            buf.Write(0); // 68 - 71: extra_headers
            buf.Write(0); // 72 - 75: reserved1
            buf.Write(0); // 76 - 79: reserved2
            return 80;
        }

        /// <summary>
		/// Builds a Speex Header.
		/// </summary>
		/// <returns>a Speex Header.</returns>
		public static byte[] BuildSpeexHeader(
            int sampleRate, int mode,
            int channels, bool vbr, int nframes)
        {
            byte[] data = new byte[80];
            WriteSpeexHeader(new BinaryWriter(new MemoryStream(data)), sampleRate, mode, channels, vbr, nframes);
            return data;
        }

        /// <summary>
		/// Writes a Speex Comment to the given byte array.
		/// </summary>
		/// <param name="buf">the buffer to write to.</param>
		/// <param name="offset">the from which to start writing.</param>
		/// <param name="comment">the comment.</param>
		/// <returns>the amount of data written to the buffer.</returns>
		public static int WriteSpeexComment(BinaryWriter buf, String comment)
        {
            int length = comment.Length;
            buf.Write(length); // vendor comment size
            buf.Write(System.Text.Encoding.UTF8.GetBytes(comment), 0, length); // vendor comment
            buf.Write(0); // user comment list length
            return length + 8;
        }

        /// <summary>
		/// Builds and returns a Speex Comment.
		/// </summary>
		/// <param name="comment">the comment.</param>
		/// <returns>a Speex Comment.</returns>
		public static byte[] BuildSpeexComment(String comment)
        {
            byte[] data = new byte[comment.Length + 8];
            WriteSpeexComment(new BinaryWriter(new MemoryStream(data)), comment);
            return data;
        }

        /**
         * Writes a Little-endian int.
         * @param os - the output stream to write to.
         * @param v - the value to write.
         * @exception IOException
         */
        public static void writeInt(BinaryWriter os, int v)
        {
            os.Write(0xff & v);
            os.Write(0xff & (v >> 8));
            os.Write(0xff & (v >> 16));
            os.Write(0xff & (v >> 24));
        }
    }
}
