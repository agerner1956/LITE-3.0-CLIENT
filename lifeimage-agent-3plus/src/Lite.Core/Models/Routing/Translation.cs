using System.Collections.Generic;

namespace Lite.Core.Models
{
    /***
Need a way to look up defined translations and to record what we've done to a study
Translations are needed for lists so they are not hard-coded in a script and can be updated without a script update
Possible file representation:
/translations/ThisOrThat.json {"0008,0080": [{”University of Rochester”: ”100”}, {”University of Bochester”: ”101”}]}
/translationhistory/{studyid}.json {"0008,0080": {”University of Rochester”: ”100”}}
 */
    public class Translation
    {
        public string Tag { get; set; }
        public Dictionary<string, string> FromTo { get; set; }
    }
}
