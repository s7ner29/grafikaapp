using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kursovaya
{
    // Храню глобальные параметры сессии
    public static class AppSession
    {
        public static string DbPath { get; set; } = @"C:\Users\S7ner\Desktop\dnevnik\shkila.FDB";
        public static string CurrentUsername { get; set; } = string.Empty;
        public static string CurrentUserRole { get; set; } = string.Empty;
    }
}
