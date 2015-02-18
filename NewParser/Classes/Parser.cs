using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using NewParser.classes;
using NewParser.Models;

namespace NewParser
{
    public class Parser
    {

        private readonly BookInfoEntities _dbContext= new BookInfoEntities();

        private double _currectItem = 1;

        private double _maxItem = 1;

        private bool CurrentStepCompleted;

        private List<string> contentList;

        private IEnumerable<string> urlList;

        public IEnumerable<Book> AllWorker(List<string> contentList)
        {
            var books = new List<Book>();
            var bookUrls = new List<string>();
            foreach (var content in contentList)
            {
                bookUrls.AddRange(SelecrUrl(content));
            }
            contentList.Clear();
            foreach (var bookUrl in bookUrls)
            {
                string urlContents = GetURLContents (bookUrl);
                contentList.Add(urlContents);
            }
            foreach (var content in contentList)
            {
                books.Add(Parse(content));
            }
            return books;
        }

        private string GetURLContents (string url)
        {
            var webReq = (HttpWebRequest)WebRequest.Create(url);
            using (WebResponse response = webReq.GetResponse ())
            {
                using (var sr = new StreamReader(response.GetResponseStream()))
                {
                    var responseJson = sr.ReadToEnd();
                    return responseJson;
                }
            }
        }

        public Book Parse(string content)
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

        public List<string> SelecrUrl(string content)
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

        public List<string> LoadData (IEnumerable<string> urlList)
        {
            var contentList = new List<string>();
            try
            {
                foreach (var url in urlList)
                {
                    var urlContents =  GetURLContents (url);
                    contentList.Add(urlContents);
                    urlContents = string.Empty;

                    urlContents =  GetURLContents (url + "#2");
                    contentList.Add(urlContents);
                    urlContents = string.Empty;

                    urlContents =  GetURLContents (url + "#3");
                    contentList.Add(urlContents);
                    urlContents = string.Empty;

                    urlContents =  GetURLContents (url + "#4");
                    contentList.Add(urlContents);
                    urlContents = string.Empty;

                    urlContents =  GetURLContents (url + "#5");
                    contentList.Add(urlContents);
                    urlContents = string.Empty;
                }
            }
            catch{}
            return contentList;
        }
    }
}
