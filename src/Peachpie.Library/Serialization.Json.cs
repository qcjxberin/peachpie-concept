﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pchp.Core;
using Pchp.Core.Reflection;
using static Pchp.Library.JsonSerialization;

namespace Pchp.Library
{
    static partial class PhpSerialization
    {
        #region JsonLastError

        class JsonLastError
        {
            public int LastError;
        }

        internal static int GetLastJsonError(Context ctx)
        {
            var p = ctx.TryGetProperty<JsonLastError>();
            return (p != null) ? p.LastError : 0;
        }

        #endregion

        #region JsonSerializer

        internal sealed class JsonSerializer : Serializer
        {
            #region Tokens

            /// <summary>
            /// Contains definition of tokens that constitute PHP serialized data.
            /// </summary>
            internal class Tokens
            {
                internal const char ObjectOpen = '{';
                internal const char ObjectClose = '}';

                internal const char ItemsSeparator = ',';
                internal const string ItemsSeparatorString = ",";

                internal const char PropertyKeyValueSeparator = ':';
                internal const string PropertyKeyValueSeparatorString = ":";

                internal const char Quote = '"';
                internal const string QuoteString = "\""; // "
                internal const string DoubleQuoteString = "\"\""; // ""
                internal const char Escape = '\\';

                internal const string EscapedNewLine = @"\n";
                internal const string EscapedCR = @"\r";
                internal const string EscapedTab = @"\t";
                internal const string EscapedBackspace = @"\b";
                internal const string EscapedQuote = "\\\"";
                internal const string EscapedReverseSolidus = @"\\";
                internal const string EscapedSolidus = @"\/";
                internal const string EscapedFormFeed = @"\f";
                internal const string EscapedUnicodeChar = @"\u";   // 4-digit number follows

                internal const char ArrayOpen = '[';
                internal const char ArrayClose = ']';

                internal const string NullLiteral = "null";
                internal const string TrueLiteral = "true";
                internal const string FalseLiteral = "false";

            }

            #endregion

            #region ObjectWriter

            sealed class ObjectWriter : PhpVariableVisitor
            {
                Encoding Encoding => _ctx.StringEncoding;

                readonly Stack<object> _recursionStack = new Stack<object>();

                /// <summary>
                /// Result data.
                /// </summary>
                readonly PhpString.Blob _result = new PhpString.Blob();

                readonly Context _ctx;
                readonly RuntimeTypeHandle _caller;
                readonly JsonEncodeOptions _encodeOptions;

                #region Options

                bool HasForceObject => (_encodeOptions & JsonEncodeOptions.JSON_FORCE_OBJECT) != 0;
                bool HasHexAmp => (_encodeOptions & JsonEncodeOptions.JSON_HEX_AMP) != 0;
                bool HasHexApos => (_encodeOptions & JsonEncodeOptions.JSON_HEX_APOS) != 0;
                bool HasHexQuot => (_encodeOptions & JsonEncodeOptions.JSON_HEX_QUOT) != 0;
                bool HasHexTag => (_encodeOptions & JsonEncodeOptions.JSON_HEX_TAG) != 0;
                bool HasNumericCheck => (_encodeOptions & JsonEncodeOptions.JSON_NUMERIC_CHECK) != 0;
                bool HasPrettyPrint => (_encodeOptions & JsonEncodeOptions.JSON_PRETTY_PRINT) != 0;
                bool HasUnescapedSlashes => (_encodeOptions & JsonEncodeOptions.JSON_UNESCAPED_SLASHES) != 0;
                bool HasUnescapedUnicode => (_encodeOptions & JsonEncodeOptions.JSON_UNESCAPED_UNICODE) != 0;

                #endregion

                private ObjectWriter(Context ctx, JsonEncodeOptions encodeOptions, RuntimeTypeHandle caller)
                {
                    Debug.Assert(ctx != null);
                    _ctx = ctx;
                    _encodeOptions = encodeOptions;
                    _caller = caller;
                }

                public static PhpString Serialize(Context ctx, PhpValue variable, JsonEncodeOptions encodeOptions, RuntimeTypeHandle caller)
                {
                    ObjectWriter writer;
                    variable.Accept(writer = new ObjectWriter(ctx, encodeOptions, caller));
                    return new PhpString(writer._result);
                }

                bool PushObject(object obj)
                {
                    int count = 0;
                    if (_recursionStack.Count != 0)
                    {
                        foreach (var x in _recursionStack)
                        {
                            if (x == obj)
                            {
                                count++;
                            }
                        }
                    }


                    if (count < 2)
                    {
                        _recursionStack.Push(obj);
                        return true;
                    }
                    else
                    {
                        PhpException.Throw(PhpError.Warning, Resources.LibResources.recursion_detected);
                        return false;
                    }
                }

                void PopObject(object obj)
                {
                    Debug.Assert(_recursionStack.Count != 0);
                    var x = _recursionStack.Pop();
                    Debug.Assert(x == obj);
                }

                void Write(string str) => _result.Append(str);
                void Write(char ch) => _result.Append(ch.ToString());

                public override void AcceptNull()
                {
                    WriteNull();
                }

                public override void Accept(bool obj)
                {
                    WriteBoolean(obj);
                }

                public override void Accept(long obj)
                {
                    Write(obj.ToString());
                }

                public override void Accept(string obj)
                {
                    WriteString(obj);
                }

                public override void Accept(PhpString obj)
                {
                    WriteString(obj.ToString(_ctx));
                }

                public override void Accept(double obj)
                {
                    Write(obj.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                /// <summary>
                /// Determines if the array is integer indexed in sequnece from 0 without "holes".
                /// </summary>
                /// <remarks>Determines if the array can be encoded as JSON array.</remarks>
                static bool IsSequentialArray(PhpArray/*!*/array)
                {
                    if (array.Count != 0)
                    {
                        // TODO: once we implement packed arrays, just check if the array is packed without holes and return

                        int next = 0;
                        var enumerator = array.GetFastEnumerator();
                        while (enumerator.MoveNext())
                        {
                            var key = enumerator.CurrentKey;
                            if (key.IsString || key.Integer != next++)
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }

                public override void Accept(PhpArray array)
                {
                    if (PushObject(array))
                    {
                        if (HasForceObject || !IsSequentialArray(array))
                        {
                            // array are encoded as objects or there are keyed values that has to be encoded as object
                            WriteObject(JsonArrayProperties(array));
                        }
                        else
                        {
                            // we can write JSON array
                            WriteArray(array);
                        }

                        PopObject(array);
                    }
                    else
                    {
                        WriteNull();
                    }
                }

                public override void AcceptObject(object obj)
                {
                    if (obj is JsonSerializable)
                    {
                        var data = ((JsonSerializable)obj).jsonSerialize();
                        if ((obj = data.AsObject()) == null)
                        {
                            data.Accept(this);
                            return;
                        }
                        else
                        {
                            // serialize `obj` without checking JsonSerializable interface ... 
                        }
                    }

                    if (obj is PhpResource)
                    {
                        WriteUnsupported(PhpResource.PhpTypeName);
                        return;
                    }

                    if (PushObject(obj))
                    {
                        WriteObject(JsonObjectProperties(obj));
                        PopObject(obj);
                    }
                    else
                    {
                        WriteNull();
                    }
                }

                #region encoding strings

                /// <summary>
                /// Determines if given character is printable character. Otherwise it must be encoded.
                /// </summary>
                /// <param name="c"></param>
                /// <returns></returns>
                private static bool CharIsPrintable(char c)
                {
                    return
                        (c <= 0x7f) &&   // ASCII
                        (!char.IsControl(c)) && // not control
                        (!(c >= 9 && c <= 13)); // not BS, HT, LF, Vertical Tab, Form Feed, CR
                }

                /// <summary>
                /// Determines if given character should be encoded.
                /// </summary>
                private bool CharShouldBeEncoded(char c)
                {
                    switch (c)
                    {
                        case '\n':
                        case '\r':
                        case '\t':
                        case '/':
                        case Tokens.Escape:
                        case '\b':
                        case '\f':
                        case Tokens.Quote:
                            return true;

                        case '\'':
                            return HasHexApos;

                        case '<':
                            return HasHexTag;

                        case '>':
                            return HasHexTag;

                        case '&':
                            return HasHexAmp;

                        default:
                            return !CharIsPrintable(c);
                    }
                }

                /// <summary>
                /// Determines if some chars in given string should be encoded.
                /// </summary>
                private bool StringShouldBeEncoded(string str)
                {
                    Debug.Assert(str != null);

                    for (int i = 0; i < str.Length; i++)
                    {
                        if (CharShouldBeEncoded(str[i])) return true;
                    }

                    return false;
                }

                /// <summary>
                /// Convert 16b character into json encoded character.
                /// </summary>
                /// <param name="value">The full string to be encoded.</param>
                /// <param name="i">The index of character to be encoded. Can be increased if more characters are processed.</param>
                /// <returns>The encoded part of string, from value[i] to value[i after method call]</returns>
                private string EncodeStringIncremental(string value, ref int i)
                {
                    char c = value[i];

                    switch (c)
                    {
                        case '\n': return (Tokens.EscapedNewLine);
                        case '\r': return (Tokens.EscapedCR);
                        case '\t': return (Tokens.EscapedTab);
                        case '/':
                            if (HasUnescapedSlashes) { goto default; }
                            return (Tokens.EscapedSolidus);
                        case Tokens.Escape: return (Tokens.EscapedReverseSolidus);
                        case '\b': return (Tokens.EscapedBackspace);
                        case '\f': return (Tokens.EscapedFormFeed);
                        case Tokens.Quote: return (HasHexQuot ? (Tokens.EscapedUnicodeChar + "0022") : Tokens.EscapedQuote);
                        case '\'': return (HasHexApos ? (Tokens.EscapedUnicodeChar + "0027") : "'");
                        case '<': return (HasHexTag ? (Tokens.EscapedUnicodeChar + "003C") : "<");
                        case '>': return (HasHexTag ? (Tokens.EscapedUnicodeChar + "003E") : ">");
                        case '&': return (HasHexAmp ? (Tokens.EscapedUnicodeChar + "0026") : "&");
                        default:
                            {
                                if (CharIsPrintable(c))
                                {
                                    int start = i++;
                                    while (i < value.Length && !CharShouldBeEncoded(value[i])) ++i;

                                    return value.Substring(start, (i--) - start);   // accumulate characters, mostly it is entire string value (faster)
                                }
                                else
                                {
                                    return (Tokens.EscapedUnicodeChar + ((int)c).ToString("X4"));
                                }
                            }
                    }
                }

                #endregion

                /// <summary>
                /// Serializes null and throws an exception.
                /// </summary>
                /// <param name="TypeName"></param>
                private void WriteUnsupported(string TypeName)
                {
                    PhpException.Throw(PhpError.Warning, Resources.LibResources.serialization_unsupported_type, TypeName);
                    WriteNull();
                }

                void WriteNull()
                {
                    _result.Append(Tokens.NullLiteral);
                }

                void WriteBoolean(bool value)
                {
                    _result.Append(value ? Tokens.TrueLiteral : Tokens.FalseLiteral);
                }

                /// <summary>
                /// Serializes JSON string.
                /// </summary>
                /// <param name="value">The string.</param>
                void WriteString(string value)
                {
                    if (HasNumericCheck)
                    {
                        long l;
                        double d;
                        var result = Core.Convert.StringToNumber(value, out l, out d);
                        if ((result & Core.Convert.NumberInfo.IsNumber) != 0)
                        {
                            if ((result & Core.Convert.NumberInfo.LongInteger) != 0) _result.Append(l.ToString());
                            if ((result & Core.Convert.NumberInfo.Double) != 0) _result.Append(d.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            return;
                        }
                    }

                    if (value.Length == 0)
                    {
                        // empty string
                        _result.Append(Tokens.DoubleQuoteString);   // ""
                    }
                    else if (!StringShouldBeEncoded(value)) // most common case
                    {
                        // string can be appended as it is
                        _result.Append(Tokens.QuoteString);
                        _result.Append(value);
                        _result.Append(Tokens.QuoteString);
                    }
                    else
                    {
                        var strVal = new StringBuilder(value.Length + 4);

                        strVal.Append(Tokens.Quote);

                        for (int i = 0; i < value.Length; ++i)
                        {
                            strVal.Append(EncodeStringIncremental(value, ref i));
                        }

                        strVal.Append(Tokens.Quote);

                        _result.Append(strVal.ToString());
                    }
                }

                void WriteArray(PhpArray array)
                {
                    // [
                    Write(Tokens.ArrayOpen);

                    //
                    if (array.Count != 0)
                    {
                        bool bFirst = true;

                        var enumerator = array.GetFastEnumerator();
                        while (enumerator.MoveNext())
                        {
                            // ,
                            if (bFirst) bFirst = false;
                            else Write(Tokens.ItemsSeparatorString);

                            Debug.Assert(enumerator.CurrentKey.IsInteger);

                            // value
                            Accept(enumerator.CurrentValue);
                        }
                    }

                    // ]
                    Write(Tokens.ArrayClose);
                }

                void WriteObject(IEnumerable<KeyValuePair<string, PhpValue>> properties)
                {
                    // {
                    Write(Tokens.ObjectOpen);

                    bool bFirst = true;
                    foreach (var pair in properties)
                    {
                        // ,
                        if (bFirst) bFirst = false;
                        else Write(Tokens.ItemsSeparatorString);

                        // "key":value
                        WriteString(pair.Key);
                        Write(Tokens.PropertyKeyValueSeparatorString);
                        pair.Value.Accept(this);
                    }

                    // }
                    Write(Tokens.ObjectClose);
                }

                IEnumerable<KeyValuePair<string, PhpValue>> JsonArrayProperties(PhpArray array)
                {
                    var enumerator = array.GetFastEnumerator();
                    while (enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        yield return new KeyValuePair<string, PhpValue>(current.Key.ToString(), current.Value);
                    }
                }

                IEnumerable<KeyValuePair<string, PhpValue>> JsonObjectProperties(object/*!*/obj)
                {
                    return TypeMembersUtils.EnumerateInstanceFields(obj, (f, d) => f.Name, (k) => k.ToString());
                }
            }

            #endregion

            #region Serializer

            public override string Name => "JSON";

            protected override PhpValue CommonDeserialize(Context ctx, Stream data, RuntimeTypeHandle caller)
            {
                var jsonerror = ctx.GetStatic<JsonLastError>();

                var options = _decodeOptions ?? new DecodeOptions();
                var scanner = new Json.JsonScanner(new StreamReader(data), options);
                var parser = new Json.Parser(options) { Scanner = scanner };

                jsonerror.LastError = JsonSerialization.JSON_ERROR_NONE;

                if (!parser.Parse())
                {
                    var errorcode = JsonSerialization.JSON_ERROR_SYNTAX;

                    if ((options.Options & JsonDecodeOptions.JSON_THROW_ON_ERROR) == 0)
                    {
                        jsonerror.LastError = errorcode;
                        return PhpValue.Null;
                    }
                    else
                    {
                        throw new JsonException(code: errorcode);
                    }
                }

                //
                return parser.Result;
            }

            protected override PhpString CommonSerialize(Context ctx, PhpValue variable, RuntimeTypeHandle caller)
            {
                return ObjectWriter.Serialize(ctx, variable, _encodeOptions, caller);
            }

            #endregion

            public JsonSerializer(DecodeOptions decodeOptions = null, JsonEncodeOptions encodeOptions = JsonEncodeOptions.Default)
            {
                _decodeOptions = decodeOptions;
                _encodeOptions = encodeOptions;
            }

            #region Options

            /// <summary>
            /// Decode (unserialize) options.
            /// </summary>
            public class DecodeOptions
            {
                public JsonDecodeOptions Options = JsonDecodeOptions.Default;

                /// <summary>
                /// When TRUE, returned object s will be converted into associative array s. 
                /// </summary>
                public bool Assoc = false;

                /// <summary>
                /// User specified recursion depth. 
                /// </summary>
                public int Depth = 512;

                public bool BigIntAsString => (Options & JsonDecodeOptions.JSON_BIGINT_AS_STRING) != 0;
            }

            readonly DecodeOptions _decodeOptions;
            readonly JsonEncodeOptions _encodeOptions;

            #endregion
        }

        #endregion
    }

    [PhpExtension("json")]
    public static class JsonSerialization
    {
        #region Constants

        // 
        // Values returned by json_last_error function.
        //
        /// <summary>
        /// No error has occurred  	 
        /// </summary>
        public const int JSON_ERROR_NONE = 0;

        /// <summary>
        /// The maximum stack depth has been exceeded  	 
        /// </summary>
        public const int JSON_ERROR_DEPTH = 1;

        /// <summary>
        /// Occurs with underflow or with the modes mismatch.
        /// </summary>
        public const int JSON_ERROR_STATE_MISMATCH = 2;

        /// <summary>
        /// Control character error, possibly incorrectly encoded  	 
        /// </summary>
        public const int JSON_ERROR_CTRL_CHAR = 3;

        /// <summary>
        /// Syntax error  	 
        /// </summary>
        public const int JSON_ERROR_SYNTAX = 4;

        /// <summary>
        /// 
        /// </summary>
        public const int JSON_ERROR_UTF8 = 5;

        /// <summary>
        /// 
        /// </summary>
        public const int JSON_THROW_ON_ERROR = 1 << 22;

        /// <summary>
        /// Options given to json_encode function.
        /// </summary>
        [PhpHidden]
        public enum JsonEncodeOptions
        {
            /// <summary>
            /// No options specified.
            /// </summary>
            Default = 0,

            /// <summary>
            /// All &lt; and &gt; are converted to \u003C and \u003E. 
            /// </summary>
            JSON_HEX_TAG = 1,

            /// <summary>
            /// All &amp;s are converted to \u0026. 
            /// </summary>
            JSON_HEX_AMP = 2,

            /// <summary>
            /// All ' are converted to \u0027. 
            /// </summary>
            JSON_HEX_APOS = 4,

            /// <summary>
            /// All " are converted to \u0022. 
            /// </summary>
            JSON_HEX_QUOT = 8,

            /// <summary>
            /// Outputs an object rather than an array when a non-associative array is used. Especially useful when the recipient of the output is expecting an object and the array is empty. 
            /// </summary>
            JSON_FORCE_OBJECT = 16,

            /// <summary>
            /// Encodes numeric strings as numbers. 
            /// </summary>
            JSON_NUMERIC_CHECK = 32,

            /// <summary>
            /// Don't escape /.
            /// </summary>
            JSON_UNESCAPED_SLASHES = 64,

            /// <summary>
            /// Use whitespace in returned data to format it.
            /// </summary>
            JSON_PRETTY_PRINT = 128,

            /// <summary>
            /// Encode multibyte Unicode characters literally (default is to escape as \uXXXX).
            /// </summary>
            JSON_UNESCAPED_UNICODE = 256,

            JSON_THROW_ON_ERROR = JsonSerialization.JSON_THROW_ON_ERROR,
        }

        public const int JSON_HEX_TAG = (int)JsonEncodeOptions.JSON_HEX_TAG;
        public const int JSON_HEX_AMP = (int)JsonEncodeOptions.JSON_HEX_AMP;
        public const int JSON_HEX_APOS = (int)JsonEncodeOptions.JSON_HEX_APOS;
        public const int JSON_HEX_QUOT = (int)JsonEncodeOptions.JSON_HEX_QUOT;
        public const int JSON_FORCE_OBJECT = (int)JsonEncodeOptions.JSON_FORCE_OBJECT;
        public const int JSON_NUMERIC_CHECK = (int)JsonEncodeOptions.JSON_NUMERIC_CHECK;
        public const int JSON_UNESCAPED_SLASHES = (int)JsonEncodeOptions.JSON_UNESCAPED_SLASHES;
        public const int JSON_PRETTY_PRINT = (int)JsonEncodeOptions.JSON_PRETTY_PRINT;
        public const int JSON_UNESCAPED_UNICODE = (int)JsonEncodeOptions.JSON_UNESCAPED_UNICODE;

        /// <summary>
        /// Options given to json_decode function.
        /// </summary>
        [PhpHidden]
        public enum JsonDecodeOptions
        {
            Default = 0,

            /// <summary>
            /// Big integers represent as strings rather than floats.
            /// </summary>
            JSON_BIGINT_AS_STRING = 1,

            JSON_THROW_ON_ERROR = JsonSerialization.JSON_THROW_ON_ERROR,
        }

        public const int JSON_BIGINT_AS_STRING = (int)JsonDecodeOptions.JSON_BIGINT_AS_STRING;

        #endregion

        #region json_encode, json_decode, json_last_error

        public static PhpString json_encode(Context ctx, PhpValue value, JsonEncodeOptions options = JsonEncodeOptions.Default)
        {
            return new PhpSerialization.JsonSerializer(encodeOptions: options).Serialize(ctx, value, default);
        }

        /// <summary>
        /// Takes a JSON encoded string and converts it into a PHP variable.
        /// </summary>
        /// <param name="ctx">Runtime context.</param>
        /// <param name="json"></param>
        /// <param name="assoc">When TRUE, returned object's will be converted into associative array s. </param>
        /// <param name="depth">User specified recursion depth. </param>
        /// <param name="options"></param>
        /// <returns>Returns the value encoded in json in appropriate PHP type. Values true, false and null are returned as TRUE, FALSE and NULL respectively. NULL is returned if the json cannot be decoded or if the encoded data is deeper than the recursion limit.</returns>
        public static PhpValue json_decode(Context ctx, PhpString json, bool assoc = false, int depth = 512, JsonDecodeOptions options = JsonDecodeOptions.Default)
        {
            if (json.IsEmpty) return PhpValue.Null;

            var decodeoptions = new PhpSerialization.JsonSerializer.DecodeOptions()
            {
                Assoc = assoc,
                Depth = depth,
                Options = options,
            };

            return new PhpSerialization.JsonSerializer(decodeOptions: decodeoptions).Deserialize(ctx, json, default);
        }

        public static int json_last_error(Context ctx) => PhpSerialization.GetLastJsonError(ctx);

        /// <summary>
        /// Returns the error string of the last <see cref="json_encode"/> or <see cref="json_decode"/> call.
        /// </summary>
        public static string json_last_error_msg(Context ctx)
        {
            switch (PhpSerialization.GetLastJsonError(ctx))
            {
                case JSON_ERROR_NONE: return "No error";
                case JSON_ERROR_DEPTH: return "Maximum stack depth exceeded";
                case JSON_ERROR_STATE_MISMATCH: return "State mismatch (invalid or malformed JSON)";
                case JSON_ERROR_CTRL_CHAR: return "Control character error, possibly incorrectly encoded";
                case JSON_ERROR_SYNTAX: return "Syntax error";
                case JSON_ERROR_UTF8: return "Malformed UTF-8 characters, possibly incorrectly encoded";
                default: throw new ArgumentOutOfRangeException();
            }
        }

        #endregion
    }

    /// <summary>
    /// Exception thrown by <see cref="json_encode"/> and <see cref="json_decode"/> in case of an error,
    /// if <see cref="JSON_THROW_ON_ERROR"/> flag is specified.
    /// </summary>
    [PhpType(PhpTypeAttribute.InheritName)]
    [PhpExtension("json")]
    public class JsonException : Spl.Exception
    {
        [PhpFieldsOnlyCtor]
        protected JsonException() { }

        public JsonException(string message = "", long code = 0, Spl.Throwable previous = null)
            : base(message, code, previous)
        {
        }
    }
}

/// <summary>
/// Objects implementing JsonSerializable can customize their JSON representation when encoded with <see cref="Pchp.Library.JsonSerialization.json_encode"/>.
/// </summary>
[PhpType(PhpTypeAttribute.InheritName)]
[PhpExtension("json")]
public interface JsonSerializable
{
    /// <summary>
    /// Serializes the object to a value that can be serialized natively by <see cref="Pchp.Library.JsonSerialization.json_encode"/>.
    /// </summary>
    /// <returns>
    /// Returns data which can be serialized by <see cref="Pchp.Library.JsonSerialization.json_encode"/>,
    /// which is a value of any type other than a resource.
    /// </returns>
    PhpValue jsonSerialize();
}