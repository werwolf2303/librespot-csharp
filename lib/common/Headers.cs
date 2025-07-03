using System;
using System.Collections.Generic;

namespace lib.common
{
    public class Headers : Dictionary<String, String>
    {
        private Headers(Dictionary<String, String> headers)
        {
            foreach (String key in headers.Keys) 
                Add(key, headers[key]);
        }
            
        public class Builder
        {
            private Dictionary<String, String> _headers = new Dictionary<string, string>();

            public Builder Add(String key, String value)
            {
                _headers.Add(key, value);
                return this;
            }

            public Headers Build()
            {
                return new Headers(_headers);
            }
        }
    }
}