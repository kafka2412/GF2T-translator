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
using System.Windows.Shapes;
using WpfScreenHelper;

namespace GF2T
{
    /// <summary>
    /// OcrAreaWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class OcrAreaWindow : Window
    {
        private static bool isUIInitialized = false;
        public OcrAreaWindow()
        {
            InitializeComponent();
            isUIInitialized = true;
        }

        public void SetWindowSize(double width, double height)
        {
            this.Width = width;
            this.Height = height;
        }
    }
}
