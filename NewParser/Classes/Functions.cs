using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewParser.classes
{
    //public class Category
    //{
    //    public string Name;
    //}

    public static class Func
    {
        public static double ParsePrice(this string line)
        {
            line = line
                .Replace(" ", "")
                .Replace(".", ",")
                .Replace("\n", "")
                .Replace("$","");

            return double.Parse(line);
        }

        public static int ParseCount(this string line)
        {
            return Convert.ToInt32(line);
        }

        public static int ParseRank(this string rank)
        {
            rank = rank.Substring(rank.IndexOf('#') + 1);
            rank = rank.Split(' ')[0];
            return (int) (Convert.ToDouble(rank));
        }

        public static DateTime ParseDate(this string date)
        {
            date = date.Split(' ')[3];
            date = date.Substring(date.IndexOf('"') + 1);
            date = date.Substring(0, date.Length - 2);

            DateTime dateFormat = Convert.ToDateTime(date);
            return dateFormat;
        }

        public static string ParseAuthor(this string authors)
        {
            authors = authors.Substring(authors.IndexOf('>') + 1);
            authors = authors.Split('<')[0];
            return authors;
        }
        //todo дописати ще одну ф-цію на парщшенння
        public static string ParseURL(this string url)
        {
            url = url.Replace(" & ", "-").Replace(" ", "-").Replace(",", "");
            return url;
        }
    }
}
