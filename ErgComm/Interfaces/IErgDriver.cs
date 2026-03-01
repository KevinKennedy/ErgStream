using ErgComm.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ErgComm.Interfaces
{
    /// <summary>
    /// Interface for communicating with an ergometer device.
    /// </summary>
    public interface IErgDriver
    {
        /// <summary>
        /// Connects to the erg and starts streaming data.
        /// </summary>
        /// <param name="ergId">The unique identifier of the erg to connect to.</param>
        /// <param name="dataCallback">Callback invoked when new data is available.</param>
        /// <param name="cancellationToken">Token to cancel the streaming.</param>
        Task ConnectAndStreamAsync(string ergId, Action<ErgData> dataCallback, CancellationToken cancellationToken);

        /// <summary>
        /// Discovers available ergs.
        /// </summary>
        /// <param name="ergListCallback">Callback invoked with updated list of discovered ergs.</param>
        /// <param name="cancellationToken">Token to cancel the discovery process.</param>
        Task StartDiscoveryAsync(Action<List<ErgInfo>> ergListCallback, CancellationToken cancellationToken);
    }
}