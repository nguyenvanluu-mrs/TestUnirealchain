// Crockford's supplant method (poor man's templating)
if (!String.prototype.supplant) {
    String.prototype.supplant = function (o) {
        return this.replace(/{([^{}]*)}/g,
            function (a, b) {
                var r = o[b];
                return typeof r === 'string' || typeof r === 'number' ? r : a;
            }
        );
    };
}

var stockTable = document.getElementById('stockTable');
var stockTableBody = stockTable.getElementsByTagName('tbody')[0];
var rowTemplate = '<td>{symbol}</td><td>{lastPrice}</td><td>{openTime}</td><td>{highPrice}</td><td>{lowPrice}</td><td class="changeValue"><span class="dir {directionClass}">{direction}</span> {priceChange}</td><td>{priceChangePercent}</td><td>{volume}</td><td>{quoteVolume}</td>';
var tickerTemplate = '<span class="symbol">{symbol}</span> <span class="price">{lastPrice}</span> <span class="changeValue"><span class="dir {directionClass}">{direction}</span> {priceChange} ({priceChangePercent})</span>';
var stockTicker = document.getElementById('stockTicker');
var stockTickerBody = stockTicker.getElementsByTagName('ul')[0];
var up = '▲';
var down = '▼';

let connection = new signalR.HubConnectionBuilder()
    .withUrl("/stocks")
    .build();

connection.start().then(function () {
    connection.invoke("GetAllStocks").then(function (stocks) {
        for (let i = 0; i < stocks.length; i++) {
            displayStock(stocks[i]);
        }
    });

    connection.invoke("GetMarketState").then(function (state) {
        if (state === 'Open') {
            marketOpened();
            startStreaming();
        } else {
            marketClosed();
        }
    });

    document.getElementById('open').onclick = function () {
        connection.invoke("OpenMarket");
    }

    document.getElementById('close').onclick = function () {
        connection.invoke("CloseMarket");
    }

    document.getElementById('reset').onclick = function () {
        connection.invoke("Reset").then(function () {
            connection.invoke("GetAllStocks").then(function (stocks) {
                for (let i = 0; i < stocks.length; ++i) {
                    displayStock(stocks[i]);
                }
            });
        });
    }
});

connection.on("marketOpened", function () {
    marketOpened();
    startStreaming();
});

connection.on("marketClosed", function () {
    marketClosed();
});

function startStreaming() {
    connection.stream("StreamStocks").subscribe({
        close: false,
        next: displayStock,
        error: function (err) {
            logger.log(err);
        }
    });
}

var pos = 30;
var tickerInterval;
stockTickerBody.style.marginLeft = '30px';

function moveTicker() {
    pos--;
    if (pos < -600) {
        pos = 500;
    }

    stockTickerBody.style.marginLeft = pos + 'px';
}

function marketOpened() {
    tickerInterval = setInterval(moveTicker, 20);
    document.getElementById('open').setAttribute("disabled", "disabled");
    document.getElementById('close').removeAttribute("disabled");
    document.getElementById('reset').setAttribute("disabled", "disabled");
}

function marketClosed() {
    if (tickerInterval) {
        clearInterval(tickerInterval);
    }
    document.getElementById('open').removeAttribute("disabled");
    document.getElementById('close').setAttribute("disabled", "disabled");
    document.getElementById('reset').removeAttribute("disabled");
}

function displayStock(stock) {
    var displayStock = formatStock(stock);
    addOrReplaceStock(stockTableBody, displayStock, 'tr', rowTemplate);
    addOrReplaceStock(stockTickerBody, displayStock, 'li', tickerTemplate);
}

function addOrReplaceStock(table, stock, type, template) {
    var child = createStockNode(stock, type, template);

    // try to replace
    var stockNode = document.querySelector(type + "[data-symbol=" + stock.symbol + "]");
    if (stockNode) {
        var change = stockNode.querySelector(".changeValue");
        var prevChange = parseFloat(change.childNodes[1].data);
        if (prevChange > Number(stock.priceChange)) {
            child.className = "decrease";
        }
        else if (prevChange < Number(stock.priceChange)) {
            child.className = "increase";
        }
        else {
            return;
        }
        table.replaceChild(child, stockNode);
    } else {
        // add new stock
        table.appendChild(child);
    }
}

function formatStock(stock) {
    //stock.lastPrice = stock.lastPrice.toFixed(2);
    stock.priceChangePercent = Number(stock.priceChangePercent).toFixed(2) + '%';
    stock.direction = stock.priceChange == "0" ? '' : Number(stock.priceChange) >= 0 ? up : down;
    stock.directionClass = stock.priceChange == "0" ? 'even' : Number(stock.priceChange) >= 0 ? 'up' : 'down';
    stock.openTime = Convertfromtimestamptodate(stock.openTime);
    return stock;
}

function createStockNode(stock, type, template) {
    var child = document.createElement(type);
    child.setAttribute('data-symbol', stock.symbol);
    child.setAttribute('class', stock.symbol);
    child.innerHTML = template.supplant(stock);
    return child;
}

function Convertfromtimestamptodate(timestamp) {
    var date = new Date(timestamp * 1000);
    // Hours part from the timestamp
    var hours = date.getHours();
    // Minutes part from the timestamp
    var minutes = "0" + date.getMinutes();
    // Seconds part from the timestamp
    var seconds = "0" + date.getSeconds();

    // Will display time in 10:30:23 format
    var formattedTime = hours + ':' + minutes.substr(-2) + ':' + seconds.substr(-2);
    return formattedTime;
}