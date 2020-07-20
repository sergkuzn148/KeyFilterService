using System;
 
namespace Server
{
   static class StringHelper
    {
        public static double? ToDouble(this string s)
        {
            double name;
            return Double.TryParse(s, out name) ? name : (double?)null;
        }
        public static int? ToInt(this string s)
        {
            int name;
            return Int32.TryParse(s, out name) ? name : (int?)null;
        }
        public static DateTime? ToDateTime(this string s)
        {
            DateTime name;
            return DateTime.TryParse(s, out name) ? name : (DateTime?)null;

        }
    }

}