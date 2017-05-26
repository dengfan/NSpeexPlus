using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NSpeex.Plus
{
    /**
 * Ogg Speex Writer
 * 
 * @author Marc Gimpel, Wimba S.A. (mgimpel@horizonwimba.com)
 * @version $Revision$
 */
    public class OggSpeexWriter : AudioFileWriter
    {
        /** Number of packets in an Ogg page (must be less than 255) */
        public static readonly int PACKETS_PER_OGG_PAGE = 250;

        /** The OutputStream */
        private BinaryWriter xout;

        /** Defines the encoder mode (0=NB, 1=WB and 2-UWB). */
        private int mode;
        /** Defines the sampling rate of the audio input. */
        private int sampleRate;
        /** Defines the number of channels of the audio input (1=mono, 2=stereo). */
        private int channels;
        /** Defines the number of frames per speex packet. */
        private int nframes;
        /** Defines whether or not to use VBR (Variable Bit Rate). */
        private bool vbr;
        /** Ogg Stream Serial Number */
        private int streamSerialNumber;
        /** Data buffer */
        private byte[] dataBuffer;
        /** Pointer within the Data buffer */
        private int dataBufferPtr;
        /** Header buffer */
        private byte[] headerBuffer;
        /** Pointer within the Header buffer */
        private int headerBufferPtr;
        /** Ogg Page count */
        private int pageCount;
        /** Speex packet count within an Ogg Page */
        private int packetCount;
        /**
         * Absolute granule position
         * (the number of audio samples from beginning of file to end of Ogg Packet).
         */
        private long granulepos;

        /**
         * Builds an Ogg Speex Writer. 
         */
        public OggSpeexWriter()
        {
            if (streamSerialNumber == 0)
                streamSerialNumber = new Random().Next();
            dataBuffer = new byte[65565];
            dataBufferPtr = 0;
            headerBuffer = new byte[255];
            headerBufferPtr = 0;
            pageCount = 0;
            packetCount = 0;
            granulepos = 0;
        }

        /**
         * Builds an Ogg Speex Writer. 
         * @param mode       the mode of the encoder (0=NB, 1=WB, 2=UWB).
         * @param sampleRate the number of samples per second.
         * @param channels   the number of audio channels (1=mono, 2=stereo, ...).
         * @param nframes    the number of frames per speex packet.
         * @param vbr
         */
        public OggSpeexWriter(int mode,
                              int sampleRate,
                              int channels,
                              int nframes,
                              bool vbr) : this()
        {
            setFormat(mode, sampleRate, channels, nframes, vbr);
        }

        /**
         * Sets the output format.
         * Must be called before WriteHeader().
         * @param mode       the mode of the encoder (0=NB, 1=WB, 2=UWB).
         * @param sampleRate the number of samples per second.
         * @param channels   the number of audio channels (1=mono, 2=stereo, ...).
         * @param nframes    the number of frames per speex packet.
         * @param vbr
         */
        private void setFormat(int mode,
                               int sampleRate,
                               int channels,
                               int nframes,
                               bool vbr)
        {
            this.mode = mode;
            this.sampleRate = sampleRate;
            this.channels = channels;
            this.nframes = nframes;
            this.vbr = vbr;
        }

        /**
         * Sets the Stream Serial Number.
         * Must not be changed mid stream.
         * @param serialNumber
         */
        public void setSerialNumber(int serialNumber)
        {
            this.streamSerialNumber = serialNumber;
        }

        /**
         * Closes the output file.
         * @exception IOException if there was an exception closing the Audio Writer.
         */
        public override void Close()
        {
            flush(true);
            xout.Close();
        }

        /**
         * Open the output file. 
         * @param file - file to open.
         * @exception IOException if there was an exception opening the Audio Writer.
         */
        public override void Open(Stream file)
        {
            xout = new BinaryWriter(file);
        }

        /**
         * Open the output file. 
         * @param filename - file to open.
         * @exception IOException if there was an exception opening the Audio Writer.
         */
        public override void Open(String filename)
        {
            Open(new FileStream(filename, FileMode.OpenOrCreate));
        }

        /**
         * Writes the header pages that start the Ogg Speex file. 
         * Prepares file for data to be written.
         * @param comment description to be included in the header.
         * @exception IOException
         */
        public override void WriteHeader(String comment)
        {
            int chksum;
            byte[]
            header;
            byte[]
            data;
            /* writes the OGG header page */
            header = buildOggPageHeader(2, 0, streamSerialNumber, pageCount++, 1,
                                        new byte[] { 80 });
            data = buildSpeexHeader(sampleRate, mode, channels, vbr, nframes);
            chksum = OggCrc.checksum(0, header, 0, header.Length);
            chksum = OggCrc.checksum(chksum, data, 0, data.Length);
            writeInt(header, 22, chksum);
            xout.Write(header);
            xout.Write(data);
            /* writes the OGG comment page */
            header = buildOggPageHeader(0, 0, streamSerialNumber, pageCount++, 1,
                                            new byte[] { (byte)(comment.Length + 8) });
            data = buildSpeexComment(comment);
            chksum = OggCrc.checksum(0, header, 0, header.Length);
            chksum = OggCrc.checksum(chksum, data, 0, data.Length);
            writeInt(header, 22, chksum);
            xout.Write(header);
            xout.Write(data);
        }

        /**
         * Writes a packet of audio. 
         * @param data - audio data.
         * @param offset - the offset from which to start reading the data.
         * @param len - the length of data to read.
         * @exception IOException
         */
        public override void WritePacket(byte[] data,
                                int offset,
                                int len)
        {
            if (len <= 0)
            { // nothing to write
                return;
            }
            if (packetCount > PACKETS_PER_OGG_PAGE)
            {
                flush(false);
            }
            Array.Copy(data, offset, dataBuffer, dataBufferPtr, len);
            dataBufferPtr += len;
            headerBuffer[headerBufferPtr++] = (byte)len;
            packetCount++;
            granulepos += nframes * (mode == 2 ? 640 : (mode == 1 ? 320 : 160));
        }

        /**
         * Flush the Ogg page out of the buffers into the file.
         * @param eos - end of stream
         * @exception IOException
         */
        private void flush(bool eos)
        {
            int chksum;
            byte[] header;
            /* writes the OGG header page */
            header = buildOggPageHeader((eos ? 4 : 0), granulepos, streamSerialNumber,
                                        pageCount++, packetCount, headerBuffer);
            chksum = OggCrc.checksum(0, header, 0, header.Length);
            chksum = OggCrc.checksum(chksum, dataBuffer, 0, dataBufferPtr);
            writeInt(header, 22, chksum);
            xout.Write(header);
            xout.Write(dataBuffer, 0, dataBufferPtr);
            dataBufferPtr = 0;
            headerBufferPtr = 0;
            packetCount = 0;
        }
    }

}
