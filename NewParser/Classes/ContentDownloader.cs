using System;
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
    public  class ContentDownloader
    {
        private static  BookInfoEntities _dbContext;
        private List<Category> categories;
        private List<Category> subCategories; 
        private readonly static string mainUrl = "http://www.amazon.com/Best-Sellers-Kindle-Store-eBooks/zgbs/digital-text/154606011/ref=zg_bs_fvp_p_f_154606011?_encoding=UTF8&tf=1";

        public ContentDownloader()
        {
            categories = new List<Category>();
            subCategories = new List<Category>();
            _dbContext = new BookInfoEntities();

            
        }
        public async Task DownloadContent(int booksCount)
        {
            try
            {
                // завантажуємо контент для головного посилання
                string content = await GetURLContentsAsync(mainUrl);
                // отримуємо категорії
                categories = GetCategories(content);
                // записуємо в базу. 
                //треба розділити логіку робити зі списками від логіки збереження даних у базі
                // потім винести у окрему функцію


                //foreach (var category in categories)
                //{
                //    _dbContext.Categories.Add(category);
                //}



                // отримуємо підкатегорії
                foreach (Category category in categories)
                {
                    // отримуємо список підкатегорій
                    // todo щоб присвоїти підкатегорії parentid треба спочатку зберегти у базу
                    // щоб присвоти всім категоріям id. або додавати вручну
                    // 

                    subCategories =await GetSubCategory(category.Id, category.Url);

                    // todo додаємо підкатегорії до головного списку категорій
                    //
                    foreach (var ssCategory in subCategories)
                    {
                        // отримати список ППкатегорій
                        // занести у базу
                        // коли буде готово даш знати
                    }

                }
               // _dbContext.SaveChanges();
               //// очищуємо базу. щоб бачити зміни
               // clearcategories();
               // _dbContext.SaveChanges();
            }
            catch (Exception)
            {
            }
          
        }

        public async Task<List<Category>> GetSubCategory(int pId, string parentUrl)
        {
            List<Category> list = new List<Category>();
            string content = await GetURLContentsAsync(parentUrl);

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
                                Category cat = new Category()
                                {
                                    Name = keys[i],
                                    Url = tmp,
                                    ParentId = pId
                                };
                                list.Add(cat);

                            }
                        return list;
                    }
                }
            }
            catch (Exception)
            {

            }
            return null;

        }
        
        public void ClearCategories()
        {
            var rows = from o in _dbContext.Categories
                       select o;
            foreach (var row in rows)
            {
                _dbContext.Categories.Remove(row);
            }
        }

        // load content async
        private async Task<string> GetURLContentsAsync(string url)
        {
            // сам точно не знаю шо тут до чого. copy-paste
            var webReq = (HttpWebRequest)WebRequest.Create(url);

            using (WebResponse response = await webReq.GetResponseAsync())
            {
                using (var sr = new StreamReader(response.GetResponseStream()))
                {
                    // getting string
                    var responseJson = sr.ReadToEnd();
                    return responseJson;
                }
            }
        }

        private  List<Category> GetCategories(string content)
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
                                Category cat = new Category()
                                {
                                    Name = keys[i],
                                    Url = tmp,
                                    ParentId = 0
                                };
                                categories.Add(cat);
                               
                            }
                        return categories;
                    }
                }
            }
            catch (Exception)
            {
                
            }
            return null;
        }

     
     
    }
}

