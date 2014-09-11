﻿#region

using System;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Tabster.Core.Data;
using Tabster.Core.Data.Processing;
using Tabster.Core.Types;

#endregion

namespace UltimateGuitar
{
    internal class UltimateGuitarParser : ITablatureWebpageImporter
    {
        #region Implementation of ITabParser

        public string SiteName
        {
            get { return "Ultimate Guitar"; }
        }

        public TablatureDocument Parse(string text, TabType? type)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(text);

            var tabType = type;
            string song = null, artist = null;

            //get values from meta keywords
            var metaNodes = doc.DocumentNode.SelectNodes("/html/head/meta");
            foreach (var mn in metaNodes)
            {
                if (mn.HasAttributes && mn.Attributes.Contains("name") && mn.Attributes["name"].Value == "keywords")
                {
                    var split = mn.Attributes["content"].Value.Split(',');
                    song = split[0].Trim();

                    var typeStr = split[1].Trim();

                    if (!type.HasValue)
                    {
                        if (typeStr.IndexOf("bass", StringComparison.OrdinalIgnoreCase) > -1)
                            tabType = TabType.Bass;
                        else if (typeStr.IndexOf("chord", StringComparison.OrdinalIgnoreCase) > -1)
                            tabType = TabType.Chords;
                        else if (typeStr.IndexOf("drum", StringComparison.OrdinalIgnoreCase) > -1)
                            tabType = TabType.Drum;
                        else if (typeStr.IndexOf("ukulele", StringComparison.OrdinalIgnoreCase) > -1)
                            tabType = TabType.Ukulele;
                    }

                    artist = split[2].Trim();
                    break;
                }
            }

            var contentsNode = doc.DocumentNode.SelectSingleNode("//div[@id='cont']/pre[2]");

            if (contentsNode != null)
            {
                var contents = StripHTML(contentsNode.InnerHtml);
                contents = ConvertNewlines(contents);
                return new TablatureDocument(artist, song, tabType.GetValueOrDefault(), contents);
            }

            return null;
        }

        public TablatureDocument Parse(Uri url, TabType? type)
        {
            throw new NotImplementedException();
        }

        public bool MatchesUrlPattern(Uri url)
        {
            return url.IsWellFormedOriginalString() && ((url.DnsSafeHost == "ultimate-guitar.com" || url.DnsSafeHost == "www.ultimate-guitar.com" ||
                                                         url.DnsSafeHost == "tabs.ultimate-guitar.com") && url.AbsolutePath.Split('/').Length >= 4);
        }

        #endregion

        private static readonly Regex NewlineRegex = new Regex("(?<!\r)\n", RegexOptions.Compiled);

        private static string ConvertNewlines(string source)
        {
            return NewlineRegex.Replace(source, Environment.NewLine);
        }

        private static string StripHTML(string source)
        {
            var array = new char[source.Length];
            var arrayIndex = 0;
            var inside = false;

            for (var i = 0; i < source.Length; i++)
            {
                var let = source[i];

                if (let == '<')
                {
                    inside = true;
                    continue;
                }

                if (let == '>')
                {
                    inside = false;
                    continue;
                }

                if (!inside)
                {
                    array[arrayIndex] = let;
                    arrayIndex++;
                }
            }

            return new string(array, 0, arrayIndex);
        }
    }
}