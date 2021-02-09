using Lite.Core;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;

namespace Lite.Services
{
    public sealed class RegistryHelper : IRegistryHelper
    {
        private readonly ILogger _logger;

        public RegistryHelper(ILogger logger)
        {
            Throw.IfNull(logger);
            _logger = logger;
        }

        public void RegisterKey(string path, string value)
        {
            //if Windows set the running version in the registry
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            try
            {
                var rs = new RegistrySecurity();
                string user = Environment.UserDomainName + "\\" + Environment.UserName;
                rs.AddAccessRule(new RegistryAccessRule(user,
                    RegistryRights.WriteKey | RegistryRights.SetValue,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                RegistryKey key;
                using (key = Registry.LocalMachine.CreateSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree, rs))
                {
                    key.SetValue("DisplayVesion", value, RegistryValueKind.String);
                    //key.Close();
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
            }
        }

        public void RegisterProduct(string version)
        {
            var registryKeyPath = $"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{Constants.ProductKey}";
            RegisterKey(registryKeyPath, version);
        }
    }
}
