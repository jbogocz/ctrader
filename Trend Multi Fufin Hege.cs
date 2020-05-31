using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
using System.Collections;
using System.Collections.Generic;
 
 
namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MultiFufinHege : Robot
    {
        [Parameter("Max No of Positions", DefaultValue = 100, MinValue = 1, MaxValue = 1000, Step = 1)]
        public int NoOfPositions { get; set; }
 
        [Parameter("Max Spread", DefaultValue = 3.0, MinValue = 0, MaxValue = 1000, Step = 0.1)]
        public double MaxSpread { get; set; }
 
        [Parameter("Volume Start (in USD)", DefaultValue = 1000, MinValue = 0, MaxValue = 1000000, Step = 1)]
        public int Volume { get; set; }
 
        [Parameter("Stop Loss", DefaultValue = 0, MinValue = 0, MaxValue = 200, Step = 1)]
        public int StopLoss { get; set; }
 
        [Parameter("Take Profit", DefaultValue = 60, MinValue = 0, MaxValue = 200, Step = 1)]
        public int TakeProfit { get; set; }
 
        // Multi Hedge
 
        [Parameter("--Multi Fufin Hedge No Limits on/off--", DefaultValue = "--Hedge--")]
        public string Opis1 { get; set; }
 
        [Parameter("   Multi Fufin Hedge No Limits", DefaultValue = true)]
        public bool MulitiHedge { get; set; }
 
        [Parameter("   Allow Close when Pips > (if Fufin off)", DefaultValue = 3, MinValue = 0, MaxValue = 50, Step = 1)]
        public int PipsSize { get; set; }
 
        // Indicators
 
        [Parameter("--INDICATORS--", DefaultValue = "--INDICATORS--")]
        public string Description1 { get; set; }
 
        [Parameter("   MA Type")]
        public MovingAverageType MAType { get; set; }
 
        [Parameter("   Source")]
        public DataSeries SourceSeries { get; set; }
 
        [Parameter("   Slow Periods", DefaultValue = 14, MinValue = 0, MaxValue = 100, Step = 1)]
        public int SlowPeriods { get; set; }
 
        [Parameter("   Fast Periods", DefaultValue = 4, MinValue = 0, MaxValue = 100, Step = 1)]
        public int FastPeriods { get; set; }
 
        [Parameter("   Change Direction After SL Hit", DefaultValue = true)]
        public bool TrendSignal2 { get; set; }
 
        // Parametrs
 
        [Parameter("--PARAMETERS MULTIPLER--", DefaultValue = "--MULTIPLER--")]
        public string Decription2 { get; set; }
 
        [Parameter("   Multiplier", DefaultValue = false)]
        public bool MultiplierYN { get; set; }
 
        [Parameter("   Multiplier (if yes)", DefaultValue = 2, MinValue = 0, MaxValue = 20, Step = 0.1)]
        public double Multiplier { get; set; }
 
        [Parameter("--PARAMETERS ADD VOLUME--", DefaultValue = "--ADD VOLUME--")]
        public string Decription3 { get; set; }
 
        [Parameter("   Add Volume (in USD, if Multipler no)", DefaultValue = 1, MinValue = 0, MaxValue = 1000000, Step = 1)]
        public int addVolume { get; set; }
 
        // Safety
 
        [Parameter("--SAFETY PARAMETERS--", DefaultValue = "--SAFETY1--")]
        public string Description4 { get; set; }
 
        [Parameter("   Volume Max. (in USD)", DefaultValue = 15000, MinValue = 0, MaxValue = 1000000, Step = 1)]
        public int VolumeMax { get; set; }
 
        [Parameter("   Maximum DrawDown %", DefaultValue = 20.0, MinValue = 1.0, MaxValue = 100.0)]
        public double maxDrawDown { get; set; }
 
        [Parameter("   Stop when Equity Hit (in USD)", DefaultValue = 10000, MinValue = 0, MaxValue = 1000000, Step = 1)]
        public double EquityLevel { get; set; }
 
        [Parameter("--SAFETY PARAMETERS IF SL IS SET--", DefaultValue = "--SAFETY2--")]
        public string Description5 { get; set; }
 
        [Parameter("   Volume Trigger for Equity (+) (1k USD)", DefaultValue = 10000, MinValue = 0, MaxValue = 1000000, Step = 1)]
        public int VolumeEquityClose { get; set; }
 
        [Parameter("   Close on Equity (+) (Max.Balance %)", DefaultValue = 100.5, MinValue = 0, MaxValue = 1000, Step = 0.01)]
        public double Zarobek2 { get; set; }
 
        [Parameter("   Volume Trigger for Equity (-) (1k USD)", DefaultValue = 50000, MinValue = 0, MaxValue = 1000000, Step = 1)]
        public int VolumeEquityCloseNegative { get; set; }
 
        [Parameter("   Close on Equity (-) (Max.Balance %)", DefaultValue = 90, MinValue = 0, MaxValue = 1000, Step = 0.01)]
        public double Zarobek2Negative { get; set; }
 
        private MovingAverage slowMa;
        private MovingAverage fastMa;
        private const string label = "Trend";
        private double sp_d;
        public double balance;
        public double Pips;
        public double Ask;
        private List<int> listID = new List<int>();
 
        protected override void OnStart()
        {
            balance = Account.Balance;
            Positions.Closed += OnPositionsClosed;
            fastMa = Indicators.MovingAverage(SourceSeries, FastPeriods, MAType);
            slowMa = Indicators.MovingAverage(SourceSeries, SlowPeriods, MAType);
        }
 
        protected override void OnBar()
        {
            var lastTrade = History.LastOrDefault();
            var longPosition = Positions.Find(label, Symbol, TradeType.Buy);
            var shortPosition = Positions.Find(label, Symbol, TradeType.Sell);
            var currentSlowMa = slowMa.Result.Last(0);
            var currentFastMa = fastMa.Result.Last(0);
            var previousSlowMa = slowMa.Result.Last(1);
            var previousFastMa = fastMa.Result.Last(1);
 
            //Print("max spread {0}", MaxSpread);
            sp_d = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
            //Print("spread {0}", sp_d);
            if (MaxSpread >= sp_d && Positions.Count < NoOfPositions)
            {
                // PipSize
                if (PipsSize > 0)
                {
                    if ((MulitiHedge ? true : longPosition == null) && previousSlowMa > previousFastMa && currentSlowMa <= currentFastMa)
                    {
                        if (shortPosition != null && shortPosition.Pips >= PipsSize)
                            ClosePosition(shortPosition);
                        ExecuteMarketOrder(TradeType.Buy, Symbol, Symbol.NormalizeVolume(Volume), label, StopLoss, TakeProfit);
                    }
                    else if ((MulitiHedge ? true : shortPosition == null) && previousSlowMa < previousFastMa && currentSlowMa >= currentFastMa)
                    {
                        if (longPosition != null && longPosition.Pips > PipsSize)
                            ClosePosition(longPosition);
                        ExecuteMarketOrder(TradeType.Sell, Symbol, Symbol.NormalizeVolume(Volume), label, StopLoss, TakeProfit);
                    }
                }
 
                else
                {
                    if ((MulitiHedge ? true : longPosition == null) && previousSlowMa > previousFastMa && currentSlowMa <= currentFastMa)
                    {
                        if (shortPosition != null)
                            ClosePosition(shortPosition);
                        ExecuteMarketOrder(TradeType.Buy, Symbol, Symbol.NormalizeVolume(Volume), label, StopLoss, TakeProfit);
                    }
                    else if ((MulitiHedge ? true : shortPosition == null) && previousSlowMa < previousFastMa && currentSlowMa >= currentFastMa)
                    {
                        if (longPosition != null)
                            ClosePosition(longPosition);
                        ExecuteMarketOrder(TradeType.Sell, Symbol, Symbol.NormalizeVolume(Volume), label, StopLoss, TakeProfit);
                    }
                }
 
            }
 
            // Maximum DrawDown %
            if (Account.Equity < Account.Balance * ((100 - maxDrawDown) / 100))
            {
 
                foreach (var position in Positions)
                {
                    listID.Add(position.Id);
                    position.Close();
                }
                Stop();
            }
 
            // Stop When Equity
            if (EquityLevel < Account.Equity)
            {
                foreach (var position in Positions)
                {
                    position.Close();
                }
                Print("Closed all positions on equity (in USD) = {0}", Account.Equity);
                Stop();
            }
 
 
            // Close Transactions When Eqity is * % Higher than Max. Balance
            if (lastTrade != null)
            {
                double maximumBalance = History.Max(x => x.Balance);
                double minimumBalance = History.Min(x => x.Balance);
                var volumenOstatniej = Symbol.NormalizeVolumeInUnits(lastTrade.Volume);
 
                if (lastTrade.Volume >= VolumeEquityClose && Account.Equity >= maximumBalance * (Zarobek2 / 100))
                {
                    foreach (var position in Positions)
                    {
                        listID.Add(position.Id);
                        ClosePosition(position);
                        Print("Position: {0} closed by Eqiuty", position.Id);
                    }
                    Print("Maximum position volume (in USD) = {0}", lastTrade.Volume);
                    Print("Closed all positions on equity (in %) = {0}", Account.Equity);
                    Print("New Max target is = {0} * {1}", maximumBalance, (Zarobek2 / 100));
                }
            }
 
            // Close Transactions When Eqity is * % Lower than Max. Balance
            if (lastTrade != null)
            {
                double maximumBalance = History.Max(x => x.Balance);
                double minimumBalance = History.Min(x => x.Balance);
                var volumenOstatniej = Symbol.NormalizeVolumeInUnits(lastTrade.Volume);
 
                if (lastTrade.Volume >= VolumeEquityCloseNegative && Account.Equity >= maximumBalance * (Zarobek2Negative / 100))
                {
                    foreach (var position in Positions)
                    {
                        listID.Add(position.Id);
                        ClosePosition(position);
                        Print("Position: {0} closed by Eqiuty", position.Id);
                    }
                    Print("Maximum position volume (in USD) = {0}", lastTrade.Volume);
                    Print("Closed all positions on equity (in %) = {0}", Account.Equity);
                    Print("New Max target is = {0} * {1}", maximumBalance, (Zarobek2Negative / 100));
                }
            }
 
 
        }
 
        private void OnPositionsClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            var lastTradeVolUnit = Symbol.NormalizeVolumeInUnits(position.Volume);
 
            // NetProfit is negative
            if (position.NetProfit < 0 && listID.Contains(position.Id) == false)
            {
                // Change Direction
                if (TrendSignal2 == true)
                {
                    if (position.TradeType == TradeType.Sell && VolumeMax > position.Volume)
                    {
                        if (MultiplierYN == true)
                            ExecuteMarketOrder(TradeType.Buy, Symbol, Symbol.NormalizeVolume(position.Volume * Multiplier), "AfterSL", StopLoss, TakeProfit);
                        else
                            ExecuteMarketOrder(TradeType.Buy, Symbol, Symbol.NormalizeVolume(position.Volume + addVolume), "AfterSL", StopLoss, TakeProfit);
                    }
                    if (position.TradeType == TradeType.Sell && VolumeMax <= position.Volume)
                    {
                        ExecuteMarketOrder(TradeType.Buy, Symbol, Symbol.NormalizeVolume(position.Volume), "AfterSL", StopLoss, TakeProfit);
                    }
                    if (position.TradeType == TradeType.Buy && VolumeMax > position.Volume)
                    {
                        if (MultiplierYN == true)
                            ExecuteMarketOrder(TradeType.Sell, Symbol, Symbol.NormalizeVolume(position.Volume * Multiplier), "AfterSL", StopLoss, TakeProfit);
                        else
                            ExecuteMarketOrder(TradeType.Sell, Symbol, Symbol.NormalizeVolume(position.Volume + addVolume), "AfterSL", StopLoss, TakeProfit);
                    }
                    if (position.TradeType == TradeType.Buy && VolumeMax <= position.Volume)
                    {
 
                        ExecuteMarketOrder(TradeType.Sell, Symbol, Symbol.NormalizeVolume(position.Volume), "AfterSL", StopLoss, TakeProfit);
                    }
 
                }
 
                // Same Direction
                //if (TrendSignal2 == false)
                else
                {
                    if (position.TradeType == TradeType.Sell)
                    {
                        if (MultiplierYN == true)
                            ExecuteMarketOrder(TradeType.Sell, Symbol, Symbol.NormalizeVolume(position.Volume * Multiplier), "AfterSL", StopLoss, TakeProfit);
                        else
                            ExecuteMarketOrder(TradeType.Sell, Symbol, Symbol.NormalizeVolume(position.Volume + addVolume), "AfterSL", StopLoss, TakeProfit);
                    }
                    if (position.TradeType == TradeType.Buy)
                    {
                        if (MultiplierYN == true)
                            ExecuteMarketOrder(TradeType.Buy, Symbol, Symbol.NormalizeVolume(position.Volume * Multiplier), "AfterSL", StopLoss, TakeProfit);
                        else
                            ExecuteMarketOrder(TradeType.Buy, Symbol, Symbol.NormalizeVolume(position.Volume + addVolume), "AfterSL", StopLoss, TakeProfit);
                    }
 
                }
 
            }
 
 
        }
 
 
 
    }
}