﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using FluentSharp.CoreLib;
using HtmlAgilityPack;
using NewParser.Models;

namespace NewParser.classes
{
    using System.Threading;

    public class ContentDownloader
    {
        private readonly BookInfoEntities dbContext;
        private readonly Parser parser;
        private const string MainUrl = "http://www.amazon.com/Best-Sellers-Kindle-Store-eBooks/zgbs/digital-text/154606011/ref=zg_bs_fvp_p_f_154606011?_encoding=UTF8&tf=1";
        private int CategoryId;
        //private Timer

        public ContentDownloader()
        {
            dbContext = new BookInfoEntities();
            parser = new Parser();
            CategoryId = 0;
        }

        public async Task DownloadContent(int BooksCount)
        {
            try
            {
                if (!this.dbContext.Categories.Any())
                {
                    var content = await GetURLContentsAsync(MainUrl);
                    var categories = GetCategories(content);
                    categories.forEach<Category>(category => this.dbContext.Categories.Add(category));
                    foreach (var category in categories)
                    {
                        var subCategories = await  GetSubCategory(category.Id, category.Url);
                        subCategories.forEach<Category>(c => this.dbContext.Categories.Add(c));
                        foreach (var ssCategory in subCategories)
                        {
                            var subSubCategory = await GetSubSubCategory(ssCategory.Id, ssCategory.Url);
                            subSubCategory.forEach<Category>(c => this.dbContext.Categories.Add(c));
                        }
                    }
                    dbContext.SaveChanges();
                }
                foreach (var category in this.dbContext.Categories)
                {
                    (await parser.SelecrUrl(await GetURLContentsAsync(category.Url)))
                        .forEach<string>(async url =>
                            dbContext.Books.Add(
                            await parser.Parse((
                            await GetURLContentsAsync(url)), 
                            url, 
                            category.Id)));
                }
                dbContext.SaveChanges();
            }
            catch
            {
            }
        }

        public async Task<List<Category>> GetSubSubCategory(int pId, string parentUrl)
        {
            var list = new List<Category>();
            var content = await GetURLContentsAsync(parentUrl);
            try
            {
                var htmlDoc = new HtmlDocument { OptionFixNestedTags = true };
                htmlDoc.LoadHtml(content);
                if (htmlDoc.DocumentNode != null)
                {
                    var mainNode = htmlDoc.DocumentNode.SelectSingleNode("//ul[@id='zg_browseRoot']/ul/ul/ul/ul/ul");
                    if (mainNode != null)
                    {
                        var keys = mainNode.ChildNodes.Where(node => node.HasChildNodes)
                                .Select(node => node.FirstChild)
                                .Select(item => item.InnerText).toArray();
                        var values = mainNode.ChildNodes.Where(node => node.HasChildNodes)
                                .Select(node => node.FirstChild)
                                .Select(item => item.OuterHtml).toArray();
                        if (keys.notNull() && values.notNull())
                            for (var i = 0; i < values.Count(); i++)
                            {
                                var tmp = values[i].Remove(0, 9);
                                tmp = tmp.Remove(tmp.IndexOf('\''));
                                var cat = new Category()
                                {
                                    Id = CategoryId,
                                    Name = keys[i],
                                    Url = tmp,
                                    ParentId = pId
                                };
                                CategoryId++;
                                list.Add(cat);
                            }
                        return list;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public async Task<List<Category>> GetSubCategory(int pId, string parentUrl)
        {
            var list = new List<Category>();
            var content = await GetURLContentsAsync(parentUrl);
            try
            {
                var htmlDoc = new HtmlDocument { OptionFixNestedTags = true };
                htmlDoc.LoadHtml(content);
                if (htmlDoc.DocumentNode != null)
                {
                    var mainNode = htmlDoc.DocumentNode.SelectSingleNode("//ul[@id='zg_browseRoot']/ul/ul/ul/ul");
                    if (mainNode != null)
                    {
                        var keys = mainNode.ChildNodes.Where(node => node.HasChildNodes)
                                .Select(node => node.FirstChild)
                                .Select(item => item.InnerText).toArray();
                        var values = mainNode.ChildNodes.Where(node => node.HasChildNodes)
                                .Select(node => node.FirstChild)
                                .Select(item => item.OuterHtml).toArray();
                        if (keys.notNull() && values.notNull())
                            for (var i = 0; i < values.Count(); i++)
                            {
                                var tmp = values[i].Remove(0, 9);
                                tmp = tmp.Remove(tmp.IndexOf('\''));
                                var cat = new Category()
                                {
                                    Id = CategoryId,
                                    Name = keys[i],
                                    Url = tmp,
                                    ParentId = pId
                                };
                                CategoryId++;
                                list.Add(cat);
                            }
                        return list;
                    }
                }
            }
            catch 
            {     
            }
            return null;
        }
        
        public void ClearBooks()
        {
            var rows = from o in this.dbContext.Books
                       select o;
            foreach (var row in rows)
                this.dbContext.Books.Remove(row);
        }

        private async Task<string> GetURLContentsAsync(string url)
        {
            var webReq = (HttpWebRequest)WebRequest.Create(url);
            using (var response = await webReq.GetResponseAsync())
            {
                try
                {
                    using (var sr = new StreamReader(response.GetResponseStream()))
                    {
                        var responseJson = sr.ReadToEnd();
                        return responseJson;
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        private List<Category> GetCategories(string content)
        {
            try
            {
                var htmlDoc = new HtmlDocument { OptionFixNestedTags = true };
                htmlDoc.LoadHtml(content);
                if (htmlDoc.DocumentNode != null)
                {
                    var mainNode = htmlDoc.DocumentNode.SelectSingleNode("//ul[@id='zg_browseRoot']/ul/ul/ul");
                    if (mainNode != null)
                    {
                        var categories = new List<Category>();
                        var keys = mainNode.ChildNodes.Where(node => node.HasChildNodes)
                                .Select(node => node.FirstChild)
                                .Select(item => item.InnerText).toArray();
                        var values = mainNode.ChildNodes.Where(node => node.HasChildNodes)
                                .Select(node => node.FirstChild)
                                .Select(item => item.OuterHtml).toArray();
                        if (keys.notNull() && values.notNull())
                            for (var i = 0; i < values.Count(); i++)
                            {
                                var tmp = values[i].Remove(0, 9);
                                tmp = tmp.Remove(tmp.IndexOf('\''));
                                var cat = new Category()
                                {
                                    Id = CategoryId,
                                    Name = keys[i],
                                    Url = tmp,
                                    ParentId = 0
                                };
                                CategoryId++;
                                categories.Add(cat);                               
                            }
                        return categories;
                    }
                }
            }
            catch
            {
            }
            return null;
        }
    }
}

