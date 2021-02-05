using Lite.Core.Enums;
using Lite.Core.Json;
using Lite.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Lite.Core.Connections
{
    public sealed class DcmtkConnection : Connection
    {
        [JsonPropertyOrder(-90)]
        [JsonPropertyName("localAETitle")]
        public string localAETitle { get; set; }

        [JsonPropertyOrder(-90)]
        [JsonPropertyName("remoteAETitle")]
        public string remoteAETitle { get; set; }

        [JsonPropertyName("ModalityList")]
        public List<string> ModalityList { get; set; }

        [JsonPropertyName("storescpCfgFile")]
        public string storescpCfgFile { get; set; }

        [NonSerialized()]
        public ObservableCollection<RoutedItem> toDicom = new ObservableCollection<RoutedItem>();

        [NonSerialized()]
        public ObservableCollection<RoutedItem> toDcmsend = new ObservableCollection<RoutedItem>();

        [NonSerialized()]
        public ObservableCollection<RoutedItem> toFindSCU = new ObservableCollection<RoutedItem>();

        [NonSerialized()]
        public ObservableCollection<RoutedItem> toMoveSCU = new ObservableCollection<RoutedItem>();

        public DcmtkConnection()
        {
            connType = ConnectionType.dcmtk;
            //toDicom.CollectionChanged += ToDicomCollectionChanged;
            //            toRules.CollectionChanged += ToRulesCollectionChanged;

        }
    }
}
