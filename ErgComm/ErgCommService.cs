using ErgComm.Drivers;
using ErgComm.Interfaces;
using ErgComm.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ErgComm
{
    /// <summary>
    /// Main service for discovering and communicating with ergometer devices.
    /// </summary>
    public class ErgCommService
    {
        private readonly List<IErgDriver> _deviceDrivers = new();

        public ErgCommService()
        {
            // Register available device drivers
            _deviceDrivers.Add(new Concept2RowerDriver());
            _deviceDrivers.Add(new MockErgDriver());
        }

        /// <summary>
        /// Starts discovering available ergometers in the area.
        /// </summary>
        /// <param name="ergListCallback">Callback invoked with the updated list of discovered ergs, sorted by signal strength.</param>
        /// <param name="cancellationToken">Token to cancel the discovery process.</param>
        /// <param name="includeMockErg">Whether to include a mock erg in the results for testing.</param>
        public async Task StartFindErgsAsync(
            Action<List<ErgInfo>> ergListCallback, 
            CancellationToken cancellationToken,
            bool includeMockErg = false)
        {
            var allErgs = new List<ErgInfo>();
            var ergUpdateLock = new object();

            // Create tasks for each device driver
            var discoveryTasks = new List<Task>();

            foreach (var driver in _deviceDrivers)
            {
                // Skip mock erg driver if not requested
                if (driver is MockErgDriver && !includeMockErg)
                    continue;

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await driver.StartDiscoveryAsync(ergs =>
                        {
                            lock (ergUpdateLock)
                            {
                                // Remove old entries from this driver and add new ones
                                allErgs.RemoveAll(e => ergs.Any(ne => ne.Id == e.Id));
                                allErgs.AddRange(ergs);

                                // Sort by signal strength (descending)
                                var sortedErgs = allErgs.OrderByDescending(e => e.SignalStrength).ToList();
                                
                                ergListCallback(sortedErgs);
                            }
                        }, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelled
                    }
                    catch (NotImplementedException)
                    {
                        // Driver not yet implemented - skip silently
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't crash discovery process
                        System.Diagnostics.Debug.WriteLine($"Error in device discovery: {ex.Message}");
                    }
                }, cancellationToken);

                discoveryTasks.Add(task);
            }

            await Task.WhenAll(discoveryTasks);
        }

        /// <summary>
        /// Connects to a specific ergometer and starts streaming data.
        /// </summary>
        /// <param name="ergId">The unique identifier of the erg to connect to.</param>
        /// <param name="statusDataCallback">Callback invoked when new status data is available from the erg.</param>
        /// <param name="strokeDataCallback">Callback invoked when new stroke data is available from the erg.</param>
        /// <param name="cancellationToken">Token to cancel the connection and stop streaming data.</param>
        public async Task ConnectToErgAsync(
            string ergId, 
            Action<ErgStatus> statusDataCallback,
            Action<StrokeData> strokeDataCallback, 
            CancellationToken cancellationToken)
        {
            // Determine which driver to use based on ergId
            IErgDriver? targetDriver = null;

            if (ergId.StartsWith("MOCK-ERG"))
            {
                targetDriver = _deviceDrivers.OfType<MockErgDriver>().FirstOrDefault();
            }
            else
            {
                // For now, assume Concept2 Rower for real devices
                // In the future, you could add logic to determine device type from ID
                targetDriver = _deviceDrivers.OfType<Concept2RowerDriver>().FirstOrDefault();
            }

            if (targetDriver == null)
            {
                throw new InvalidOperationException($"No suitable driver found for erg ID: {ergId}");
            }

            await targetDriver.ConnectAndStreamAsync(ergId, statusDataCallback, strokeDataCallback, cancellationToken);
        }
    }
}