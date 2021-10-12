using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RestSharp;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace TestUrealchain.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BinanceCoinController : ControllerBase
    {

        private readonly ILogger<BinanceCoinController> _logger;
        public IConfiguration Configuration { get; }

        public BinanceCoinController(ILogger<BinanceCoinController> logger, IConfiguration configuration)
        {
            Configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Get Information Coin
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("GetInformationCoin")]
        public async Task<IActionResult> GetInformationCoin()
        {
            _logger.LogInformation("Prepare parameter");
            var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var parmeterRequest = "timestamp=" + timestamp + Configuration["BinanceConfig:recvWindow"];
            var signature = Signature(parmeterRequest, Configuration["BinanceConfig:secretKey"]);
            string url = Configuration["BinanceConfig:binanceUrl"] + "?timestamp=" + timestamp + Configuration["BinanceConfig:recvWindow"] + "&signature=" + signature;

            _logger.LogInformation("Initial parameter and request");
            var client = new RestClient(url);
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Configuration["BinanceConfig:secretName"], Configuration["BinanceConfig:apiKey"]);

            _logger.LogInformation("Send request");
            IRestResponse response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Not get information");
            }
            else
            {
                if (!string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogInformation("Get result");
                    var result = JsonSerializer.Deserialize<object>(response.Content);

                    return Ok(result);
                }
                else
                {
                    return NotFound();
                }
            }

        }
        private string Signature(string message, string secret)
        {
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] keyBytes = encoding.GetBytes(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            System.Security.Cryptography.HMACSHA256 cryptographer = new System.Security.Cryptography.HMACSHA256(keyBytes);

            byte[] bytes = cryptographer.ComputeHash(messageBytes);

            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

    }
}
