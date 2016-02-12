/* *********************************************************************** *
 * File   : SitemapManagerForm.cs                         Part of Sitecore *
 * Version: 1.0.0                                         www.sitecore.net *
 *                                                                         *
 *                                                                         *
 * Purpose: Codebehind of ManagerForm                                      *
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

using System;
using System.Collections.Specialized;
using System.Text;

using Sitecore.Diagnostics;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;

namespace Sitecore.Modules.SitemapXML
{
    public class SitemapManagerForm : BaseForm
    {
        protected Literal Message;
        protected Button RefreshButton;

        protected override void OnLoad(EventArgs args)
        {
            base.OnLoad(args);
            if (!Context.ClientPage.IsEvent)
            {
                RefreshButton.Click = "RefreshButtonClick";
            }
        }

        protected void RefreshButtonClick()
        {
            var sh = new SitemapHandler();
            sh.RefreshSitemap(this, new EventArgs());

            StringDictionary sites = SitemapManagerConfiguration.GetSites();
            var sb = new StringBuilder();

            const string messageTemplate =
                "The sitemap of site '<b>{0}</b>' has been refreshed filename '<b>{1}</b><br />'";

            foreach (string sitename in sites.Keys)
            {
                sb.AppendFormat(messageTemplate, sitename, sites[sitename]);
            }

            if (SitemapManagerConfiguration.ShouldGenerateRobotsTxt)
            {
                sb.Append("And added to robots txt");
            }

            Message.Text = sb.ToString();

            RefreshPanel("MainPanel");
        }

        private static void RefreshPanel(string panelName)
        {
            var ctl = Context.ClientPage.FindControl(panelName) as
                Panel;
            Assert.IsNotNull(ctl, "can't find panel");

            Context.ClientPage.ClientResponse.Refresh(ctl);
        }
    }
}