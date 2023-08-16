using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.IO;
using FuturesHelpers;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using System.Diagnostics;
using System.Configuration;
using System.Web.UI;

namespace Forecasting_Dashboard
{
    class ForecastingDashboardConfig
    {
        public string RootPythonPredictionScriptPath;

        public int ModelsCount;

        // Application data for predicting and normal operations
        public string TargetHistoricalFileStore;

        public string TargetCurrentBarsAndLags;

        public string PythonPredictionScriptFile;

        public string SourcePythonPredictionOutputFile;

        public string PythonExecutablePath;

        public string ChromeExecutablePath;

        public string RootSymbolBeingPredicted;

        public string QuoteFrameEmbeddedDisplayUrl;

        public string XPathToRenderedLastPriceElement;

        public string XPathToRenderedAskPriceElement;

        public string XPathToRenderedBidPriceElement;

        public string XPathToRenderedVolumeElement;

        public string StartLoginUrl;

        public string LoginIdElementId;

        public string LoginIDValue;

        public string LoginActionElementId;

        public string PasswordElementId;

        public string PasswordValue;

        public List<string> RootSymbolsList;
    }

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            ConfigureApp();

            foreach (string rootSym in Config.RootSymbolsList)
            {
                Symbols.Add(new FuturesPrice(FuturesHelpers.FuturesHelpers.GetFrontContract(DateTime.Now, rootSym).Item2.Symbol));
                Symbols.Add(new FuturesPrice(FuturesHelpers.FuturesHelpers.GetSecondContract(DateTime.Now, rootSym).Item2.Symbol));
            }
            CurrentPredictionInputSet = new Tuple<DateTime, FuturesPrice[]>[Symbols.Count];

            PredictorSeries = new Series[Config.ModelsCount];

            for (int i = 0; i < Config.ModelsCount; i++)
            {
                PredictorSeries[i] = new Series(""+i)
                {
                    ChartType = SeriesChartType.Line, //line chart
                    ChartArea = "ChartArea1",
                    Color = Color.Red
                };
                this.chart1.Series.Add(PredictorSeries[i]);
            }
            PriceDisplay = new Series("Price")
            {
                ChartType = SeriesChartType.Line, //line chart
                ChartArea = "ChartArea2",
                Color = Color.Blue
            };
            this.chart1.Series.Add(PriceDisplay);

            for (int i =  0; i < 20; i ++)
            {
                PredictionPriorityColors[i] = Color.FromArgb(
                    (byte)(int)(200 + i * 256 / 100), 
                    (byte)(256-(16 + i * 256 / 40)),
                    (byte)(256-(16 + i * 256 / 40)) );
            }

        }

        Configuration ConfigFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        ForecastingDashboardConfig Config = new ForecastingDashboardConfig();

        List<FuturesPrice> Symbols = new List<FuturesPrice>();

        Tuple<DateTime, FuturesPrice[]>[] CurrentPredictionInputSet;

        Timer StartPredictingTimer = null;

        Timer RepeatedPredictingTimer = null;

        Timer PickupPythonPredictionResultTimer = null;

        // Selenium/web information for scraping and data collection
        ChromeDriver Browser;

        string SessionID = "";

        Timer StartCollectingTimer = null;

        Timer RepeatedCollectingTimer = null;

        // Chart and display information for presenting predictions
        Series[] PredictorSeries;

        Series PriceDisplay = new Series();

        Color[] PredictionPriorityColors = new Color[20];




        private void ConfigureApp()
        {
            AppSettingsSection appSettings = ConfigFile.AppSettings;

            KeyValueConfigurationCollection settings = appSettings.Settings;

            Config.ChromeExecutablePath = ConfigurationManager.AppSettings.Get("ChromeExecutablePath");
            Config.RootPythonPredictionScriptPath = ConfigurationManager.AppSettings.Get("RootPythonPredictionScriptPath");
            Config.LoginIdElementId = ConfigurationManager.AppSettings.Get("LoginIdElementId");
            Config.LoginIDValue = ConfigurationManager.AppSettings.Get("LoginIDValue");
            Config.LoginActionElementId = ConfigurationManager.AppSettings.Get("LoginActionElementId");
            Int32.TryParse(ConfigurationManager.AppSettings.Get("ModelsCount"), out Config.ModelsCount);
            Config.PasswordElementId = ConfigurationManager.AppSettings.Get("PasswordElementId");
            Config.PasswordValue = ConfigurationManager.AppSettings.Get("PasswordValue");
            Config.PythonExecutablePath = ConfigurationManager.AppSettings.Get("PythonExecutablePath");
            Config.PythonPredictionScriptFile = Path.Combine(Config.RootPythonPredictionScriptPath, ConfigurationManager.AppSettings.Get("PythonPredictionScriptFile"));
            Config.QuoteFrameEmbeddedDisplayUrl = ConfigurationManager.AppSettings.Get("QuoteFrameEmbeddedDisplayUrl");
            Config.RootSymbolBeingPredicted = ConfigurationManager.AppSettings.Get("RootSymbolBeingPredicted");
            Config.RootSymbolsList = ConfigurationManager.AppSettings.Get("RootSymbolsList").Split(new char[] { ',' }).ToList();
            Config.StartLoginUrl = ConfigurationManager.AppSettings.Get("StartLoginUrl");
            Config.SourcePythonPredictionOutputFile = Path.Combine(Config.RootPythonPredictionScriptPath, ConfigurationManager.AppSettings.Get("SourcePythonPredictionOutputFile"));
            Config.TargetCurrentBarsAndLags = Path.Combine(Config.RootPythonPredictionScriptPath, ConfigurationManager.AppSettings.Get("TargetCurrentBarsAndLags"));
            Config.TargetHistoricalFileStore = Path.Combine(Config.RootPythonPredictionScriptPath, ConfigurationManager.AppSettings.Get("TargetHistoricalFileStore"));
            Config.XPathToRenderedAskPriceElement = ConfigurationManager.AppSettings.Get("XPathToRenderedAskPriceElement");
            Config.XPathToRenderedLastPriceElement = ConfigurationManager.AppSettings.Get("XPathToRenderedLastPriceElement");
            Config.XPathToRenderedBidPriceElement = ConfigurationManager.AppSettings.Get("XPathToRenderedBidPriceElement");
            Config.XPathToRenderedVolumeElement = ConfigurationManager.AppSettings.Get("XPathToRenderedVolumeElement");
        }

        // Start predictions - start prediction timer -> repeat prediction timer -> pick up predictions.
        private void button1_Click(object sender, EventArgs e)
        {
            if ((RepeatedPredictingTimer != null && RepeatedPredictingTimer.Enabled) || (StartPredictingTimer != null && StartPredictingTimer.Enabled))
            {
                return;
            }
            DateTime now = DateTime.Now;
            DateTime firstFiring;
            if (StartPredictingTimer != null)
            {
                RepeatedPredictingTimer.Stop();
                RepeatedPredictingTimer = null;
                return;
            }

            // Erghh!   Go get the damn record of previously recorded price action, to start with.
            // This is the only way the python script can have the CurrentBars.csv input file it needs
            // in memory without waiting twelve periods for any useful predictions to change from the
            // output file.
            string pythonOutput = File.ReadAllText(Config.TargetHistoricalFileStore);
            string[] lines = pythonOutput.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            // NumLags x number of archived data in the file store for each time slice, i.e. 12 x 12
            string[] lastLines = lines.Skip(lines.Length-144).ToArray(); 
            pythonOutput = null;
            lines = null;
            int timeSliceIdx = 0;
            int withinSliceIdx = 0;
            DateTime sliceTime = DateTime.MaxValue.AddSeconds(-1);
            double price = -1;
            foreach (string lastLine in lastLines)
            {
                string[] elts = lastLine.Split(new string[] { "," }, StringSplitOptions.None);
                DateTime newTimeSliceTime = DateTime.Now.AddDays(-1);
                DateTime.TryParse(elts[1], out newTimeSliceTime);
                if (newTimeSliceTime > sliceTime.AddSeconds(1))
                {
                    if (DateTime.TryParse(elts[1], out newTimeSliceTime))
                    {
                        sliceTime = newTimeSliceTime;
                        withinSliceIdx = 0;
                        timeSliceIdx++;
                    }
                }
                if (CurrentPredictionInputSet[timeSliceIdx] == null && DateTime.TryParse(elts[1], out sliceTime))
                {
                    CurrentPredictionInputSet[timeSliceIdx] = new Tuple<DateTime, FuturesPrice[]>(sliceTime, new FuturesPrice[Symbols.Count]);
                }
                if (Double.TryParse(elts[2], out price))
                {
                    CurrentPredictionInputSet[timeSliceIdx].Item2[withinSliceIdx++] = new FuturesPrice(elts[0]) { LastPrice = price };
                }
            }
            using (StreamWriter tFile = File.CreateText(Config.TargetCurrentBarsAndLags))
            {
                foreach (string lastLine in lastLines)
                {
                    tFile.WriteLine(lastLine);
                }
            }

            // Start on the next five minute clock stroke, plus 10 seconds.
            if (((int)(now.Minute / 5) + 1) * 5 == 60)
            {
                if (now.Hour == 23 && ((int)(now.Minute / 5) + 1) * 5 == 60)
                {
                    firstFiring = new DateTime(now.Year, now.Month, now.Day + 1, 0, 0, 0);
                }
                else
                {
                    firstFiring = new DateTime(now.Year, now.Month, now.Day, now.Hour + 1, 0, 0);
                }
            }
            else
            {
                firstFiring = new DateTime(now.Year, now.Month, now.Day, now.Hour, ((int)(now.Minute / 5) + 1) * 5, 0);
            }

            StartPredictingTimer = new Timer();
            StartPredictingTimer.Interval = (int)firstFiring.Subtract(now).TotalSeconds * 1000 + 10000; // + Estimated time of collection time to leave a current + lags artifact
            StartPredictingTimer.Enabled = true;
            StartPredictingTimer.Tick += new EventHandler(MyPredictionTimer_Start);
            StartPredictingTimer.Start();
        }

        private void MyPredictionTimer_Start(object sender, EventArgs e)
        {
            StartPredictingTimer.Stop();
            StartPredictingTimer.Enabled = false;
            StartPredictingTimer = null;
            RepeatedPredictingTimer = new Timer();
            RepeatedPredictingTimer.Interval = 1000 * 60 * 5;
            RepeatedPredictingTimer.Enabled = true;
            RepeatedPredictingTimer.Tick += new EventHandler(MyPredictionTimer_Continue);
            RepeatedPredictingTimer.Start();
            // Payload - start the prediction run script asynchronously
            runPythonPredictionScript();
            PickupPythonPredictionResultTimer = new Timer();
            PickupPythonPredictionResultTimer.Interval = 30000; // Python run script estimated run time
            PickupPythonPredictionResultTimer.Enabled = true;
            PickupPythonPredictionResultTimer.Tick += new EventHandler(pickUpLastPredictionResult);
        }

        private void MyPredictionTimer_Continue(object sender, EventArgs e)
        {
            // Payload - start the prediction run script asynchronously
            runPythonPredictionScript();
            PickupPythonPredictionResultTimer = new Timer();
            PickupPythonPredictionResultTimer.Interval = 30000; // Python run script estimated run time
            PickupPythonPredictionResultTimer.Enabled = true;
            PickupPythonPredictionResultTimer.Tick += new EventHandler(pickUpLastPredictionResult);
        }

        private void runPythonPredictionScript()
        {
            var psi = new ProcessStartInfo();
            psi.FileName = Config.PythonExecutablePath; // or any other python environment

            psi.Arguments = $"\"{Config.PythonPredictionScriptFile}\"";

            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = Config.RootPythonPredictionScriptPath;

            string output;
            string errors;
            using (var process = Process.Start(psi))
            {
                output = process.StandardOutput.ReadToEnd();
                errors = process.StandardError.ReadToEnd();
            }
        }

        private void pickUpLastPredictionResult(object sender, EventArgs e)
        {
            PickupPythonPredictionResultTimer.Enabled = false;
            PickupPythonPredictionResultTimer = null;

            string pythonOutput = File.ReadAllText(Config.SourcePythonPredictionOutputFile);
            Tuple<double, double>[] pythonPredictions = new Tuple<double, double>[PredictorSeries.Count()];
            int lineIdx = 0;
            foreach (string line in pythonOutput.Split(new char[] { '\n' }))
            {
                if (line.Equals(""))
                {
                    continue;
                }
                string [] items = line.Split(new char[] { ',' } );
                double pred;
                if (!Double.TryParse(items[0], out pred))
                {
                    MessageBox.Show("Error - the python prediction output that was dropped does not match the assumptions coded up here. One line first item fails to parse to double.", "Error, Skipping");
                    return;
                }
                double rsq;
                if (!Double.TryParse(items[1], out rsq))
                {
                    MessageBox.Show("Error - the python prediction output that was dropped does not match the assumptions coded up here. One line second item fails to parse to double.", "Error, Skipping");
                    return;
                }
                if (lineIdx >= pythonPredictions.Length)
                {
                    MessageBox.Show("Error - the python prediction output that was dropped does not match the assumptions coded up here. There are more lines than in the application predictor series.", "Error, Skipping");
                    return;
                }
                pythonPredictions[lineIdx++] = new Tuple<double, double>(pred, rsq);
            }

            // Payload - the prediction model outputs that were dropped by the run script
            int pIdx = 0;
            foreach (Series series in PredictorSeries)
            {
                int priorityColor = (int)(pythonPredictions[pIdx].Item2 * 2000);
                if (priorityColor < 0)
                {
                    priorityColor = 0;
                }
                if (priorityColor >= 20)
                {
                    priorityColor = 19;
                }
                series.Points.Add(pythonPredictions[pIdx].Item1);
                series.Points.Last().MarkerStyle = MarkerStyle.Diamond;
                series.Points.Last().MarkerSize = 10;
                series.Points.Last().MarkerColor = PredictionPriorityColors[priorityColor];
                if (series.Points.Count > 100)
                {
                    series.Points.RemoveAt(0);
                }
                series.Color = PredictionPriorityColors[priorityColor];
                series.LegendText = pythonPredictions[pIdx].Item2.ToString();
                pIdx++;
            }

            List<FuturesPrice> mostRecentSnapshot = new List<FuturesPrice>();
            foreach (FuturesPrice snapshot in Symbols)
            {
                if (snapshot.Symbol.StartsWith(Config.RootSymbolBeingPredicted))
                {
                    mostRecentSnapshot.Add(snapshot);
                }
                else
                {
                    break;
                }
            }
            if (mostRecentSnapshot.Count() > 0 && RepeatedCollectingTimer != null && RepeatedCollectingTimer.Enabled && mostRecentSnapshot.First().LastPrice.HasValue)
            {
                PriceDisplay.Points.Add(mostRecentSnapshot.First().LastPrice.Value);
                if (PriceDisplay.Points.Count > 100)
                {
                    PriceDisplay.Points.RemoveAt(0);
                }
            }
        }

        // The authorized scrapes, proper
        private void takeLastPriceSnapshot()
        {
            try
            {
                IWebElement[] testElements = new IWebElement[0];
                if (Browser != null && SessionID != null && SessionID != "")
                {
                    WebDriverWait waitForTest = new WebDriverWait(Browser, new TimeSpan(300000000));
                    Browser.Navigate().GoToUrl(Config.QuoteFrameEmbeddedDisplayUrl + SessionID.ToUpper() + "&symbol=" + Symbols[0].Symbol);
                    try
                    {
                        waitForTest.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath(Config.XPathToRenderedLastPriceElement)));
                    }
                    catch (Exception)
                    {
                        ;
                    }
                    testElements = Browser.FindElements(By.XPath(Config.XPathToRenderedLastPriceElement)).ToArray();
                    if (testElements.Length == 0)
                    {
                        SessionID = "";
                        Browser = null;
                    }
                }

                if (Browser == null || testElements.Count() == 0)
                {
                    var options = new ChromeOptions()
                    {
                        BinaryLocation = Config.ChromeExecutablePath
                    };

                    options.AddArguments(new List<string>() { "disable-gpu", "--disable-blink-features=AutomationControlled", "excludeSwitches=enable-automation", "userAutomationExtension=false" });

                    Dictionary<String, Object> chromeLocalStatePrefs = new Dictionary<String, Object>();
                    chromeLocalStatePrefs.Add("same-site-by-default-cookies", "true");
                    options.AddLocalStatePreference("localState", chromeLocalStatePrefs);

                    Browser = new ChromeDriver(options);
                    Browser.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})", new object[] { });

                    LogIntoSite(Config.StartLoginUrl);

                    WebDriverWait waitForLogin = new WebDriverWait(Browser, new TimeSpan(300000000));
                    waitForLogin.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("//frame[@src]")));
                    IWebElement[] framesForSessionId = Browser.FindElements(By.XPath("//frame[@src]")).ToArray();
                    foreach (IWebElement frame in framesForSessionId)
                    {
                        HtmlAgilityPack.HtmlDocument parseable = new HtmlAgilityPack.HtmlDocument();
                        parseable.LoadHtml(frame.GetAttribute("src"));
                        string sub = parseable.Text.Substring(parseable.Text.IndexOf("?"));
                        SessionID = sub.Substring(sub.IndexOf("=") + 1);
                    }
                }

                foreach (FuturesPrice symbol in Symbols)
                {
                    if (symbol == Symbols[0] && testElements.Count() > 0) // efficiency: if the first one worked, then skip the request, just place it.
                    {
                        symbol.LastPrice = Double.Parse(testElements[0].Text.Substring(0, testElements[0].Text.IndexOf(Environment.NewLine)));
                        continue;
                    }
                    Browser.Navigate().GoToUrl(Config.QuoteFrameEmbeddedDisplayUrl + SessionID.ToUpper() + "&symbol=" + symbol.Symbol);
                    IWebElement[] qElements = Browser.FindElements(By.XPath(Config.XPathToRenderedLastPriceElement)).ToArray();
                    foreach (IWebElement elt in qElements)
                    {
                        string lastP = elt.Text.Substring(0, elt.Text.IndexOf(Environment.NewLine));
                        double dP;
                        if (!Double.TryParse(lastP, out dP))
                        {
                            if (!FuturesHelpers.FuturesHelpers.TryParseFractionalPrice(lastP, 32, out dP))
                            {
                                ;
                            }
                        }
                        symbol.LastPrice = dP;
                    }
                    IWebElement[] vElements = Browser.FindElements(By.XPath(Config.XPathToRenderedVolumeElement)).ToArray();
                    foreach (IWebElement elt in vElements)
                    {
                        string lastV = elt.Text.Substring(0, elt.Text.IndexOf(Environment.NewLine));
                        int cumVol;
                        if (!Int32.TryParse(lastV, out cumVol))
                        {
                                ;
                        }
                        symbol.LastDayVolume = cumVol;
                    }
                }

                using (StreamWriter tFile = File.AppendText(Config.TargetHistoricalFileStore))
                {
                    foreach (FuturesPrice symbol in Symbols)
                    {
                        tFile.WriteLine(String.Format("{0},{1:yyyy-MM-dd HH:mm:ss.ffffff},{2},{3}", symbol.Symbol, DateTime.Now, symbol.LastPrice, symbol.LastDayVolume));
                    }
                }

                FuturesPrice[] latestSymbolsCopy = new FuturesPrice[Symbols.Count];
                for (int i=0; i < Symbols.Count; i++)
                {
                    latestSymbolsCopy[i] = new FuturesPrice(Symbols[i].Symbol);
                    latestSymbolsCopy[i].LastPrice = Symbols[i].LastPrice;
                    latestSymbolsCopy[i].LastDayVolume = Symbols[i].LastDayVolume;
                }
                for (int i = 0; i < Symbols.Count - 1; i++)
                {
                    CurrentPredictionInputSet[i] = CurrentPredictionInputSet[i+1];
                }
                CurrentPredictionInputSet[Symbols.Count - 1] = new Tuple<DateTime, FuturesPrice[]>(DateTime.Now, latestSymbolsCopy);

                Browser.Manage().Window.Minimize();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
            finally
            {
                ;
            }
        }

        private void LogIntoSite(string startURL)
        {
            Browser.Navigate().GoToUrl(startURL);
            //browser.Manage().Window.Maximize();

            IWebElement[] allElements = Browser.FindElements(By.XPath("//iframe")).ToArray();

            foreach (var element in allElements)
            {
                string outerXml = element.GetAttribute("outerHTML");
                HtmlAgilityPack.HtmlDocument htmlDocument = new HtmlAgilityPack.HtmlDocument();
                htmlDocument.LoadHtml(outerXml);

                if (htmlDocument.DocumentNode.ChildNodes.First().Attributes.Contains("src")
                    && htmlDocument.DocumentNode.ChildNodes.First().Attributes["src"].Value != "javascript:void(0)")
                {
                    Browser.SwitchTo().Frame(element);

                    Browser.FindElements(By.XPath("//input"));

                    IWebElement[] inputElts = Browser.FindElements(By.XPath("//input")).ToArray();

                    foreach (var subElt in inputElts)
                    {
                        string subEltId = subElt.GetAttribute("id");
                        if (subEltId == Config.LoginIdElementId)
                        {
                            subElt.SendKeys(Config.LoginIDValue);
                        }
                        else if (subEltId == Config.PasswordElementId)
                        {
                            subElt.SendKeys(Config.PasswordValue);
                        }
                    }

                    IWebElement[] submitButtons = Browser.FindElements(By.XPath("//button")).ToArray();

                    foreach (var subElt in submitButtons)
                    {
                        string subEltId = subElt.GetAttribute("id");
                        if (subEltId == Config.LoginActionElementId)
                        {
                            subElt.Click();
                        }
                    }
                }
            }
        }

        // Start collecting -> Start collection timer -> repeated collection timer 
        private void button2_Click(object sender, EventArgs e)
        {
            if ((RepeatedCollectingTimer != null && RepeatedCollectingTimer.Enabled) || (StartCollectingTimer != null && StartCollectingTimer.Enabled))
            {
                return;
            }
            DateTime now = DateTime.Now;
            DateTime firstFiring;
            if (StartCollectingTimer != null)
            {
                RepeatedCollectingTimer.Stop();
                RepeatedCollectingTimer = null;
                return;
            }

            if (((int)(now.Minute / 5) + 1) * 5 == 60)
            {
                if (now.Hour == 23 && ((int)(now.Minute / 5) + 1) * 5 == 60)
                {
                    firstFiring = new DateTime(now.Year, now.Month, now.Day + 1, 0, 0, 0);
                }
                else
                {
                    firstFiring = new DateTime(now.Year, now.Month, now.Day, now.Hour + 1, 0, 0);
                }
            }
            else 
            {
                firstFiring = new DateTime(now.Year, now.Month, now.Day, now.Hour, ((int)(now.Minute/5) + 1)*5, 0);
            }

            StartCollectingTimer = new Timer();
            StartCollectingTimer.Interval = (int)firstFiring.Subtract(now).TotalSeconds*1000;
            StartCollectingTimer.Tick += new EventHandler(MyCollectionTimer_Start);
            StartCollectingTimer.Start();
        }

        // Drop the most recent data plus lags, to power the python prediction script.
        private void replaceCurrentBarsFile()
        {
            using (StreamWriter tFile = File.CreateText(Config.TargetCurrentBarsAndLags))
            {
                for (int i =0; i < Symbols.Count; i++)
                {
                    if (CurrentPredictionInputSet[i] != null)
                    {
                        DateTime tstamp = CurrentPredictionInputSet[i].Item1;
                        foreach (FuturesPrice symbol in CurrentPredictionInputSet[i].Item2)
                        {
                            tFile.WriteLine(String.Format("{0},{1:yyyy-MM-dd HH:mm:ss.ffffff},{2},{3}", symbol.Symbol, tstamp, symbol.LastPrice, symbol.LastDayVolume));
                        }
                    }
                }
            }
        }

        private void MyCollectionTimer_Start(object sender, EventArgs e)
        {
            StartCollectingTimer.Stop();
            StartCollectingTimer.Enabled = false;
            StartCollectingTimer = null;
            RepeatedCollectingTimer = new Timer();
            RepeatedCollectingTimer.Interval = 1000 * 60 * 5;
            RepeatedCollectingTimer.Tick += new EventHandler(MyCollectionTimer_Continue);
            RepeatedCollectingTimer.Start();
            takeLastPriceSnapshot();
            replaceCurrentBarsFile();
        }

        private void MyCollectionTimer_Continue(object sender, EventArgs e)
        {
            takeLastPriceSnapshot();
            replaceCurrentBarsFile();
        }


        private void button3_Click(object sender, EventArgs e)
        {
            SessionID = null;
            RepeatedCollectingTimer.Stop();
            RepeatedCollectingTimer.Enabled = false;
            RepeatedCollectingTimer = null;
        }

    }
}
