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
            try
            {
                return double.Parse(line);
            }
            catch (Exception)
            {
                return new double();
            }
            
        }

        public static int ParseCount(this string line)
        {
            try
            {
            return Convert.ToInt32(line);
            }
            catch (Exception)
            {
                return new int();
            }
        }

        public static int ParseRank(this string rank)
        {
            try
            {
                rank = rank.Substring(rank.IndexOf('#') + 1);
                rank = rank.Split(' ')[0];
                return (int)(Convert.ToDouble(rank));
            }
            catch (Exception)
            {
                return new int();
            }
        }

        public static DateTime ParseDate(this string date)
        {
            try
            {
                date = date.Split(' ')[3];
                date = date.Substring(date.IndexOf('"') + 1);
                date = date.Substring(0, date.Length - 2);
                return Convert.ToDateTime(date);
            }
            catch (Exception)
            {
                return new DateTime();
            }
        }

        public static string ParseAuthor(this string authors)
        {
            try
            {
                authors = authors.Substring(authors.IndexOf('>') + 1);
                authors = authors.Split('<')[0];
                return authors;
            }
            catch 
            {
                return string.Empty;
            }
        }
        //todo дописати ще одну ф-цію на парщшенння
        public static string ParseURL(this string url)
        {
            url = url.Replace(" & ", "-").Replace(" ", "-").Replace(",", "");
            return url;
        }
    }
}
