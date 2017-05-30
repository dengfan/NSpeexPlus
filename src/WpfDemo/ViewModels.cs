using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace WpfDemo
{
    public class MainViewModel
    {
        public ObservableCollection<SpxItemViewModel> SpxList { get; set; }
    }

    public class SpxItemViewModel : INotifyPropertyChanged
    {
        private double timeLength;
        private string encodedSpxFilePath;
        private string decodedWavFilePath;

        public double TimeLength
        {
            get
            {
                return timeLength;
            }
            set
            {
                if (value != this.timeLength)
                {
                    this.timeLength = value;
                    NotifyPropertyChanged("TimeLength");
                    NotifyPropertyChanged("SecondInfo");
                    NotifyPropertyChanged("Width");
                }
            }
        }
        public string EncodedSpxFilePath
        {
            get
            {
                return encodedSpxFilePath;
            }
            set
            {
                if (value != this.encodedSpxFilePath)
                {
                    this.encodedSpxFilePath = value;
                    NotifyPropertyChanged("EncodedSpxFilePath");
                    NotifyPropertyChanged("SpxFileName");
                }
            }
        }
        public string DecodedWavFilePath
        {
            get
            {
                return decodedWavFilePath;
            }
            set
            {
                if (value != this.decodedWavFilePath)
                {
                    this.decodedWavFilePath = value;
                    NotifyPropertyChanged("DecodedWavFilePath");
                    NotifyPropertyChanged("BackgroundColor");
                    NotifyPropertyChanged("IsShowLoadingIcon");
                    NotifyPropertyChanged("IsSpinLoadingIcon");
                }
            }
        }
        public string SpxFileName
        {
            get
            {
                return EncodedSpxFilePath.Split('\\').Last().ToString();
            }
        }
        public string SecondInfo
        {
            get
            {
                return string.Format(@"{0}""", Convert.ToInt32(TimeLength));
            }
        }
        public int Width
        {
            get
            {
                return Convert.ToInt32(TimeLength * 5);
            }
        }

        public SolidColorBrush BackgroundColor
        {
            get
            {
                return string.IsNullOrEmpty(DecodedWavFilePath) ? new SolidColorBrush(Colors.MediumSpringGreen) : new SolidColorBrush(Colors.LightSkyBlue);
            }
        }

        public Visibility IsShowLoadingIcon
        {
            get
            {
                return string.IsNullOrEmpty(DecodedWavFilePath) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool IsSpinLoadingIcon
        {
            get
            {
                return string.IsNullOrEmpty(DecodedWavFilePath);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String propertyName = "")
        {
            if (this.PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }
}
