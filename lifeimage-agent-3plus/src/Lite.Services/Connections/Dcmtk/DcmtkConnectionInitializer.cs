using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lite.Services.Connections.Dcmtk
{
    public interface IDcmtkConnectionInitializer
    {
        void Init(DcmtkConnectionManager connectionManager);
    }

    public sealed class DcmtkConnectionInitializer : IDcmtkConnectionInitializer
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IRoutedItemLoader _routedItemLoader;
        private readonly IUtil _util;        

        public DcmtkConnectionInitializer(
            IProfileStorage profileStorage,
            IRoutedItemLoader routedItemLoader,
            IUtil util)
        {
            _profileStorage = profileStorage;
            _routedItemLoader = routedItemLoader;
            _util = util;            
        }

        public void Init(DcmtkConnectionManager connectionManager)
        {
            Throw.IfNull(connectionManager);

            var connection = connectionManager.Connection;

            //read the persisted RoutedItems bound for DICOM
            LoadToDicom(connection);

            //read the persisted RoutedItems bound for Rules
            LoadToRules(connection);

            //read the persisted RoutedItems bound for dcmsend
            LoadToDcmsend(connection);

            //read the persisted RoutedItems bound for findSCU
            LoadToFindSCU(connection);

            //read the persisted RoutedItems bound for MoveSCU
            LoadToMoveSCU(connection);
        }

        private List<RoutedItem> ReadRouteItemsByPath(DcmtkConnection connection, string folderName)
        {
            Throw.IfNull(connection);
            Throw.IfNullOrWhiteSpace(folderName);

            var profile = _profileStorage.Current;
            string dir = profile.tempPath + Path.DirectorySeparatorChar + connection.name + Path.DirectorySeparatorChar + folderName + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
            Directory.CreateDirectory(dir);
            var fileEntries = _util.DirSearch(dir, Constants.Extensions.MetaExt.ToSearchPattern());

            List<RoutedItem> result = new List<RoutedItem>();

            foreach (string file in fileEntries)
            {
                var st = _routedItemLoader.LoadFromFile(file);
                if (st == null)
                {
                    continue;
                }

                st.fromConnection = connection.name;
                result.Add(st);
            }

            return result;
        }

        private void LoadToDicom(DcmtkConnection Connection)
        {
            //read the persisted RoutedItems bound for DICOM

            var toDicomRoutes = ReadRouteItemsByPath(Connection, "toDicom");

            foreach (var st in toDicomRoutes)
            {
                if (!Connection.toDicom.Contains(st))
                {
                    Connection.toDicom.Add(st);
                }
            }
        }

        private void LoadToRules(DcmtkConnection Connection)
        {
            //read the persisted RoutedItems bound for Rules

            var toRulesRoutes = ReadRouteItemsByPath(Connection, "toRules");

            foreach (var st in toRulesRoutes)
            {
                if (!Connection.toRules.Contains(st))
                {
                    Connection.toRules.Add(st);
                }
            }
        }

        private void LoadToDcmsend(DcmtkConnection Connection)
        {
            //read the persisted RoutedItems bound for dcmsend
            var toDcmsendRoutes = ReadRouteItemsByPath(Connection, "toDcmsend");

            foreach (var st in toDcmsendRoutes)
            {
                if (!Connection.toDcmsend.Contains(st))
                {
                    Connection.toDcmsend.Add(st);
                }
            }
        }

        private void LoadToFindSCU(DcmtkConnection Connection)
        {
            var toFindSCURoutes = ReadRouteItemsByPath(Connection, "toFindSCU");

            foreach (var st in toFindSCURoutes)
            {
                if (!Connection.toFindSCU.Contains(st))
                {
                    Connection.toFindSCU.Add(st);
                }
            }
        }

        private void LoadToMoveSCU(DcmtkConnection Connection)
        {
            var toMoveSCURoutes = ReadRouteItemsByPath(Connection, "toMoveSCU");

            foreach (var st in toMoveSCURoutes)
            {
                if (!Connection.toMoveSCU.Contains(st))
                {
                    Connection.toMoveSCU.Add(st);
                }
            }
        }
    }
}
