/* *********************************************************************** *
 * File   : SitemapManagerConfiguration.cs                Part of Sitecore *
 * Version: 1.0.0                                         www.sitecore.net *
 *                                                                         *
 *                                                                         *
 * Purpose: Class for getting config information from db and conf file     *
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
using System.Linq;
using System.Xml;

using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Sites;
using Sitecore.Xml;

using Sitemp_XML.sitecore_modules.Shell.sitemap_XML.Classes;

namespace Sitecore.Modules.SitemapXML
{
    public class SitemapManagerConfiguration
    {
        #region properties

        public static string XmlnsTpl
        {
            get { return GetValueByName("xmlnsTpl"); }
        }

        public static string XmlnsImg
        {
            get { return GetValueByName("xmlnsImg"); }
        }

        public static string WorkingDatabase
        {
            get { return GetValueByName("database"); }
        }

        public static string SitemapConfigurationItemPath
        {
            get { return GetValueByName("sitemapConfigurationItemPath"); }
        }

        public static string EnabledTemplates
        {
            get { return GetValueByNameFromDatabase("Enabled templates"); }
        }

        public static string ExcludeItems
        {
            get { return GetValueByNameFromDatabase("Exclude items"); }
        }

        public static string ExcludeByQuery
        {
            get { return GetValueByNameFromDatabase("Exclude by query"); }
        }

        public static bool IsProductionEnvironment
        {
            get
            {
                string production = GetValueByName("productionEnvironment");
                return !string.IsNullOrEmpty(production) && (production.ToLower() == "true" || production == "1");
            }
        }

        public static bool ShouldGenerateRobotsTxt
        {
            get
            {
                string generateRobotsSetting = GetValueByName("generateRobotsTxt");
                return !string.IsNullOrEmpty(generateRobotsSetting) &&
                       (generateRobotsSetting.ToLower() == "true" || generateRobotsSetting == "1");
            }
        }
        #endregion properties

        private static string GetValueByName(string name)
        {
            string result = string.Empty;

            foreach (XmlNode node in Factory.GetConfigNodes("sitemapVariables/sitemapVariable"))
            {
                if (XmlUtil.GetAttribute("name", node) == name)
                {
                    result = XmlUtil.GetAttribute("value", node);
                    break;
                }
            }

            return result;
        }

        private static string GetValueByNameFromDatabase(string name)
        {
            string result = string.Empty;

            Database db = Factory.GetDatabase(WorkingDatabase);
            if (db != null)
            {
                Item configItem = db.Items[SitemapConfigurationItemPath];
                if (configItem != null)
                {
                    result = configItem[name];
                }
            }

            return result;
        }

        public static IEnumerable<SiteConfigurationDto> GetSites()
        {
            var sites = new List<SiteConfigurationDto>();
            foreach (XmlNode node in Factory.GetConfigNodes("sitemapVariables/sites/site"))
            {
                if (!string.IsNullOrEmpty(XmlUtil.GetAttribute("name", node)) &&
                    !string.IsNullOrEmpty(XmlUtil.GetAttribute("filename", node)))
                {
                    var site = new SiteConfigurationDto
                    {
                        FileName = XmlUtil.GetAttribute("filename", node),
                        ImageSitemapFileName = XmlUtil.GetAttribute("imageSitemapFilename", node),
                        Name = XmlUtil.GetAttribute("name", node)
                    };

                    if (!string.IsNullOrEmpty(XmlUtil.GetAttribute("extraPathToInclude", node)))
                    {
                        site.ExtraPathToInclude = XmlUtil.GetAttribute("extraPathToInclude", node);
                    }

                    if (!string.IsNullOrEmpty(XmlUtil.GetAttribute("mediaPath", node)))
                    {
                        site.MediaPath = XmlUtil.GetAttribute("mediaPath", node);
                    }

                    if (!string.IsNullOrEmpty(XmlUtil.GetAttribute("componentsFolderPath", node)))
                    {
                        site.ComponentsFolderPath = XmlUtil.GetAttribute("componentsFolderPath", node);
                    }

                    sites.Add(site);
                }
            }

            if (sites.Count == 0)
            {
                sites = (List<SiteConfigurationDto>) BuildSiteListBasedOnSiteManagerConfiguration();
            }

            return sites;
        }

        private static IEnumerable<SiteConfigurationDto> BuildSiteListBasedOnSiteManagerConfiguration()
        {
            var siteList = new List<SiteConfigurationDto>();

            var siteExclusionList = new List<string>
            {
                "shell",
                "login",
                "admin",
                "service",
                "modules_shell",
                "modules_website",
                "scheduler",
                "system",
                "publisher"
            };

            IEnumerable<Site> sites = SiteManager.GetSites().Where(c => !siteExclusionList.Contains(c.Name));

            foreach (Site site in sites)
            {
                siteList.Add(new SiteConfigurationDto
                {
                    Name = site.Name,
                    FileName = string.Format("sitemap-{0}.xml", site.Name)
                });
            }

            return siteList;
        }

        public static string GetServerUrlBySite(string name)
        {
            string result = string.Empty;

            foreach (XmlNode node in Factory.GetConfigNodes("sitemapVariables/sites/site"))
            {
                if (XmlUtil.GetAttribute("name", node) == name)
                {
                    result = XmlUtil.GetAttribute("serverUrl", node);
                    break;
                }
            }

            return result;
        }
    }
}