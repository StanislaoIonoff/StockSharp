namespace StockSharp.BitStamp
{
	using System;
	using System.Linq;

	using Ecng.Collections;
	using Ecng.Common;

	using MoreLinq;

	using StockSharp.BitStamp.Native;
	using StockSharp.Messages;

	partial class BitStampMessageAdapter
	{
		private readonly CachedSynchronizedDictionary<SecurityId, int> _subscribedDepths = new CachedSynchronizedDictionary<SecurityId, int>();
		private readonly CachedSynchronizedSet<SecurityId> _subscribedTicks = new CachedSynchronizedSet<SecurityId>();

		private void SessionOnNewTrade(Trade trade)
		{
			SendOutMessage(new ExecutionMessage
			{
				ExecutionType = ExecutionTypes.Tick,
				SecurityId = _btcUsd,
				TradeId = trade.Id,
				TradePrice = (decimal)trade.Price,
				Volume = (decimal)trade.Amount,
				ServerTime = CurrentTime.Convert(TimeZoneInfo.Utc),
			});
		}

		private void SessionOnNewOrderBook(OrderBook book)
		{
			SendOutMessage(new QuoteChangeMessage
			{
				SecurityId = _btcUsd,
				Bids = book.Bids.Select(b => b.ToStockSharp(Sides.Buy)),
				Asks = book.Asks.Select(b => b.ToStockSharp(Sides.Sell)),
				ServerTime = CurrentTime.Convert(TimeZoneInfo.Utc),
			});
		}

		private void ProcessMarketData(MarketDataMessage mdMsg)
		{
			switch (mdMsg.DataType)
			{
				case MarketDataTypes.Level1:
				{
					//if (mdMsg.IsSubscribe)
					//	_subscribedLevel1.Add(secCode);
					//else
					//	_subscribedLevel1.Remove(secCode);

					break;
				}
				case MarketDataTypes.MarketDepth:
				{
					if (mdMsg.IsSubscribe)
					{
						_subscribedDepths.Add(mdMsg.SecurityId, mdMsg.MaxDepth);

						if (_subscribedDepths.Count == 1)
							_pusherClient.SubscribeDepths();
					}
					else
					{
						_subscribedDepths.Remove(mdMsg.SecurityId);

						if (_subscribedDepths.Count == 0)
							_pusherClient.UnSubscribeDepths();
					}

					break;
				}
				case MarketDataTypes.Trades:
				{
					if (mdMsg.IsSubscribe)
					{
						if (mdMsg.From == DateTime.Today)
						{
							_httpClient.RequestTransactions().Select(t => new ExecutionMessage
							{
								ExecutionType = ExecutionTypes.Tick,
								SecurityId = _btcUsd,
								TradeId = t.Id,
								TradePrice = (decimal)t.Price,
								Volume = (decimal)t.Amount,
								ServerTime = t.Time.ApplyTimeZone(TimeZoneInfo.Utc)
							}).ForEach(SendOutMessage);
						}

						_subscribedTicks.Add(mdMsg.SecurityId);

						if (_subscribedTicks.Count == 1)
							_pusherClient.SubscribeTrades();
					}
					else
					{
						_subscribedTicks.Remove(mdMsg.SecurityId);

						if (_subscribedTicks.Count == 0)
							_pusherClient.UnSubscribeTrades();
					}

					break;
				}
				default:
				{
					SendOutMarketDataNotSupported(mdMsg.TransactionId);
					return;
				}
			}

			var reply = (MarketDataMessage)mdMsg.Clone();
			reply.OriginalTransactionId = mdMsg.OriginalTransactionId;
			SendOutMessage(reply);
		}

		private void ProcessSecurityLookup(SecurityLookupMessage message)
		{
			SendOutMessage(new SecurityMessage
			{
				OriginalTransactionId = message.TransactionId,
				SecurityId = _btcUsd,
				SecurityType = SecurityTypes.CryptoCurrency,
				VolumeStep = 0.00000001m,
				PriceStep = 0.01m,
			});

			SendOutMessage(new SecurityMessage
			{
				OriginalTransactionId = message.TransactionId,
				SecurityId = _eurUsd,
				SecurityType = SecurityTypes.Currency,
				PriceStep = 0.0001m,
			});

			SendOutMessage(new SecurityLookupResultMessage { OriginalTransactionId = message.TransactionId });
		}
	}
}