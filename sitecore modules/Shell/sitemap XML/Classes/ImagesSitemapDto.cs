using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Data.Items;

namespace Sitemp_XML.sitecore_modules.Shell.sitemap_XML.Classes
{
    public class ImagesSitemapDto
    {
        public ImagesSitemapDto()
        {
            ImagesSitemapItems = new List<Item>();
        }
        public Item SitemapItem { get; set; }
        public List<Item> ImagesSitemapItems { get; set; }
    }
}