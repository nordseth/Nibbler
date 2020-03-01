using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nibbler.Utils
{
    // https://stackoverflow.com/a/57263137/24821
    public class AuthParamParser
    {
        private string _buffer;
        private int _i;

        private AuthParamParser(string param)
        {
            _buffer = param;
            _i = 0;
        }

        public static Dictionary<string, string> Parse(string param)
        {
            var state = new AuthParamParser(param);
            var result = new Dictionary<string, string>();
            var token = state.ReadToken();
            while (!string.IsNullOrEmpty(token))
            {
                if (!state.ReadDelim('='))
                {
                    return result;
                }

                result.Add(token, state.ReadString());
                if (!state.ReadDelim(','))
                {
                    return result;
                }

                token = state.ReadToken();
            }

            return result;
        }

        private string ReadToken()
        {
            var start = _i;
            while (_i < _buffer.Length && ValidTokenChar(_buffer[_i]))
            {
                _i++;
            }

            return _buffer.Substring(start, _i - start);
        }

        private bool ReadDelim(char ch)
        {
            while (_i < _buffer.Length && char.IsWhiteSpace(_buffer[_i]))
            {
                _i++;
            }

            if (_i >= _buffer.Length || _buffer[_i] != ch)
            {
                return false;
            }

            _i++;
            while (_i < _buffer.Length && char.IsWhiteSpace(_buffer[_i]))
            {
                _i++;
            }

            return true;
        }

        private string ReadString()
        {
            if (_i < _buffer.Length && _buffer[_i] == '"')
            {
                var buffer = new StringBuilder();
                _i++;
                while (_i < _buffer.Length)
                {
                    if (_buffer[_i] == '\\' && (_i + 1) < _buffer.Length)
                    {
                        _i++;
                        buffer.Append(_buffer[_i]);
                        _i++;
                    }
                    else if (_buffer[_i] == '"')
                    {
                        _i++;
                        return buffer.ToString();
                    }
                    else
                    {
                        buffer.Append(_buffer[_i]);
                        _i++;
                    }
                }

                return buffer.ToString();
            }
            else
            {
                return ReadToken();
            }
        }

        private bool ValidTokenChar(char ch)
        {
            if (ch < 32)
            {
                return false;
            }

            if (ch == '(' || ch == ')' || ch == '<' || ch == '>' || ch == '@'
              || ch == ',' || ch == ';' || ch == ':' || ch == '\\' || ch == '"'
              || ch == '/' || ch == '[' || ch == ']' || ch == '?' || ch == '='
              || ch == '{' || ch == '}' || ch == 127 || ch == ' ' || ch == '\t')
            {
                return false;
            }

            return true;
        }
    }
}
