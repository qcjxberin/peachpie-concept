﻿using Pchp.Core;
using Pchp.Library.PerlRegex;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pchp.Library.Resources;

namespace Pchp.Library
{
    /// <summary>
    /// .NET implementation of Perl-Compatible Regular Expressions.
    /// </summary>
    /// <remarks>
    /// You should be aware of limitations of this implementation.
    /// The .NET implementation of PCRE does not provide the same behavior, the notes will be updated.
    /// </remarks>
    [PhpExtension("pcre")]
    public static class PCRE
    {
        #region Constants

        /// <summary>
        /// Orders results so that
        /// $matches[0] is an array of full pattern matches,
        /// $matches[1] is an array of strings matched by the first parenthesized subpattern,
        /// and so on.
        /// 
        /// This flag is only used with preg_match_all().	
        /// </summary>
        public const int PREG_PATTERN_ORDER = 1;

        /// <summary>
        /// Orders results so that
        /// $matches[0] is an array of first set of matches,
        /// $matches[1] is an array of second set of matches,
        /// and so on.
        /// 
        /// This flag is only used with preg_match_all().	
        /// </summary>
        public const int PREG_SET_ORDER = 2;

        /// <summary>
        /// <see cref="PREG_SPLIT_OFFSET_CAPTURE"/>.
        /// </summary>
        public const int PREG_OFFSET_CAPTURE = 256;

        /// <summary>
        /// This flag tells preg_split() to return only non-empty pieces.
        /// </summary>
        public const int PREG_SPLIT_NO_EMPTY = 1;

        /// <summary>
        /// This flag tells preg_split() to capture parenthesized expression in the delimiter pattern as well.
        /// </summary>
        public const int PREG_SPLIT_DELIM_CAPTURE = 2;

        /// <summary>
        /// If this flag is set, for every occurring match the appendant string offset will also be returned.
        /// Note that this changes the return values in an array where every element is an array consisting of the matched string at offset 0 and
        /// its string offset within subject at offset 1.
        /// This flag is only used for preg_split().	
        /// </summary>
        public const int PREG_SPLIT_OFFSET_CAPTURE = 4;

        public const int PREG_REPLACE_EVAL = 1;

        public const int PREG_GREP_INVERT = 1;

        public const int PREG_NO_ERROR = 0;
        public const int PREG_INTERNAL_ERROR = 1;
        public const int PREG_BACKTRACK_LIMIT_ERROR = 2;
        public const int PREG_RECURSION_LIMIT_ERROR = 3;
        public const int PREG_BAD_UTF8_ERROR = 4;
        public const int PREG_BAD_UTF8_OFFSET_ERROR = 5;
        public const int PREG_JIT_STACKLIMIT_ERROR = 6;

        /// <summary>PCRE version and release date</summary>
        public const string PCRE_VERSION = "7.2 .NET";

        #endregion

        #region Function stubs

        public static int preg_last_error()
        {
            return 0;
        }

        /// <summary>
        /// Return array entries that match the pattern.
        /// </summary>
        /// <param name="ctx">Current context. Cannot be <c>null</c>.</param>
        /// <param name="pattern">The pattern to search for.</param>
        /// <param name="input">The input array.</param>
        /// <param name="flags">If set to <see cref="PREG_GREP_INVERT"/>, this function returns the elements of the input array that do not match the given pattern.</param>
        /// <returns>Returns an array indexed using the keys from the input array.</returns>
        [return: CastToFalse]
        public static PhpArray preg_grep(Context ctx, string pattern, PhpArray input, int flags = 0)
        {
            if (input == null)
            {
                return null;
            }

            var result = new PhpArray(input.Count);

            if (input.Count != 0)
            {
                var regex = new PerlRegex.Regex(pattern);

                var enumerator = input.GetFastEnumerator();
                while (enumerator.MoveNext())
                {
                    var str = enumerator.CurrentValue.ToStringOrThrow(ctx);
                    var m = regex.Match(str);

                    // move a copy to return array if success and not invert or
                    // not success and invert
                    if (m.Success ^ (flags & PREG_GREP_INVERT) != 0)
                    {
                        result.Add(enumerator.CurrentKey, enumerator.CurrentValue.DeepCopy());
                    }
                }
            }

            //
            return result;
        }

        public static PhpValue preg_replace(Context ctx, PhpValue pattern, PhpValue replacement, PhpValue subject, long limit = -1)
            => preg_replace(ctx, pattern, replacement, subject, limit, out long count);

        /// <summary>
        /// Perform a regular expression search and replace.
        /// </summary>
        /// <param name="ctx">A reference to current context. Cannot be <c>null</c>.</param>
        /// <param name="pattern">The pattern to search for. It can be either a string or an array with strings.</param>
        /// <param name="replacement">The string or an array with strings to replace.
        /// If this parameter is a string and the pattern parameter is an array, all patterns will be
        /// replaced by that string. If both pattern and replacement parameters are arrays, each pattern will be
        /// replaced by the replacement counterpart. If there are fewer elements in the replacement array than
        /// in the pattern array, any extra patterns will be replaced by an empty string.</param>
        /// <param name="subject">The string or an array with strings to search and replace.
        /// If subject is an array, then the search and replace is performed on every entry of subject, and the return value is an array as well.</param>
        /// <param name="limit">The maximum possible replacements for each pattern in each subject string. Defaults to <c>-1</c> (no limit).</param>
        /// <param name="count">This variable will be filled with the number of replacements done.</param>
        /// <returns></returns>
        public static PhpValue preg_replace(Context ctx, PhpValue pattern, PhpValue replacement, PhpValue subject, long limit, out long count)
        {
            count = 0;

            // PHP's behaviour for undocumented limit range
            if (limit < -1)
            {
                limit = 0;
            }

            //
            var replacement_array = replacement.AsArray();
            var pattern_array = pattern.AsArray();

            if (pattern_array == null)
            {
                if (replacement_array == null)
                {
                    // string pattern
                    // string replacement

                    return PregReplaceInternal(ctx, pattern.ToStringOrThrow(ctx), replacement.ToStringOrThrow(ctx), null, subject, (int)limit, ref count);
                }
                else
                {
                    // string pattern and array replacement not allowed:
                    PhpException.InvalidArgument(nameof(replacement), LibResources.replacement_array_pattern_not);
                    return PhpValue.Null;
                }
            }
            else if (replacement_array == null)
            {
                // array  pattern
                // string replacement

                using (var pattern_enumerator = pattern_array.GetFastEnumerator())
                    while (pattern_enumerator.MoveNext())
                    {
                        subject = PregReplaceInternal(ctx, pattern_enumerator.CurrentValue.ToStringOrThrow(ctx), replacement.ToStringOrThrow(ctx),
                            null, subject, (int)limit, ref count);
                    }

                //
                return subject;
            }
            else
            {
                // array pattern
                // array replacement

                var replacement_enumerator = replacement_array.GetFastEnumerator();

                bool replacement_valid = true;
                string replacement_string;

                using (var pattern_enumerator = pattern_array.GetFastEnumerator())
                    while (pattern_enumerator.MoveNext())
                    {
                        // replacements are in array, move to next item and take it if possible, in other case take empty string:
                        if (replacement_valid && replacement_enumerator.MoveNext())
                        {
                            replacement_string = replacement_enumerator.CurrentValue.ToStringOrThrow(ctx);
                        }
                        else
                        {
                            replacement_string = string.Empty;
                            replacement_valid = false;  // end of replacement_enumerator, do not call MoveNext again!
                        }

                        subject = PregReplaceInternal(ctx, pattern_enumerator.CurrentValue.ToStringOrThrow(ctx), replacement_string,
                            null, subject, (int)limit, ref count);
                    }

                //
                return subject;
            }
        }

        public static PhpValue preg_replace_callback(Context ctx, PhpValue pattern, IPhpCallable callback, PhpValue subject, long limit = -1)
        {
            long count = 0;
            return preg_replace_callback(ctx, pattern, callback, subject, limit, ref count);
        }

        public static PhpValue preg_replace_callback(Context ctx, PhpValue pattern, IPhpCallable callback, PhpValue subject, long limit, ref long count)
        {
            count = 0;

            // PHP's behaviour for undocumented limit range
            if (limit < -1)
            {
                limit = 0;
            }

            //
            var pattern_array = pattern.AsArray();

            if (pattern_array == null)
            {
                // string pattern
                return PregReplaceInternal(ctx, pattern.ToStringOrThrow(ctx), null, callback, subject, (int)limit, ref count);
            }
            else
            {
                // array pattern
            }

            throw new NotImplementedException();
        }

        public static PhpValue preg_replace_callback_array(Context ctx, PhpArray patterns_and_callbacks, PhpValue subject, long limit = -1)
        {
            long count;
            return preg_replace_callback_array(ctx, patterns_and_callbacks, subject, limit, out count);
        }

        /// <summary>
        /// Perform a regular expression search and replace using callbacks.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="patterns_and_callbacks">An associative array mapping patterns (keys) to callbacks (values).</param>
        /// <param name="subject">The string or an array with strings to search and replace.</param>
        /// <param name="limit">The maximum possible replacements for each pattern in each subject string. Defaults to -1 (no limit).</param>
        /// <param name="count">If specified, this variable will be filled with the number of replacements done.</param>
        /// <returns>
        /// preg_replace_callback_array() returns an array if the subject parameter is an array, or a string otherwise. On errors the return value is NULL.
        /// If matches are found, the new subject will be returned, otherwise subject will be returned unchanged.
        /// </returns>
        public static PhpValue preg_replace_callback_array(Context ctx, PhpArray patterns_and_callbacks, PhpValue subject, long limit, out long count)
        {
            if (patterns_and_callbacks == null)
            {
                throw new ArgumentNullException(nameof(patterns_and_callbacks));
            }

            count = 0;

            var enumerator = patterns_and_callbacks.GetFastEnumerator();
            while (enumerator.MoveNext())
            {
                var pattern = enumerator.CurrentKey.String ?? throw new ArgumentException();
                var callback = enumerator.CurrentValue.AsCallable();

                subject = PregReplaceInternal(ctx, pattern, null, callback, subject, (int)limit, ref count);
            }

            //
            return subject;
        }

        static PhpValue PregReplaceInternal(Context ctx, string pattern, string replacement, IPhpCallable callback, PhpValue subject, int limit, ref long count)
        {
            var regex = new PerlRegex.Regex(pattern);

            // callback
            PerlRegex.MatchEvaluator evaluator = null;
            if (callback != null)
            {
                evaluator = (match) =>
                {
                    var matches_arr = new PhpArray(0);
                    GroupsToPhpArray(match.PcreGroups, false, matches_arr);

                    return callback
                        .Invoke(ctx, (PhpValue)matches_arr)
                        .ToStringOrThrow(ctx);
                };
            }

            // TODO: subject as a binary string would be corrupted after Replace - https://github.com/peachpiecompiler/peachpie/issues/178

            //
            var subject_array = subject.AsArray();
            if (subject_array == null)
            {
                return PhpValue.Create(
                    evaluator == null
                        ? regex.Replace(subject.ToStringOrThrow(ctx), replacement, limit, ref count)
                        : regex.Replace(subject.ToStringOrThrow(ctx), evaluator, limit, ref count));
            }
            else
            {
                var arr = new PhpArray(subject_array.Count);

                var enumerator = subject_array.GetFastEnumerator();
                while (enumerator.MoveNext())
                {
                    var newvalue = evaluator == null
                        ? regex.Replace(enumerator.CurrentValue.ToStringOrThrow(ctx), replacement, limit, ref count)
                        : regex.Replace(enumerator.CurrentValue.ToStringOrThrow(ctx), evaluator, limit, ref count);

                    // TODO: trick on how to quickly update values od enumerated array without hashing:
                    // enumerator.CurrentValue = PhpValue.Create(newvalue);

                    arr[enumerator.CurrentKey] = newvalue;
                }

                return PhpValue.Create(arr);
            }
        }

        [return: CastToFalse]
        public static int preg_match_all(Context ctx, string pattern, string subject)
        {
            PhpArray matches;
            return preg_match_all(ctx, pattern, subject, out matches);
        }

        /// <summary>
        /// Perform a global regular expression match.
        /// </summary>
        [return: CastToFalse]
        public static int preg_match_all(Context ctx, string pattern, string subject, out PhpArray matches, int flags = PREG_PATTERN_ORDER, int offset = 0)
        {
            return Match(ctx, pattern, subject, out matches, flags, offset, true);
        }

        /// <summary>
        /// Perform a regular expression match.
        /// </summary>
        [return: CastToFalse]
        public static int preg_match(Context ctx, string pattern, string subject)
        {
            var regex = new PerlRegex.Regex(pattern);
            return regex.Match(subject ?? string.Empty).Success ? 1 : 0;
        }

        /// <summary>
        /// Perform a regular expression match.
        /// </summary>
        [return: CastToFalse]
        public static int preg_match(Context ctx, string pattern, string subject, out PhpArray matches, int flags = 0, long offset = 0)
        {
            return Match(ctx, pattern, subject, out matches, flags, offset, false);
        }

        /// <summary>
        /// Perform a regular expression match.
        /// </summary>
        static int Match(Context ctx, string pattern, string subject, out PhpArray matches, int flags, long offset, bool matchAll)
        {
            subject = subject ?? string.Empty;

            var regex = new PerlRegex.Regex(pattern);
            var m = regex.Match(subject, (offset < subject.Length) ? (int)offset : subject.Length);

            if ((regex.Options & PerlRegex.RegexOptions.PCRE_ANCHORED) != 0 && m.Success && m.Index != offset)
            {
                matches = PhpArray.NewEmpty();
                return -1;
            }

            if (m.Success)
            {
                if (!matchAll || (flags & PREG_PATTERN_ORDER) != 0)
                {
                    matches = new PhpArray(m.Groups.Count);
                }
                else
                {
                    matches = new PhpArray();
                }

                if (!matchAll)
                {
                    GroupsToPhpArray(m.PcreGroups, (flags & PREG_OFFSET_CAPTURE) != 0, matches);
                    return 1;
                }

                // store all other matches in PhpArray matches
                if ((flags & PREG_SET_ORDER) != 0) // cannot test PatternOrder, it is 0, SetOrder must be tested
                    return FillMatchesArrayAllSetOrder(regex, m, ref matches, (flags & PREG_OFFSET_CAPTURE) != 0);
                else
                    return FillMatchesArrayAllPatternOrder(regex, m, ref matches, (flags & PREG_OFFSET_CAPTURE) != 0);
            }

            // no match has been found
            if (matchAll && (flags & PREG_SET_ORDER) == 0)
            {
                // in that case PHP returns an array filled with empty arrays according to parentheses count
                matches = new PhpArray(m.Groups.Count);
                for (int i = 0; i < regex.GetGroupNumbers().Length; i++)
                {
                    AddGroupNameToResult(regex, matches, i, (ms, groupName) =>
                    {
                        ms[groupName] = (PhpValue)new PhpArray();
                    });

                    matches[i] = (PhpValue)new PhpArray();
                }
            }
            else
            {
                matches = PhpArray.NewEmpty(); // empty array
            }

            return 0;
        }

        /// <summary>
        /// Quote regular expression characters.
        /// </summary>
        /// <remarks>
        /// The special regular expression characters are: . \ + * ? [ ^ ] $ ( ) { } = ! &lt; &gt; | : -
        /// Note that / is not a special regular expression character.
        /// </remarks>
        /// <param name="str">The string to be escaped.</param>
        /// <param name="delimiter">If the optional delimiter is specified, it will also be escaped.
        /// This is useful for escaping the delimiter that is required by the PCRE functions. The / is the most commonly used delimiter.</param>
        public static string preg_quote(string str, string delimiter = null)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            char delimiterChar = string.IsNullOrEmpty(delimiter)
                ? char.MaxValue // unused (?)
                : delimiter[0];

            StringBuilder result = null;
            int lastEscape = 0;

            for (int i = 0; i < str.Length; i++)
            {
                char ch = str[i];
                bool escape = ch == delimiterChar || PerlRegex.RegexParser.IsDelimiterChar(ch);

                if (escape)
                {
                    if (result == null)
                    {
                        result = new StringBuilder(str.Length + 4);
                    }

                    result.Append(str, lastEscape, i - lastEscape);
                    result.Append('\\');
                    lastEscape = i;
                }
            }

            if (result != null)
            {
                result.Append(str, lastEscape, str.Length - lastEscape);
                return result.ToString();
            }
            else
            {
                return str;
            }
        }

        /// <summary>
        /// Splits <paramref name="subject"/> along boundaries matched by <paramref name="pattern"/> and returns an array containing substrings.
        /// 
        /// <paramref name="limit"/> specifies the maximum number of strings returned in the resulting array. If (limit-1) matches is found
        /// and there remain some characters to match whole remaining string is returned as the last element of the array.
        /// 
        /// Some flags may be specified. <see cref="PREG_SPLIT_NO_EMPTY"/> means no empty strings will be
        /// in the resulting array. <see cref="PREG_SPLIT_DELIM_CAPTURE"/> adds also substrings matching
        /// the delimiter and <see cref="PREG_SPLIT_OFFSET_CAPTURE"/> returns instead substrings the arrays
        /// containing appropriate substring at index 0 and the offset of this substring in original
        /// <paramref name="subject"/> at index 1.
        /// </summary>
        /// <param name="pattern">Regular expression to match to boundaries.</param>
        /// <param name="subject">String or string of bytes to split.</param>
        /// <param name="limit">Max number of elements in the resulting array.</param>
        /// <param name="flags">Flags affecting the returned array.</param>
        /// <returns>An array containing substrings.</returns>
        [return: CastToFalse]
        public static PhpArray preg_split(string pattern, string subject, int limit = -1, int flags = 0)
        {
            if (limit == 0) // 0 does not make sense, php's behavior is as it is -1
                limit = -1;
            if (limit < -1) // for all other negative values it seems that is as limit == 1
                limit = 1;

            var regex = new PerlRegex.Regex(pattern);
            //if (!regex.IsValid) return null;

            var m = regex.Match(subject);

            bool offset_capture = (flags & PREG_SPLIT_OFFSET_CAPTURE) != 0;
            PhpArray result = new PhpArray();
            int last_index = 0;

            while (m.Success && (limit == -1 || --limit > 0) && last_index < subject.Length)
            {
                // add part before match
                int length = m.Index - last_index;
                if (length > 0 || (flags & PREG_SPLIT_NO_EMPTY) == 0)
                    result.Add(NewArrayItem(subject.Substring(last_index, length), last_index, offset_capture));

                if (m.Value.Length > 0)
                {
                    if ((flags & PREG_SPLIT_DELIM_CAPTURE) != 0) // add all captures but not whole pattern match (start at 1)
                    {
                        List<object> lastUnsucessfulGroups = null;  // value of groups that was not successful since last succesful one
                        for (int i = 1; i < m.Groups.Count; i++)
                        {
                            Group g = m.Groups[i];
                            if (g.Length > 0 || (flags & PREG_SPLIT_NO_EMPTY) == 0)
                            {
                                // the value to be added into the result:
                                object value = NewArrayItem(g.Value, g.Index, offset_capture);

                                if (g.Success)
                                {
                                    // group {i} was matched:
                                    // if there was some unsuccesfull matches before, add them now:
                                    if (lastUnsucessfulGroups != null && lastUnsucessfulGroups.Count > 0)
                                    {
                                        foreach (var x in lastUnsucessfulGroups)
                                            result.Add(x);
                                        lastUnsucessfulGroups.Clear();
                                    }
                                    // add the matched group:
                                    result.Add(value);
                                }
                                else
                                {
                                    // The match was unsuccesful, remember all the unsuccesful matches
                                    // and add them only if some succesful match will follow.
                                    // In PHP, unsuccessfully matched groups are trimmed by the end
                                    // (regexp processing stops when other groups cannot be matched):
                                    if (lastUnsucessfulGroups == null) lastUnsucessfulGroups = new List<object>();
                                    lastUnsucessfulGroups.Add(value);
                                }
                            }
                        }
                    }

                    last_index = m.Index + m.Length;
                }
                else // regular expression match an empty string => add one character
                {
                    // always not empty
                    result.Add(NewArrayItem(subject.Substring(last_index, 1), last_index, offset_capture));
                    last_index++;
                }

                m = m.NextMatch();
            }

            // add remaining string (might be empty)
            if (last_index < subject.Length || (flags & PREG_SPLIT_NO_EMPTY) == 0)
                result.Add(NewArrayItem(subject.Substring(last_index), last_index, offset_capture));

            return result;
        }

        #endregion

        static void AddGroupNameToResult(Regex regex, PhpArray matches, int i, Action<PhpArray, string> action)
        {
            var groupName = GetGroupName(regex, i);
            if (!string.IsNullOrEmpty(groupName))
            {
                action(matches, groupName);
            }
        }

        /// <summary>
        /// Goes through <paramref name="m"/> matches and fill <paramref name="matches"/> array with results
        /// according to Pattern Order.
        /// </summary>
        /// <param name="r"><see cref="Regex"/> that produced the match</param>
        /// <param name="m"><see cref="Match"/> to iterate through all matches by NextMatch() call.</param>
        /// <param name="matches">Array for storing results.</param>
        /// <param name="addOffsets">Whether or not add arrays with offsets instead of strings.</param>
        /// <returns>Number of full pattern matches.</returns>
        static int FillMatchesArrayAllPatternOrder(Regex r, Match m, ref PhpArray matches, bool addOffsets)
        {
            // second index, increases at each match in pattern order
            int j = 0;
            while (m.Success)
            {
                // add all groups
                for (int i = 0; i < m.Groups.Count; i++)
                {
                    var arr = NewArrayItem(m.Groups[i].Value, m.Groups[i].Index, addOffsets);

                    AddGroupNameToResult(r, matches, i, (ms, groupName) =>
                    {
                        if (j == 0) ms[groupName] = (PhpValue)new PhpArray();
                        ((PhpArray)ms[groupName])[j] = arr;
                    });

                    if (j == 0) matches[i] = (PhpValue)new PhpArray();
                    ((PhpArray)matches[i])[j] = arr;
                }

                j++;
                m = m.NextMatch();
            }

            return j;
        }

        /// <summary>
        /// Goes through <paramref name="m"/> matches and fill <paramref name="matches"/> array with results
        /// according to Set Order.
        /// </summary>
        /// <param name="r"><see cref="Regex"/> that produced the match</param>
        /// <param name="m"><see cref="Match"/> to iterate through all matches by NextMatch() call.</param>
        /// <param name="matches">Array for storing results.</param>
        /// <param name="addOffsets">Whether or not add arrays with offsets instead of strings.</param>
        /// <returns>Number of full pattern matches.</returns>
        static int FillMatchesArrayAllSetOrder(Regex r, Match m, ref PhpArray matches, bool addOffsets)
        {
            // first index, increases at each match in set order
            int i = 0;

            while (m.Success)
            {
                var pa = new PhpArray(m.Groups.Count);

                // add all groups
                for (int j = 0; j < m.Groups.Count; j++)
                {
                    var arr = NewArrayItem(m.Groups[j].Value, m.Groups[j].Index, addOffsets);

                    AddGroupNameToResult(r, pa, j, (p, groupName) =>
                    {
                        p[groupName] = arr;
                    });

                    pa[j] = arr;
                }

                matches[i] = (PhpValue)pa;
                i++;
                m = m.NextMatch();
            }

            return i;
        }

        static int GetLastSuccessfulGroup(GroupCollection/*!*/ groups)
        {
            Debug.Assert(groups != null);

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (groups[i].Success)
                    return i;
            }

            return -1;
        }

        static string GetGroupName(Regex regex, int index)
        {
            var name = regex.GroupNameFromNumber(index);

            // anonymous groups and indexed groups:
            if (string.IsNullOrEmpty(name) || name.Equals(index.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat)))
            {
                name = null;
            }

            return name;
        }

        /// <summary>
        /// Used for handling Offset Capture flags. Returns just <paramref name="item"/> if
        /// <paramref name="offsetCapture"/> is <B>false</B> or an <see cref="PhpArray"/> containing
        /// <paramref name="item"/> at index 0 and <paramref name="index"/> at index 1.
        /// </summary>
        /// <param name="item">Item to add to return value.</param>
        /// <param name="index">Index to specify in return value if <paramref name="offsetCapture"/> is
        /// <B>true</B>.</param>
        /// <param name="offsetCapture">Whether or not to make <see cref="PhpArray"/> with item and index.</param>
        /// <returns></returns>
        static PhpValue NewArrayItem(string item, int index, bool offsetCapture)
        {
            if (!offsetCapture)
            {
                return (PhpValue)item;
            }

            var arr = new PhpArray(2);
            arr.AddValue((PhpValue)item);
            arr.AddValue((PhpValue)index);
            return (PhpValue)arr;
        }

        static void GroupsToPhpArray(PcreGroupCollection groups, bool offsetCapture, PhpArray result)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                var item = NewArrayItem(g.Value, g.Index, offsetCapture);

                // All groups should be named.
                if (g.IsNamedGroup)
                {
                    result[g.Name] = item.DeepCopy();
                }

                result[i] = item;
            }
        }
    }
}
