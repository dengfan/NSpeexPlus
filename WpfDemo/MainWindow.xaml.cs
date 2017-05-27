using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NSpeex.Plus;

namespace WpfDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private WaveIn waveSource;
        private WaveFileWriter waveFile;
        private string wavFilePath;
        private string spxFilePath;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;

            waveSource = new WaveIn();
            waveSource.WaveFormat = new WaveFormat(44100, 1);

            waveSource.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable);
            waveSource.RecordingStopped += new EventHandler<StoppedEventArgs>(waveSource_RecordingStopped);

            wavFilePath = string.Format(@"C:\Temp\{0}.wav", DateTime.Now.ToString("yyyyMMddHHmmsss"));
            spxFilePath = wavFilePath + ".spx";
            waveFile = new WaveFileWriter(wavFilePath, waveSource.WaveFormat);

            waveSource.StartRecording();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            btnStop.IsEnabled = false;

            waveSource.StopRecording();
        }

        private void btnWav2Spx_Click(object sender, RoutedEventArgs e)
        {
            wavFilePath = @"C:\Temp\20170526230839.wav";
            new NSpeexEnc().encode(wavFilePath, wavFilePath + ".spx");
        }

        private void btnSpx2Wav_Click(object sender, RoutedEventArgs e)
        {
            spxFilePath = @"C:\Temp\20170526230839.wav.spx";
            new NSpeexDec().decode(spxFilePath, spxFilePath + ".wav");
        }

        void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (waveFile != null)
            {
                waveFile.Write(e.Buffer, 0, e.BytesRecorded);
                waveFile.Flush();
            }
        }

        void waveSource_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (waveSource != null)
            {
                waveSource.Dispose();
                waveSource = null;
            }

            if (waveFile != null)
            {
                waveFile.Dispose();
                waveFile = null;
            }

            btnStart.IsEnabled = true;
        }
    }
}
