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
using Sitecore.Resources.Media;
using Sitecore.Security.Accounts;
using Sitecore.Sites;
using Sitecore.StringExtensions;
using Sitecore.Web;

using Sitemp_XML.sitecore_modules.Shell.sitemap_XML.Classes;

namespace Sitecore.Modules.SitemapXML
{
    public class ImagesSitemapManager
    {
        private static List<SiteConfigurationDto> sites;

        public ImagesSitemapManager()
        {
            sites = (List<SiteConfigurationDto>) SitemapManagerConfiguration.GetSites();

            foreach (SiteConfigurationDto site in sites)
            {
               BuildImagesSiteMap(site);
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

        private void BuildImagesSiteMap(SiteConfigurationDto siteConfigurationDto)
        {

            Site site = SiteManager.GetSite(siteConfigurationDto.Name);
            SiteContext siteContext = Factory.GetSite(siteConfigurationDto.Name);
            using (new LanguageSwitcher(siteContext.Language))
            {
                string rootPath = siteContext.StartPath;

                if (!string.IsNullOrEmpty(rootPath))
                {
                    var imagesSitemapList = new List<ImagesSitemapDto>();
                    List<Item> items = GetSitemapItems(rootPath);

                    if (!siteConfigurationDto.ExtraPathToInclude.IsNullOrEmpty())
                    {
                        items.AddRange(GetSitemapItems(siteConfigurationDto.ExtraPathToInclude));
                    }
                    
                    foreach (var sitemapItem in items)
                    {
                        var imageSitemapDto = new ImagesSitemapDto();
                        imageSitemapDto.SitemapItem = sitemapItem;

                        AddMediaItem(sitemapItem, imageSitemapDto, siteConfigurationDto, 0);

                        imagesSitemapList.Add(imageSitemapDto);
                    }

                    string fullPath = MainUtil.MapPath(string.Concat("/", siteConfigurationDto.ImageSitemapFileName));
                    XmlDocument xmlDocument = BuildImagesSitemapXml(imagesSitemapList, site);

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
                    Log.Warn(string.Format("Skipping site {0} no startpath specified", siteConfigurationDto.Name), this);
                }
            }
        }

        private void AddMediaItem(Item contentItem, ImagesSitemapDto imagesSitemapDto, SiteConfigurationDto siteConfigurationDto, int depth)
        {
            var list = Globals.LinkDatabase.GetItemReferences(contentItem, false);

            var componentFolderDescendants = GetComponentFolderDescendants(siteConfigurationDto);

            var mediapath = siteConfigurationDto.MediaPath;

            if (string.IsNullOrEmpty(mediapath))
            {
                return;
            }

            foreach (var itemLink in list)
            {
                if (itemLink.TargetPath.Contains(mediapath))
                {
                    imagesSitemapDto.ImagesSitemapItems.Add(itemLink.GetTargetItem());
                }

                else if (componentFolderDescendants.Any(x => x.ID == itemLink.TargetItemID))
                {
                    if (depth > 2)
                    {
                        return;
                    }
                    AddMediaItem(itemLink.GetTargetItem(), imagesSitemapDto, siteConfigurationDto, depth + 1);
                }
            }
        }

        private List<Item> GetComponentFolderDescendants(SiteConfigurationDto siteConfigurationDto)
        {
            var componentFolderDescendants = new List<Item>();

            if (!componentFolderDescendants.Any())
            {
                var componentspath = siteConfigurationDto.ComponentsFolderPath;
                if (string.IsNullOrEmpty(componentspath))
                {
                    return componentFolderDescendants;
                }

                Item componentFolderRoot = Db.Items[componentspath];

                Item[] componentFolderDescendents;
                User user = Sitecore.Security.Accounts.User.FromName(@"extranet\Anonymous", true);
                using (new UserSwitcher(user))
                {
                    componentFolderDescendents = componentFolderRoot.Axes.GetDescendants();
                }
                componentFolderDescendants.AddRange(componentFolderDescendents.ToList());
            }
            return componentFolderDescendants;
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
                        foreach (string sitemapUrl in sites.Select(c => c.ImageSitemapFileName))
                        {
                            SubmitEngine(engineHttpRequestString, sitemapUrl);
                        }
                    }
                }
                result = true;
            }

            return result;
        }

        private XmlDocument BuildImagesSitemapXml(List<ImagesSitemapDto> imagesSitemapDtos, Site site)
        {
            var doc = new XmlDocument();
            XmlNode declarationNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(declarationNode);

            XmlElement urlsetElement = doc.CreateElement("urlset", SitemapManagerConfiguration.XmlnsTpl);
            urlsetElement.SetAttribute("xmlns:image", SitemapManagerConfiguration.XmlnsImg);

            doc.AppendChild(urlsetElement);
            foreach (ImagesSitemapDto itm in imagesSitemapDtos.Where(x => x.ImagesSitemapItems.Any()))
            {
                doc = BuildSitemapItem(doc, itm, site);
            }

            return doc;
        }

        private XmlDocument BuildSitemapItem(XmlDocument doc, ImagesSitemapDto imagesSitemapDto, Site site)
        {
            string url = GetItemUrl(imagesSitemapDto.SitemapItem, site);

            XmlNode urlsetNode = doc.LastChild;

            XmlElement urlNode = doc.CreateElement("url");
            urlsetNode.AppendChild(urlNode);

            XmlElement locNode = doc.CreateElement("loc");
            urlNode.AppendChild(locNode);
            locNode.AppendChild(doc.CreateTextNode(url));

            foreach (var imageSiteMapItem in imagesSitemapDto.ImagesSitemapItems.Distinct())
            {
                var itemImageUrl = GetMediaItemUrl(imageSiteMapItem, site);

                XmlNode imageNode = doc.CreateElement("image:image", SitemapManagerConfiguration.XmlnsImg);
                urlNode.AppendChild(imageNode);
                XmlNode imageLocNode = doc.CreateElement("image:loc", SitemapManagerConfiguration.XmlnsImg);
                imageNode.AppendChild(imageLocNode);
                imageLocNode.AppendChild(doc.CreateTextNode(itemImageUrl));
            }

            return doc;
        }

        private string GetMediaItemUrl(Item item, Site site)
        {
            MediaUrlOptions options = MediaUrlOptions.Empty;

            if (!string.IsNullOrEmpty(site.Properties["language"]))
            {
                var language = (Language) null;
                Language.TryParse(site.Properties["language"], out language);
                if (!string.IsNullOrEmpty(language.Name))
                {
                    options.Language = language;
                }
            }
            options.AlwaysIncludeServerUrl = false;
            
            string url = MediaManager.GetMediaUrl(item, options);

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

        private string GetItemUrl(Item item, Site site)
        {
            UrlOptions options = UrlOptions.DefaultOptions;

            if (!string.IsNullOrEmpty(site.Properties["language"]))
            {
                var language = (Language)null;
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