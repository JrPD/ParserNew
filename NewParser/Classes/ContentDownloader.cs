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
    using System.Text;
    using System.Threading;

    using Microsoft.Ajax.Utilities;

    using WebGrease.Css.Extensions;

    public class ContentDownloader
    {
        private readonly BookInfoEntities dbContext;
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
            main = new List<Category>();
            smain = new List<Category>();
            ssmain = new List<Category>();
            CategoryId = 0;
        }

#region Logic for download all cateogies ones, and then download only books

        public void OneMainDb(int count, string url = MainUrl)
        {
            try
            {
                dbContext.Database.Connection.Close();
                dbContext.Database.Connection.Open();
                ClearBooks();
                main.Clear();
                smain.Clear();
                ssmain.Clear();
                if (!this.dbContext.Categories.Any(c=>c.LevelName==(int?)LevelName.SubSubCategory))
                {
                    ClearCategories();
                    var content = GetURLContents(url);
                    main = GetCategories(content);
                    main.All(
                        c =>
                            {
                                c.LevelName = (int?)LevelName.Category;
                                return true;
                            });
                    main.forEach<Category>(c => dbContext.Categories.Add(c));
                    dbContext.SaveChanges();
                    foreach (
                        var category in
                            this.dbContext.Categories.Where(c => c.LevelName == (int?)LevelName.Category)
                                .Select(category => category))
                    {
                        var tmpSCat = this.GetSubCategory(category.Id, category.Url);
                        this.smain.addRange(tmpSCat);
                        this.smain.All(
                            c =>
                                {
                                    c.LevelName = (int?)LevelName.SubCategory;
                                    return true;
                                });
                        smain.forEach<Category>(c => this.dbContext.Categories.Add(c));
                        this.dbContext.SaveChanges();
                        foreach (var ssCategory in this.smain)
                        {
                            foreach (
                                var ID in
                                    this.dbContext.Categories.Where(
                                        c => c.Name == ssCategory.Name && c.Url == ssCategory.Url).Select(c => c.Id))
                            {
                                var tmpSSCat = this.GetSubSubCategory(ID, ssCategory.Url);
                                this.ssmain.AddRange(tmpSSCat);
                                this.ssmain.All(
                                    c =>
                                        {
                                            c.LevelName = (int?)LevelName.SubSubCategory;
                                            return true;
                                        });
                                tmpSSCat.forEach<Category>(c => this.dbContext.Categories.Add(c));
                                this.dbContext.SaveChanges();
                                this.ssmain.Clear();
                            }
                        }
                        this.smain.Clear();
                    }
                }
                GetBooks(count);
            }
            catch
            {
            }
        }

        private List<string> SelecrUrl(string content)
        {
            var htmlDoc = new HtmlDocument { OptionFixNestedTags = true };
            htmlDoc.LoadHtml(content);
            if (htmlDoc.DocumentNode != null)
            {
                HtmlNode mainNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='zg_centerListWrapper']");
                if (mainNode != null)
                {
                    var nodes = mainNode.SelectNodes("//div[@class='zg_title']");
                    var cont = from a in nodes.Descendants("a") select a.Attributes["href"].Value.Replace("\n", "");
                    var bookURLs = new List<string>();

                    // adding items from cont to list
                    foreach (var item in cont.Where(item => !bookURLs.Contains(item)))
                    {
                        bookURLs.Add(item);
                    }
                    return bookURLs;
                }
            }
            return new List<string>();
        }

        private void GetBooks(int count)
        {
            foreach (var subCategory in dbContext.Categories.Where(c => c.LevelName == (int?)LevelName.SubCategory))
            {
                ssmain.AddRange(dbContext.Categories.Where(ssc => ssc.LevelName == (int?)LevelName.SubSubCategory
                && ssc.ParentId == subCategory.Id));
                foreach (var ssc in ssmain)
                {
                    var bookUrls = new List<string>();
                    var max = (int)((double)count / 20 + 0.95);
                    if (max < 2) max = 2;
                    for (var j = 1; j < max; j++)
                    {
                        bookUrls.AddRange(SelecrUrl(GetURLContents(ssc.Url+"#1")));
                        var contentList = bookUrls.Select(this.GetURLContents).ToList();
                        dbContext.Books.AddRange(contentList.Select(Parse).Select(dummy => (Book)dummy).ToList());
                    }
                }
                ssmain.Clear();
            }
           
        }

        private Book Parse(string content)
        {
            lock (this)
            {
                try
                {
                    var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                    htmlDoc.OptionFixNestedTags = true;
                    htmlDoc.LoadHtml(content);
                    if (htmlDoc.DocumentNode != null)
                    {
                        var mainNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='singlecolumnminwidth']");
                        if (mainNode != null)
                        {
                            var _image = "";
                            var _Name = "";
                            var _Author = "";
                            var _Comments = new int();
                            var _Price = new double();
                            var _BestSellersRank = new int();
                            var _Categories = "";
                            var _PublicationDate = new DateTime();
                            HtmlNode workNode = null;

                            //Image
                            workNode = mainNode.SelectSingleNode("//img[@id='main-image']");
                            if (workNode != null) _image = workNode.Attributes["src"].Value;

                            // Name
                            workNode = mainNode.SelectSingleNode("//span[@id='btAsinTitle']");
                            if (workNode != null) _Name = workNode.ChildNodes[0].InnerText;

                            // Author
                            workNode = mainNode.SelectSingleNode("//div[@class='buying']/span");
                            if (workNode != null) _Author = workNode.InnerHtml.ParseAuthor();

                            // Comments

                            workNode = mainNode.SelectSingleNode("//div[@class='fl gl5 mt3 txtnormal acrCount']/a");
                            if (workNode != null) _Comments = workNode.ChildNodes[0].InnerText.ParseCount();

                            // Price
                            workNode = mainNode.SelectSingleNode("//b[@class='priceLarge']");
                            if (workNode != null) _Price = workNode.InnerText.ParsePrice();

                            // Amazon Best Sellers Rank
                            workNode = mainNode.SelectSingleNode("//li[@id='SalesRank']");
                            if (workNode != null) _BestSellersRank = workNode.InnerText.ParseRank();

                            // Categories
                            // select ul with categories
                            workNode = mainNode.SelectSingleNode("//ul[@class='zg_hrsr']");
                            IEnumerable<Category> cont = null;
                            if (workNode != null)
                                cont = from li in workNode.Descendants("li")
                                       from span in li.Descendants("span")
                                       from a in span.Descendants("a")
                                       select new Category { Name = a.InnerText };

                            // list for cont (context)
                            var categories = new List<string>();

                            // adding items from cont to list
                            if (cont != null)
                                foreach (var item in cont.Where(item => !categories.Contains(item.Name)))
                                {
                                    categories.Add(item.Name);
                                }

                            // make string
                            var sb = new StringBuilder();
                            foreach (var category in categories)
                            {
                                sb.Append(category + '\r');
                            }

                            // return string
                            _Categories = sb.ToString();

                            // Publication Data
                            workNode = mainNode.SelectSingleNode("//input[@id='pubdate']");
                            if (workNode != null) _PublicationDate = workNode.OuterHtml.ParseDate();

                            //new book
                            var book = new Book()
                            {
                                Image = _image,
                                Name = _Name,
                                Author = _Author,
                                BestSellersRank = _BestSellersRank,
                                //Categories = _Categories,
                                Comments = _Comments,
                                Price = _Price,
                                PublicationDate = _PublicationDate
                            };
                            // return book  
                            return book;
                        }
                    }
                }
                catch
                {
                }
            }
            return null;
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

        public List<Category> GetSubSubCategory(int pId, string parentUrl)
        {

            var list = new List<Category>();
            var content = GetURLContents(parentUrl);
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
                                tmp = tmp.Remove(tmp.IndexOf("\'>"));
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
            var content = GetURLContents(parentUrl);
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
#endregion

        public async Task  DownloadContent(int BooksCount, string url)
        {
                
                //    for preload all DB only once and after allways search in DB
            if (url.IsNullOrWhiteSpace()) OneMainDb(BooksCount);
            else OneMainDb(BooksCount, url);

            //tmp class for dunamicly download
            //return  DunamiclyDownload(url,BooksCount);
        }
        #region Logic for dunamicly download only current subsubcategories
        //private List<Book> DunamiclyDownload(string url, int count)
        //{
        //    //with auto increment Id
        //    if (main.Count == 0 || smain.Count == 0 || ssmain.Count == 0)
        //    {
        //        dbContext.Database.Connection.Close();
        //        dbContext.Database.Connection.Open();
        //        main = new List<Category>();
        //        smain = new List<Category>();
        //        ssmain = new List<Category>();
        //        var content = GetURLContentsAsync(url);
        //        ClearCategories();
        //        ClearBooks();
        //        try
        //        {
        //            var htmlDoc = new HtmlDocument { OptionFixNestedTags = true };
        //            htmlDoc.LoadHtml(content);
        //            HtmlNode category = null;
        //            var parId = 0;
        //            if (htmlDoc.DocumentNode != null)
        //            {
        //                var mainNode = htmlDoc.DocumentNode.SelectSingleNode("//ul[@id='zg_browseRoot']/ul/ul/ul");
        //                if (mainNode != null)
        //                {
        //                    this.GetCategory(mainNode,ref parId);
        //                    mainNode = htmlDoc.DocumentNode.SelectSingleNode("//ul[@id='zg_browseRoot']/ul/ul/ul/ul");
        //                    this.GetSubCategory(mainNode,ref parId);
        //                    var max = (int)(count / 20);
        //                    if (max < 2) max = 3;
        //                    for (var j = 2; j < max; j++)
        //                    {

        //                        mainNode =
        //                            htmlDoc.DocumentNode.SelectSingleNode("//ul[@id='zg_browseRoot']/ul/ul/ul/ul/ul");
        //                        this.GetSubSubCategory(mainNode,ref parId);
        //                        content = GetURLContentsAsync(url + "#" + j);
        //                        htmlDoc.LoadHtml(content);
        //                    }
        //                    dbContext.Categories.AddRange(ssmain.take(count));
        //                    dbContext.SaveChanges();
        //                }
        //            }
        //        }
        //        catch
        //        {
        //        }
        //    }
        //    return dbContext.Books.Where(c=>c.Name!=null).toList();
        //}

        //private void GetSubSubCategory(HtmlNode mainNode,ref int parId)
        //{
        //    if (mainNode != null)
        //    {
        //        var keys =
        //            mainNode.ChildNodes.Where(node => node.HasChildNodes)
        //                .Select(node => node.FirstChild)
        //                .Select(item => item.InnerText)
        //                .toArray();
        //        var values =
        //            mainNode.ChildNodes.Where(node => node.HasChildNodes)
        //                .Select(node => node.FirstChild)
        //                .Select(item => item.OuterHtml)
        //                .toArray();
        //        if (keys.notNull() && values.notNull())
        //            for (var i = 0; i < values.Count(); i++)
        //            {
        //                var tmp = values[i].Remove(0, 9);
        //                tmp = tmp.Remove(tmp.IndexOf('\''));
        //                var cat = new Category()
        //                {
        //                    Id = CategoryId,
        //                    Name = keys[i],
        //                    Url = tmp,
        //                    ParentId = parId,
        //                    LevelName = (int?)LevelName.SubSubCategory
        //                };
        //                CategoryId++;
        //                ssmain.Add(cat);
        //            }
        //    }
        //}

        //private void GetSubCategory(HtmlNode mainNode,ref int parId)
        //{
        //    if (mainNode != null)
        //    {
        //        var category = mainNode.SelectSingleNode("li/a");
        //        if (category.isNotNull())
        //        {
        //            var CategoryUrl = category.OuterHtml.Remove(0, 9);
        //            CategoryUrl = CategoryUrl.Remove(CategoryUrl.IndexOf("\">"));
        //            smain.Add(
        //                new Category()
        //                {
        //                    Name = category.InnerText,
        //                    LevelName = (int?)LevelName.SubCategory,
        //                    ParentId = parId,
        //                    Url = CategoryUrl
        //                });
        //            dbContext.Categories.AddRange(smain);
        //            dbContext.SaveChanges();
        //            var tmpCat =
        //                this.dbContext.Categories.FirstOrDefault(
        //                    c => c.Name == category.InnerText && c.Url == CategoryUrl);
        //            if (tmpCat != null)
        //            {
        //                parId = tmpCat.Id;
        //            }
        //        }
        //    }
        //}

        //private void GetCategory(HtmlNode mainNode, ref int parId)
        //{
        //    var category = mainNode.SelectSingleNode("li/a");
        //    if (category.isNotNull())
        //    {
        //        var CategoryUrl = category.OuterHtml.Remove(0, 9);
        //        CategoryUrl = CategoryUrl.Remove(CategoryUrl.IndexOf("\">"));
        //        main.Add(
        //            new Category()
        //            {
        //                Name = category.InnerText,
        //                LevelName = (int?)LevelName.Category,
        //                ParentId = 0,
        //                Url = CategoryUrl
        //            });
        //        dbContext.Categories.AddRange(main);
        //        dbContext.SaveChanges();
        //        var tmpCat =
        //            this.dbContext.Categories.FirstOrDefault(
        //                c => c.Name == category.InnerText && c.Url == CategoryUrl);
        //        if (tmpCat != null)
        //        {
        //            parId = tmpCat.Id;
        //        }
        //    }
        //}
#endregion

        #region Get books
        public async Task<Book> Parse(string content, string url, int id)
        {
            lock (this)
            {
                try
                {
                    var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                    htmlDoc.OptionFixNestedTags = true;
                    htmlDoc.LoadHtml(content);
                    if (htmlDoc.DocumentNode != null)
                    {
                        var mainNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='singlecolumnminwidth']");
                        if (mainNode != null)
                        {
                            var _image = "";
                            var _Name = "";
                            var _Author = "";
                            var _Comments = new int();
                            var _Price = new double();
                            var _BestSellersRank = new int();
                            var _Categories = "";
                            var _PublicationDate = new DateTime();
                            HtmlNode workNode = null;

                            //Image
                            workNode = mainNode.SelectSingleNode("//img[@id='main-image']");
                            if (workNode != null) _image = workNode.Attributes["src"].Value;

                            // Name
                            workNode = mainNode.SelectSingleNode("//span[@id='btAsinTitle']");
                            if (workNode != null) _Name = workNode.ChildNodes[0].InnerText;

                            // Author
                            workNode = mainNode.SelectSingleNode("//div[@class='buying']/span");
                            if (workNode != null) _Author = workNode.InnerHtml.ParseAuthor();

                            // Comments

                            workNode = mainNode.SelectSingleNode("//div[@class='fl gl5 mt3 txtnormal acrCount']/a");
                            if (workNode != null) _Comments = workNode.ChildNodes[0].InnerText.ParseCount();

                            // Price
                            workNode = mainNode.SelectSingleNode("//b[@class='priceLarge']");
                            if (workNode != null) _Price = workNode.InnerText.ParsePrice();

                            // Amazon Best Sellers Rank
                            workNode = mainNode.SelectSingleNode("//li[@id='SalesRank']");
                            if (workNode != null) _BestSellersRank = workNode.InnerText.ParseRank();

                            // Categories
                            // select ul with categories
                            workNode = mainNode.SelectSingleNode("//ul[@class='zg_hrsr']");
                            IEnumerable<Category> cont = null;
                            if (workNode != null)
                                cont = from li in workNode.Descendants("li")
                                       from span in li.Descendants("span")
                                       from a in span.Descendants("a")
                                       select new Category { Name = a.InnerText };

                            // list for cont (context)
                            var categories = new List<string>();

                            // adding items from cont to list
                            if (cont != null)
                                foreach (var item in cont.Where(item => !categories.Contains(item.Name)))
                                {
                                    categories.Add(item.Name);
                                }

                            // Publication Data
                            workNode = mainNode.SelectSingleNode("//input[@id='pubdate']");
                            if (workNode != null) _PublicationDate = workNode.OuterHtml.ParseDate();

                            //new book
                            var book = new Book()
                            {
                                Image = _image,
                                Name = _Name,
                                Author = _Author,
                                BestSellersRank = _BestSellersRank,
                                Comments = _Comments,
                                Price = _Price,
                                PublicationDate = _PublicationDate,
                                Category_Id = id,
                                Url = url
                            };
                            // return book  
                            return book;
                        }
                    }
                }
                catch (Exception e)
                {
                }
            }
            return null;
        }

        #endregion


        public void ClearBooks()
        {
            dbContext.Books.Select(c=>c).ForEach(c=>dbContext.Books.Remove(c));
            dbContext.SaveChanges();
        }

        public void ClearCategories()
        {
            dbContext.Categories.RemoveRange(dbContext.Categories.Select(c=>c));
            dbContext.SaveChanges();
        }

        private string GetURLContents(string url)
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

        
    }
}

