using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CsharpClient
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/stocks")
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                })
                .AddMessagePackProtocol()
                .Build();

            await connection.StartAsync();

            Console.WriteLine("Starting connection. Press Ctrl-C to close.");
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, a) =>
            {
                a.Cancel = true;
                cts.Cancel();
            };

            connection.Closed += e =>
            {
                Console.WriteLine("Connection closed with error: {0}", e);

                cts.Cancel();
                return Task.CompletedTask;
            };


            connection.On("marketOpened", () =>
            {
                Console.WriteLine("Market opened");
            });

            connection.On("marketClosed", () =>
            {
                Console.WriteLine("Market closed");
            });

            connection.On("marketReset", () =>
            {
                // We don't care if the market rest
            });

            var channel = await connection.StreamAsChannelAsync<PriceChangeInformation>("StreamStocks", CancellationToken.None);
            while (await channel.WaitToReadAsync() && !cts.IsCancellationRequested)
            {
                while (channel.TryRead(out var stock))
                {
                    Console.WriteLine($"{stock.symbol} {stock.lastPrice}");
                }
            }
        }
    }

    public class PriceChangeInformation
    {
        public string symbol { get; set; }
        public string priceChange { get; set; }
        public string priceChangePercent { get; set; }
        public string weightedAvgPrice { get; set; }
        public string prevClosePrice { get; set; }
        public string lastPrice { get; set; }
        public string lastQty { get; set; }
        public string bidPrice { get; set; }
        public string bidQty { get; set; }
        public string askPrice { get; set; }
        public string askQty { get; set; }
        public string openPrice { get; set; }
        public string highPrice { get; set; }
        public string lowPrice { get; set; }
        public string volume { get; set; }
        public string quoteVolume { get; set; }
        public long openTime { get; set; }
        public long closeTime { get; set; }
        public int firstId { get; set; }
        public int lastId { get; set; }
        public int count { get; set; }
    }
}
