﻿using System;
using System.Collections.Generic;

namespace SteamTrader.Core.Configuration
{
    public class Settings
    {
        public DMarketSettings DMarketSettings { get; set; }
        public LootFarmSettings LootFarmSettings { get; set; }
        public ProxyConfigItem[] Proxies { get; set; }
        public TimeSpan ProxyLimitTime { get; set; }
        public TimeSpan HttpTimeout { get; set; }

        public decimal SteamCommissionPercent { get; set; }
        public decimal TargetDMarketToSteamProfitPercent { get; set; }
        public Dictionary<string, TimeSpan> ProxyLockTime { get; set; }

    }

    public class DMarketSettings
    {
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
        public long MaxTradeBan { get; set; }
        public string[] BuyGameIds { get; set; }
        public double SaleCommissionPercent { get; set; }
        public int NeededQtySalesForTwoDays { get; set; }
    }

    public class ProxyConfigItem
    {
        public string Ip { get; set; }
        public int Port { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
    }
    public class LootFarmSettings
    {
        public string[] LootFarmToDMarketSyncingGames { get; set; }
        public double SaleCommissionPercent { get; set; }
        public double TargetMarginPercentForSaleOnDMarket { get; set; }
    }
}