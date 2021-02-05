using System;
using System.Collections.Generic;
using System.Text;

namespace Lite.Core.Models
{
    public class Message
    {
        private const string MSH = "MSH";

        private const int MSH_MSG_TIME = 7;
        private const int MSH_MSG_TYPE = 9;
        private const int MSH_MSG_CONTROL_ID = 10;

        private LinkedList<Segment> _segments;

        public Message()
        {
            Clear();
        }

        public void Clear()
        {
            _segments = new LinkedList<Segment>();
        }

        protected Segment Header()
        {
            if (_segments.Count == 0 || _segments.First.Value.Name != MSH)
            {
                return null;
            }
            return _segments.First.Value;
        }

        public void Add(Segment segment)
        {
            if (!String.IsNullOrEmpty(segment.Name) && segment.Name.Length == 3)
            {
                _segments.AddLast(segment);
            }
        }

        public string Serialize()
        {
            var builder = new StringBuilder();
            char[] delimiter = { '\r', '\n' };

            foreach (var segment in _segments)
            {
                builder.Append(segment.Serialize());
                builder.Append("\r");
            }
            return builder.ToString().TrimEnd(delimiter);
        }
    }

    // Taken from HL7Message Project, microsoft hl7 server project, trimmed down just keep the parts we need
    // Should be broken out if this expands.
    public class Segment
    {
        private readonly Dictionary<int, string> _fields;

        public Segment()
        {
            _fields = new Dictionary<int, string>(20);
        }

        public Segment(string name)
        {
            _fields = new Dictionary<int, string>(20)
            {
                { 0, name }
            };
        }

        public string Name
        {
            get
            {
                if (!_fields.ContainsKey(0)) return String.Empty;
                return _fields[0];
            }
        }

        public string Field(int index)
        {
            // This implementation supports only vertical bars as field delimiters
            if (Name == "MSH" && index == 1) return "|";

            if (!_fields.ContainsKey(index))
            {
                return String.Empty;
            }
            return _fields[index];
        }


        public void Field(int index, string value)
        {
            // This implementation supports only vertical bars as field delimiters
            if (Name == "MSH" && index == 1) return;

            if (_fields.ContainsKey(index))
            {
                _fields.Remove(index);
            }

            if (!String.IsNullOrEmpty(value))
            {
                _fields.Add(index, value);
            }
        }

        public void Parse(string text)
        {
            int count = 0;
            char[] delimiter = { '|' };

            string temp = text.Trim('|');
            var tokens = temp.Split(delimiter, StringSplitOptions.None);

            foreach (var item in tokens)
            {
                Field(count, item);
                if (item == "MSH")
                {
                    // Treat the special case "MSH" - the delimiter after the segment name counts as first field
                    ++count;
                }
                ++count;
            }
        }

        public string Serialize()
        {
            int max = 0;
            foreach (var field in _fields)
            {
                if (max < field.Key) max = field.Key;
            }

            var tmp = new StringBuilder();

            for (int i = 0; i <= max; i++)
            {
                if (_fields.ContainsKey(i))
                {
                    tmp.Append(_fields[i]);

                    // Treat special case "MSH" - the first delimiter after segment name counts as first field
                    if (i == 0 && Name == "MSH")
                    {
                        ++i;
                    }
                }
                if (i != max) tmp.Append("|");
            }
            return tmp.ToString();
        }
    }
}
