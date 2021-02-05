using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Lite.Services.Connections.Dicom.Models
{
    /// <summary>
    /// cFindQueryParams represents some easy to use defaults to search a DICOMConnection along with definable search and return tags
    /// </summary>
    public class LiCloudRequest
    {
        public Dictionary<string, string> searchTags = new Dictionary<string, string>();  // tags to search against

        public LiCloudRequest()
        {
        }

        public LiCloudRequest(Dictionary<string, string> searchTags)
        {
            this.searchTags = searchTags;
        }

        // todo: move from static to service
        public static LiCloudRequest FromJson(string json, ILogger logger)
        {
            logger.Log(LogLevel.Debug, $"json: {json}");

            var temp = System.Text.Json.JsonSerializer.Deserialize<LiCloudRequest>(json);

            //2018-09-25 shb these fields which should have been dynamic were hard-coded on the server side and will be placed into the standard dynamic tag array.  
            //When these fields disappear from cloud this logic will 
            //be skipped by the fact that the dynObj will only have searchTags: attribute and nothing else.  If something else shows up it will not match the names
            //we expect below, so it's backward compatbile and future bug proof.
            //"{\"patientId\":\"1352185168\",\"patientName\":null,\"studyDateTime\":null,\"accessionNumber\":\"635285605238\",\"studyId\":null,\"modalitiesInStudy\":null,\"studyInstanceUid\":null}"
            if (temp == null)
            {
                temp = new LiCloudRequest();
            }
            dynamic dynObj = System.Text.Json.JsonSerializer.Deserialize<dynamic>(json);
            foreach (var child in dynObj)
            {
                if (child.Path != "searchTags" && child.Value != "{}")
                {
                    switch (child.Path)
                    {
                        case "patientId":
                            temp.searchTags.TryAdd("0010,0020", (string)child.Value);
                            break;
                        case "patientName":
                            temp.searchTags.TryAdd("0010,0010", (string)child.Value);
                            break;
                        case "studyDateTime":
                            temp.searchTags.TryAdd("0008,0020", (string)child.Value);
                            break;
                        case "accessionNumber":
                            temp.searchTags.TryAdd("0008,0050", (string)child.Value);
                            break;
                        case "studyId":
                            temp.searchTags.TryAdd("0020,0010", (string)child.Value);
                            break;
                        case "modalitiesInStudy":
                            temp.searchTags.TryAdd("0008,0061", (string)child.Value);
                            break;
                        case "studyInstanceUid":
                            temp.searchTags.TryAdd("0020,000D", (string)child.Value);
                            break;
                        default:
                            //temp.searchTags.Add((string)child.Path, (string)child.Value);  //this is risky and will fail to parse
                            break;
                    }
                }
            }
            return temp;
        }

        public string ToJson()
        {
            //this was hard-coded on the server side and will be pull from the standard tag array and busted out into fields until Cloud no longer needs them
            //"{\"patientId\":\"1352185168\",\"patientName\":null,\"studyDateTime\":null,\"accessionNumber\":\"635285605238\",\"studyId\":null,\"modalitiesInStudy\":null,\"studyInstanceUid\":null}"
            var temp = System.Text.Json.JsonSerializer.Serialize(this);

            foreach (var tag in searchTags)
            {
                switch (tag.Key)
                {
                    case "0010,0020":
                        temp = $"{{\"patientId\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0010,0010":
                        temp = $"{{\"patientName\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0008,0020":
                        temp = $"{{\"studyDateTime\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0008,0050":
                        temp = $"{{\"accessionNumber\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0020,0010":
                        temp = $"{{\"studyId\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0008,0061":
                        temp = $"{{\"modalitiesInStudy\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0020,000D":
                        temp = $"{{\"studyInstanceUid\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    default:

                        break;
                }
            }

            return temp;
        }

        public static string ToJson(Dictionary<string, Dictionary<string, string>> results)
        {
            //this was hard-coded on the server side and will be pull from the standard tag array and busted out into fields until Cloud no longer needs them
            //"{\"patientId\":\"1352185168\",\"patientName\":null,\"studyDateTime\":null,\"accessionNumber\":\"635285605238\",\"studyId\":null,\"modalitiesInStudy\":null,\"studyInstanceUid\":null}"
            var temp = System.Text.Json.JsonSerializer.Serialize(results);

            foreach (var tag in results["response"])
            {
                switch (tag.Key)
                {
                    case "0010,0020":
                        temp = $"{{\"patientId\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0010,0010":
                        temp = $"{{\"patientName\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0008,0020":
                        temp = $"{{\"studyDateTime\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0008,0050":
                        temp = $"{{\"accessionNumber\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0020,0010":
                        temp = $"{{\"studyId\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0008,0061":
                        temp = $"{{\"modalitiesInStudy\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    case "0020,000D":
                        temp = $"{{\"studyInstanceUid\":\"{tag.Value}\"," + temp.Substring(1);
                        break;
                    default:

                        break;
                }
            }

            return temp;
        }
    }
}
