using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Models;
using System.Collections.Generic;

namespace Lite.Services.Connections.Lite.Features
{
    public sealed class LiteProfileUtils
    {
        private readonly Profile _profile;
        public LiteProfileUtils(Profile profile)
        {
            _profile = profile;
        }

        public List<ShareDestinations> GetBoxes()
        {
            List<ShareDestinations> boxes = new List<ShareDestinations>();
            var conns = _profile.connections.FindAll(a => a.connType == ConnectionType.cloud && ((LifeImageCloudConnection)a).enabled);
            foreach (var conn in conns)
            {
                var licon = (LifeImageCloudConnection)conn;
                foreach (var dest in licon.Boxes)
                {
                    if (!boxes.Exists(e => e.boxUuid == dest.boxUuid))
                    {
                        boxes.AddRange(licon.Boxes);
                    }
                }
            }

            return boxes;
        }

        public List<ShareDestinations> GetShareDestinations()
        {
            List<ShareDestinations> shareDestinations = new List<ShareDestinations>();
            var conns = _profile.connections.FindAll(a => a.connType == ConnectionType.cloud && ((LifeImageCloudConnection)a).enabled);
            foreach (var conn in conns)
            {
                var licon = (LifeImageCloudConnection)conn;
                shareDestinations.AddRange(licon.shareDestinations);
            }
            return shareDestinations;
        }
    }
}
