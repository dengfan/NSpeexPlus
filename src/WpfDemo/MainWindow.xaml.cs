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
        static WaveOutEvent wavePlayer;
        static WaveIn waveSource;
        static WaveFileWriter waveFile;
        static DispatcherTimer dispatcherTimer;
        static SpxItemViewModel recordingItem;
        static bool? isCancel;
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
            // 录音开始时，强制关闭已有的播放
            if (wavePlayer != null)
            {
                wavePlayer.Stop();
                wavePlayer.Dispose();
            }

            btnRecord.IsEnabled = false;

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

                btnRecord.IsEnabled = true;

                if (isCancel == null || isCancel == false) // 发送或未取消
                {
                    // 为了演示压缩和解压的功能，这里先压缩原始wav为spx文件，再解压spx文件为wav文件，最后插入数据，UI自动更新
                    new NSpeexEnc().Encode(wavFilePath, (sfp) =>
                    {
                        new NSpeexDec().Decode(sfp, (wfp) =>
                        {
                            double seconds = 0;
                            using (var wfr = new WaveFileReader(wavFilePath))
                            {
                                seconds = wfr.TotalTime.TotalSeconds;
                            }

                            // 本次录音完成，更新数据并自动刷新UI
                            recordingItem.TimeLength = seconds;
                            recordingItem.EncodedSpxFilePath = sfp;
                            recordingItem.DecodedWavFilePath = wfp;

                            scrollViewer1.ScrollToBottom();
                        });
                    });
                }
                else // 被取消
                {
                    if (recordingItem != null)
                    {
                        vm.SpxList.Remove(recordingItem);
                    }
                    
                    File.Delete(wavFilePath);
                }
            };

            string dateTimeStr = DateTime.Now.ToString("yyyyMMddHHmmsss");
            wavFilePath = string.Format(@"{0}\{1}.wav", dir, dateTimeStr);
            spxFilePath = wavFilePath + ".spx";
            waveFile = new WaveFileWriter(wavFilePath, waveSource.WaveFormat);

            // 录音中的占位项
            recordingItem = new SpxItemViewModel { TimeLength = 1, EncodedSpxFilePath = string.Format("{0}.wav.spx is recording...", dateTimeStr), DecodedWavFilePath = null };
            vm.SpxList.Add(recordingItem);

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
            isCancel = currentTicks < 20; // 录音时音过短
            StopRecording();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            isCancel = true;
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

        private void SpeechItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!btnRecord.IsEnabled) // 正在录音中，不允许播放
            {
                return;
            }

            var g = sender as Grid;
            var vm = g.DataContext as SpxItemViewModel;
            if (vm == null || string.IsNullOrEmpty(vm.DecodedWavFilePath))
            {
                return;
            }

            if (wavePlayer != null)
            {
                wavePlayer.Stop();
                wavePlayer.Dispose();
            }

            WaveStream wareStream = new WaveFileReader(vm.DecodedWavFilePath);
            WaveChannel32 volumeStream = new WaveChannel32(wareStream);
            wavePlayer = new WaveOutEvent();
            wavePlayer.Init(volumeStream);
            wavePlayer.Play();
            wavePlayer.PlaybackStopped += (s2, e2) =>
            {
                var p = s2 as WaveOutEvent;
                if (p != null)
                {
                    p.Stop();
                    p.Dispose();

                    volumeStream.Close();
                    volumeStream.Dispose();

                    wareStream.Close();
                    wareStream.Dispose();
                }
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (wavePlayer != null)
            {
                wavePlayer.Dispose();
            }
        }
    }
}
