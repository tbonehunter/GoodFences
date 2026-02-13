// Handlers/ShippingHandler.cs
using StardewModdingAPI;
using StardewValley;
using GoodFences.Models;
using System.Linq;

namespace GoodFences.Handlers
{
    /// <summary>Handles shipping revenue adjustments for landlord mode.</summary>
    public class ShippingHandler
    {
        /*********
        ** Fields
        *********/
        private readonly IMonitor Monitor;
        private readonly QuadrantManager QuadrantManager;

        /*********
        ** Constructor
        *********/
        public ShippingHandler(IMonitor monitor, QuadrantManager quadrantManager)
        {
            this.Monitor = monitor;
            this.QuadrantManager = quadrantManager;
        }

        /*********
        ** Public Methods
        *********/
        /// <summary>Process daily shipping and apply landlord cut if applicable.</summary>
        public void ProcessDailyShipping()
        {
            // Only apply in Landlord mode
            if (!this.QuadrantManager.IsLandlordMode())
                return;

            // Only host processes this
            if (!Context.IsMainPlayer)
                return;

            var host = Game1.MasterPlayer;
            if (host == null)
                return;

            int cutPercent = ModEntry.Config?.LandlordCutPercent ?? 10;
            int totalCut = 0;

            // Process each farmhand's shipping bin contributions
            foreach (var farmer in Game1.getAllFarmers())
            {
                // Skip the host - they don't pay themselves
                if (farmer.IsMainPlayer)
                    continue;

                // Get this farmer's shipping revenue for the day
                // Note: In Stardew, the shipping bin items are in Farm.getShippingBin()
                // and tracked per-farmer when using separate wallets
                int farmerRevenue = this.GetFarmerDailyShippingRevenue(farmer);
                
                if (farmerRevenue <= 0)
                    continue;

                // Calculate landlord cut
                int cut = (farmerRevenue * cutPercent) / 100;
                if (cut <= 0)
                    continue;

                // Transfer the cut from farmhand to host
                // This happens before the day officially ends, so we adjust money directly
                this.TransferLandlordCut(farmer, host, cut);
                totalCut += cut;

                if (ModEntry.Config?.DebugMode == true)
                {
                    this.Monitor.Log($"Landlord cut: {farmer.Name} pays {cut}g ({cutPercent}% of {farmerRevenue}g)", LogLevel.Debug);
                }
            }

            if (totalCut > 0)
            {
                this.Monitor.Log($"Total landlord revenue: {totalCut}g from {cutPercent}% cut", LogLevel.Info);
                Game1.addHUDMessage(new HUDMessage($"Landlord income: {totalCut}g", HUDMessage.newQuest_type));
            }
        }

        /*********
        ** Private Methods
        *********/
        /// <summary>Get the total shipping revenue for a farmer today.</summary>
        /// <remarks>
        /// In multiplayer with separate wallets, each farmer's items in the shipping bin
        /// are tracked. However, the actual revenue calculation happens at end of day
        /// in a way that's hard to intercept cleanly. 
        /// 
        /// This implementation estimates based on items currently in the shipping bin
        /// that belong to this farmer.
        /// </remarks>
        private int GetFarmerDailyShippingRevenue(Farmer farmer)
        {
            var farm = Game1.getFarm();
            if (farm == null)
                return 0;

            // Get items in the shipping bin
            // In multiplayer, we need to track who added what
            // For now, estimate based on total bin value divided by contributors
            // A more accurate implementation would track deposits as they happen
            
            int totalValue = 0;
            foreach (var item in farm.getShippingBin(farmer))
            {
                totalValue += Utility.getSellToStorePriceOfItem(item) * item.Stack;
            }

            return totalValue;
        }

        /// <summary>Transfer the landlord cut from farmhand to host.</summary>
        private void TransferLandlordCut(Farmer fromFarmer, Farmer toHost, int amount)
        {
            // The shipping revenue hasn't been added to wallets yet at DayEnding
            // We need to adjust after the game processes shipping
            // 
            // Strategy: We can't easily intercept the shipping calculation,
            // so instead we do a direct money transfer at end of day.
            // The farmhand loses 'amount' and the host gains 'amount'.
            //
            // Note: This happens after items are sold but before day fully ends.
            // The farmhand may briefly see full revenue before the cut is applied.

            if (fromFarmer.Money >= amount)
            {
                fromFarmer.Money -= amount;
                toHost.Money += amount;
            }
            else
            {
                // Farmhand doesn't have enough money (edge case - new player)
                // Take what they have
                int available = fromFarmer.Money;
                if (available > 0)
                {
                    fromFarmer.Money = 0;
                    toHost.Money += available;
                    this.Monitor.Log($"Farmhand {fromFarmer.Name} only had {available}g for landlord cut (expected {amount}g)", LogLevel.Debug);
                }
            }
        }
    }
}
