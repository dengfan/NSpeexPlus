using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace WpfDemo
{
    public class MainViewModel
    {
        public ObservableCollection<SpxItemViewModel> SpxList { get; set; }
    }

    public class SpxItemViewModel
    {
        public double TimeLength { get; set; }
        public string SpxFilePath { get; set; }
        public string SpxFileName
        {
            get
            {
                return SpxFilePath.Split('\\').Last().ToString();
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
    }
}
