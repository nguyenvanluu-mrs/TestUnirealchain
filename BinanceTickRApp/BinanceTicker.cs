using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using RestSharp;
using StockTickR.Hubs;

namespace StockTickR
{
    public class BinanceTicker
    {
        private readonly SemaphoreSlim _marketStateLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _updateStockPricesLock = new SemaphoreSlim(1, 1);

        private readonly ConcurrentDictionary<string, PriceChangeInformation> _stocks = new ConcurrentDictionary<string, PriceChangeInformation>();

        private readonly Subject<PriceChangeInformation> _subject = new Subject<PriceChangeInformation>();

        // Coin can go up or down by a percentage of this factor on each change
        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(3000);

        private Timer _timer;
        private volatile bool _updatingStockPrices;
        private volatile MarketState _marketState;

        public BinanceTicker(IHubContext<BinanceTickerHub> hub)
        {
            Hub = hub;
            LoadDefaultStocks();
        }

        private IHubContext<BinanceTickerHub> Hub
        {
            get;
            set;
        }

        public MarketState MarketState
        {
            get { return _marketState; }
            private set { _marketState = value; }
        }

        public IEnumerable<PriceChangeInformation> GetAllStocks()
        {
            return _stocks.Values;
        }

        public IObservable<PriceChangeInformation> StreamStocks()
        {
            return _subject;
        }

        public async Task OpenMarket()
        {
            await _marketStateLock.WaitAsync();
            try
            {
                if (MarketState != MarketState.Open)
                {
                    _timer = new Timer(UpdateStockPrices, null, _updateInterval, _updateInterval);

                    MarketState = MarketState.Open;

                    await BroadcastMarketStateChange(MarketState.Open);
                }
            }
            finally
            {
                _marketStateLock.Release();
            }
        }

        public async Task CloseMarket()
        {
            await _marketStateLock.WaitAsync();
            try
            {
                if (MarketState == MarketState.Open)
                {
                    if (_timer != null)
                    {
                        _timer.Dispose();
                    }

                    MarketState = MarketState.Closed;

                    await BroadcastMarketStateChange(MarketState.Closed);
                }
            }
            finally
            {
                _marketStateLock.Release();
            }
        }

        public async Task Reset()
        {
            await _marketStateLock.WaitAsync();
            try
            {
                if (MarketState != MarketState.Closed)
                {
                    throw new InvalidOperationException("Market must be closed before it can be reset.");
                }

                LoadDefaultStocks();
                await BroadcastMarketReset();
            }
            finally
            {
                _marketStateLock.Release();
            }
        }

        private void LoadDefaultStocks()
        {
            _stocks.Clear();

            var stocks = new List<PriceChangeInformation>
            {
                new PriceChangeInformation { symbol = "ETHBTC",  lastPrice = "0.6011",highPrice="0",lowPrice="0",priceChange="0",priceChangePercent = "0",openTime=1634196690912,volume="0",quoteVolume="0"},
                new PriceChangeInformation { symbol = "LTCBTC",  lastPrice = "0.6012",highPrice="0",lowPrice="0",priceChange="0",priceChangePercent = "0",openTime=1634196690912,volume="0",quoteVolume="0"},
                new PriceChangeInformation { symbol = "BNBBTC",  lastPrice = "0.6013",highPrice="0",lowPrice="0",priceChange="0",priceChangePercent = "0",openTime=1634196690912,volume="0",quoteVolume="0"},
                new PriceChangeInformation { symbol = "NEOBTC",  lastPrice = "0.6014",highPrice="0",lowPrice="0",priceChange="0",priceChangePercent = "0",openTime=1634196690912,volume="0",quoteVolume="0"},
                new PriceChangeInformation { symbol = "QTUMETH",  lastPrice = "0.6015",highPrice="0",lowPrice="0",priceChange="0",priceChangePercent = "0",openTime=1634196690912,volume="0",quoteVolume="0"}
            };

            stocks.ForEach(stock => _stocks.TryAdd(stock.symbol, stock));
        }

        private async void UpdateStockPrices(object state)
        {
            // This function must be re-entrant as it's running as a timer interval handler
            await _updateStockPricesLock.WaitAsync();
            try
            {
                if (!_updatingStockPrices)
                {
                    _updatingStockPrices = true;
                    var informationCoin = GetInformationCoin();
                    foreach (var stock in _stocks.Values)
                    {
                        var stockRemote = informationCoin.Where(x => x.symbol == stock.symbol).FirstOrDefault();
                        TryUpdateStockPrice(stock, stockRemote);

                        _subject.OnNext(stock);
                    }

                    _updatingStockPrices = false;
                }
            }
            finally
            {
                _updateStockPricesLock.Release();
            }
        }

        private bool TryUpdateStockPrice(PriceChangeInformation stock, PriceChangeInformation stockRemote)
        {
            stock.lastPrice = stockRemote.lastPrice;
            stock.priceChange = stockRemote.priceChange;
            stock.lastPrice = stockRemote.lastPrice;
            stock.highPrice = stockRemote.highPrice;
            stock.lowPrice = stockRemote.lowPrice;
            stock.priceChangePercent = stockRemote.priceChangePercent;
            stock.lastPrice = stockRemote.lastPrice;
            stock.quoteVolume = stockRemote.quoteVolume;
            stock.volume = stockRemote.volume;
            _subject.OnNext(stock);
            return true;

        }
        private List<PriceChangeInformation> GetInformationCoin()
        {
            var client = new RestClient("https://api.binance.com/api/v3/ticker/24hr");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Content-Type", "application/json");
            IRestResponse response = client.Execute(request);
            var result = JsonSerializer.Deserialize<List<PriceChangeInformation>>(response.Content);
            return result;
        }

        private async Task BroadcastMarketStateChange(MarketState marketState)
        {
            switch (marketState)
            {
                case MarketState.Open:
                    await Hub.Clients.All.SendAsync("marketOpened");
                    break;
                case MarketState.Closed:
                    await Hub.Clients.All.SendAsync("marketClosed");
                    break;
                default:
                    break;
            }
        }

        private async Task BroadcastMarketReset()
        {
            await Hub.Clients.All.SendAsync("marketReset");
        }
    }

    public enum MarketState
    {
        Closed,
        Open
    }
}