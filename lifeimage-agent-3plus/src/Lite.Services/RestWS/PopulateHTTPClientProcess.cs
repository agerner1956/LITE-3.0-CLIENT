/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Executes HTTP Client (POST, PUT, GET, DELETE) Bearer Token authenticated Requests vs generic base url.  
 */

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Lite.Services.RestWS
{
    public class PopulateHTTPClientProcess
    {
        private static HttpClient httpClient = new HttpClient();
        

        public static async Task<object> PopulateHttpClient(string baseUrl, string wsMethodName, string wsMethod,
            string bearerAccessToken, object obj)
        {
            var jsonString = JsonConvert.SerializeObject(obj);

            string url = baseUrl + Path.DirectorySeparatorChar + wsMethodName;
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerAccessToken);
            HttpResponseMessage response = null;
            if (wsMethod.Equals("POST"))
            {
                response = await httpClient.PostAsync(url, content);
            }
            else if (wsMethod.Equals("PUT"))
            {
                response = await httpClient.PutAsync(url, content);
            }
            else if (wsMethod.Equals("GET"))
            {
                response = await httpClient.GetAsync(url);
            }
            else if (wsMethod.Equals("DELETE"))
            {
                response = await httpClient.DeleteAsync(url);
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            obj = JsonConvert.DeserializeObject<object>(responseBody);
            return obj;
        }

     
    }

    
}