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
        private static readonly Guid PM5CharacteristicForceCurveData = new("CE060038-43E5-11E4-916C-0800200C9A66");

        private readonly IBluetoothLE _bluetoothLE;
        private readonly IAdapter _adapter;
        private IDevice? _connectedDevice;

        // Cache for accumulated data from multiple characteristics
        private ErgData _currentData = new();
        private readonly object _dataLock = new();

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

        public async Task ConnectAndStreamAsync(string ergId, Action<ErgData> dataCallback, CancellationToken cancellationToken)
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

                var generalStatusChar = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicGeneralStatus);
                var additionalStatusChar = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicAdditionalStatus);
                var strokeDataChar = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicStrokeData);
                var additionalStrokeDataChar = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicAdditionalStrokeData);
                var forceCurveChar = characteristics.FirstOrDefault(c => c.Id == PM5CharacteristicForceCurveData);

                // Subscribe to Rowing General Status (primary data source)
                if (generalStatusChar != null)
                {
                    generalStatusChar.ValueUpdated += (s, e) =>
                    {
                        var data = ParseGeneralStatus(e.Characteristic.Value);
                        lock (_dataLock)
                        {
                            UpdateErgData(_currentData, data);
                            dataCallback(CloneErgData(_currentData));
                        }
                    };
                    await generalStatusChar.StartUpdatesAsync(cancellationToken);
                }

                // Subscribe to Additional Status (workout type, drag factor)
                if (additionalStatusChar != null)
                {
                    additionalStatusChar.ValueUpdated += (s, e) =>
                    {
                        var data = ParseAdditionalStatus(e.Characteristic.Value);
                        lock (_dataLock)
                        {
                            UpdateErgData(_currentData, data);
                        }
                    };
                    await additionalStatusChar.StartUpdatesAsync(cancellationToken);
                }

                // Subscribe to Stroke Data (stroke-end events)
                if (strokeDataChar != null)
                {
                    strokeDataChar.ValueUpdated += (s, e) =>
                    {
                        var data = ParseStrokeData(e.Characteristic.Value);
                        lock (_dataLock)
                        {
                            UpdateErgData(_currentData, data);
                            // Callback on stroke-end event
                            dataCallback(CloneErgData(_currentData));
                        }
                    };
                    await strokeDataChar.StartUpdatesAsync(cancellationToken);
                }

                // Subscribe to Additional Stroke Data (more stroke details)
                if (additionalStrokeDataChar != null)
                {
                    additionalStrokeDataChar.ValueUpdated += (s, e) =>
                    {
                        var data = ParseAdditionalStrokeData(e.Characteristic.Value);
                        lock (_dataLock)
                        {
                            UpdateErgData(_currentData, data);
                        }
                    };
                    await additionalStrokeDataChar.StartUpdatesAsync(cancellationToken);
                }

                // Subscribe to Force Curve Data (power curve)
                if (forceCurveChar != null)
                {
                    forceCurveChar.ValueUpdated += (s, e) =>
                    {
                        var powerCurve = ParseForceCurveData(e.Characteristic.Value);
                        lock (_dataLock)
                        {
                            _currentData.PowerCurve = powerCurve;
                        }
                    };
                    await forceCurveChar.StartUpdatesAsync(cancellationToken);
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

        // Parse Rowing General Status characteristic (0x0031)
        private ErgData ParseGeneralStatus(byte[] data)
        {
            if (data == null || data.Length < 19)
                return new ErgData();

            return new ErgData
            {
                Timestamp = DateTime.Now,
                ElapsedTime = BitConverter.ToUInt32(data, 0) / 100.0, // centiseconds to seconds
                Distance = BitConverter.ToUInt32(data, 4) / 10.0, // decimeters to meters
                WorkoutState = data[8],
                StrokeState = data[12],
                StrokeRate = data[13],
                HeartRate = data[14] == 255 ? null : (int?)data[14],
                Pace = BitConverter.ToUInt16(data, 15) / 10.0, // centiseconds per 500m
                Power = BitConverter.ToUInt16(data, 17), // watts
            };
        }

        // Parse Additional Status characteristic (0x0032)
        private ErgData ParseAdditionalStatus(byte[] data)
        {
            if (data == null || data.Length < 7)
                return new ErgData();

            return new ErgData
            {
                ElapsedTime = BitConverter.ToUInt32(data, 0) / 100.0,
                Calories = BitConverter.ToUInt16(data, 4),
                WorkoutType = data[6]
            };
        }

        // Parse Stroke Data characteristic (0x0035) - Stroke-End Events
        private ErgData ParseStrokeData(byte[] data)
        {
            if (data == null || data.Length < 19)
                return new ErgData();

            return new ErgData
            {
                Timestamp = DateTime.Now,
                ElapsedTime = BitConverter.ToUInt32(data, 0) / 100.0,
                Distance = BitConverter.ToUInt32(data, 4) / 10.0,
                Power = BitConverter.ToUInt16(data, 8),
                StrokeRate = data[10],
                Calories = BitConverter.ToUInt16(data, 11),
                Pace = BitConverter.ToUInt16(data, 15) / 10.0,
            };
        }

        // Parse Additional Stroke Data characteristic (0x0036)
        private ErgData ParseAdditionalStrokeData(byte[] data)
        {
            if (data == null || data.Length < 17)
                return new ErgData();

            return new ErgData
            {
                ElapsedTime = BitConverter.ToUInt32(data, 0) / 100.0,
                Power = BitConverter.ToUInt16(data, 4),
                DragFactor = data[12]
            };
        }

        // Parse Force Curve Data characteristic (0x0038)
        private List<int> ParseForceCurveData(byte[] data)
        {
            if (data == null || data.Length < 4)
                return new List<int>();

            var powerCurve = new List<int>();
            int dataPoints = BitConverter.ToUInt16(data, 0);
            
            for (int i = 0; i < dataPoints && (2 + i * 2) < data.Length; i++)
            {
                int force = BitConverter.ToUInt16(data, 2 + i * 2);
                powerCurve.Add(force);
            }

            return powerCurve;
        }

        // Helper to merge partial data into current data
        private void UpdateErgData(ErgData current, ErgData update)
        {
            if (update.Timestamp != default)
                current.Timestamp = update.Timestamp;
            if (update.ElapsedTime > 0)
                current.ElapsedTime = update.ElapsedTime;
            if (update.Distance > 0)
                current.Distance = update.Distance;
            if (update.StrokeRate > 0)
                current.StrokeRate = update.StrokeRate;
            if (update.HeartRate.HasValue)
                current.HeartRate = update.HeartRate;
            if (update.Pace > 0)
                current.Pace = update.Pace;
            if (update.Power > 0)
                current.Power = update.Power;
            if (update.Calories > 0)
                current.Calories = update.Calories;
            if (update.DragFactor.HasValue)
                current.DragFactor = update.DragFactor;
            if (update.StrokeState.HasValue)
                current.StrokeState = update.StrokeState;
            if (update.WorkoutState > 0)
                current.WorkoutState = update.WorkoutState;
            if (update.WorkoutType.HasValue)
                current.WorkoutType = update.WorkoutType;
        }

        // Helper to clone ErgData for thread-safe callbacks
        private ErgData CloneErgData(ErgData source)
        {
            return new ErgData
            {
                Timestamp = source.Timestamp,
                ElapsedTime = source.ElapsedTime,
                Distance = source.Distance,
                StrokeRate = source.StrokeRate,
                HeartRate = source.HeartRate,
                Pace = source.Pace,
                Power = source.Power,
                Calories = source.Calories,
                DragFactor = source.DragFactor,
                StrokeState = source.StrokeState,
                PowerCurve = source.PowerCurve?.ToList(),
                WorkoutState = source.WorkoutState,
                WorkoutType = source.WorkoutType
            };
        }
    }
}