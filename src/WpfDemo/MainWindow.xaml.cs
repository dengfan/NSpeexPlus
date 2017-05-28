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
using System.Windows.Threading;
using System.Collections.ObjectModel;
using NSpeex.Plus;

namespace WpfDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        static string dir = @"C:\Temp";
        static WaveIn waveSource;
        static WaveFileWriter waveFile;
        static DispatcherTimer dispatcherTimer;
        static double currentTicks = 0;
        static readonly double interval = 60;
        static readonly double totalTicks = 60000 / interval;
        static MainViewModel vm;

        string wavFilePath;
        string spxFilePath;

        public MainWindow()
        {
            InitializeComponent();

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            vm = new MainViewModel()
            {
                SpxList = new ObservableCollection<SpxItemViewModel>()
            };

            DataContext = vm;
        }


        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            borderRecordBar.Visibility = Visibility.Visible;

            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(interval);
            dispatcherTimer.Start();
            borderProcess.Width = 0;
            currentTicks = 0;

            waveSource = new WaveIn()
            {
                WaveFormat = new WaveFormat(8000, 1)
            };

            waveSource.DataAvailable += (s2, e2) =>
            {
                if (waveFile != null)
                {
                    waveFile.Write(e2.Buffer, 0, e2.BytesRecorded);
                    waveFile.Flush();
                }
            };

            waveSource.RecordingStopped += (s3, e3) =>
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

                // 压缩 wav 为 spx 文件
                new NSpeexEnc().Encode(wavFilePath, (s) => {
                    double seconds = 0;
                    using (var wfr = new WaveFileReader(wavFilePath))
                    {
                        seconds = wfr.TotalTime.TotalSeconds;
                    }

                    vm.SpxList.Add(new SpxItemViewModel { TimeLength = seconds, SpxFilePath = s });
                    File.Delete(wavFilePath);
                });
            };

            wavFilePath = string.Format(@"{0}\{1}.wav", dir, DateTime.Now.ToString("yyyyMMddHHmmsss"));
            spxFilePath = wavFilePath + ".spx";
            waveFile = new WaveFileWriter(wavFilePath, waveSource.WaveFormat);

            waveSource.StartRecording();
        }

        private void StopRecording()
        {
            currentTicks = 0;
            dispatcherTimer.Stop();

            borderRecordBar.Visibility = Visibility.Collapsed;

            waveSource.StopRecording();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (currentTicks - totalTicks >= 5)
            {
                StopRecording();
                return;
            }

            borderProcess.Width = (ActualWidth / totalTicks) * ++currentTicks;
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            StopRecording();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            StopRecording();
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
                new NSpeexDec(PrintLevel.Debug).Decode(filename, null);
            }
        }


    }
}
