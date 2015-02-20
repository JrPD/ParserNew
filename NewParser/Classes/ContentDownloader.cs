using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using FluentSharp.CoreLib;
using HtmlAgilityPack;
using NewParser.Models;
using System.Threading.Tasks; 

namespace NewParser.classes
{
    using System.Threading;

    using Microsoft.Ajax.Utilities;

    using WebGrease.Css.Extensions;

    public class ContentDownloader
    {
        private readonly BookInfoEntities dbContext;
        private readonly Parser parser;
        private const string MainUrl = "http://www.amazon.com/Best-Sellers-Kindle-Store-eBooks/zgbs/digital-text/154606011/ref=zg_bs_fvp_p_f_154606011?_encoding=UTF8&tf=1";
        private int CategoryId;
        private List<Category> main;
        private List<Category> smain;
        private List<Category> ssmain;
        private readonly NLog.Logger Log = MvcApplication.logger;

        //private Timer

        public ContentDownloader()
        {
            dbContext = new BookInfoEntities();
            parser = new Parser();
            main = new List<Category>();
            smain = new List<Category>();
            ssmain = new List<Category>();
            CategoryId = 0;
        }

        public void OneMainDb()
        {
            //without auto increment Id, only manual set)
            try
            {
                    main = new List<Category>();
                    smain = new List<Category>();
                    ssmain = new List<Category>();
                    if (!this.dbContext.Categories.Any())
                    {
                        var content = GetURLContentsAsync(MainUrl);
                        main = GetCategories(content);
                        main.All(c => {c.LevelName = (int?)LevelName.Category; return true;});
                        main.forEach<Category>(c => dbContext.Categories.Add(c));
                        lock (dbContext)
                        {
                            dbContext.SaveChanges();
                        }
                        foreach (var tmpSCat in this.main
                            .Select(category => (this.GetSubCategory(category.Id, category.Url))))
                        {
                            this.smain.addRange(tmpSCat);
                            smain.All(c => { c.LevelName = (int?)LevelName.SubCategory; return true; });
                            tmpSCat.forEach<Category>(c => this.dbContext.Categories.Add(c));
                            lock (dbContext)
                            {
                                dbContext.SaveChanges();
                            }
                            foreach (var tmpSSCat in this.smain
                                .Select(ssCategory => this.GetSubSubCategory(ssCategory.Id, ssCategory.Url)))
                            {
                                this.ssmain.AddRange(tmpSSCat);
                                ssmain.All(c => { c.LevelName = (int?)LevelName.SubSubCategory; return true; });
                                tmpSSCat.forEach<Category>(c => this.dbContext.Categories.Add(c));
                                lock (dbContext)
                                {
                                    dbContext.SaveChanges();
                                }
                            }
                        }
                    }
                    else
                    {
                        main.AddRange(dbContext.Categories.Where(c => c.LevelName == (int?)LevelName.Category));
                        smain.AddRange(dbContext.Categories.Where(c => c.LevelName == (int?)LevelName.SubCategory));
                        ssmain.AddRange(dbContext.Categories.Where(c => c.LevelName == (int?)LevelName.SubSubCategory));
                    }
            }
            catch
            {}
        }


        public async Task<List<Book>>  DownloadContent(int BooksCount, string url)
        {
                //if (main.Count == 0 || smain.Count == 0 || ssmain.Count == 0)
                //{
                //    for preload all DB only once and after allways search in DB
                //    OneMainDb();
                //}
                //tmp class for dunamicly download
                return  DunamiclyDownload(url,BooksCount);
        }

        private List<Book> DunamiclyDownload(string url, int count)
        {
            //with auto increment Id
            main = new List<Category>();
            smain = new List<Category>();
            ssmain = new List<Category>();
            var content = GetURLContentsAsync(url);
            dbContext.Categories.RemoveRange(dbContext.Categories.Where(c=>c!=null));
            try
            {
                var htmlDoc = new HtmlDocument { OptionFixNestedTags = true };
                htmlDoc.LoadHtml(content);
                HtmlNode category = null;
                int parId = 0;
                if (htmlDoc.DocumentNode != null)
                {
                    var mainNode = htmlDoc.DocumentNode.SelectSingleNode("//ul[@id='zg_browseRoot']/ul/ul/ul");
                    if (mainNode != null)
                    {
                        category = mainNode.SelectSingleNode("li");
                        if (category.isNotNull())
                        {
                            var CategoryUrl = category.OuterHtml.Remove(0, 9);
                            CategoryUrl = CategoryUrl.Remove(CategoryUrl.IndexOf('\''));
                            main.Add(
                                new Category()
                                    {
                                        Name = category.InnerText,
                                        LevelName = (int?)LevelName.Category,
                                        ParentId = 0,
                                        Url = CategoryUrl
                                    });
                            dbContext.Categories.AddRange(main);
                            dbContext.SaveChanges();
                            var tmpCat =
                                this.dbContext.Categories.FirstOrDefault(
                                    c => c.Name == category.InnerText && c.Url == CategoryUrl);
                            if (tmpCat != null)
                            {
                                parId = tmpCat.Id;
                            }
                        }
                        mainNode = htmlDoc.DocumentNode.SelectSingleNode("//ul[@id='zg_browseRoot']/ul/ul/ul/ul");
                        if (mainNode != null)
                        {
                            category = mainNode.SelectSingleNode("li");
                            if (category.isNotNull())
                            {
                                var CategoryUrl = category.OuterHtml.Remove(0, 9);
                                CategoryUrl = CategoryUrl.Remove(CategoryUrl.IndexOf('\''));
                                smain.Add(
                                    new Category()
                                        {
                                            Name = category.InnerText,
                                            LevelName = (int?)LevelName.SubCategory,
                                            ParentId = parId,
                                            Url = CategoryUrl
                                        });
                                dbContext.Categories.AddRange(smain);
                                dbContext.SaveChanges();
                                var tmpCat =
                                    this.dbContext.Categories.FirstOrDefault(
                                        c => c.Name == category.InnerText && c.Url == CategoryUrl);
                                if (tmpCat != null)
                                {
                                    parId = tmpCat.Id;
                                }
                            }
                        }
                        var max = (int)(count / 20);
                        if (max < 2)
                            max = 3;
                        for (var j = 2; j < max; j++)
                        {

                            mainNode = htmlDoc.DocumentNode.SelectSingleNode("//ul[@id='zg_browseRoot']/ul/ul/ul/ul/ul");
                            if (mainNode != null)
                            {
                                var keys =
                                    mainNode.ChildNodes.Where(node => node.HasChildNodes)
                                        .Select(node => node.FirstChild)
                                        .Select(item => item.InnerText)
                                        .toArray();
                                var values =
                                    mainNode.ChildNodes.Where(node => node.HasChildNodes)
                                        .Select(node => node.FirstChild)
                                        .Select(item => item.OuterHtml)
                                        .toArray();
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
                                                          ParentId = parId,
                                                          LevelName = (int?)LevelName.SubSubCategory
                                                      };
                                        CategoryId++;
                                        ssmain.Add(cat);
                                    }
                            }
                            content = GetURLContentsAsync(url + "#" + j);
                            htmlDoc.LoadHtml(content);
                        }
                        dbContext.Categories.AddRange(ssmain.take(count));
                        dbContext.SaveChanges();
                    }
                }
            }
            catch
            {}
            return dbContext.Books.Where(c=>c.Name!=null).toList();
        }

        private  void SaveCategories(List<Category> categories)
        {
            main.forEach<Category>(b => dbContext.Categories.Add(b));
            dbContext.SaveChangesAsync();
            main = new List<Category>();
        }
       

        public List<Category> GetSubSubCategory(int pId, string parentUrl)
        {
            var list = new List<Category>();
            var content = GetURLContentsAsync(parentUrl);
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

        public List<Category> GetSubCategory(int pId, string parentUrl)
        {
            var list = new List<Category>();
            var content = GetURLContentsAsync(parentUrl);
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
                                    //Id = CategoryId,
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

        private string GetURLContentsAsync(string url)
        {
            var webReq = (HttpWebRequest)WebRequest.Create(url);
            using (var response = webReq.GetResponseAsync().Result)
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
                                   // Id = CategoryId,
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

