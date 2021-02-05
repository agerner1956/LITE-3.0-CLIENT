using Dicom;
using Lite.Core;
using Lite.Core.Guard;
using Lite.Core.Interfaces.Scripting;
using Lite.Core.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Lite.Services.Scripting
{
    public class ScriptService : IScriptService
    {
        private readonly ILogger _logger;

        public ScriptService(ILogger<ScriptService> logger)
        {
            _logger = logger;
        }

        public void Compile(Core.Models.Script item)
        {
            Throw.IfNull(item);

            ScriptOptions scriptOptions = ScriptOptions.Default;

            // Add reference to mscorlib
            var mscorlib = typeof(object).GetTypeInfo().Assembly;
            var systemCore = typeof(System.Linq.Enumerable).GetTypeInfo().Assembly;
            var dicom = typeof(Dicom.DicomAgeString).GetTypeInfo().Assembly;

            var assemblies = new[] { mscorlib, systemCore, dicom };
            scriptOptions = scriptOptions.AddReferences(assemblies);

            List<Assembly> referenceAssemblies = new List<Assembly>();
            foreach (var referenceString in item.references)
            {
                referenceAssemblies.Add(Type.GetType(referenceString).Assembly);
            }
            scriptOptions.AddReferences(referenceAssemblies);

            scriptOptions.AddImports(item.imports);
            
            scriptOptions = scriptOptions.AddReferences(typeof(Enumerable).Assembly).AddImports("System.Linq", "System");
            scriptOptions = scriptOptions.AddReferences(typeof(DicomTag).Assembly).AddImports("Dicom");
            scriptOptions = scriptOptions.AddReferences(typeof(Logger).Assembly).AddImports("LifeImageLite");

            using var interactiveLoader = new InteractiveAssemblyLoader();
            foreach (var reference in referenceAssemblies)
            {
                interactiveLoader.RegisterDependency(reference);
            }

            item.script = CSharpScript.Create<RoutedItem>(item.source, options: scriptOptions, globalsType: typeof(RoutedItem), assemblyLoader: interactiveLoader);

            try
            {
                item.script.Compile();
            }
            catch (CompilationErrorException e)
            {
                item.errors = $"{e.Message} {e.StackTrace}";
                _logger.Log(LogLevel.Warning, $"{e.Message} {e.StackTrace}");
            }
            catch (System.IO.FileLoadException e)
            {
                item.errors = $"{e.Message} {e.StackTrace}";
                _logger.Log(LogLevel.Warning, $"{e.Message} {e.StackTrace}");
            }
        }

        public async Task<ScriptState> RunAsync(Core.Models.Script item, RoutedItem routedItem)
        {
            Throw.IfNull(item);

            ScriptState scriptState = null;

            try
            {
                if (item.script == null)
                {
                    Compile(item);
                }
                scriptState = await item.script.RunAsync(globals: routedItem);
                item.errors = $"No Errors.  Last executed at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff")}";
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);

                item.errors = $"{e.Message} {e.StackTrace}";                
                int i = 1;
                int j = 0;
                var lines = item.script.Code.Split("\n");
                var formattedCode = "";
                foreach (var line in lines)
                {
                    formattedCode += $"[{i++},{j}] " + line.Replace("\n", "").Trim() + (line == "" ? "" : "\n");
                    j += line.Length + 1;
                }
                _logger.Log(LogLevel.Critical, $"code: \n{formattedCode}");
            }
            return scriptState;
        }
    }
}
