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
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Diagnostics;
using System.Collections.Specialized;
using System.Text;

namespace Sitecore.Modules.SitemapXML
{
    public class SitemapManagerForm : Sitecore.Web.UI.Sheer.BaseForm
    {
        protected Button RefreshButton;
        protected Literal Message;

        protected override void OnLoad(EventArgs args)
        {
            base.OnLoad(args);
            if (!Sitecore.Context.ClientPage.IsEvent)
            {
                RefreshButton.Click = "RefreshButtonClick";
            }
        }

        protected void RefreshButtonClick()
        {
            var sh = new SitemapHandler();
            sh.RefreshSitemap(this, new EventArgs());

            StringDictionary sites = SitemapManagerConfiguration.GetSites();
            StringBuilder sb = new StringBuilder();
           
            const string messageTemplate = "The sitemap of site '<b>{0}</b>' has been refreshed filename '<b>{1}</b><br />'";

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
            Sitecore.Web.UI.HtmlControls.Panel ctl = Sitecore.Context.ClientPage.FindControl(panelName) as
                Sitecore.Web.UI.HtmlControls.Panel;
            Assert.IsNotNull(ctl, "can't find panel");

            Sitecore.Context.ClientPage.ClientResponse.Refresh(ctl);
        }
    }
}
