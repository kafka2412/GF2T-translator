using HtmlAgilityPack;
using PuppeteerSharp;
using Page = PuppeteerSharp.Page;
using System.Windows;
using System.Diagnostics;
using System.Windows.Threading;

namespace GF2T
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string sk = "zh-CN";
        private string tk = "ko";

        //private readonly TaskQueue taskQueue = new();
        //private readonly BlockingCollection<string> trList = new();

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
            InitWebBrowser();

            // Following code will watch automatically kill chromeDriver.exe
            BootWatchDog();
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
    }
}