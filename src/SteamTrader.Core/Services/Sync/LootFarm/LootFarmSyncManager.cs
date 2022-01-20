﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SteamTrader.Core.Configuration;
using SteamTrader.Core.Services.ApiClients.DMarket;
using SteamTrader.Core.Services.ApiClients.LootFarm;

namespace SteamTrader.Core.Services.Sync.LootFarm
{
    public class LootFarmSyncManager
    {
        public bool IsSyncingNow { get; private set; }

        private readonly ILogger<LootFarmSyncManager> _logger;
        private readonly IDMarketApiClient _dMarketApiClient;
        private readonly ILootFarmApiClient _lootFarmApiClient;
        private readonly Settings _settings;

        public LootFarmSyncManager(ILogger<LootFarmSyncManager> logger, 
            IDMarketApiClient dMarketApiClient, 
            ILootFarmApiClient lootFarmApiClient, 
            IOptions<Settings> settings)
        {
            _logger = logger;
            _dMarketApiClient = dMarketApiClient;
            _lootFarmApiClient = lootFarmApiClient;
            _settings = settings.Value;
        }

        public async Task SyncForBuyFromLootFarmToSaleOnDMarket()
        {
            if (IsSyncingNow)
            {
                _logger.LogWarning("{0}: Синхронизация пропущена, так как сервис занят",
                    nameof(LootFarmSyncManager));
                return;
            }
            
            IsSyncingNow = true;
            
            try
            {
                foreach (var gameName in _settings.LootFarmSettings.LootFarmToDMarketSyncingGames)
                {
                    _logger.LogInformation("{0}: Начинаю синхронизацию по выводу из LootFarm-a на DMarket, игра {1}",
                        nameof(LootFarmSyncManager), gameName);
                    _logger.BeginScope("{0}: Начинаю синхронизацию по игре {1}",
                        nameof(LootFarmSyncManager), gameName);
                    
                    var items = gameName switch
                    {
                        "a8db" => await _lootFarmApiClient.GetPricesForCsGo(),
                        "tf2" => await _lootFarmApiClient.GetPricesForTf2(),
                        "9a92" => await _lootFarmApiClient.GetPricesForDota2(),
                        _ => throw new NotSupportedException($"Указан не поддерживаемый тип игры для синхронизации в сервисе {nameof(LootFarmSyncManager)}")
                    };
                    
                    _logger.LogInformation("{0}: Найдено айтемов на LootFarm {1}, начинаю сравнение с DMarket-ом",
                        nameof(LootFarmSyncManager), items.Length);

                    var countHandledItems = 0;
                    
                    using var semaphore = new SemaphoreSlim(10);
                    var tasks = items.Select(async x =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var dMarketDetails = await _dMarketApiClient.GetLastSales(gameName, x.Name);
                            var countLastSales = dMarketDetails.LastSales
                                .Count(i => DateTimeOffset.FromUnixTimeSeconds(i.Date) > DateTime.Today.AddDays(-2));

                            if (countLastSales >= _settings.DMarketSettings.NeededQtySalesForTwoDays)
                            {
                                var middleSalePrice = (long) dMarketDetails.LastSales
                                    .Take(_settings.DMarketSettings.NeededQtySalesForTwoDays)
                                    .Average(i => i.Price.Amount);
                                var lastSalePrice = dMarketDetails.LastSales.First().Price.Amount;

                                var targetPrice = Math.Min(middleSalePrice, lastSalePrice);

                                var profit = targetPrice * (1 - _settings.DMarketSettings.SaleCommissionPercent / 100) -
                                             x.Price;
                                var margin = profit / x.Price;

                                if (margin >= _settings.LootFarmSettings.TargetMarginPercentForSaleOnDMarket / 100)
                                {
                                    _logger.LogWarning(
                                        "{0}: Потенциальная покупка с LootFarm-a и продажи на DMarket-e, lootFarmPrice : {1}, dmarketPrice: {2}, margin: {3}, name: {4}",
                                        nameof(LootFarmSyncManager), (decimal) x.Price / 100,
                                        (decimal) targetPrice / 100, margin, x.Name);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "{0}: Возникла ошибка во время синка игры {1}",
                                nameof(LootFarmSyncManager), gameName);
                        }
                        finally
                        {
                            semaphore.Release();
                            
                            countHandledItems++;
                            
                            if (countHandledItems % 100 == 0)
                                _logger.LogInformation("{0}: Обработано {1} айтемов из {2}",
                                    nameof(LootFarmSyncManager), countHandledItems, items.Length);
                        }
                    });

                    await Task.WhenAll(tasks);
                    _logger.LogInformation("{0}: Синхронизация айтемов с DMarket-ом по игре {1} завершена",
                        nameof(LootFarmSyncManager), gameName);

                }
            }
            finally
            {
                IsSyncingNow = false;
            }
        }
    }
}