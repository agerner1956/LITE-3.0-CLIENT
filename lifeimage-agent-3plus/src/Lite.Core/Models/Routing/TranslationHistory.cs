using System.Collections.Generic;

namespace Lite.Core.Models
{
    public class TranslationHistory
    {
        public string Study { get; set; }
        public List<Translation> Translations { get; set; } = new List<Translation>();
    }
}
