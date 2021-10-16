using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace StockTickR.Hubs
{
    public class BinanceTickerHub : Hub
    {
        private readonly BinanceTicker _stockTicker;

        public BinanceTickerHub(BinanceTicker stockTicker)
        {
            _stockTicker = stockTicker;
        }

        public IEnumerable<PriceChangeInformation> GetAllStocks()
        {
            return _stockTicker.GetAllStocks();
        }

        public ChannelReader<PriceChangeInformation> StreamStocks()
        {
            return _stockTicker.StreamStocks().AsChannelReader(10);
        }

        public string GetMarketState()
        {
            return _stockTicker.MarketState.ToString();
        }

        public async Task OpenMarket()
        {
            await _stockTicker.OpenMarket();
        }

        public async Task CloseMarket()
        {
            await _stockTicker.CloseMarket();
        }

        public async Task Reset()
        {
            await _stockTicker.Reset();
        }
    }
}