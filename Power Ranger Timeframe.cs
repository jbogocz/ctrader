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
    public class PowerRangerTimeframe : Robot
    {
 
        [Parameter("Buy", DefaultValue = 20, MinValue = 0, Step = 1)]
        public int Buy { get; set; }
 
        [Parameter("Sell", DefaultValue = 20, MinValue = 0, Step = 1)]
        public int Sell { get; set; }
 
        [Parameter("Fixed Pip Step", DefaultValue = 0, MinValue = 0)]
        public int PipStep { get; set; }
 
        [Parameter("Volatility Pip Step (if Fixed = 0)", DefaultValue = 15, MinValue = 1, Step = 1)]
        public int VolPeriods { get; set; }
 
        [Parameter("   Positions per Volatility", DefaultValue = 5, MinValue = 1, Step = 1)]
        public int VolPositions { get; set; }
 
        [Parameter("1. Pivot Points", DefaultValue = true)]
        public bool PivotPointsYN { get; set; }
 
        [Parameter("  Pivot Point Timeframe", DefaultValue = "Hour")]
        public TimeFrame PivotPointTimeFrame { get; set; }
 
        [Parameter("  1.1 Pivot Points Levels", DefaultValue = false)]
        public bool PivotPointsLevels { get; set; }
 
        [Parameter("      Positions per Level", DefaultValue = 4, MinValue = 1, MaxValue = 200, Step = 1)]
        public int PositionsLevels { get; set; }
 
        [Parameter("  1.2 Pivot Points Range", DefaultValue = "-- if Levels 'no' --")]
        public string PivotPointsRange { get; set; }
 
        [Parameter("      Periods (H/L)", DefaultValue = 1, MinValue = 1, Step = 1)]
        public int Periods { get; set; }
 
        [Parameter("      Resistance (R1, R2, R3, R4)", DefaultValue = 2, MinValue = 1, MaxValue = 4, Step = 1)]
        public int PivotResistance { get; set; }
 
        [Parameter("      Support (S1, S2, S3, S4)", DefaultValue = 2, MinValue = 1, MaxValue = 4, Step = 1)]
        public int PivotSupport { get; set; }
 
        [Parameter("2. Trend", DefaultValue = true)]
        public bool Trend { get; set; }
 
        [Parameter("   SMA Timeframe", DefaultValue = "Hour")]
        public TimeFrame SMATimeFrame { get; set; }
 
        [Parameter("   SMA Periods", DefaultValue = 7, MinValue = 0, MaxValue = 10000, Step = 1)]
        public int SMAPeriods { get; set; }
 
        [Parameter("First Position Trend", DefaultValue = false)]
        public bool FirstTrend { get; set; }
 
        [Parameter("First Volume", DefaultValue = 1000, MinValue = 1, Step = 1)]
        public int FirstVolume { get; set; }
 
        [Parameter("Max Spread", DefaultValue = 0.5)]
        public double MaxSpread { get; set; }
 
        [Parameter("Fixed TP", DefaultValue = 0, MinValue = 0)]
        public int FixedTP { get; set; }
 
        [Parameter("Start TP Range (if Fixed = 0)", DefaultValue = 50, MinValue = 1)]
        public int MovingTP { get; set; }
 
        [Parameter("   Min. TP", DefaultValue = 10, MinValue = 1)]
        public int MinTP { get; set; }
 
        [Parameter("trigger", DefaultValue = 0)]
        public int Trigger { get; set; }
 
        [Parameter("Trailing", DefaultValue = 0)]
        public int Trailing { get; set; }
 
        [Parameter("NetLoss", DefaultValue = 0, MinValue = -100, Step = 0.01)]
        public double NetLoss { get; set; }
 
        [Parameter("Volume Exponent on/off", DefaultValue = false)]
        public bool VolumeExponentYN { get; set; }
 
        [Parameter("   Volume Exponent", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 5.0)]
        public double VolumeExponent { get; set; }
 
        [Parameter("Add Volume (in USD)", DefaultValue = 1000, MinValue = 1, MaxValue = 1000000)]
        public double AddVolume { get; set; }
 
        [Parameter("Maximum Volume", DefaultValue = 15000, MinValue = 1, Step = 1000)]
        public int MaxPositionVolume { get; set; }
 
        [Parameter("-Equity Position Count-", DefaultValue = 15, MinValue = 0, MaxValue = 200, Step = 1)]
        public int EquityPositionCount { get; set; }
 
        [Parameter("-Close on Equity (Max.Balance %)-", DefaultValue = 90, MinValue = 0, MaxValue = 1000, Step = 0.01)]
        public double CloseOnEquity { get; set; }
 
        [Parameter("Close Positions (SWAP)", DefaultValue = 1, MinValue = 0, Step = 0.01)]
        public double KillHours { get; set; }
 
        [Parameter("   Close Buy", DefaultValue = true)]
        public bool KillBuy { get; set; }
 
        [Parameter("   Close Sell", DefaultValue = true)]
        public bool KillSell { get; set; }
 
        private string Label = "PowerRanger";
        private Position position;
        private DateTime buyOpenTime;
        private DateTime sellOpenTime;
        private int orderStatus;
        private double currentSpread;
        private bool initial_start = true;
        private bool cStop = false;
        private MovingAverage SMATrend;
 
        //public double balance;
        private List<int> listID = new List<int>();
 
        protected override void OnStart()
        {
            Positions.Closed += OnPositionsClosed;
            var SMASeries = MarketData.GetSeries(SMATimeFrame);
            SMATrend = Indicators.MovingAverage(SMASeries.Close, SMAPeriods, MovingAverageType.Simple);
        }
        protected override void OnTick()
        {
            var AverageTPR = AverageTakeProfit();
 
            currentSpread = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
            if (activeDirectionCount(TradeType.Buy) > 0)
                TrailBuySL(AverageEntryPrice(TradeType.Buy), (FixedTP > 0 ? FixedTP : AverageTPR));
            if (activeDirectionCount(TradeType.Sell) > 0)
                TrailSellSL(AverageEntryPrice(TradeType.Sell), (FixedTP > 0 ? FixedTP : AverageTPR));
            if (MaxSpread >= currentSpread && !cStop)
                SimpleLogic();
            //DrawDescisionLines();
 
            if (KillHours != 0)
            {
                if (KillBuy == true)
                {
                    foreach (var position in Positions.FindAll(Label, Symbol, TradeType.Buy))
                    {
                        if (Server.Time > position.EntryTime.AddMinutes(((FixedTP > 0 ? FixedTP : MinTP) / KillHours) * 24 * 60))
                            ClosePosition(position);
                    }
                }
 
                if (KillSell == true)
                {
                    foreach (var position in Positions.FindAll(Label, Symbol, TradeType.Sell))
                    {
                        if (Server.Time > position.EntryTime.AddMinutes(((FixedTP > 0 ? FixedTP : MinTP) / KillHours) * 24 * 60))
                            ClosePosition(position);
                    }
                }
 
            }
 
            // Close Transactions when Position Counts && Eqity is * % Lower than Max. Balance
            var lastTrade = History.LastOrDefault();
            if (lastTrade != null)
            {
                double maximumBalance = History.Max(x => x.Balance);
                double minimumBalance = History.Min(x => x.Balance);
 
                if ((activeDirectionCount(TradeType.Buy) >= EquityPositionCount || activeDirectionCount(TradeType.Sell) >= EquityPositionCount) && Account.Equity >= maximumBalance * (CloseOnEquity / 100))
                {
                    foreach (var position in Positions)
                    {
                        ClosePosition(position);
 
                    }
                    Print("Closed all positions on equity (in %) = {0}", Account.Equity);
                    Print("New Max target is = {0} * {1}", maximumBalance, (CloseOnEquity / 100));
                }
            }
        }
 
        int AverageTakeProfit()
        {
            int AverageTPV = MovingTP / (Positions.Count == 0 ? 1 : Positions.Count) + MinTP;
            return AverageTPV;
 
        }
 
        protected override void OnError(Error error)
        {
            if (error.Code == ErrorCode.NoMoney)
            {
                cStop = true;
                Print("openning stopped because: not enough money");
            }
        }
        protected override void OnBar()
        {
            RefreshData();
        }
        protected override void OnStop()
        {
            //ChartObjects.RemoveAllObjects();
        }
        private void SimpleLogic()
        {
            if (initial_start)
            {
                TRAILING();
                {
                    //SMA Trend
                    var MA1 = SMATrend.Result.Last(1);
 
                    //Pivot Points Levels
                    var PivotPoints = MarketData.GetSeries(PivotPointTimeFrame);
 
                    var closepip = PivotPoints.Close.Last(1);
                    var Pricey = (Symbol.Ask + Symbol.Bid) / 2;
 
                    var highest = MarketSeries.High.Last(1);
                    var lowest = MarketSeries.Low.Last(1);
 
                    //Pivot Points: Levels
                    var PP = (highest + lowest + closepip) / 3;
                    var R4 = 3 * PP + (highest - 3 * lowest);
                    var R3 = 2 * PP + (highest - 2 * lowest);
                    var R2 = PP + (highest - lowest);
                    var R1 = 2 * PP - lowest;
                    var S1 = 2 * PP - highest;
                    var S2 = PP - (highest - lowest);
                    var S3 = 2 * PP - (2 * highest - lowest);
                    var S4 = 3 * PP - (3 * highest - lowest);
 
 
                    //Pivot Point Range
                    var highestR = MarketSeries.High.Last(1);
                    for (int i = 2; i <= Periods; i++)
                    {
                        if (MarketSeries.High.Last(i) > highestR)
                            highestR = MarketSeries.High.Last(i);
                    }
 
                    var lowestR = MarketSeries.Low.Last(1);
                    for (int i = 2; i <= Periods; i++)
                    {
                        if (MarketSeries.Low.Last(i) < lowestR)
                            lowestR = MarketSeries.Low.Last(i);
                    }
 
                    //Pivot Points: Range
                    var R4R = 3 * PP + (highestR - 3 * lowestR);
                    var R3R = 2 * PP + (highestR - 2 * lowestR);
                    var R2R = PP + (highestR - lowestR);
                    var R1R = 2 * PP - lowestR;
                    var S1R = 2 * PP - highestR;
                    var S2R = PP - (highestR - lowestR);
                    var S3R = 2 * PP - (2 * highestR - lowestR);
                    var S4R = 3 * PP - (3 * highestR - lowestR);
 
 
                    //Entry Signal previous bar larger than the one before
                    if ((FirstTrend ? (Trend ? closepip >= MA1 : true) && (PivotPointsYN ? (PivotPointsLevels ? Pricey <= (activeDirectionCount(TradeType.Buy) < PositionsLevels ? S1 : (activeDirectionCount(TradeType.Buy) >= PositionsLevels && activeDirectionCount(TradeType.Buy) < PositionsLevels * 2 ? S2 : (activeDirectionCount(TradeType.Buy) >= PositionsLevels * 2 && activeDirectionCount(TradeType.Buy) < PositionsLevels * 3 ? S3 : S4))) : (Pricey <= (PivotSupport == 1 ? S1R : (PivotSupport == 2 ? S2R : (PivotSupport == 3 ? S3R : S4R))))) : true) : true) && Buy >= 1 && activeDirectionCount(TradeType.Buy) == 0 && MarketSeries.Close.Last(1) > MarketSeries.Close.Last(2))
                    {
                        orderStatus = OrderSend(TradeType.Buy, Volumizer(FirstVolume));
                        if (orderStatus > 0)
                            buyOpenTime = MarketSeries.OpenTime.Last(0);
                        else
                            Print("First BUY openning error at: ", Symbol.Ask, "Error Type: ", LastResult.Error);
                    }
                    //Entry signal  previous bar smaller than the one before
                    if ((FirstTrend ? (Trend ? closepip <= MA1 : true) && (PivotPointsYN ? (PivotPointsLevels ? Pricey >= (activeDirectionCount(TradeType.Sell) < PositionsLevels ? R1 : (activeDirectionCount(TradeType.Sell) >= PositionsLevels && activeDirectionCount(TradeType.Sell) < PositionsLevels * 2 ? R2 : (activeDirectionCount(TradeType.Sell) >= PositionsLevels * 2 && activeDirectionCount(TradeType.Sell) < PositionsLevels * 3 ? R3 : R4))) : (Pricey >= (PivotResistance == 1 ? R1R : (PivotResistance == 2 ? R2R : (PivotResistance == 3 ? R3R : R4R))))) : true) : true) && Sell >= 1 && activeDirectionCount(TradeType.Sell) == 0 && MarketSeries.Close.Last(2) > MarketSeries.Close.Last(1))
                    {
                        orderStatus = OrderSend(TradeType.Sell, Volumizer(FirstVolume));
                        if (orderStatus > 0)
                            sellOpenTime = MarketSeries.OpenTime.Last(0);
                        else
                            Print("First SELL openning error at: ", Symbol.Bid, "Error Type: ", LastResult.Error);
                    }
                }
                Gridernize();
            }
        }
 
 
        //Create the Grid sistem based on the PipStep
        private void Gridernize()
        {
 
            TRAILING();
            {
                //SMA Trend
                var MA1 = SMATrend.Result.Last(1);
 
                //Pivot Points Levels
                var PivotPoints = MarketData.GetSeries(PivotPointTimeFrame);
 
                var closepip = PivotPoints.Close.Last(1);
                var Pricey = (Symbol.Ask + Symbol.Bid) / 2;
 
                var highest = MarketSeries.High.Last(1);
                var lowest = MarketSeries.Low.Last(1);
 
                //Pivot Points: Levels
                var PP = (highest + lowest + closepip) / 3;
                var R4 = 3 * PP + (highest - 3 * lowest);
                var R3 = 2 * PP + (highest - 2 * lowest);
                var R2 = PP + (highest - lowest);
                var R1 = 2 * PP - lowest;
                var S1 = 2 * PP - highest;
                var S2 = PP - (highest - lowest);
                var S3 = 2 * PP - (2 * highest - lowest);
                var S4 = 3 * PP - (3 * highest - lowest);
 
 
                //Pivot Point Range
                var highestR = MarketSeries.High.Last(1);
                for (int i = 2; i <= Periods; i++)
                {
                    if (MarketSeries.High.Last(i) > highestR)
                        highestR = MarketSeries.High.Last(i);
                }
 
                var lowestR = MarketSeries.Low.Last(1);
                for (int i = 2; i <= Periods; i++)
                {
                    if (MarketSeries.Low.Last(i) < lowestR)
                        lowestR = MarketSeries.Low.Last(i);
                }
 
                //Pivot Points: Range
                var R4R = 3 * PP + (highestR - 3 * lowestR);
                var R3R = 2 * PP + (highestR - 2 * lowestR);
                var R2R = PP + (highestR - lowestR);
                var R1R = 2 * PP - lowestR;
                var S1R = 2 * PP - highestR;
                var S2R = PP - (highestR - lowestR);
                var S3R = 2 * PP - (2 * highestR - lowestR);
                var S4R = 3 * PP - (3 * highestR - lowestR);
 
                var PipSteps = CalculatePips();
 
                if ((Trend ? closepip >= MA1 : true) && (PivotPointsYN ? (PivotPointsLevels ? Pricey <= (activeDirectionCount(TradeType.Buy) < PositionsLevels ? S1 : (activeDirectionCount(TradeType.Buy) >= PositionsLevels && activeDirectionCount(TradeType.Buy) < PositionsLevels * 2 ? S2 : (activeDirectionCount(TradeType.Buy) >= PositionsLevels * 2 && activeDirectionCount(TradeType.Buy) < PositionsLevels * 3 ? S3 : S4))) : (Pricey <= (PivotSupport == 1 ? S1R : (PivotSupport == 2 ? S2R : (PivotSupport == 3 ? S3R : S4R))))) : true) && activeDirectionCount(TradeType.Buy) > 0 && activeDirectionCount(TradeType.Buy) < Buy)
                {
                    if (Math.Round(Symbol.Ask, Symbol.Digits) < Math.Round(GetHighestBuyEntry(TradeType.Buy) - (PipStep > 0 ? PipStep : PipSteps) * Symbol.PipSize, Symbol.Digits) && buyOpenTime != MarketSeries.OpenTime.Last(0))
                    {
                        long b_lotS = NextLotSize(TradeType.Buy);
                        orderStatus = OrderSend(TradeType.Buy, Volumizer(b_lotS));
                        if (orderStatus > 0)
                            buyOpenTime = MarketSeries.OpenTime.Last(0);
                        else
                            Print("Next BUY openning error at: ", Symbol.Ask, "Error Type: ", LastResult.Error);
                    }
                }
 
                if ((Trend ? closepip <= MA1 : true) && (PivotPointsYN ? (PivotPointsLevels ? Pricey >= (activeDirectionCount(TradeType.Sell) < PositionsLevels ? R1 : (activeDirectionCount(TradeType.Sell) >= PositionsLevels && activeDirectionCount(TradeType.Sell) < PositionsLevels * 2 ? R2 : (activeDirectionCount(TradeType.Sell) >= PositionsLevels * 2 && activeDirectionCount(TradeType.Sell) < PositionsLevels * 3 ? R3 : R4))) : (Pricey >= (PivotResistance == 1 ? R1R : (PivotResistance == 2 ? R2R : (PivotResistance == 3 ? R3R : R4R))))) : true) && activeDirectionCount(TradeType.Sell) > 0 && activeDirectionCount(TradeType.Sell) < Sell)
                {
                    if (Math.Round(Symbol.Bid, Symbol.Digits) > Math.Round(GetLowestSellEntry(TradeType.Sell) + (PipStep > 0 ? PipStep : PipSteps) * Symbol.PipSize, Symbol.Digits) && sellOpenTime != MarketSeries.OpenTime.Last(0))
                    {
                        long s_lotS = NextLotSize(TradeType.Sell);
                        orderStatus = OrderSend(TradeType.Sell, Volumizer(s_lotS));
                        if (orderStatus > 0)
                            sellOpenTime = MarketSeries.OpenTime.Last(0);
                        else
                            Print("Next SELL openning error at: ", Symbol.Bid, "Error Type: ", LastResult.Error);
                    }
                }
            }
        }
        double CalculatePips()
        {
            //Volatility Range
            var highestV = MarketSeries.High.Last(1);
            for (int i = 2; i <= VolPeriods; i++)
            {
                if (MarketSeries.High.Last(i) > highestV)
                    highestV = MarketSeries.High.Last(i);
            }
 
            var lowestV = MarketSeries.Low.Last(1);
            for (int i = 2; i <= VolPeriods; i++)
            {
                if (MarketSeries.Low.Last(i) < lowestV)
                    lowestV = MarketSeries.Low.Last(i);
            }
 
            double PipSteper = ((highestV - lowestV) / VolPositions) * 10000;
            Print("highestV ", highestV);
            Print("lowestV ", lowestV);
            Print("pipsteper ", PipSteper);
            return PipSteper;
 
        }
 
        private void TRAILING()
        {
            if (Trailing > 0 && Trigger > 0)
            {
 
                Position[] positions = Positions.FindAll(Label, Symbol);
 
                foreach (Position position in positions)
                {
 
                    if (position.TradeType == TradeType.Sell)
                    {
 
                        double distance = position.EntryPrice - Symbol.Ask;
 
                        if (distance >= Trigger * Symbol.PipSize)
                        {
 
                            double newStopLossPrice = Symbol.Ask + Trailing * Symbol.PipSize;
 
                            if (position.StopLoss == null || newStopLossPrice < position.StopLoss)
                            {
 
                                ModifyPosition(position, newStopLossPrice, position.TakeProfit);
 
                            }
                        }
                    }
 
                    else
                    {
 
                        double distance = Symbol.Bid - position.EntryPrice;
 
                        if (distance >= Trigger * Symbol.PipSize)
                        {
 
                            double newStopLossPrice = Symbol.Bid - Trailing * Symbol.PipSize;
 
                            if (position.StopLoss == null || newStopLossPrice > position.StopLoss)
                            {
 
                                ModifyPosition(position, newStopLossPrice, position.TakeProfit);
 
 
                            }
                        }
                    }
                }
            }
        }
 
        private int OrderSend(TradeType TrdTp, long iVol)
        {
            int orderStatus = 0;
            var symbolPositionsCount = Positions.FindAll(Label, Symbol);
            if (symbolPositionsCount.Length < (Buy + Sell) && Positions.Count < 200)
            {
                if (iVol > 0)
                {
                    Print("symbolPositionsCount ", symbolPositionsCount.Length);
                    TradeResult result = ExecuteMarketOrder(TrdTp, Symbol, iVol, Label, 0, 0, 0, "smart_grid");
 
 
                    if (result.IsSuccessful)
                    {
                        Print(TrdTp, "Opened at: ", result.Position.EntryPrice);
                        orderStatus = 1;
                    }
                    else
                        Print(TrdTp, "Openning Error: ", result.Error);
                }
            }
            else
                Print("Volume calculation error: Calculated Volume is: ", iVol);
            return orderStatus;
        }
 
 
        //Trail the stoploss position for a BUY Order
        private void TrailBuySL(double price, int tp)
        {
            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TradeType.Buy)
                    {
                        double? new_tp = Math.Round(price + tp * Symbol.PipSize, Symbol.Digits);
                        if (position.TakeProfit != new_tp)
                            ModifyPosition(position, position.StopLoss, new_tp);
                    }
                }
            }
        }
 
 
        //Trail the stoploss position for a SELL Order
        private void TrailSellSL(double price, int tp)
        {
            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TradeType.Sell)
                    {
                        double? new_tp = Math.Round(price - tp * Symbol.PipSize, Symbol.Digits);
                        if (position.TakeProfit != new_tp)
                            ModifyPosition(position, position.StopLoss, new_tp);
                    }
                }
            }
        }
 
        //Draw the Action lines to illustrate the trades
        private void DrawDescisionLines()
        {
            if (activeDirectionCount(TradeType.Buy) > 1)
            {
                double y = AverageEntryPrice(TradeType.Buy);
                ChartObjects.DrawHorizontalLine("bpoint", y, Colors.Yellow, 2, LineStyle.Dots);
            }
            else
                ChartObjects.RemoveObject("bpoint");
            if (activeDirectionCount(TradeType.Sell) > 1)
            {
                double z = AverageEntryPrice(TradeType.Sell);
                ChartObjects.DrawHorizontalLine("spoint", z, Colors.HotPink, 2, LineStyle.Dots);
            }
            else
                ChartObjects.RemoveObject("spoint");
            ChartObjects.DrawText("pan", botText(), StaticPosition.TopLeft, Colors.Tomato);
        }
 
        //Text to be printed on Screen
        private string botText()
        {
            string printString = "";
            string BPos = "";
            string SPos = "";
            string spread = "";
            string BTA = "";
            string STA = "";
            double CBPOS = 0;
            double CSPOS = 0;
 
            CBPOS = activeDirectionCount(TradeType.Buy);
            CSPOS = activeDirectionCount(TradeType.Sell);
            spread = "\nSpread = " + Math.Round(currentSpread, 1);
            if (CBPOS > 0)
                BPos = "\nBuy Positions = " + activeDirectionCount(TradeType.Buy);
            if (CSPOS > 0)
                SPos = "\nSell Positions = " + activeDirectionCount(TradeType.Sell);
            if (activeDirectionCount(TradeType.Buy) > 0)
            {
                double abta = Math.Round((AverageEntryPrice(TradeType.Buy) - Symbol.Bid) / Symbol.PipSize, 1);
                BTA = "\nBuy Target Away = " + abta;
            }
            if (activeDirectionCount(TradeType.Sell) > 0)
            {
                double asta = Math.Round((Symbol.Ask - AverageEntryPrice(TradeType.Sell)) / Symbol.PipSize, 1);
                STA = "\nSell Target Away = " + asta;
            }
            if (currentSpread > MaxSpread)
                printString = "MAX SPREAD EXCEED";
            else
                printString = "Foxy Grid" + BPos + spread + SPos + BTA + STA;
            return (printString);
        }
 
        //Return the active positions of this bot
        private int ActiveLabelCount()
        {
            int ASide = 0;
            for (int i = Positions.Count - 1; i >= 0; i--)
            {
                position = Positions[i];
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                    ASide++;
            }
            return ASide;
        }
 
        //Return the position count of trades of specific type (BUY/SELL)
        private int activeDirectionCount(TradeType TrdTp)
        {
            int TSide = 0;
            for (int i = Positions.Count - 1; i >= 0; i--)
            {
                position = Positions[i];
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TrdTp)
                        TSide++;
                }
            }
            return TSide;
        }
 
        //The Avarage EtryPrice for all positions of a specific type (SELL/BUY)
        private double AverageEntryPrice(TradeType TrdTp)
        {
            double Result = 0;
            double AveragePrice = 0;
            long Count = 0;
            for (int i = Positions.Count - 1; i >= 0; i--)
            {
                position = Positions[i];
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TrdTp)
                    {
                        AveragePrice += position.EntryPrice * position.Volume;
                        Count += position.Volume;
                    }
                }
            }
            if (AveragePrice > 0 && Count > 0)
                Result = Math.Round(AveragePrice / Count, Symbol.Digits);
            return Result;
        }
 
 
        private double GetHighestBuyEntry(TradeType TrdTp)
        {
            double GetHighestBuyEntry = 0;
            for (int i = Positions.Count - 1; i >= 0; i--)
            {
                position = Positions[i];
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TrdTp)
                    {
                        if (GetHighestBuyEntry == 0)
                        {
                            GetHighestBuyEntry = position.EntryPrice;
                            continue;
                        }
                        if (position.EntryPrice < GetHighestBuyEntry)
                            GetHighestBuyEntry = position.EntryPrice;
                    }
                }
            }
            return GetHighestBuyEntry;
        }
 
 
        private double GetLowestSellEntry(TradeType TrdTp)
        {
            double GetLowestSellEntry = 0;
            for (int i = Positions.Count - 1; i >= 0; i--)
            {
                position = Positions[i];
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TrdTp)
                    {
                        if (GetLowestSellEntry == 0)
                        {
                            GetLowestSellEntry = position.EntryPrice;
                            continue;
                        }
                        if (position.EntryPrice > GetLowestSellEntry)
                            GetLowestSellEntry = position.EntryPrice;
                    }
                }
            }
            return GetLowestSellEntry;
        }
 
 
        private double LastEntry(TradeType TrdTp)
        {
            double LastEntryPrice = 0;
            int APositionID = 0;
            for (int i = Positions.Count - 1; i >= 0; i--)
            {
                position = Positions[i];
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TrdTp)
                    {
                        if (APositionID == 0 || APositionID > position.Id)
                        {
                            LastEntryPrice = position.EntryPrice;
                            APositionID = position.Id;
                        }
                    }
                }
            }
            return LastEntryPrice;
        }
 
 
        private long LastVolume(TradeType TrdTp)
        {
            long LastVolumeTraded = 0;
            int APositionID = 0;
            for (int i = Positions.Count - 1; i >= 0; i--)
            {
                position = Positions[i];
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TrdTp)
                    {
                        if (APositionID == 0 || APositionID > position.Id)
                        {
                            LastVolumeTraded = position.Volume;
                            APositionID = position.Id;
                        }
                    }
                }
            }
            return LastVolumeTraded;
        }
 
 
        private long clt(TradeType TrdTp)
        {
            long Result = 0;
            for (int i = Positions.Count - 1; i >= 0; i--)
            {
                position = Positions[i];
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TrdTp)
                        Result += position.Volume;
                }
            }
            return Result;
        }
 
 
        private int GridCount(TradeType TrdTp1, TradeType TrdTp2)
        {
            double LastEntryPrice = LastEntry(TrdTp2);
            int APositionID = 0;
            for (int i = Positions.Count - 1; i >= 0; i--)
            {
                position = Positions[i];
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TrdTp1 && TrdTp1 == TradeType.Buy)
                    {
                        if (Math.Round(position.EntryPrice, Symbol.Digits) <= Math.Round(LastEntryPrice, Symbol.Digits))
                            APositionID++;
                    }
                    if (position.TradeType == TrdTp1 && TrdTp1 == TradeType.Sell)
                    {
                        if (Math.Round(position.EntryPrice, Symbol.Digits) >= Math.Round(LastEntryPrice, Symbol.Digits))
                            APositionID++;
                    }
                }
            }
            return APositionID;
        }
 
 
        private long NextLotSize(TradeType TrdRp)
        {
            int current_Volume = GridCount(TrdRp, TrdRp);
            long last_Volume = LastVolume(TrdRp);
            long next_Volume2 = (VolumeExponentYN == true ? Symbol.NormalizeVolume(last_Volume * Math.Pow(VolumeExponent, current_Volume)) : Symbol.NormalizeVolume(last_Volume + (current_Volume * AddVolume)));
            long next_Volume = (MaxPositionVolume <= next_Volume2 ? Symbol.NormalizeVolume(MaxPositionVolume) : ((VolumeExponentYN == true ? Symbol.NormalizeVolume(last_Volume * Math.Pow(VolumeExponent, current_Volume)) : Symbol.NormalizeVolume(last_Volume + (current_Volume * AddVolume)))));
            return next_Volume;
        }
 
 
        private long Volumizer(long vol)
        {
            long volmin = Symbol.VolumeMin;
            long volmax = Symbol.VolumeMax;
            long voltemp = vol;
 
            if (voltemp < volmin)
                voltemp = volmin;
            if (voltemp > volmax)
                voltemp = volmax;
            return voltemp;
        }
 
        private void OnPositionsClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.NetProfit < NetLoss && MaxPositionVolume >= position.VolumeInUnits && position.Label == Label && position.SymbolCode == Symbol.Code && Trailing > Trigger)
            {
                TradeType tt = TradeType.Sell;
                if (position.TradeType == TradeType.Sell)
                    tt = TradeType.Buy;
                ExecuteMarketOrder(tt, Symbol, Symbol.NormalizeVolume(position.Volume * 2), Label, 0, 0, 0, "smart_grid");
            }
 
            if (position.NetProfit < NetLoss && MaxPositionVolume < position.VolumeInUnits && position.Label == Label && position.SymbolCode == Symbol.Code && Trailing > Trigger)
            {
                TradeType tt = TradeType.Sell;
                if (position.TradeType == TradeType.Sell)
                    tt = TradeType.Buy;
                ExecuteMarketOrder(tt, Symbol, Symbol.NormalizeVolume(position.Volume), Label, 0, 0, 0, "smart_grid");
 
            }
        }
    }
}