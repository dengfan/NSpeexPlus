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

        /**
         * Writes a Speex Header to the given byte array.
         * 
         * Speex Header structure:
         * <pre>
         *  0 -  7: speex_string
         *  8 - 27: speex_version
         * 28 - 31: speex_version_id
         * 32 - 35: header_size
         * 36 - 39: rate
         * 40 - 43: mode (0=NB, 1=WB, 2=UWB)
         * 44 - 47: mode_bitstream_version
         * 48 - 51: nb_channels
         * 52 - 55: bitrate
         * 56 - 59: frame_size (NB=160, WB=320, UWB=640)
         * 60 - 63: vbr
         * 64 - 67: frames_per_packet
         * 68 - 71: extra_headers
         * 72 - 75: reserved1
         * 76 - 79: reserved2
         * </pre>
         *
         * @param buf     the buffer to write to.
         * @param offset  the from which to start writing.
         * @param sampleRate
         * @param mode
         * @param channels
         * @param vbr
         * @param nframes
         * @return the amount of data written to the buffer.
         */
        //public static int WriteSpeexHeader(byte[] buf, int offset, int sampleRate,
        //                                   int mode, int channels, bool vbr,
        //                                   int nframes)
        //{
        //    writeString(buf, offset, "Speex   ");    //  0 -  7: speex_string
        //    writeString(buf, offset + 8, "speex-1.0"); //  8 - 27: speex_version
        //    Array.Copy(new byte[11], 0, buf, offset + 17, 11); // : speex_version (fill in up to 20 bytes)
        //    writeInt(buf, offset + 28, 1);           // 28 - 31: speex_version_id
        //    writeInt(buf, offset + 32, 80);          // 32 - 35: header_size
        //    writeInt(buf, offset + 36, sampleRate);  // 36 - 39: rate
        //    writeInt(buf, offset + 40, mode);        // 40 - 43: mode (0=NB, 1=WB, 2=UWB)
        //    writeInt(buf, offset + 44, 4);           // 44 - 47: mode_bitstream_version
        //    writeInt(buf, offset + 48, channels);    // 48 - 51: nb_channels
        //    writeInt(buf, offset + 52, -1);          // 52 - 55: bitrate
        //    writeInt(buf, offset + 56, 160 << mode); // 56 - 59: frame_size (NB=160, WB=320, UWB=640)
        //    writeInt(buf, offset + 60, vbr ? 1 : 0);     // 60 - 63: vbr
        //    writeInt(buf, offset + 64, nframes);     // 64 - 67: frames_per_packet
        //    writeInt(buf, offset + 68, 0);           // 68 - 71: extra_headers
        //    writeInt(buf, offset + 72, 0);           // 72 - 75: reserved1
        //    writeInt(buf, offset + 76, 0);           // 76 - 79: reserved2
        //    return 80;
        //}

        /// <summary>
        /// Writes a Speex Header to the given byte array.
        /// </summary>
        /// <param name="buf">the buffer to write to.</param>
        /// <param name="offset">the from which to start writing.</param>
        /// <returns>the amount of data written to the buffer.</returns>
        protected static int WriteSpeexHeader(
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

        /**
         * Builds a Speex Header.
         * @param sampleRate
         * @param mode
         * @param channels
         * @param vbr
         * @param nframes
         * @return a Speex Header.
         */
        //public static byte[] BuildSpeexHeader(int sampleRate, int mode, int channels,
        //                                      bool vbr, int nframes)
        //{
        //    byte[] data = new byte[80];
        //    WriteSpeexHeader(data, 0, sampleRate, mode, channels, vbr, nframes);
        //    return data;
        //}

        /// <summary>
        /// Builds a Speex Header.
        /// </summary>
        /// <returns>a Speex Header.</returns>
        protected static byte[] BuildSpeexHeader(
            int sampleRate, int mode,
            int channels, bool vbr, int nframes)
        {
            byte[] data = new byte[80];
            WriteSpeexHeader(new BinaryWriter(new MemoryStream(data)), sampleRate, mode, channels, vbr, nframes);
            return data;
        }

        /**
         * Writes a Speex Comment to the given byte array.
         * @param buf     the buffer to write to.
         * @param offset  the from which to start writing.
         * @param comment the comment.
         * @return the amount of data written to the buffer.
         */
        //public static int WriteSpeexComment(byte[] buf, int offset, String comment)
        //{
        //    int length = comment.Length;
        //    writeInt(buf, offset, length);       // vendor comment size
        //    writeString(buf, offset + 4, comment); // vendor comment
        //    writeInt(buf, offset + length + 4, 0);   // user comment list length
        //    return length + 8;
        //}

        /// <summary>
		/// Writes a Speex Comment to the given byte array.
		/// </summary>
		/// <param name="buf">the buffer to write to.</param>
		/// <param name="offset">the from which to start writing.</param>
		/// <param name="comment">the comment.</param>
		/// <returns>the amount of data written to the buffer.</returns>
		protected static int WriteSpeexComment(BinaryWriter buf, String comment)
        {
            int length = comment.Length;
            buf.Write(length); // vendor comment size
            buf.Write(System.Text.Encoding.UTF8.GetBytes(comment), 0, length); // vendor comment
            buf.Write(0); // user comment list length
            return length + 8;
        }

        /**
         * Builds and returns a Speex Comment.
         * @param comment the comment.
         * @return a Speex Comment.
         */
        //public static byte[] BuildSpeexComment(String comment)
        //{
        //    byte[] data = new byte[comment.Length + 8];
        //    WriteSpeexComment(data, 0, comment);
        //    return data;
        //}

        /// <summary>
        /// Builds and returns a Speex Comment.
        /// </summary>
        /// <param name="comment">the comment.</param>
        /// <returns>a Speex Comment.</returns>
        protected static byte[] BuildSpeexComment(String comment)
        {
            byte[] data = new byte[comment.Length + 8];
            WriteSpeexComment(new BinaryWriter(new MemoryStream(data)), comment);
            return data;
        }

        /**
         * Writes a Little-endian short.
         * @param os - the output stream to write to.
         * @param v - the value to write.
         * @exception IOException
         */
        public static void writeShort(BinaryWriter os, short v)
        {
            os.Write((0xff & v));
            os.Write((0xff & (aaa(v, 8))));
        }

        public static void writeShort(BinaryWriter os, ushort v)
        {
            os.Write((0xff & v));
            os.Write((0xff & (aaa(v, 8))));
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
            os.Write(0xff & (aaa(v, 8)));
            os.Write(0xff & (aaa(v, 16)));
            os.Write(0xff & (aaa(v, 24)));
        }

        /**
         * Writes a Little-endian long.
         * @param os - the output stream to write to.
         * @param v - the value to write.
         * @exception IOException
         */
        public static void writeLong(BinaryWriter os, long v)
        {
            os.Write((int)(0xff & v));
            os.Write((int)(0xff & (aaa(v, 8))));
            os.Write((int)(0xff & (aaa(v, 16))));
            os.Write((int)(0xff & (aaa(v, 24))));
            os.Write((int)(0xff & (aaa(v, 32))));
            os.Write((int)(0xff & (aaa(v, 40))));
            os.Write((int)(0xff & (aaa(v, 48))));
            os.Write((int)(0xff & (aaa(v, 56))));
        }

        /**
         * Writes a Little-endian short.
         * @param data   the array into which the data should be written.
         * @param offset the offset from which to start writing in the array.
         * @param v      the value to write.
         */
        public static void writeShort(byte[] data, int offset, int v)
        {
            data[offset] = (byte)(0xff & v);
            data[offset + 1] = (byte)(0xff & (aaa(v, 8)));
        }

        /**
         * Writes a Little-endian int.
         * @param data   the array into which the data should be written.
         * @param offset the offset from which to start writing in the array.
         * @param v      the value to write.
         */
        public static void writeInt(byte[] data, int offset, int v)
        {
            data[offset] = (byte)(0xff & v);
            data[offset + 1] = (byte)(0xff & (aaa(v, 8)));
            data[offset + 2] = (byte)(0xff & (aaa(v, 16)));
            data[offset + 3] = (byte)(0xff & (aaa(v, 24)));
        }

        /**
         * Writes a Little-endian long.
         * @param data   the array into which the data should be written.
         * @param offset the offset from which to start writing in the array.
         * @param v      the value to write.
         */
        public static void writeLong(byte[] data, int offset, long v)
        {
            data[offset] = (byte)(0xff & v);
            data[offset + 1] = (byte)(0xff & (aaa(v, 8)));
            data[offset + 2] = (byte)(0xff & (aaa(v, 16)));
            data[offset + 3] = (byte)(0xff & (aaa(v, 24)));
            data[offset + 4] = (byte)(0xff & (aaa(v, 32)));
            data[offset + 5] = (byte)(0xff & (aaa(v, 40)));
            data[offset + 6] = (byte)(0xff & (aaa(v, 48)));
            data[offset + 7] = (byte)(0xff & (aaa(v, 56)));
        }

        /**
         * Writes a String.
         * @param data   the array into which the data should be written.
         * @param offset the offset from which to start writing in the array.
         * @param v      the value to write.
         */
        public static void writeString(byte[] data, int offset, String v)
        {
            byte[] str = Encoding.Default.GetBytes(v);
            Array.Copy(str, 0, data, offset, str.Length);
        }

        public static int aaa(int x, int y)
        {
            int mask = 0x7fffffff; //Integer.MAX_VALUE
            for (int i = 0; i < y; i++)
            {
                x >>= 1;
                x &= mask;
            }
            return x;
        }

        public static long aaa(long x, int y)
        {
            int mask = 0x7fffffff; //Integer.MAX_VALUE
            for (int i = 0; i < y; i++)
            {
                x >>= 1;
                x &= mask;
            }
            return x;
        }

    }

}
