using Lite.Core.Models;

namespace Lite.Services
{
    public sealed class CloudAgentConstants
    {
        public const string AgentApiBaseUrl = "/api/agent/v1";

        public const string PingUrl = AgentApiBaseUrl + "/ping";
        public const string GetAgentTasksUrl = AgentApiBaseUrl + "/agent-tasks";
        public const string AgentConfigurationUrl = AgentApiBaseUrl + "/agent-configuration";
        public const string GetStudies = AgentApiBaseUrl + "studies?state=NEEDS_DOWNLOADING&lifeImageSummary=true";
        public const string StowStudies = AgentApiBaseUrl + "/stow/studies";

        public const string RegisterUrl = "/api/admin/v1/agents/setup";
        public const string RegisterAsOrgUrl = "/appregistry/v1/register";
        public const string GetShareDestinationUrl = "/api/box/v3/listAllPublishable";

        public static string GetUploadCloseUrl(string study)
        {
            return $"{AgentApiBaseUrl}/study/{study}/upload-close";
        }

        public static string GetAgentTaskResultUrl(string routedItemid)
        {
            return $"{AgentApiBaseUrl}/agent-task-results/{routedItemid}";
        }

        public static string GetPutHl7Url(string connection)
        {
            return $"{AgentApiBaseUrl}/hl7-upload?connectionName={connection}";
        }
    }

    public sealed class LiteAgentConstants
    {
        public const string BaseUrl = "/api/LITE";
    }

    public sealed class FileAgentConstants
    {
        public const string BaseUrl = "/api/File/";

        public static string GetDeleteUrl(string box, string resource)
        {
            return $"{BaseUrl}{box}/{resource}";
        }

        public static string GetDeleteUrl(RoutedItem routedItem)
        {
            return GetDeleteUrl(routedItem.box, routedItem.resource);
        }

        public static string GetDownloadUrl(RoutedItem routedItem)
        {
            return  $"{BaseUrl}{routedItem.box}/{routedItem.resource}";
        }
    }
}
