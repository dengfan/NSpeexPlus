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
using System.IO;

namespace WpfDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static WaveIn waveSource;
        private static WaveFileWriter waveFile;
        private static string dir = @"C:\Temp";
        private string wavFilePath;
        private string spxFilePath;

        public MainWindow()
        {
            InitializeComponent();

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;

            waveSource = new WaveIn()
            {
                WaveFormat = new WaveFormat(8000, 1)
            };

            waveSource.DataAvailable += new EventHandler<WaveInEventArgs>(WaveSource_DataAvailable);
            waveSource.RecordingStopped += new EventHandler<StoppedEventArgs>(WaveSource_RecordingStopped);

            wavFilePath = string.Format(@"{0}\{1}.wav", dir, DateTime.Now.ToString("yyyyMMddHHmmsss"));
            spxFilePath = wavFilePath + ".spx";
            waveFile = new WaveFileWriter(wavFilePath, waveSource.WaveFormat);

            waveSource.StartRecording();
        }

        void WaveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (waveFile != null)
            {
                waveFile.Write(e.Buffer, 0, e.BytesRecorded);
                waveFile.Flush();
            }
        }

        void WaveSource_RecordingStopped(object sender, StoppedEventArgs e)
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

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            btnStop.IsEnabled = false;

            waveSource.StopRecording();
        }

        private void btnWav2Spx_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog()
            {
                InitialDirectory = dir,
                DefaultExt = ".wav",
                Filter = "WAVE Files (*.wav)|*.wav"
            };

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                string filename = dlg.FileName;
                new NSpeexEnc(PrintLevel.Debug).Encode(filename, null);
            }
        }

        private void btnSpx2Wav_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog()
            {
                InitialDirectory = dir,
                DefaultExt = ".spx",
                Filter = "Ogg Speex Files (*.spx)|*.spx"
            };

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                string filename = dlg.FileName;
                new NSpeexDec(PrintLevel.Debug).Decode(filename, filename + ".wav");
            }
        }
    }
}
