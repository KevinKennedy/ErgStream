using ErgComm.Interfaces;
using ErgComm.Models;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ErgComm.Drivers
{
    /// <summary>
    /// Concept2 Rower BLE communication implementation.
    /// </summary>
    public class Concept2RowerDriver : IErgDriver
    {
        // Concept2 PM5 BLE Service UUIDs (from Concept2 BLE Interface Definition)
        private static readonly Guid PM5ServiceUuid = new("CE060030-43E5-11E4-916C-0800200C9A66");
        private static readonly Guid PM5ServiceUuidInfo = new("CE060020-43E5-11E4-916C-0800200C9A66");
        
        // PM Data Characteristics
        private static readonly Guid PM5CharacteristicGeneralStatus = new("CE060031-43E5-11E4-916C-0800200C9A66");
        private static readonly Guid PM5CharacteristicAdditionalStatus = new("CE060032-43E5-11E4-916C-0800200C9A66");
        private static readonly Guid PM5CharacteristicAdditionalStatus2 = new("CE060033-43E5-11E4-916C-0800200C9A66");
        private static readonly Guid PM5CharacteristicStrokeData = new("CE060035-43E5-11E4-916C-0800200C9A66");
        private static readonly Guid PM5CharacteristicAdditionalStrokeData = new("CE060036-43E5-11E4-916C-0800200C9A66");
        private static readonly Guid PM5CharacteristicForceCurveData = new("CE06003D-43E5-11E4-916C-0800200C9A66");

        private readonly IBluetoothLE _bluetoothLE;
        private readonly IAdapter _adapter;
        private IDevice? _connectedDevice;

        public Concept2RowerDriver()
        {
            _bluetoothLE = CrossBluetoothLE.Current;
            _adapter = CrossBluetoothLE.Current.Adapter;
        }

        public async Task StartDiscoveryAsync(Action<List<ErgInfo>> ergListCallback, CancellationToken cancellationToken)
        {
            if (!_bluetoothLE.IsAvailable)
            {
                throw new InvalidOperationException("Bluetooth is not available on this device");
            }

            if (!_bluetoothLE.IsOn)
            {
                throw new InvalidOperationException("Bluetooth is not enabled. Please enable Bluetooth and try again.");
            }

            var discoveredErgs = new Dictionary<string, ErgInfo>();
            var updateTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            _adapter.DeviceDiscovered += async (s, e) =>
            {
                IDevice device = e.Device;

                // Check if device advertises Concept2 PM service
                if (device.Name?.Contains("PM5", StringComparison.OrdinalIgnoreCase) == true ||
                    device.Name?.Contains("Concept2", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // TODO: We may be able to use IDevice.AdvertisementRecords here.
                    IService pm5Service = await device.GetServiceAsync(PM5ServiceUuid);

                    if(pm5Service == null)
                    {
                        // Not a PM5 device
                        return;
                    }

                    var ergInfo = new ErgInfo
                    {
                        Id = device.Id.ToString(),
                        Name = device.Name ?? "Concept2 PM5",
                        SignalStrength = device.Rssi,
                        ErgType = ErgType.Concept2Rower,
                        IsMockErg = false
                    };

                    lock (discoveredErgs)
                    {
                        discoveredErgs[ergInfo.Id] = ergInfo;
                    }
                }
            };

            // Start scanning for devices
            var scanTask = Task.Run(async () =>
            {
                try
                {
                    await _adapter.StartScanningForDevicesAsync(
                        //serviceUuids: new[] { PM5ServiceUuid },
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelled
                }
                finally
                {
                    await _adapter.StopScanningForDevicesAsync();
                }
            }, cancellationToken);

            // Periodically update callback with discovered devices
            var updateTask = Task.Run(async () =>
            {
                try
                {
                    while (await updateTimer.WaitForNextTickAsync(cancellationToken))
                    {
                        List<ErgInfo> ergList;
                        lock (discoveredErgs)
                        {
                            ergList = discoveredErgs.Values
                                .OrderByDescending(e => e.SignalStrength)
                                .ToList();
                        }

                        if (ergList.Any())
                        {
                            ergListCallback(ergList);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelled
                }
            }, cancellationToken);

            await Task.WhenAll(scanTask, updateTask);
        }

        public async Task ConnectAndStreamAsync(string ergId, Action<ErgStatus> statusDataCallback, Action<StrokeData> strokeDataCallback, CancellationToken cancellationToken)
        {
            try
            {
                // Find the device
                var deviceGuid = Guid.Parse(ergId);
                var device = await _adapter.ConnectToKnownDeviceAsync(deviceGuid, cancellationToken: cancellationToken);
                _connectedDevice = device;

                // Discover services
                var services = await device.GetServicesAsync(cancellationToken);
                var pmService = services.FirstOrDefault(s => s.Id == PM5ServiceUuid);
                
                if (pmService == null)
                {
                    throw new InvalidOperationException("Concept2 PM service not found on device");
                }

                // Get characteristics
                var characteristics = await pmService.GetCharacteristicsAsync();

                var generalStatusCharacteristic = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicGeneralStatus);
                var additionalStatusCharacteristic = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicAdditionalStatus);
                var strokeDataCharacteristic = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicStrokeData);
                var additionalStrokeDataCharacteristic = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicAdditionalStrokeData);
                var forceCurveCharacteristic = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicForceCurveData);

                Concept2ErgDataAssembler ergDataAssembler = new();

                // Subscribe to Rowing General Status (primary data source)
                if (generalStatusCharacteristic != null)
                {
                    generalStatusCharacteristic.ValueUpdated += (s, e) =>
                    {
                        LogBleData("GeneralStatus", e.Characteristic.Value);
                        ergDataAssembler.OnGeneralStatusMessage(e.Characteristic.Value);
                        if (ergDataAssembler.TryGetUpdatedStatus() is ErgStatus status)
                        {
                            statusDataCallback(status);
                        }
                    };
                    await generalStatusCharacteristic.StartUpdatesAsync(cancellationToken);
                }

                // Subscribe to Additional Status (workout type, drag factor)
                if (additionalStatusCharacteristic != null)
                {
                    additionalStatusCharacteristic.ValueUpdated += (s, e) =>
                    {
                        LogBleData("AdditionalStatus", e.Characteristic.Value);
                        ergDataAssembler.OnAdditionalStatusMessage(e.Characteristic.Value);
                        if (ergDataAssembler.TryGetUpdatedStatus() is ErgStatus status)
                        {
                            statusDataCallback(status);
                        }
                    };
                    await additionalStatusCharacteristic.StartUpdatesAsync(cancellationToken);
                }

                // Subscribe to Stroke Data (stroke-end events)
                if (strokeDataCharacteristic != null)
                {
                    strokeDataCharacteristic.ValueUpdated += (s, e) =>
                    {
                        LogBleData("StrokeData", e.Characteristic.Value);
                        ergDataAssembler.OnStrokeDataMessage(e.Characteristic.Value);
                        if (ergDataAssembler.TryGetUpdatedStroke() is StrokeData stroke)
                        {
                            strokeDataCallback(stroke);
                        }
                    };
                    await strokeDataCharacteristic.StartUpdatesAsync(cancellationToken);
                }

                // Subscribe to Additional Stroke Data (more stroke details)
                if (additionalStrokeDataCharacteristic != null)
                {
                    additionalStrokeDataCharacteristic.ValueUpdated += (s, e) =>
                    {
                        LogBleData("AdditionalStrokeData", e.Characteristic.Value);
                        ergDataAssembler.OnAdditionalStrokeDataMessage(e.Characteristic.Value);
                        if (ergDataAssembler.TryGetUpdatedStroke() is StrokeData stroke)
                        {
                            strokeDataCallback(stroke);
                        }
                    };
                    await additionalStrokeDataCharacteristic.StartUpdatesAsync(cancellationToken);
                }

                // Subscribe to Force Curve Data
                if (forceCurveCharacteristic != null)
                {
                    forceCurveCharacteristic.ValueUpdated += (s, e) =>
                    {
                        LogBleData("ForceCurveData", e.Characteristic.Value);
                        ergDataAssembler.OnForceCurveMessage(e.Characteristic.Value);
                        if (ergDataAssembler.TryGetUpdatedStroke() is StrokeData stroke)
                        {
                            strokeDataCallback(stroke);
                        }
                    };

                    await forceCurveCharacteristic.StartUpdatesAsync(cancellationToken);
                }

                // Keep connection alive until cancelled
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (DeviceConnectionException ex)
            {
                throw new InvalidOperationException($"Failed to connect to device: {ex.Message}", ex);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
            finally
            {
                if (_connectedDevice != null)
                {
                    await _adapter.DisconnectDeviceAsync(_connectedDevice);
                    _connectedDevice = null;
                }
            }
        }

        /// <summary>
        /// Logs BLE characteristic data in hex format for debugging and analysis.
        /// Format: CharacteristicName|Length|HexData
        /// Example: GeneralStatus|19|0A00000064000000010D8F0F00B401
        /// To parse back: Split by '|', take HexData, and use Convert.FromHexString() or equivalent.
        /// </summary>
        private static void LogBleData(string characteristicName, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"BLE|{characteristicName}|0|");
                return;
            }

            var hexString = Convert.ToHexString(data);
            System.Diagnostics.Debug.WriteLine($"BLE|{characteristicName}|{data.Length}|{hexString}");
        }
    }
}