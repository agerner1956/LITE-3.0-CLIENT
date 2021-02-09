using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Models;
using Lite.Core.Utils;

namespace Lite.Services.Connections.Files.Features
{
    public interface IFilePathFormatterHelper
    {
        string PathFormatter(RoutedItem routedItem, FileConnection connection);
    }

    public sealed class FilePathFormatterHelper : IFilePathFormatterHelper
    {
        private readonly IUtil _util;
        public FilePathFormatterHelper(IUtil util)
        {
            _util = util;
        }

        public string PathFormatter(RoutedItem routedItem, FileConnection connection)
        {
            Throw.IfNull(connection);

            //transform the inpath as needed by replacing variable names included in the inpath with the current values
            string path = connection.inpath;
            int end = 0;
            if (connection.inpath.Contains("{"))
            {
                do
                {
                    int start = end;
                    //we need to replace variable specifiers
                    start = connection.inpath.IndexOf("{", start);
                    if (start == -1)
                    {
                        break;
                    }

                    end = connection.inpath.IndexOf("}", start);
                    if (end == -1)
                    {
                        break;
                    }

                    var variable = connection.inpath.Substring(start + 1, end - start - 1);
                    RoutedItem.PropertyNames name;
                    if (System.Enum.TryParse(variable, out name))
                    {
                        switch (name)
                        {
                            case RoutedItem.PropertyNames.type:
                                path = path.Replace("{type}",
                                    _util.RemoveInvalidPathAndFileCharacters(routedItem.type.ToString("G")));
                                break;
                            case RoutedItem.PropertyNames.AccessionNumber:
                                path = path.Replace("{AccessionNumber}",
                                    _util.RemoveInvalidPathAndFileCharacters(routedItem.AccessionNumber));
                                break;
                            case RoutedItem.PropertyNames.InstanceID:
                                path = path.Replace("{InstanceID}",
                                    _util.RemoveInvalidPathAndFileCharacters($"{routedItem.InstanceID}"));
                                break;
                            case RoutedItem.PropertyNames.PatientID:
                                path = path.Replace("{PatientID}",
                                    _util.RemoveInvalidPathAndFileCharacters($"{routedItem.PatientID}"));
                                break;
                            case RoutedItem.PropertyNames.PatientIDIssuer:
                                path = path.Replace("{PatientIDIssuer}",
                                    _util.RemoveInvalidPathAndFileCharacters(routedItem.PatientIDIssuer));
                                break;
                            case RoutedItem.PropertyNames.Series:
                                path = path.Replace("{Series}",
                                    _util.RemoveInvalidPathAndFileCharacters(routedItem.Series));
                                break;
                            case RoutedItem.PropertyNames.Sop:
                                path = path.Replace("{Sop}", _util.RemoveInvalidPathAndFileCharacters(routedItem.Sop));
                                break;
                            case RoutedItem.PropertyNames.Study:
                                path = path.Replace("{Study}",
                                    _util.RemoveInvalidPathAndFileCharacters(routedItem.Study));
                                break;
                            case RoutedItem.PropertyNames.creationTimeUtc:
                                path = path.Replace("{creationTimeUtc}",
                                    _util.RemoveInvalidPathAndFileCharacters($"{routedItem.creationTimeUtc}"));
                                break;
                            case RoutedItem.PropertyNames.error:
                                path = path.Replace("{error}",
                                    _util.RemoveInvalidPathAndFileCharacters(routedItem.Error));
                                break;
                            case RoutedItem.PropertyNames.fromConnection:
                                path = path.Replace("{fromConnection}",
                                    _util.RemoveInvalidPathAndFileCharacters(routedItem.fromConnection));
                                break;
                            case RoutedItem.PropertyNames.lastAccessTimeUtc:
                                path = path.Replace("{lastAccessTimeUtc}",
                                    _util.RemoveInvalidPathAndFileCharacters($"{routedItem.lastAccessTimeUtc}"));
                                break;
                            case RoutedItem.PropertyNames.lastWriteTimeUtc:
                                path = path.Replace("{lastWriteTimeUtc}",
                                    _util.RemoveInvalidPathAndFileCharacters($"{routedItem.lastWriteTimeUtc}"));
                                break;
                            case RoutedItem.PropertyNames.length:
                                path = path.Replace("{length}",
                                    _util.RemoveInvalidPathAndFileCharacters($"{routedItem.length}"));
                                break;
                            case RoutedItem.PropertyNames.name:
                                path = path.Replace("{name}", _util.RemoveInvalidPathAndFileCharacters(routedItem.name));
                                break;
                            case RoutedItem.PropertyNames.priority:
                                path = path.Replace("{priority}",
                                    _util.RemoveInvalidPathAndFileCharacters($"{routedItem.priority}"));
                                break;
                            case RoutedItem.PropertyNames.status:
                                path = path.Replace("{status}",
                                    _util.RemoveInvalidPathAndFileCharacters($"{routedItem.status}"));
                                break;
                            case RoutedItem.PropertyNames.taskID:
                                path = path.Replace("{taskID}",
                                    _util.RemoveInvalidPathAndFileCharacters($"{routedItem.TaskID}"));
                                break;
                            default:
                                break;
                        }
                    }
                } while (true);
            }

            return path;
        }
    }
}
