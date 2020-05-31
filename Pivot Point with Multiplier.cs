using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
 
namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class PivotPointwithMultiplier : Robot
    {
        [Parameter("Stop Loss", DefaultValue = 50)]
        public int StopLoss { get; set; }
 
        [Parameter("Take Profit", DefaultValue = 60)]
        public int TakeProfit { get; set; }
 
        [Parameter(DefaultValue = 1000, MinValue = 0)]
        public int Volume { get; set; }
 
        [Parameter("Pivot Points Periods (H/L)", DefaultValue = 1, MinValue = 1, Step = 1)]
        public int Periods { get; set; }
 
        [Parameter("   Resistance (R1, R2, R3, R4)", DefaultValue = 2, MinValue = 1, MaxValue = 4, Step = 1)]
        public int PivotResistance { get; set; }
 
        [Parameter("   Support (S1, S2, S3, S4)", DefaultValue = 2, MinValue = 1, MaxValue = 4, Step = 1)]
        public int PivotSupport { get; set; }
 
        [Parameter("Simple Moving Average", DefaultValue = true)]
        public bool Trend { get; set; }
 
        [Parameter("   SMA Periods", DefaultValue = 7, MinValue = 0, MaxValue = 10000, Step = 1)]
        public int SMAPeriods { get; set; }
 
        [Parameter("Multiplier", DefaultValue = true)]
        public bool MultiplierYN { get; set; }
 
        [Parameter("   Multiplier", DefaultValue = 2, MinValue = 1, MaxValue = 10, Step = 0.01)]
        public double Multiplier { get; set; }
 
 
        private const string Label = "Pivot Point";
        private MovingAverage SMATrend;
 
        protected override void OnStart()
        {
            Positions.Closed += OnPositionsClosed;
            SMATrend = Indicators.MovingAverage(MarketSeries.Close, SMAPeriods, MovingAverageType.Simple);
        }
 
        protected override void OnTick()
        {
            var longPosition = Positions.Find(Label, Symbol, TradeType.Buy);
            var shortPosition = Positions.Find(Label, Symbol, TradeType.Sell);
            //SMA Trend
            var MA1 = SMATrend.Result.Last(1);
 
            //Pivot Points Levels
            var PivotPoints = MarketData.GetSeries(TimeFrame);
 
            var closepip = PivotPoints.Close.Last(1);
            var Pricey = (Symbol.Ask + Symbol.Bid) / 2;
 
            var highest = MarketSeries.High.Last(1);
            var lowest = MarketSeries.Low.Last(1);
 
            //Pivot Point
            var PP = (highest + lowest + closepip) / 3;
 
            var highestR = MarketSeries.High.Last(1);
            for (int i = 2; i <= Periods; i++)
            {
                if (MarketSeries.High.Last(i) > highestR)
                    highestR = MarketSeries.High.Last(i);
            }
            Print("highest: ", highestR);
 
            var lowestR = MarketSeries.Low.Last(1);
            for (int i = 2; i <= Periods; i++)
            {
                if (MarketSeries.Low.Last(i) < lowestR)
                    lowestR = MarketSeries.Low.Last(i);
            }
            Print("lowest: ", lowestR);
 
            //Pivot Points: Range
            var R4R = 3 * PP + (highestR - 3 * lowestR);
            var R3R = 2 * PP + (highestR - 2 * lowestR);
            var R2R = PP + (highestR - lowestR);
            var R1R = 2 * PP - lowestR;
            var S1R = 2 * PP - highestR;
            var S2R = PP - (highestR - lowestR);
            var S3R = 2 * PP - (2 * highestR - lowestR);
            var S4R = 3 * PP - (3 * highestR - lowestR);
 
            if ((Trend ? closepip >= MA1 : true) && (Pricey <= (PivotSupport == 1 ? S1R : (PivotSupport == 2 ? S2R : (PivotSupport == 3 ? S3R : S4R)))) && longPosition == null)
            {
                ExecuteMarketOrder(TradeType.Buy, Symbol, Symbol.NormalizeVolume(Volume), Label, StopLoss, TakeProfit);
            }
            else if ((Trend ? closepip <= MA1 : true) && (Pricey >= (PivotResistance == 1 ? R1R : (PivotResistance == 2 ? R2R : (PivotResistance == 3 ? R3R : R4R)))) && shortPosition == null)
            {
                ExecuteMarketOrder(TradeType.Sell, Symbol, Symbol.NormalizeVolume(Volume), Label, StopLoss, TakeProfit);
            }
        }
 
 
        private void OnPositionsClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.NetProfit < 0 && MultiplierYN == true)
            {
                TradeType tt = TradeType.Sell;
                if (position.TradeType == TradeType.Sell)
                    tt = TradeType.Buy;
                ExecuteMarketOrder(tt, Symbol, Symbol.NormalizeVolume(position.Volume * Multiplier), "Multiplier", StopLoss, TakeProfit);
            }
        }
    }
}