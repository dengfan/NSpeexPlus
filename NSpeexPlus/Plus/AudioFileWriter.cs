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
         * Writes an Ogg Page Header to the given byte array.
         * 
         * Ogg Page Header structure:
         * <pre>
         *  0 -  3: capture_pattern
         *       4: stream_structure_version
         *       5: header_type_flag
         *  6 - 13: absolute granule position
         * 14 - 17: stream serial number
         * 18 - 21: page sequence no
         * 22 - 25: page checksum
         *      26: page_segments
         * 27 -  x: segment_table
         * </pre>
         * 
         * @param buf     the buffer to write to.
         * @param offset  the from which to start writing.
         * @param headerType the header type flag
         *          (0=normal, 2=bos: beginning of stream, 4=eos: end of stream).
         * @param granulepos the absolute granule position.
         * @param streamSerialNumber
         * @param pageCount
         * @param packetCount
         * @param packetSizes
         * @return the amount of data written to the buffer.
         */
        public static int writeOggPageHeader(byte[] buf, int offset, int headerType,
                                             long granulepos, int streamSerialNumber,
                                             int pageCount, int packetCount,
                                             byte[] packetSizes)
        {
            writeString(buf, offset, "OggS");             //  0 -  3: capture_pattern
            buf[offset + 4] = 0;                            //       4: stream_structure_version
            buf[offset + 5] = (byte)headerType;            //       5: header_type_flag
            writeLong(buf, offset + 6, granulepos);         //  6 - 13: absolute granule position
            writeInt(buf, offset + 14, streamSerialNumber); // 14 - 17: stream serial number
            writeInt(buf, offset + 18, pageCount);          // 18 - 21: page sequence no
            writeInt(buf, offset + 22, 0);                  // 22 - 25: page checksum
            buf[offset + 26] = (byte)packetCount;          //      26: page_segments
            Array.Copy(packetSizes, 0,              // 27 -  x: segment_table
                             buf, offset + 27, packetCount);
            return packetCount + 27;
        }

        /**
         * Builds and returns an Ogg Page Header.
         * @param headerType the header type flag
         *          (0=normal, 2=bos: beginning of stream, 4=eos: end of stream).
         * @param granulepos the absolute granule position.
         * @param streamSerialNumber
         * @param pageCount
         * @param packetCount
         * @param packetSizes
         * @return an Ogg Page Header.
         */
        public static byte[] buildOggPageHeader(int headerType, long granulepos,
                                                int streamSerialNumber, int pageCount,
                                                int packetCount, byte[] packetSizes)
        {
            byte[] data = new byte[packetCount + 27];
            writeOggPageHeader(data, 0, headerType, granulepos, streamSerialNumber,
                               pageCount, packetCount, packetSizes);
            return data;
        }

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
        public static int writeSpeexHeader(byte[] buf, int offset, int sampleRate,
                                           int mode, int channels, bool vbr,
                                           int nframes)
        {
            writeString(buf, offset, "Speex   ");    //  0 -  7: speex_string
            writeString(buf, offset + 8, "speex-1.0"); //  8 - 27: speex_version
            Array.Copy(new byte[11], 0, buf, offset + 17, 11); // : speex_version (fill in up to 20 bytes)
            writeInt(buf, offset + 28, 1);           // 28 - 31: speex_version_id
            writeInt(buf, offset + 32, 80);          // 32 - 35: header_size
            writeInt(buf, offset + 36, sampleRate);  // 36 - 39: rate
            writeInt(buf, offset + 40, mode);        // 40 - 43: mode (0=NB, 1=WB, 2=UWB)
            writeInt(buf, offset + 44, 4);           // 44 - 47: mode_bitstream_version
            writeInt(buf, offset + 48, channels);    // 48 - 51: nb_channels
            writeInt(buf, offset + 52, -1);          // 52 - 55: bitrate
            writeInt(buf, offset + 56, 160 << mode); // 56 - 59: frame_size (NB=160, WB=320, UWB=640)
            writeInt(buf, offset + 60, vbr ? 1 : 0);     // 60 - 63: vbr
            writeInt(buf, offset + 64, nframes);     // 64 - 67: frames_per_packet
            writeInt(buf, offset + 68, 0);           // 68 - 71: extra_headers
            writeInt(buf, offset + 72, 0);           // 72 - 75: reserved1
            writeInt(buf, offset + 76, 0);           // 76 - 79: reserved2
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
        public static byte[] buildSpeexHeader(int sampleRate, int mode, int channels,
                                              bool vbr, int nframes)
        {
            byte[] data = new byte[80];
            writeSpeexHeader(data, 0, sampleRate, mode, channels, vbr, nframes);
            return data;
        }

        /**
         * Writes a Speex Comment to the given byte array.
         * @param buf     the buffer to write to.
         * @param offset  the from which to start writing.
         * @param comment the comment.
         * @return the amount of data written to the buffer.
         */
        public static int writeSpeexComment(byte[] buf, int offset, String comment)
        {
            int length = comment.Length;
            writeInt(buf, offset, length);       // vendor comment size
            writeString(buf, offset + 4, comment); // vendor comment
            writeInt(buf, offset + length + 4, 0);   // user comment list length
            return length + 8;
        }

        /**
         * Builds and returns a Speex Comment.
         * @param comment the comment.
         * @return a Speex Comment.
         */
        public static byte[] buildSpeexComment(String comment)
        {
            byte[] data = new byte[comment.Length + 8];
            writeSpeexComment(data, 0, comment);
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
            os.Write((0xff & (v >> 8)));
        }

        public static void writeShort(BinaryWriter os, ushort v)
        {
            os.Write((0xff & v));
            os.Write((0xff & (v >> 8)));
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

        /**
         * Writes a Little-endian long.
         * @param os - the output stream to write to.
         * @param v - the value to write.
         * @exception IOException
         */
        public static void writeLong(BinaryWriter os, long v)
        {
            os.Write((int)(0xff & v));
            os.Write((int)(0xff & (v >> 8)));
            os.Write((int)(0xff & (v >> 16)));
            os.Write((int)(0xff & (v >> 24)));
            os.Write((int)(0xff & (v >> 32)));
            os.Write((int)(0xff & (v >> 40)));
            os.Write((int)(0xff & (v >> 48)));
            os.Write((int)(0xff & (v >> 56)));
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
            data[offset + 1] = (byte)(0xff & (v >> 8));
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
            data[offset + 1] = (byte)(0xff & (v >> 8));
            data[offset + 2] = (byte)(0xff & (v >> 16));
            data[offset + 3] = (byte)(0xff & (v >> 24));
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
            data[offset + 1] = (byte)(0xff & (v >> 8));
            data[offset + 2] = (byte)(0xff & (v >> 16));
            data[offset + 3] = (byte)(0xff & (v >> 24));
            data[offset + 4] = (byte)(0xff & (v >> 32));
            data[offset + 5] = (byte)(0xff & (v >> 40));
            data[offset + 6] = (byte)(0xff & (v >> 48));
            data[offset + 7] = (byte)(0xff & (v >> 56));
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
    }

}
