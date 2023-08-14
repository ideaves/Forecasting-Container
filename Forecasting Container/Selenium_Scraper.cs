using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Net;
using System.Net.Http;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.IO;
using Futures_Contract_Helpers;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using System.Diagnostics;

namespace Selenium_Scraper
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            foreach (string rootSym in new string[] { "ES", "EC", "GC", "US", "BTC", "NQ" })
            {
                Symbols.Add(new FuturesPrice(FuturesContractHelpers.GetFrontContract(DateTime.Now, rootSym).Item2.Symbol));
                Symbols.Add(new FuturesPrice(FuturesContractHelpers.GetSecondContract(DateTime.Now, rootSym).Item2.Symbol));
            }
            CurrentPredictionInputSet = new Tuple<DateTime, FuturesPrice[]>[Symbols.Count];

            for (int i = 0; i < ModelsCount; i++)
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

        ChromeDriver Browser;

        string SessionID = "";

        string TargetHistoricalFileStore = @"C:\Users\ideav\Documents\PythonWork\FiveFactor\5MinuteBars.csv";

        string TargetCurrentBarsAndLags = @"C:\Users\ideav\Documents\PythonWork\FiveFactor\CurrentBars.csv";

        string PythonPredictionScriptFile = @"C:\Users\ideav\Documents\PythonWork\FiveFactor\ES_FiveFactor_FrontOnly.py";
        string PythonPredictionScriptDirectory = @"C:\Users\ideav\Documents\PythonWork\FiveFactor";

        string SourcePythonPredictionOutputFile = @"C:\Users\ideav\Documents\PythonWork\FiveFactor\PythonOutput_ES_FiveFactor_FrontOnly.csv";

        List<FuturesPrice> Symbols = new List<FuturesPrice>();

        Tuple<DateTime, FuturesPrice[]>[] CurrentPredictionInputSet;

        static int ModelsCount = 5;
        Series[] PredictorSeries = new Series[ModelsCount];
        Series PriceDisplay = new Series();

        Color[] PredictionPriorityColors = new Color[20];

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

            // Go get the damn record of previously recorded price action, to start with.
            // This is the only way the python script can have the CurrentBars.csv input file it needs,
            // without waiting twelve periods for any useful predictions to change from the output file.
            string pythonOutput = File.ReadAllText(TargetHistoricalFileStore);
            string[] lines = pythonOutput.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
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
            using (StreamWriter tFile = File.CreateText(TargetCurrentBarsAndLags))
            {
                foreach (string lastLine in lastLines)
                {
                    tFile.WriteLine(lastLine);
                }
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
            // Payload - start the prediction script asynchronously
            runPythonPredictionScript();
            PickupPythonPredictionResultTimer = new Timer();
            PickupPythonPredictionResultTimer.Interval = 30000; // Python script estimated run time
            PickupPythonPredictionResultTimer.Enabled = true;
            PickupPythonPredictionResultTimer.Tick += new EventHandler(pickUpLastPredictionResult);
        }

        private void MyPredictionTimer_Continue(object sender, EventArgs e)
        {
            // Payload - start the prediction script asynchronously
            runPythonPredictionScript();
            PickupPythonPredictionResultTimer = new Timer();
            PickupPythonPredictionResultTimer.Interval = 30000; // Python script estimated run time
            PickupPythonPredictionResultTimer.Enabled = true;
            PickupPythonPredictionResultTimer.Tick += new EventHandler(pickUpLastPredictionResult);
        }

        Timer StartPredictingTimer = null;

        Timer RepeatedPredictingTimer = null;

        Timer PickupPythonPredictionResultTimer = null;

        private void runPythonPredictionScript()
        {
            var psi = new ProcessStartInfo();
            psi.FileName = @"C:\Users\ideav\AppData\Local\Programs\Python\Python38\python.exe"; // or any python environment

            psi.Arguments = $"\"{PythonPredictionScriptFile}\"";

            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = PythonPredictionScriptDirectory;

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

            string pythonOutput = File.ReadAllText(SourcePythonPredictionOutputFile);
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

            //this.chart1.ChartAreas["ChartArea1"].Position.X = 10; 
            //this.chart1.ChartAreas["ChartArea1"].Position.Y = 0;
            //this.chart1.ChartAreas["ChartArea2"].Position.X = 0;
            //this.chart1.ChartAreas["ChartArea2"].Position.Y = 50;
            // Payload
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
            Random rand = new Random();

            List<FuturesPrice> mostRecentSnapshot = new List<FuturesPrice>();
            foreach (FuturesPrice snapshot in Symbols)
            {
                if (snapshot.Symbol.StartsWith("ES"))
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

        private void takeLastPriceSnapshot()
        {
            try
            {
                IWebElement[] testElements = new IWebElement[0];
                if (Browser != null && SessionID != null && SessionID != "")
                {
                    WebDriverWait waitForTest = new WebDriverWait(Browser, new TimeSpan(300000000));
                    Browser.Navigate().GoToUrl("https://www.streetsmartcentral.com/OXNetTools/Quote/QuoteFrame.aspx?SESSIONID=" + SessionID.ToUpper() + "&symbol=" + Symbols[0].Symbol);
                    try
                    {
                        waitForTest.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("//table[@id='quoteTable']//tbody//tr//td[@id='tdLast']")));
                    }
                    catch (Exception)
                    {
                        ;
                    }
                    testElements = Browser.FindElements(By.XPath("//table[@id='quoteTable']//tbody//tr//td[@id='tdLast']")).ToArray();
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
                        BinaryLocation = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"
                    };

                    options.AddArguments(new List<string>() { "disable-gpu", "--disable-blink-features=AutomationControlled", "excludeSwitches=enable-automation", "userAutomationExtension=false" });

                    Dictionary<String, Object> chromeLocalStatePrefs = new Dictionary<String, Object>();
                    chromeLocalStatePrefs.Add("same-site-by-default-cookies", "true");
                    options.AddLocalStatePreference("localState", chromeLocalStatePrefs);

                    Browser = new ChromeDriver(options);
                    Browser.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})", new object[] { });

                    string startURL = "https://www.streetsmartcentral.com/login/CustomerLogin.aspx";
                    LogIntoSite(startURL);

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
                    Browser.Navigate().GoToUrl("https://www.streetsmartcentral.com/OXNetTools/Quote/QuoteFrame.aspx?SESSIONID=" + SessionID.ToUpper() + "&symbol=" + symbol.Symbol);
                    IWebElement[] qElements = Browser.FindElements(By.XPath("//table[@id='quoteTable']//tbody//tr//td[@id='tdLast']")).ToArray();
                    foreach (IWebElement elt in qElements)
                    {
                        string lastP = elt.Text.Substring(0, elt.Text.IndexOf(Environment.NewLine));
                        double dP;
                        if (!Double.TryParse(lastP, out dP))
                        {
                            if (!FuturesContractHelpers.TryParseFractionalPrice(lastP, 32, out dP))
                            {
                                ;
                            }
                        }
                        symbol.LastPrice = dP;
                    }
                    IWebElement[] vElements = Browser.FindElements(By.XPath("//table[@id='quoteTable']//tbody//tr//td[@id='tdVolume']")).ToArray();
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

                using (StreamWriter tFile = File.AppendText(TargetHistoricalFileStore))
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
                        if (subEltId == "loginIdInput")
                        {
                            subElt.SendKeys("ideaves");
                        }
                        else if (subEltId == "passwordInput")
                        {
                            subElt.SendKeys(".DaEa7322.");
                        }
                    }

                    IWebElement[] submitButtons = Browser.FindElements(By.XPath("//button")).ToArray();

                    foreach (var subElt in submitButtons)
                    {
                        string subEltId = subElt.GetAttribute("id");
                        if (subEltId == "btnLogin")
                        {
                            subElt.Click();
                        }
                    }
                }
            }
        }

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

        Timer StartCollectingTimer = null;

        Timer RepeatedCollectingTimer = null;

        private void replaceCurrentBarsFile()
        {
            using (StreamWriter tFile = File.CreateText(TargetCurrentBarsAndLags))
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
            //RepeatedTimer = new Timer();
            //RepeatedTimer.Interval = 1000 * 60 * 5;
            //RepeatedTimer.Tick += new EventHandler(MyTimer_Continue);
            //RepeatedTimer.Start();
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
