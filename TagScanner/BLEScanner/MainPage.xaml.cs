/// Copyright 2016 Kazuma Igari

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BLEScanner
{
    /// <summary>
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            startScan();
        }

        BluetoothLEAdvertisementWatcher _watcher;
        void startScan()
        {
            if (_watcher == null)
            {
                _watcher = new Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher();
                _watcher.SignalStrengthFilter.InRangeThresholdInDBm = -90;
                _watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -95;
                _watcher.Received += _watcher_Received;
                _watcher.Stopped += _watcher_Stopped;
                _catchedData = new Dictionary<ulong, CatchedBLEData>();
                _advertisementDataSource = new ObservableCollection<CatchedBLEData>();
                resultList.ItemsSource = _advertisementDataSource;
            }
            if (_watcher.Status != BluetoothLEAdvertisementWatcherStatus.Aborted)
            {
                _watcher.Start();
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(15);
                timer.Tick += (s, e) => { _watcher.Stop(); };
                timer.Start();
            }
        }

        private void _watcher_Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
        }

        Dictionary<ulong, CatchedBLEData> _catchedData;
        ObservableCollection<CatchedBLEData> _advertisementDataSource;
        DateTime _lastUpdate = DateTime.Now;
        async private void _watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (!_catchedData.ContainsKey(args.BluetoothAddress)) _catchedData.Add(args.BluetoothAddress, null);
            var newData = new CatchedBLEData(args);

            var adData = args.Advertisement;
            foreach(var uuid in adData.ServiceUuids)
            {
                Debug.WriteLine(uuid);
            }

            var adid = "";
            foreach(var data in adData.DataSections)
            {
                Debug.WriteLine(data.DataType.ToString());
                var reader = DataReader.FromBuffer(data.Data);
                byte[] bytes = new byte[data.Data.Length];
                reader.ReadBytes(bytes);
                var byteData = BitConverter.ToString(bytes);
                Debug.WriteLine(byteData);
                reader.Dispose();
                Debug.WriteLine("-----");
                if (data.DataType == 255)
                {
                    var message = Encoding.ASCII.GetString(bytes);
                    adid = byteData.Replace("-", "");
                    Debug.WriteLine(message);
                    adid = message;
                }
            }

            foreach(var data in adData.ManufacturerData)
            {
                Debug.WriteLine("Company ID " + data.CompanyId.ToString());

                var reader = DataReader.FromBuffer(data.Data);
                //var code = reader.ReadInt32();
                //var message = reader.ReadString((uint)code);

                byte[] bytes = new byte[data.Data.Length];
                reader.ReadBytes(bytes);
                var byteData = BitConverter.ToString(bytes).Replace("-", "");
                adid = byteData + "    " + adid;
                Debug.WriteLine(byteData);
                var message = Encoding.ASCII.GetString(bytes);
                Debug.WriteLine(message);
                reader.Dispose();
            }

            if (String.IsNullOrEmpty(newData.Title)) newData.Title = adid;

            lock (_catchedData)
            {
                _catchedData[args.BluetoothAddress] = newData;
            }
            await Dispatcher.RunIdleAsync((e) => 
            {
                if (DateTime.Now.Subtract(_lastUpdate).TotalSeconds > 3)
                {
                    lock (_catchedData)
                    {
                        _advertisementDataSource = new ObservableCollection<CatchedBLEData>(_catchedData.Values.Where(o => o != null).OrderByDescending(o => o.LastData.RawSignalStrengthInDBm));
                    }
                    _lastUpdate = DateTime.Now;
                    resultList.ItemsSource = _advertisementDataSource;
                }
            });

        }
    }

    class CatchedBLEData
    {
        public ulong Address { get; set; }
        public string Title { get; set; }
        public BluetoothLEAdvertisementReceivedEventArgs LastData { get; set; }
        public string Description {
            get
            {
                if (LastData != null) return String.Format("{0} {1}, [{2}]", LastData.RawSignalStrengthInDBm, Title, LastData.Timestamp.ToString("hh:mm:ss"));
                return Address.ToString();
            }
        }

        public CatchedBLEData() { }
        public CatchedBLEData(BluetoothLEAdvertisementReceivedEventArgs data)
        {
            LastData = data;
            Address = data.BluetoothAddress;
            Title = data.Advertisement.LocalName;
        }
    }

}
