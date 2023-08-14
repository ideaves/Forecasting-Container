using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Futures_Contract_Helpers
{
    public abstract class FuturesSeries
    {
        public FuturesSeries(string rootSymbol)
        {
            SymbolRoot = rootSymbol;
            Contracts = new List<Tuple<FuturesContractSpecifications, FuturesPrice>>();

            MonthNumberMapping[0] = "F";
            MonthNumberMapping[1] = "G";
            MonthNumberMapping[2] = "H";
            MonthNumberMapping[3] = "J";
            MonthNumberMapping[4] = "K";
            MonthNumberMapping[5] = "M";
            MonthNumberMapping[6] = "N";
            MonthNumberMapping[7] = "Q";
            MonthNumberMapping[8] = "U";
            MonthNumberMapping[9] = "V";
            MonthNumberMapping[10] = "X";
            MonthNumberMapping[11] = "Z";

            // This drives the DateTime. That requires a 1-based month.
            MonthLetterMapping["F"] = 1;
            MonthLetterMapping["G"] = 2;
            MonthLetterMapping["H"] = 3;
            MonthLetterMapping["J"] = 4;
            MonthLetterMapping["K"] = 5;
            MonthLetterMapping["M"] = 6;
            MonthLetterMapping["N"] = 7;
            MonthLetterMapping["Q"] = 8;
            MonthLetterMapping["U"] = 9;
            MonthLetterMapping["V"] = 10;
            MonthLetterMapping["X"] = 11;
            MonthLetterMapping["Z"] = 12;
        }

        public FuturesSeries(string rootSymbol, List<DateTime> holidays) : this(rootSymbol)
        {
            Holidays = holidays;
        }

        public string SymbolRoot;

        public Dictionary<int, string> MonthNumberMapping = new Dictionary<int, string>();

        public Dictionary<string, int> MonthLetterMapping = new Dictionary<string, int>();

        public List<string> ContractMonths;

        public List<Tuple<FuturesContractSpecifications, FuturesPrice>> Contracts;

        public List<DateTime> Holidays;

        public abstract DateTime? GetLastNonCommercialTradingDate(DateTime monthStart);

        public DateTime? GetNthWeekdayOfMonth(DateTime monthStart, int nTh, DayOfWeek? weekDay)
        {
            DateTime tryDate = monthStart;
            int tryN = 1;
            if (!weekDay.HasValue || nTh <= 0 || monthStart.Day != 1)
            {
                return null;
            }
            while (tryDate.DayOfWeek != weekDay)
            {
                tryDate = tryDate.AddDays(1);
            }
            while (tryN < nTh)
            {
                tryDate = tryDate.AddDays(7);
                tryN++;
            }
            while (tryDate.DayOfWeek == DayOfWeek.Sunday // Cannot be a holiday or weekend
                || tryDate.DayOfWeek == DayOfWeek.Saturday
                || (Holidays != null && Holidays.Contains(tryDate)))
            {
                tryDate = tryDate.AddDays(1);
            }
            return tryDate;
        }

        public DateTime? GetNBusinessDaysBeforeMonthsEnd(DateTime monthStart, int nTh)
        {
            int year = monthStart.Year;
            int month = monthStart.Month;
            int daysInMonth = 30;
            if (nTh < 0 || monthStart.Day != 1)
            {
                return null;
            }
            if (new int[] { 1, 3, 5, 7, 8, 10, 12}.ToList().Contains(month))
            {
                daysInMonth = 31;
            }
            else if (month == 2 && year % 4 == 0 && year % 100 == 0)
            {
                daysInMonth = 28;
            }
            else if (month == 2 && year % 4 == 0)
            {
                daysInMonth = 29;
            }
            else if (month == 2)
            {
                daysInMonth = 28;
            }
            DateTime endMonthDate = new DateTime(year, month, daysInMonth);

            int businessDaysBack = 0;
            if (nTh == 0) // Still make sure it's the last good business day of the month
            {
                while (endMonthDate.DayOfWeek != DayOfWeek.Sunday
                    && endMonthDate.DayOfWeek != DayOfWeek.Saturday
                    && (Holidays == null || !Holidays.Contains(endMonthDate)))
                {
                    endMonthDate = endMonthDate.AddDays(-1);
                }
            }
            while (businessDaysBack < nTh) // Normal case for nTh > 0
            {
                endMonthDate = endMonthDate.AddDays(-1);
                if (endMonthDate.DayOfWeek != DayOfWeek.Sunday
                    && endMonthDate.DayOfWeek != DayOfWeek.Saturday
                    && (Holidays == null || !Holidays.Contains(endMonthDate)))
                {
                    businessDaysBack++;
                }
            }

            return endMonthDate;
        }

        public DateTime? GetLastWeekdayOfMonth(DateTime monthStart, int nTh, DayOfWeek? weekDay)
        {
            int year = monthStart.Year;
            int month = monthStart.Month;
            if (!weekDay.HasValue || nTh < 0 || monthStart.Day != 1)
            {
                return null;
            }
            int daysInMonth = 30;
            if (new int[] { 1, 3, 5, 7, 8, 10, 12 }.ToList().Contains(month))
            {
                daysInMonth = 31;
            }
            else if (month == 2 && year % 4 == 0 && year % 100 == 0)
            {
                daysInMonth = 28;
            }
            else if (month == 2 && year % 4 == 0)
            {
                daysInMonth = 29;
            }
            else if (month == 2)
            {
                daysInMonth = 28;
            }
            DateTime endMonthDate = new DateTime(year, month, daysInMonth);
            while (endMonthDate.DayOfWeek != weekDay)
            {
                endMonthDate = endMonthDate.AddDays(-1);
            }
            while (endMonthDate.DayOfWeek == DayOfWeek.Sunday // Cannot be a holiday or weekend
                || endMonthDate.DayOfWeek == DayOfWeek.Saturday
                || (Holidays != null && Holidays.Contains(endMonthDate)))
            {
                endMonthDate = endMonthDate.AddDays(-1);
            }

            return endMonthDate;
        }

        public DateTime? GetLastBusinessDayOfPriorMonth(DateTime monthStart)
        {
            if (monthStart.Day != 1)
            {
                return null;
            }
            DateTime endMonthDate = monthStart.AddDays(-1);
            while (endMonthDate.DayOfWeek == DayOfWeek.Sunday // Cannot be a holiday or weekend
                || endMonthDate.DayOfWeek == DayOfWeek.Saturday
                || (Holidays != null && Holidays.Contains(endMonthDate)))
            {
                endMonthDate = endMonthDate.AddDays(-1);
            }
            return endMonthDate;
        }

        public void EnumerateContractSeries(string rootSymbol, DateTime startSeries, int nContracts) 
        {
            int yr = startSeries.Year;
            int mo = startSeries.Month;
            string frontContractLetter = "";
            int frontContractNumber = -1;
            foreach (KeyValuePair<int, string> month in MonthNumberMapping)
            {
                if (mo <= month.Key + 1 && ContractMonths.Contains(month.Value))
                {
                    frontContractNumber = month.Key + 1;
                    frontContractLetter = month.Value;
                    break;
                }
            }
            int contractYear = yr;
            int contractMonth = frontContractNumber;
            string contractLetter = frontContractLetter;
            for (int seriesNumber = 0; seriesNumber < nContracts; seriesNumber++)
            {
                DateTime startOfMonth = new DateTime(contractYear, contractMonth, 1);
                DateTime? lastTradingDate = GetLastNonCommercialTradingDate(startOfMonth);
                if (!lastTradingDate.HasValue) 
                {
                    throw new InvalidOperationException("Invalid call to GetLastNonCommercialTradingDate: invalid parameter values.");
                }
                if (lastTradingDate.Value < startSeries.Date)
                {
                    nContracts++;
                    if (contractLetter == ContractMonths.Last()) // wrap year on contract symbol list
                    {
                        contractYear++;
                        contractLetter = ContractMonths.First();
                        contractMonth = MonthLetterMapping[contractLetter];
                    }
                    else // next one in contract symbol list
                    {
                        contractLetter = ContractMonths[ContractMonths.IndexOf(contractLetter) + 1];
                        contractMonth = MonthLetterMapping[contractLetter];
                    }
                    continue;
                }
                //NonCommercialLastTradingDates.Add(new Tuple<DateTime, string>(lastTradingDate.Value, (rootSymbol + contractLetter + (contractYear % 100)) ));
                Contracts.Add(new Tuple<FuturesContractSpecifications, FuturesPrice>(new FuturesContractSpecifications()
                {
                    SymbolRoot = rootSymbol,
                    ContractMonth = contractLetter,
                    NonCommercialLastTradingDate = lastTradingDate.Value
                }, new FuturesPrice((rootSymbol + contractLetter + (contractYear % 100))))
                );
                if (contractLetter == ContractMonths.Last()) // wrap year on contract symbol list
                {
                    contractYear++;
                    contractLetter = ContractMonths.First();
                }
                else // next one in contract symbol list
                {
                    contractLetter = ContractMonths[ContractMonths.IndexOf(contractLetter) + 1];
                }
                contractMonth = MonthLetterMapping[contractLetter];
            }
        }
    }

    public class FuturesPrice
    {
        public string Symbol;

        public double? AskPrice;
        public double? BidPrice;
        public int AskSize = 0;
        public int BidSize = 0;
        public double? LastPrice;
        public int LastDayVolume = 0;

        public FuturesPrice(string sym)
        {
            Symbol = sym;
            LastPrice = null;
        }
    }

    public class FuturesContractSpecifications
    {
        public string SymbolRoot;
        public string ContractMonth;
        public DateTime NonCommercialLastTradingDate;
        public DateTime FirstNoticeDate;
        public DateTime FirstDeliveryDate;
        public DateTime LastNoticeDate;
        public DateTime ExpirationDate;
        public Decimal InitialMargin;
        public Decimal MaintenanceMargin;
        public int ContractSize;
    }


    public class ES_FuturesSeries : FuturesSeries
    {
        DayOfWeek DayOfWeek = DayOfWeek.Wednesday;
        int seqInMonth = 3;

        public ES_FuturesSeries(string rootSymbol, DateTime startSeries, int nContracts) : base(rootSymbol) 
        {
            ContractMonths = new string[] { "H", "M", "U", "Z" }.ToList();
            EnumerateContractSeries(rootSymbol, startSeries, nContracts);
        }

        public override DateTime? GetLastNonCommercialTradingDate(DateTime monthStart)
        {
            return GetNthWeekdayOfMonth(monthStart, seqInMonth, DayOfWeek);
        }
    }

    public class NQ_FuturesSeries : FuturesSeries
    {
        DayOfWeek DayOfWeek = DayOfWeek.Wednesday;
        int seqInMonth = 3;

        public NQ_FuturesSeries(string rootSymbol, DateTime startSeries, int nContracts) : base(rootSymbol)
        {
            ContractMonths = new string[] { "H", "M", "U", "Z" }.ToList();
            EnumerateContractSeries(rootSymbol, startSeries, nContracts);
        }

        public override DateTime? GetLastNonCommercialTradingDate(DateTime monthStart)
        {
            return GetNthWeekdayOfMonth(monthStart, seqInMonth, DayOfWeek);
        }
    }

    public class EC_FuturesSeries : FuturesSeries
    {
        DayOfWeek DayOfWeek = DayOfWeek.Wednesday;
        int seqInMonth = 3;

        public EC_FuturesSeries(string rootSymbol, DateTime startSeries, int nContracts) : base(rootSymbol)
        {
            ContractMonths = new string[] { "H", "M", "U", "Z" }.ToList();
            EnumerateContractSeries(rootSymbol, startSeries, nContracts);
        }

        public override DateTime? GetLastNonCommercialTradingDate(DateTime monthStart)
        {
            return GetNthWeekdayOfMonth(monthStart, seqInMonth, DayOfWeek);
        }
    }

    public class US_FuturesSeries : FuturesSeries
    {
        DayOfWeek DayOfWeek = DayOfWeek.Wednesday;
        int seqInMonth = 3;

        public US_FuturesSeries(string rootSymbol, DateTime startSeries, int nContracts) : base(rootSymbol)
        {
            ContractMonths = new string[] { "H", "M", "U", "Z" }.ToList();
            EnumerateContractSeries(rootSymbol, startSeries, nContracts);
        }

        public override DateTime? GetLastNonCommercialTradingDate(DateTime monthStart)
        {
            return GetNthWeekdayOfMonth(monthStart, seqInMonth, DayOfWeek);
        }
    }

    public class BTC_FuturesSeries : FuturesSeries
    {
        DayOfWeek DayOfWeek = DayOfWeek.Friday;

        public BTC_FuturesSeries(string rootSymbol, DateTime startSeries, int nContracts) : base(rootSymbol)
        {
            ContractMonths = new string[] { "F", "G", "H", "J", "K", "M", "N", "Q", "U", "V", "X", "Z" }.ToList();
            EnumerateContractSeries(rootSymbol, startSeries, nContracts);
        }

        public override DateTime? GetLastNonCommercialTradingDate(DateTime monthStart)
        {
            return GetLastWeekdayOfMonth(monthStart, 0, DayOfWeek);
        }
    }

    public class GC_FuturesSeries : FuturesSeries
    {
        public GC_FuturesSeries(string rootSymbol, DateTime startSeries, int nContracts) : base(rootSymbol)
        {
            ContractMonths = new string[] { "G", "J", "M", "Q", "Z" }.ToList();
            EnumerateContractSeries(rootSymbol, startSeries, nContracts);
        }

        public override DateTime? GetLastNonCommercialTradingDate(DateTime monthStart)
        {
            // Policy decision here - use GC first notice day as the last trading date. Brokers disallow trading,
            // even though the exchange technically has the contract designated as open for trading.
            return GetLastBusinessDayOfPriorMonth(monthStart);
        }
    }

    public class FuturesContractHelpers
    {
        static public Dictionary<string, FuturesSeries> FuturesSeriesGlobalDictionary = new Dictionary<string, FuturesSeries>();

        static public void PopulateBasicContractSeries(string rootSymbol, DateTime startDate)
        {
            PopulateSpecificContractSeries(rootSymbol, startDate, 3);
        }

        static public bool TryParseFractionalPrice(string price, int denominator, out double parsedPrice)
        {
            try
            {
                double intPart = Double.Parse(price.Substring(0, price.IndexOf("-")));
                double fracPart = Double.Parse(price.Substring(price.IndexOf("-") + 1)) / denominator;
                parsedPrice = intPart + fracPart;
                return true;
            }
            catch (Exception)
            {
                parsedPrice = 0;
                return false;
            }
        }

        static public void PopulateSpecificContractSeries(string rootSymbol, DateTime startDate, int numContracts)
        {
            switch (rootSymbol)
            {
                case "ES":
                    FuturesSeriesGlobalDictionary[rootSymbol] = new ES_FuturesSeries(rootSymbol, startDate, numContracts);
                    break;
                case "NQ":
                    FuturesSeriesGlobalDictionary[rootSymbol] = new NQ_FuturesSeries(rootSymbol, startDate, numContracts);
                    break;
                case "EC":
                    FuturesSeriesGlobalDictionary[rootSymbol] = new EC_FuturesSeries(rootSymbol, startDate, numContracts);
                    break;
                case "GC":
                    FuturesSeriesGlobalDictionary[rootSymbol] = new GC_FuturesSeries(rootSymbol, startDate, numContracts);
                    break;
                case "US":
                    FuturesSeriesGlobalDictionary[rootSymbol] = new US_FuturesSeries(rootSymbol, startDate, numContracts);
                    break;
                case "BTC":
                    FuturesSeriesGlobalDictionary[rootSymbol] = new BTC_FuturesSeries(rootSymbol, startDate, numContracts);
                    break;

                default:
                    break;
            }
        }

        static public Tuple<FuturesContractSpecifications, FuturesPrice> GetFrontContract(DateTime now, string rootSymbol)
        {
            if (!FuturesSeriesGlobalDictionary.ContainsKey(rootSymbol))
            {
                PopulateBasicContractSeries(rootSymbol, now);
            }
            return FuturesSeriesGlobalDictionary[rootSymbol].Contracts[0];
        }

        static public Tuple<FuturesContractSpecifications, FuturesPrice> GetSecondContract(DateTime now, string rootSymbol)
        {
            if (!FuturesSeriesGlobalDictionary.ContainsKey(rootSymbol))
            {
                PopulateBasicContractSeries(rootSymbol, now);
            }
            return FuturesSeriesGlobalDictionary[rootSymbol].Contracts[1];
        }

        static public Tuple<FuturesContractSpecifications, FuturesPrice> GetNthContract(DateTime now, int nContract, string rootSymbol)
        {
            if (!FuturesSeriesGlobalDictionary.ContainsKey(rootSymbol) || nContract > FuturesSeriesGlobalDictionary[rootSymbol].Contracts.Count )
            {
                PopulateSpecificContractSeries(rootSymbol, now, nContract);
            }
            return FuturesSeriesGlobalDictionary[rootSymbol].Contracts[nContract];
        }


    }
}
