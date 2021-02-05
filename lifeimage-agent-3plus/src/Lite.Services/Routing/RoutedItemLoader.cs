using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace Lite.Services
{
    public interface IRoutedItemLoader
    {
        RoutedItem LoadFromFile(string file);
    }

    public sealed class RoutedItemLoader : IRoutedItemLoader
    {
        private readonly ILogger _logger;
        public RoutedItemLoader(ILogger<RoutedItemLoader> logger)
        {
            _logger = logger;
        }

        public RoutedItem LoadFromFile(string file)
        {
            //deserialize
            //JsonSerializerSettings settings = new JsonSerializerSettings
            //{
            //    TypeNameHandling = TypeNameHandling.Objects
            //};

            string json = File.ReadAllText(file);

            if (string.IsNullOrEmpty(json))
            {
                _logger.Log(LogLevel.Error, $"{file} is null or blank.");
                return null;
            }

            try
            {
                RoutedItem st = JsonSerializer.Deserialize<RoutedItem>(json);
                return st;
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
    }
}
