using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Lite.Services
{
    public sealed class ProfileValidator : IProfileValidator
    {        
        private readonly IConnectionFinder _connectionFinder;
        private readonly ILogger _logger;

        public ProfileValidator(
            IConnectionFinder connectionFinder,
            ILogger<ProfileValidator> logger)
        {
            _connectionFinder = connectionFinder;
            _logger = logger;
        }

        public List<string> FullValidate(Profile profile, string profileJSON)
        {
            List<string> errors = new List<string>();

            try
            {
                //todo: use FluentValidation
                //// Use original json string
                //if (profileJSON != null && Profile.jsonSchemaPath != null)
                //{
                //    using (StreamReader file = File.OpenText(Profile.jsonSchemaPath))
                //    {
                //        using (JsonTextReader reader = new JsonTextReader(file))
                //        {
                //            JSchema schema = JSchema.Load(reader);
                //            IList<string> messages;

                //            JObject jObject = JObject.Parse(profileJSON);
                //            jObject.IsValid(schema, out messages);

                //            foreach (string message in messages)
                //            {
                //                errors.Add($"Error: {message}");
                //            }
                //        }
                //    }
                //}

                List<int> portList = new List<int>();

                foreach (Connection conn in profile.connections)
                {
                    List<Connection> connList;

                    // Connections require a name
                    if (conn.name == null)
                    {
                        errors.Add($"Error: Connections must have a name.");
                    }
                    else
                    {
                        // Check for duplicates
                        connList = profile.connections.FindAll(a => a.name == conn.name);

                        if (connList.Count > 1)
                        {
                            string error = $"Error: There are {connList.Count} connections with the name \"{conn.name}\"";

                            // check to make sure it is in only once
                            if (errors.Find(a => a == error) == null)
                                errors.Add(error);
                        }
                    }

                    if (conn is DICOMConnection)
                    {
                        var dicom = (DICOMConnection)conn;
                        int nPort = ((DICOMConnection)conn).localPort;

                        if (dicom.localAETitle == null)
                        {
                            errors.Add($"{conn.name}: localAETitle cannot be null");
                        }
                        else if (dicom.localAETitle.Equals(""))
                        {
                            errors.Add($"{conn.name}: localAETitle cannot be an empty string");
                        }
                        else if (dicom.localAETitle.Length > 16)
                        {
                            errors.Add($"{conn.name}: localAETitle {dicom.localAETitle} cannot be longer than 16 characters.");
                        }


                        if (dicom.remoteAETitle == null)
                        {
                            errors.Add($"{conn.name}: remoteAETitle cannot be null");
                        }
                        else if (dicom.remoteAETitle.Equals(""))
                        {
                            errors.Add($"{conn.name}: remoteAETitle cannot be an empty string");
                        }
                        else if (dicom.remoteAETitle.Length > 16)
                        {
                            errors.Add($"{conn.name}: remoteAETitle {dicom.remoteAETitle} cannot be longer than 16 characters.");
                        }
                    }
                }

                // Check the rules
                foreach (DestRule rule in profile.rules.destRules)
                {
                    if (rule.name == null)
                    {
                        errors.Add($"Warning: Rules should have a name.");
                    }
                    else
                    {
                        Connection conn;

                        conn = _connectionFinder.GetConnectionByName(profile, rule.fromConnectionName);
                        if (conn == null)
                        {
                            if (rule.fromConnectionName == null)
                                errors.Add($"Error: Rule \"{rule.name}\", fromConnection must have a value.");
                            else
                                errors.Add($"Error: Rule \"{rule.name}\", fromConnection \"{rule.fromConnectionName}\" must be a valid connection name.");
                        }
                        else
                        {
                            // warn if a connection is disabled
                            if (rule.enabled == true && conn.enabled == false)
                            {
                                errors.Add($"Warning: fromConnection \"{conn.name}\" is disabled and included in rule \"{rule.name}\".");
                            }
                        }

                        foreach (var connectionSet in rule.toConnections)
                        {
                            conn = _connectionFinder.GetConnectionByName(profile, connectionSet.connectionName);
                            if (conn == null)
                                errors.Add($"Error: Rule \"{rule.name}\", toConnection \"{connectionSet.connectionName}\" must be a valid connection name.");
                            else
                            {
                                if (rule.fromConnectionName == connectionSet.connectionName)
                                    errors.Add($"Error: A toConnections is the same as the fromConnection in \"{rule.name}\".");

                                // warn if a connection is disabled
                                if (rule.enabled == true && conn.enabled == false)
                                {
                                    errors.Add($"Warning: toConnection \"{conn.name}\" is disabled and included in rule \"{rule.name}\".");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Critical, $"{e.Message} {e.StackTrace}");
            }

            return errors;
        }
    }
}
