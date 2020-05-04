// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;

namespace Nibbler.Utils
{
    /// <summary>
    /// Provides correct handling for QueryString value when needed to reconstruct a request or redirect URI string
    /// From https://github.com/dotnet/aspnetcore/blob/master/src/Http/Http.Abstractions/src/QueryString.cs
    /// </summary>
    public readonly struct QueryString : IEquatable<QueryString>
    {
        public static readonly QueryString Empty = new QueryString(string.Empty);

        private readonly string _value;

        public QueryString(string value)
        {
            if (!string.IsNullOrEmpty(value) && value[0] != '?')
            {
                throw new ArgumentException("The leading '?' must be included for a non-empty query.", nameof(value));
            }
            _value = value;
        }

        public string Value
        {
            get { return _value; }
        }

        public bool HasValue
        {
            get { return !string.IsNullOrEmpty(_value); }
        }

        public override string ToString()
        {
            return ToUriComponent();
        }

        public string ToUriComponent()
        {
            // Escape things properly so System.Uri doesn't mis-interpret the data.
            return HasValue ? _value.Replace("#", "%23") : string.Empty;
        }

        public static QueryString FromUriComponent(string uriComponent)
        {
            if (string.IsNullOrEmpty(uriComponent))
            {
                return new QueryString(string.Empty);
            }
            return new QueryString(uriComponent);
        }

        public static QueryString FromUriComponent(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            string queryValue = uri.GetComponents(UriComponents.Query, UriFormat.UriEscaped);
            if (!string.IsNullOrEmpty(queryValue))
            {
                queryValue = "?" + queryValue;
            }
            return new QueryString(queryValue);
        }

        public static QueryString Create(string name, string value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (!string.IsNullOrEmpty(value))
            {
                value = UrlEncoder.Default.Encode(value);
            }
            return new QueryString($"?{UrlEncoder.Default.Encode(name)}={value}");
        }

        public static QueryString Create(IEnumerable<KeyValuePair<string, string>> parameters)
        {
            var builder = new StringBuilder();
            bool first = true;
            foreach (var pair in parameters)
            {
                AppendKeyValuePair(builder, pair.Key, pair.Value, first);
                first = false;
            }

            return new QueryString(builder.ToString());
        }

        public QueryString Add(QueryString other)
        {
            if (!HasValue || Value.Equals("?", StringComparison.Ordinal))
            {
                return other;
            }
            if (!other.HasValue || other.Value.Equals("?", StringComparison.Ordinal))
            {
                return this;
            }

            // ?name1=value1 Add ?name2=value2 returns ?name1=value1&name2=value2
            return new QueryString(_value + "&" + other.Value.Substring(1));
        }

        public QueryString Add(string name, string value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (!HasValue || Value.Equals("?", StringComparison.Ordinal))
            {
                return Create(name, value);
            }

            var builder = new StringBuilder(Value);
            AppendKeyValuePair(builder, name, value, first: false);
            return new QueryString(builder.ToString());
        }

        public bool Equals(QueryString other)
        {
            if (!HasValue && !other.HasValue)
            {
                return true;
            }
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return !HasValue;
            }
            return obj is QueryString && Equals((QueryString)obj);
        }

        public override int GetHashCode()
        {
            return HasValue ? _value.GetHashCode() : 0;
        }

        public static bool operator ==(QueryString left, QueryString right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(QueryString left, QueryString right)
        {
            return !left.Equals(right);
        }

        public static QueryString operator +(QueryString left, QueryString right)
        {
            return left.Add(right);
        }

        private static void AppendKeyValuePair(StringBuilder builder, string key, string value, bool first)
        {
            builder.Append(first ? "?" : "&");
            builder.Append(UrlEncoder.Default.Encode(key));
            builder.Append("=");
            if (!string.IsNullOrEmpty(value))
            {
                builder.Append(UrlEncoder.Default.Encode(value));
            }
        }
    }
}