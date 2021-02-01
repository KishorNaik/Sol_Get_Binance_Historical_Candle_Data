using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sol_Demo
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            String apiKey = "Your Binance Api Key";
            String secretKey = "Your Binance Secret Key";

            DateTime startDate = new DateTime(2020, 11, 1);
            DateTime endDate = new DateTime(2021, 1, 31);

            var kLineHistoricalDataObj = new KLineHistoricalData(new BinanceClient(new BinanceClientOptions() { ApiCredentials = new ApiCredentials(apiKey, secretKey) }));

            var historicalCandleStickData = (await kLineHistoricalDataObj.GetHistoricalKLineDataAsync("BTCUSDT", KlineInterval.FiveMinutes, startDate, endDate)).ToList();

            historicalCandleStickData
                .ToList()
                .ForEach((KLineCandleSeriesDataModel) =>
                {
                    Console.WriteLine($" time : { KLineCandleSeriesDataModel.time } | open : {KLineCandleSeriesDataModel.open} | close : {KLineCandleSeriesDataModel.close} | high : {KLineCandleSeriesDataModel.high} | low :{ KLineCandleSeriesDataModel.low }");
                });
            Console.WriteLine($"Total Row:{historicalCandleStickData.Count}");
        }
    }

    // Step 1:
    // Get list of DateTime Value as per Start Date and End Date.
    public static class DateRange
    {
        public static IEnumerable<DateTime> GetDateRange(DateTime startDate, DateTime endDate)
        {
            if (endDate < startDate)
                throw new ArgumentException("endDate must be greater than or equal to startDate");

            while (startDate <= endDate)
            {
                yield return startDate;
                startDate = startDate.AddDays(1);
            }
        }
    }

    //Step 2:
    // Set Data Model for Candle Stick Chart
    public class KLineCandleSeriesDataModel
    {
        public long time { get; set; }

        public decimal? open { get; set; }

        public decimal? close { get; set; }

        public decimal? high { get; set; }

        public decimal? low { get; set; }
    }

    //Step 3
    // historical candle data from start date to end date Parallel.
    public class KLineHistoricalData
    {
        #region Declaration

        private IBinanceClient binanceClient = null;

        #endregion Declaration

        #region Constructor

        public KLineHistoricalData(IBinanceClient binanceClient)
        {
            this.binanceClient = binanceClient;
        }

        #endregion Constructor

        #region Private Method

        // Call KLine Api using Binance Client.
        private Task<WebCallResult<IEnumerable<IBinanceKline>>> CallKLineDataApi(string symbol, KlineInterval kLineInterval, DateTime startDate)
        {
            return binanceClient.Spot.Market.GetKlinesAsync(symbol, kLineInterval, startTime: startDate, endTime: startDate.AddDays(1), limit: 1000);
        }

        // Map WebResult Data into KLineCandleSeriesData Model List
        private async Task<IEnumerable<KLineCandleSeriesDataModel>> MapKLineCandleSeriesDataAsync(IEnumerable<IBinanceKline> binanceKlines)
        {
            List<Task<KLineCandleSeriesDataModel>> kLineCandleSeriesDataModelsTask =
                (
                   binanceKlines
                    .Select(binanceKLineData => Task.Run(() =>
                    {
                        return new KLineCandleSeriesDataModel()
                        {
                            time = ((Int32)binanceKLineData.OpenTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds),
                            open = binanceKLineData.Open,
                            close = binanceKLineData.Close,
                            high = binanceKLineData.High,
                            low = binanceKLineData.Low
                        };
                    }))
                    )
                    .ToList();

            return await Task.WhenAll(kLineCandleSeriesDataModelsTask);
        }

        #endregion Private Method

        #region Public Method

        public async Task<IEnumerable<KLineCandleSeriesDataModel>> GetHistoricalKLineDataAsync(string symbol, KlineInterval kLineInterval, DateTime startDate, DateTime endDate)
        {
            // Step 1:
            // Get List of Date between Start Date and End Date.
            var dateList =
                DateRange
                .GetDateRange(startDate, endDate);

            // Step 2:
            // Call Get KLine Api Parallel as per the Date list.
            var kLineApiTaskList =
                    dateList
                    .Select((dateTimeRange) => this.CallKLineDataApi(symbol, kLineInterval, dateTimeRange));

            var getHistoricalKLineWebResultDataSet = (await Task.WhenAll(kLineApiTaskList)); //Collection of Day wise Data Set.

            // Step 3:
            // Map Web Result Data into List of KLineCandleSeriesDataModel.
            var mapTaskDataList =
                getHistoricalKLineWebResultDataSet
                .Select((webCallResult) => this.MapKLineCandleSeriesDataAsync(webCallResult.Data.SkipLast(1)));

            var mapCandleStickSeriesDataSet = (await Task.WhenAll(mapTaskDataList)); // Collection of mapping Data as per the day wise

            // Step 4:
            // Merge Large Data Set into One List for Candle Stick Chart

            IEnumerable<KLineCandleSeriesDataModel> finalHistoricalCandleStickModelData =
                (mapCandleStickSeriesDataSet.SelectMany(mergeData => mergeData))
                .OrderBy((kLineCandleSeries) => kLineCandleSeries.time);

            return finalHistoricalCandleStickModelData;
        }

        #endregion Public Method
    }
}