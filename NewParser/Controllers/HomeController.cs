﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NewParser.Models;
using PagedList;
using FluentSharp.CoreLib;
using ContentDownloader =NewParser.classes.ContentDownloader;

namespace NewParser.Controllers
{
    public class HomeController : Controller
    {

        //
        // GET: /Home/
        private List<Category> categoryList;
        private readonly NLog.Logger Log = MvcApplication.logger;

        public void InitializeList()
        {
            //categoryList = Category.DefaultUrls();
            // щоб визначати вибрану категорію по цьому значенню
            // ато по айдішці у строці не дуже вигдядає
            //foreach (var item in categoryList)
            //{
            //    item.UrlName = Func.ParseURL(item.Name);
            //}

            ViewBag.dropdownCount = new SelectList(new Dictionary<string, int>
            {
                {"20", 20},
                {"40", 40},
                {"60", 60},
                {"80", 80},
                {"100", 100}
            }, "Key", "Value");
            ViewBag.counts = 4;
        }

        public async Task<ActionResult> Index()
        {
            InitializeList();
            Log.Info("Index Method");

            ContentDownloader downloader = new ContentDownloader();
            await  downloader.DownloadContent(20);
            //var urlList = categoryList.Where(y => y.UrlName == id).Select(x => x.Url).toList();
            return View();
        }

        [HttpPost]
        public string ParseBooks(int BooksCount)
        {
            // функція, шо робить всю магію
          
            ContentDownloader downloader = new ContentDownloader();
            downloader.DownloadContent(BooksCount);

            Log.Info("Parsebooks Method{0}", BooksCount);
            return "Parsing is completed. "+ "Books count: " + BooksCount;
        }


    }

}