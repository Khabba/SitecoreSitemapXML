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

using System.Collections.Generic;
using System.IO;
using System.Xml;

using Sitecore.Data.Items;
using Sitecore.Sites;
using Sitecore.Data;
using Sitecore.Configuration;
using Sitecore.Diagnostics;

using System.Web;
using System.Text;
using System.Linq;
using System.Collections.Specialized;
using System.Collections;

using Sitecore.Globalization;

namespace Sitecore.Modules.SitemapXML
{
    public class SitemapManager
    {
        private static StringDictionary m_Sites;

        public Database Db
        {
            get
            {
                Database database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);
                return database;
            }
        }

        public SitemapManager()
        {
            m_Sites = SitemapManagerConfiguration.GetSites();
            
            foreach (DictionaryEntry site in m_Sites)
            {
                BuildSiteMap(site.Key.ToString(), site.Value.ToString());
            }
        }

        


        private void BuildSiteMap(string sitename, string sitemapUrlNew)
        {
            Site site = SiteManager.GetSite(sitename);
            SiteContext siteContext = Factory.GetSite(sitename);
            string rootPath = siteContext.StartPath;

            if (!string.IsNullOrEmpty(rootPath))
            {
                List<Item> items = GetSitemapItems(rootPath);

                string fullPath = MainUtil.MapPath(string.Concat("/", sitemapUrlNew));
                XmlDocument xmlDocument = this.BuildSitemapXML(items, site);

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

        public bool SubmitSitemapToSearchenginesByHttp()
        {
            if (!SitemapManagerConfiguration.IsProductionEnvironment)
                return false;

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
                        foreach (string sitemapUrl in m_Sites.Values)
                            this.SubmitEngine(engineHttpRequestString, sitemapUrl);
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
            foreach (string sitemapUrl in m_Sites.Values)
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

        private XmlDocument BuildSitemapXML(List<Item> items, Site site)
        {
            var doc = new XmlDocument();

            XmlNode declarationNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(declarationNode);
            XmlNode urlsetNode = doc.CreateElement("urlset", SitemapManagerConfiguration.XmlnsTpl);

            doc.AppendChild(urlsetNode);

            foreach (Item itm in items)
            {
                doc = this.BuildSitemapItem(doc, itm, site);
            }

            return doc;
        }

        private XmlDocument BuildSitemapItem(XmlDocument doc, Item item, Site site)
        {
            string url = HtmlEncode(this.GetItemUrl(item, site));
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
            Sitecore.Links.UrlOptions options = Sitecore.Links.UrlOptions.DefaultOptions;

            if (!string.IsNullOrEmpty(site.Properties["language"]))
            {
                var language = (Language) null;
                Language.TryParse(site.Properties["language"], out language);
                if (!string.IsNullOrEmpty(language.Name))
                {
                    options.Language = language;
                }
            }
            options.SiteResolving = Sitecore.Configuration.Settings.Rendering.SiteResolving;
            options.Site = SiteContext.GetSite(site.Name);
            options.AlwaysIncludeServerUrl = false;

            string url = Sitecore.Links.LinkManager.GetItemUrl(item, options);

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
                    sb.Append(Web.WebUtil.GetFullUrl(url));
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

                System.Net.HttpWebRequest httpRequest =
                    (System.Net.HttpWebRequest) System.Net.HttpWebRequest.Create(request);
                try
                {
                    System.Net.WebResponse webResponse = httpRequest.GetResponse();

                    System.Net.HttpWebResponse httpResponse = (System.Net.HttpWebResponse) webResponse;
                    if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Log.Error(string.Format("Cannot submit sitemap to \"{0}\"", engine), this);
                    }
                }
                catch
                {
                    Log.Warn(string.Format("The serachengine \"{0}\" returns an 404 error", request), this);
                }
            }
        }


        private List<Item> GetSitemapItems(string rootPath)
        {
            string disTpls = SitemapManagerConfiguration.EnabledTemplates;
            string exclNames = SitemapManagerConfiguration.ExcludeItems;


            Database database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);

            Item contentRoot = database.Items[rootPath];

            Item[] descendants;
            Sitecore.Security.Accounts.User user = Sitecore.Security.Accounts.User.FromName(@"extranet\Anonymous", true);
            using (new Sitecore.Security.Accounts.UserSwitcher(user))
            {
                descendants = contentRoot.Axes.GetDescendants();
            }
            List<Item> sitemapItems = descendants.ToList();
            sitemapItems.Insert(0, contentRoot);

            List<string> enabledTemplates = this.BuildListFromString(disTpls, '|');
            List<string> excludedNames = this.BuildListFromString(exclNames, '|');


            var selected = from itm in sitemapItems
                where itm.Template != null && enabledTemplates.Contains(itm.Template.ID.ToString()) &&
                      !excludedNames.Contains(itm.ID.ToString())
                select itm;

            return selected.ToList();
        }

        private List<string> BuildListFromString(string str, char separator)
        {
            string[] enabledTemplates = str.Split(separator);
            var selected = from dtp in enabledTemplates
                where !string.IsNullOrEmpty(dtp)
                select dtp;

            List<string> result = selected.ToList();

            return result;
        }
    }
}