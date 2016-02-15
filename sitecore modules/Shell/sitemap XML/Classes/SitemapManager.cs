/* *********************************************************************** *
 * File   : SitemapManager.cs                             Part of Sitecore *
 * Version: 1.0.0                                         www.sitecore.net *
 *                                                                         *
 *                                                                         *
 * Purpose: Manager class what contains all main logic                     *
 *                                                                         *
 * Copyright (C) 1999-2009 by Sitecore A/S. All rights reserved.           *
 *                                                                         *
 * This work is the property of:                                           *
 *                                                                         *
 *        Sitecore A/S                                                     *
 *        Meldahlsgade 5, 4.                                               *
 *        1613 Copenhagen V.                                               *
 *        Denmark                                                          *
 *                                                                         *
 * This is a Sitecore published work under Sitecore's                      *
 * shared source license.                                                  *
 *                                                                         *
 * *********************************************************************** */

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;

using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Links;
using Sitecore.Security.Accounts;
using Sitecore.Sites;
using Sitecore.StringExtensions;
using Sitecore.Web;

using Sitemp_XML.sitecore_modules.Shell.sitemap_XML.Classes;

namespace Sitecore.Modules.SitemapXML
{
    public class SitemapManager
    {
        private static List<SiteConfigurationDto> sites;

        public SitemapManager()
        {
            sites = (List<SiteConfigurationDto>) SitemapManagerConfiguration.GetSites();

            foreach (SiteConfigurationDto site in sites)
            {
                
                
                    BuildSiteMap(site.Name, site.FileName, site.ExtraPathToInclude);
                
                
            }
        }

        public Database Db
        {
            get
            {
                Database database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);
                return database;
            }
        }


        private void BuildSiteMap(string sitename, string sitemapUrlNew, string extrapath)
        {

            Site site = SiteManager.GetSite(sitename);
            SiteContext siteContext = Factory.GetSite(sitename);
            using (new LanguageSwitcher(siteContext.Language))
            {
                string rootPath = siteContext.StartPath;

                if (!string.IsNullOrEmpty(rootPath))
                {
                    List<Item> items = GetSitemapItems(rootPath);

                    if (!extrapath.IsNullOrEmpty())
                    {
                        items.AddRange(GetSitemapItems(extrapath));
                    }

                    string fullPath = MainUtil.MapPath(string.Concat("/", sitemapUrlNew));
                    XmlDocument xmlDocument = BuildSitemapXml(items, site);

                    var settings = new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "     ",
                        NewLineOnAttributes = false,
                        OmitXmlDeclaration = true
                    };
                    using (XmlWriter writer = XmlWriter.Create(fullPath, settings))
                    {
                        xmlDocument.Save(writer);
                    }
                }
                else
                {
                    Log.Warn(string.Format("Skipping site {0} no startpath specified", sitename), this);
                }
            }
        }

        public bool SubmitSitemapToSearchenginesByHttp()
        {
            if (!SitemapManagerConfiguration.IsProductionEnvironment)
            {
                return false;
            }

            bool result = false;
            Item sitemapConfig = Db.Items[SitemapManagerConfiguration.SitemapConfigurationItemPath];

            if (sitemapConfig != null)
            {
                string engines = sitemapConfig.Fields["Search engines"].Value;
                foreach (string id in engines.Split('|'))
                {
                    Item engine = Db.Items[id];
                    if (engine != null)
                    {
                        string engineHttpRequestString = engine.Fields["HttpRequestString"].Value;
                        foreach (string sitemapUrl in sites.Select(c => c.FileName))
                        {
                            SubmitEngine(engineHttpRequestString, sitemapUrl);
                        }
                    }
                }
                result = true;
            }

            return result;
        }

        public void RegisterSitemapToRobotsFile()
        {
            string robotsPath = MainUtil.MapPath(string.Concat("/", "robots.txt"));
            var sitemapContent = new StringBuilder(string.Empty);
            if (File.Exists(robotsPath))
            {
                var sr = new StreamReader(robotsPath);
                sitemapContent.Append(sr.ReadToEnd());
                sr.Close();
            }

            var sw = new StreamWriter(robotsPath, false);
            foreach (string sitemapUrl in sites.Select(c => c.FileName))
            {
                string sitemapLine = string.Concat("Sitemap: ", sitemapUrl);
                if (!sitemapContent.ToString().Contains(sitemapLine))
                {
                    sitemapContent.AppendLine(sitemapLine);
                }
            }
            sw.Write(sitemapContent.ToString());
            sw.Close();
        }

        private XmlDocument BuildSitemapXml(List<Item> items, Site site)
        {
            var doc = new XmlDocument();

            XmlNode declarationNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(declarationNode);
            XmlNode urlsetNode = doc.CreateElement("urlset", SitemapManagerConfiguration.XmlnsTpl);

            doc.AppendChild(urlsetNode);

            foreach (Item itm in items)
            {
                doc = BuildSitemapItem(doc, itm, site);
            }

            return doc;
        }

        private XmlDocument BuildSitemapItem(XmlDocument doc, Item item, Site site)
        {
            string url = GetItemUrl(item, site);
            string lastMod = HtmlEncode(item.Statistics.Updated.ToString("yyyy-MM-ddTHH:mm:sszzz"));

            XmlNode urlsetNode = doc.LastChild;

            XmlNode urlNode = doc.CreateElement("url", SitemapManagerConfiguration.XmlnsTpl);
            urlsetNode.AppendChild(urlNode);

            XmlNode locNode = doc.CreateElement("loc", SitemapManagerConfiguration.XmlnsTpl);
            urlNode.AppendChild(locNode);
            locNode.AppendChild(doc.CreateTextNode(url));

            XmlNode lastmodNode = doc.CreateElement("lastmod", SitemapManagerConfiguration.XmlnsTpl);
            urlNode.AppendChild(lastmodNode);
            lastmodNode.AppendChild(doc.CreateTextNode(lastMod));

            return doc;
        }

        private string GetItemUrl(Item item, Site site)
        {
            UrlOptions options = UrlOptions.DefaultOptions;

            if (!string.IsNullOrEmpty(site.Properties["language"]))
            {
                var language = (Language) null;
                Language.TryParse(site.Properties["language"], out language);
                if (!string.IsNullOrEmpty(language.Name))
                {
                    options.Language = language;
                }
            }
            options.SiteResolving = Settings.Rendering.SiteResolving;
            options.Site = SiteContext.GetSite(site.Name);
            options.AlwaysIncludeServerUrl = false;
            
            string url = LinkManager.GetItemUrl(item, options);

            string serverUrl = SitemapManagerConfiguration.GetServerUrlBySite(site.Name);
            if (serverUrl.Contains("http://"))
            {
                serverUrl = serverUrl.Substring("http://".Length);
            }

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(serverUrl))
            {
                if (url.Contains("://") && !url.Contains("http"))
                {
                    sb.Append("http://");
                    sb.Append(serverUrl);
                    if (url.IndexOf("/", 3) > 0)
                        sb.Append(url.Substring(url.IndexOf("/", 3)));
                }
                else
                {
                    sb.Append("http://");
                    sb.Append(serverUrl);
                    sb.Append(url);
                }
            }
            else if (!string.IsNullOrEmpty(site.Properties["hostname"]))
            {
                sb.Append("http://");
                sb.Append(site.Properties["hostname"]);
                sb.Append(url);
            }
            else
            {
                if (url.Contains("://") && !url.Contains("http"))
                {
                    sb.Append("http://");
                    sb.Append(url);
                }
                else
                {
                    sb.Append(WebUtil.GetFullUrl(url));
                }
            }

            return sb.ToString();
        }

        private static string HtmlEncode(string text)
        {
            string result = HttpUtility.HtmlEncode(text);

            return result;
        }

        private void SubmitEngine(string engine, string sitemapUrl)
        {
            //Check if it is not localhost because search engines returns an error
            if (!sitemapUrl.Contains("http://localhost"))
            {
                string request = string.Concat(engine, HtmlEncode(sitemapUrl));

                var httpRequest =
                    (HttpWebRequest) WebRequest.Create(request);
                try
                {
                    WebResponse webResponse = httpRequest.GetResponse();

                    var httpResponse = (HttpWebResponse) webResponse;
                    if (httpResponse.StatusCode != HttpStatusCode.OK)
                    {
                        Log.Error(string.Format("Cannot submit sitemap to \"{0}\"", engine), this);
                    }
                }
                catch
                {
                    Log.Warn(string.Format("The searchengine \"{0}\" returns an 404 error", request), this);
                }
            }
        }


        private List<Item> GetSitemapItems(string path)
        {
            string disTpls = SitemapManagerConfiguration.EnabledTemplates;
            string exclNames = SitemapManagerConfiguration.ExcludeItems;
            string exclQuery = SitemapManagerConfiguration.ExcludeByQuery;
            
            Database database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);
            
            Item contentRoot = database.Items[path];

            Item[] descendants;
            User user = User.FromName(@"extranet\Anonymous", true);
            using (new UserSwitcher(user))
            {
                descendants = contentRoot.Axes.GetDescendants();
            }
            List<Item> sitemapItems = descendants.ToList();
            sitemapItems.Insert(0, contentRoot);

            List<string> enabledTemplates = BuildListFromString(disTpls, '|');
            List<string> excludedNames = BuildListFromString(exclNames, '|');


            IEnumerable<Item> selected = from itm in sitemapItems
                where itm.Template != null && enabledTemplates.Contains(itm.Template.ID.ToString()) &&
                      !excludedNames.Contains(itm.ID.ToString())
                select itm;

            if (!string.IsNullOrEmpty(exclQuery) && exclQuery.StartsWith("self::"))
            {
                selected = from itm in selected
                           where itm.Axes.SelectSingleItem(exclQuery) == null
                           select itm;
            }

            return selected.ToList();
        }

        private List<string> BuildListFromString(string str, char separator)
        {
            string[] enabledTemplates = str.Split(separator);
            IEnumerable<string> selected = from dtp in enabledTemplates
                where !string.IsNullOrEmpty(dtp)
                select dtp;

            List<string> result = selected.ToList();

            return result;
        }
    }
}