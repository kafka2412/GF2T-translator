using HtmlAgilityPack;
using PuppeteerSharp;
using Page = PuppeteerSharp.Page;
using System.Windows;
using System.Diagnostics;
using System.Windows.Threading;
using System.IO;
using System.Text;
using System.Printing;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;

namespace GF2T
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string sk = "zh-CN";
        private string tk = "ko";
        private static string ocrText = string.Empty;
        private OcrAreaWindow ocrWindow;
        private double ocrWindowLeft;
        private double ocrWindowTop;

        private static string ocrLibPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ocrlib");
        private static string modelPath = Path.Combine(ocrLibPath, "models");
        private static string imageDirPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "temp");

        //private readonly TaskQueue taskQueue = new();
        //private readonly BlockingCollection<string> trList = new();

        private bool isUIInitialized = false;

        private Browser browser = null;
        private async Task<Browser> initBrowser()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
            var _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[] {
                    "--js-flags=\"--max-old-space-size=128\""
                },
            });
            return _browser;
        }
        private Page webPage = null;

        public MainWindow()
        {
            InitializeComponent();
            isUIInitialized = true;
            InitOcrWindow();
            LoadUserSettings();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            InitWebBrowser();

            // Following code will watch automatically kill chromeDriver.exe
            BootWatchDog();
        }

        private void LoadUserSettings()
        {
            var mainPosLeft = Properties.Settings.Default.mainPosLeft;
            var mainPosTop = Properties.Settings.Default.mainPosTop;
            var ocrPosLeft = Properties.Settings.Default.ocrPosLeft;
            var ocrPosTop = Properties.Settings.Default.ocrPosTop;
            var mainWidth = Properties.Settings.Default.mainWidth;
            var mainHeight = Properties.Settings.Default.mainHeight;

            if (mainPosLeft != 0 && mainPosTop != 0)
            {
                mainWindow.Left = mainPosLeft;
                mainWindow.Top = mainPosTop;
            }

            if (ocrPosLeft != 0 && ocrPosTop != 0)
            {
                ocrWindow.Left = ocrPosLeft;
                ocrWindow.Top = ocrPosTop;
                tbOcrLeft.Text = ocrPosLeft.ToString();
                tbOcrTop.Text = ocrPosTop.ToString();
            }

            if (mainWidth != 0 && mainHeight != 0)
            {
                mainWindow.Width = mainWidth;
                mainWindow.Height = mainHeight;
            }
        }

        private void InitOcrWindow()
        {
            var ocrWidth = Properties.Settings.Default.ocrWidth;
            var ocrHeight = Properties.Settings.Default.ocrHeight;
            ocrWindow = new OcrAreaWindow();
            if (ocrWidth != 0 && ocrHeight != 0)
            {
                ocrWindow.SetWindowSize(ocrWidth, ocrHeight);
            }
            ocrWindow.Show();
        }

        private static void BootWatchDog()
        {
            var pid = Process.GetCurrentProcess().Id;
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "SelfCleaner.exe";
            info.Arguments = $"{pid}";
            info.WorkingDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            Process watchdogProcess = Process.Start(info);
        }

        private void InitWebBrowser()
        {
            var browserTask = Task.Run(async () => await initBrowser());
            browser = browserTask.GetAwaiter().GetResult();
            var pageTask = Task.Run(async () => await browser.NewPageAsync());
            webPage = pageTask.GetAwaiter().GetResult();
            webPage.DefaultTimeout = 0;
        }

        private async Task<string> RequestTranslate(string url)
        {
            await webPage.GoToAsync(url, WaitUntilNavigation.Networkidle2);
            var content = await webPage.GetContentAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            string translated = string.Empty;
            try
            {
                var pathElement = doc.GetElementbyId("txtTarget");
                translated = pathElement.InnerText.Trim();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            return translated;
        }

        private void Translate()
        {
            string sentence = tbOriginal.Text;
            string testUrl = $"https://papago.naver.com/?sk={sk}&tk={tk}&st={Uri.EscapeDataString(sentence)}";

            string translated = string.Empty;
            try
            {
                var translateTask = Task.Run(async () => await RequestTranslate(testUrl));
                translated = translateTask.GetAwaiter().GetResult();
                tbTranslated.Text = translated;
            }
            catch (Exception ex)
            {
                tbTranslated.Text = "번역실패";
            }
        }

        private void btTranslate_Click(object sender, RoutedEventArgs e)
        {
            CaptureOcrArea();
            RunOcr();
            tbOriginal.Text = ocrText;
            btTranslateWork();
        }

        private void btTranslateWork()
        {
            if (!tbOriginal.Text.Equals(""))
            {
                Thread thread = new Thread(
                () =>
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    {
                        //grProgress.Visibility = Visibility.Visible;
                    }));
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        Translate();
                        //grProgress.Visibility = Visibility.Collapsed;
                    }));

                    Dispatcher.Run();
                });
                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }

        private void btOcr_Click(object sender, RoutedEventArgs e)
        {
            CaptureOcrArea();
            RunOcr();
            tbOriginal.Text = ocrText;
            btTranslateWork();
        }

        private void RunOcr()
        {
            string imagePath = Path.Combine(imageDirPath, "output.png");
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(ocrLibPath, "OcrLiteOnnx.exe"),
                Arguments = $"-d {modelPath} -i {imagePath} -t 4 -p 50 -s 0 -b 0.6 -o 0.5 -a 0 -A 0",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            //process.Exited += (s, e) =>
            //{
            // save to text file
            //var process = (Process)s;
            //using (var f = File.CreateText(Path.Combine(imageDirPath, "2.txt")))
            //{
            //    f.Write(process.StandardOutput.ReadToEnd());
            //}
            //};
            //process.OutputDataReceived += (s, e) =>
            //{
            //    //ocrText += output;
            //};
            process.Start();
            //process.BeginOutputReadLine();

            //string output = string.Empty;
            //while (!process.StandardOutput.EndOfStream)
            //{
            //    output += process.StandardOutput.ReadToEnd();
            //}

            ocrText = process.StandardOutput.ReadToEnd();

            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                tbOcr.Text += $"{ocrText}{Environment.NewLine}";
            }
            else
            {
                tbOcr.Text += $"Process failed with exit code {process.ExitCode}.{Environment.NewLine}";
            }
        }

        private void btArea_Click(object sender, RoutedEventArgs e)
        {
            ocrWindowLeft = GetWindowLeft(ocrWindow);
            ocrWindowTop = GetWindowTop(ocrWindow);
            Properties.Settings.Default.mainPosLeft = ocrWindowLeft;
            Properties.Settings.Default.mainPosTop = ocrWindowTop;
            Properties.Settings.Default.Save();

            tbOcrLeft.Text = ocrWindowLeft.ToString();
            tbOcrTop.Text = ocrWindowTop.ToString();
        }

        private double GetWindowLeft(Window window)
        {
            if (window.WindowState == WindowState.Maximized)
            {
                var leftField = typeof(Window).GetField("_actualLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return (double)leftField.GetValue(window);
            }
            else
                return window.Left;
        }

        private double GetWindowTop(Window window)
        {
            if (window.WindowState == WindowState.Maximized)
            {
                var topField = typeof(Window).GetField("_actualTop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return (double)topField.GetValue(window);
            }
            else
                return window.Top;
        }

        private void btCapture_Click(object sender, RoutedEventArgs e)
        {
            CaptureOcrArea();
        }

        private void CaptureOcrArea()
        {
            if (ocrWindow != null)
            {
                int width = (int)ocrWindow.Width;
                int height = (int)ocrWindow.Height;

                Rectangle rect = new((int)ocrWindowLeft, (int)ocrWindowTop, width, height); // Define the area to capture
                Bitmap bmp = new(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb); // Create a new bitmap
                Graphics g = Graphics.FromImage(bmp); // Get a Graphics object from the bitmap
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy); // Copy the screen content into the bitmap
                bmp.Save(Path.Combine(imageDirPath, "output.png"), ImageFormat.Png); // Save the bitmap as an image file
                
            }
        }

        private void btOcrPosReset_Click(object sender, RoutedEventArgs e)
        {
            ocrWindow.Left = 100;
            ocrWindow.Top = 100;
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (isUIInitialized)
            {
                var top = mainWindow.Top;
                var left = mainWindow.Left;
                if (isMinimized(top, left) || isMaximized(top, left))
                {
                    return;
                }
                
                if(mainWindow.WindowState.Equals(WindowState.Normal))
                {
                    Properties.Settings.Default.mainPosLeft = mainWindow.Left;
                    Properties.Settings.Default.mainPosTop = mainWindow.Top;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (isUIInitialized)
            {
                var top = mainWindow.Top;
                var left = mainWindow.Left;
                if (isMinimized(top, left) || isMaximized(top, left))
                {
                    return;
                }

                if (mainWindow.WindowState.Equals(WindowState.Normal))
                {
                    Properties.Settings.Default.mainWidth = mainWindow.Width;
                    Properties.Settings.Default.mainHeight = mainWindow.Height;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void btOcrToggle_Click(object sender, RoutedEventArgs e)
        {
            if (btOcrToggle.Content.Equals("숨기기"))
            {
                btOcrToggle.Content = "보이기";
                //ocrWindow.Visibility = Visibility.Hidden;
                ocrWindow.Hide();
            }
            else
            {
                btOcrToggle.Content = "숨기기";
                //ocrWindow.Visibility = Visibility.Visible;
                ocrWindow.Show();
            }
        }

        private void btExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private bool isMinimized(double top, double left)
        {
            if (top < -1 * SystemParameters.VirtualScreenHeight ||
                    left < -1 * SystemParameters.VirtualScreenWidth)
            {// To skip the case of minimized window
                return true;
            }
            return false;
        }

        private bool isMaximized(double top, double left)
        {
            if (top > SystemParameters.VirtualScreenHeight ||
                left > SystemParameters.VirtualScreenWidth)
            { // To skip the case of maximized window
                return true;
            }
            return false;
        }
    }
}
